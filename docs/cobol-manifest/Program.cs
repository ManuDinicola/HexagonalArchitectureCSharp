// CobolManifest — lit des sources COBOL (SELECT / OPEN), interroge le catalogue DB2 for i
// (QSYS2.SYSTABLES / SYSCOLUMNS) et écrit un manifeste JSON (programmes -> fichiers -> colonnes).
//
// Usage :
//   set IBMI_CONN=<chaîne de connexion de ton provider IBM i>
//   dotnet run -- <dossier-sources> <sortie.json> [--libs MYLIB,OTHERLIB]
//
// Paquets : Dapper (+ le provider IBM i de ton choix, voir CreateConnection plus bas).

using System.Data.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: CobolManifest <dossier-sources> <sortie.json> [--libs A,B]");
    return 1;
}

var sourceDir = args[0];
var outputPath = args[1];
var libsArg = ArgValue(args, "--libs");

var connString = Environment.GetEnvironmentVariable("IBMI_CONN")
    ?? throw new InvalidOperationException("Variable d'environnement IBMI_CONN manquante.");

await using var conn = CreateConnection(connString);
await conn.OpenAsync();

// Bibliothèques : celles passées en argument, sinon la liste de bibliothèques du job.
var libs = libsArg is not null
    ? libsArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
    : await CatalogReader.JobLibrariesAsync(conn);

Console.WriteLine($"Bibliothèques : {string.Join(", ", libs)}");

var catalog = new CatalogReader(conn, libs);
var programs = new List<ProgramDef>();

var sourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { ".cbl", ".cblle", ".cob", ".cobol" };

var sources = Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.AllDirectories)
    .Where(f => sourceExtensions.Contains(Path.GetExtension(f)))
    .Order(StringComparer.OrdinalIgnoreCase);

foreach (var path in sources)
{
    var parsed = CobolSourceParser.Parse(File.ReadAllLines(path));
    if (parsed.Files.Count == 0) continue; // programme sans fichier DATABASE-

    var files = new List<FileUse>();
    foreach (var (alias, file) in parsed.Files)
    {
        var modes = parsed.Opens
            .Where(o => o.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase))
            .Select(o => o.Mode)
            .Distinct()
            .ToArray();

        var table = await catalog.FindTableAsync(file);
        if (table is null)
        {
            Console.Error.WriteLine($"[WARN] {Path.GetFileName(path)} : fichier {file} introuvable dans les bibliothèques.");
            files.Add(new FileUse(alias, file, null, null, modes, []));
            continue;
        }

        var columns = await catalog.GetColumnsAsync(table.Schema, file);
        files.Add(new FileUse(alias, file, table.Schema, table.IsLogical, modes, columns));
    }

    programs.Add(new ProgramDef(Path.GetFileNameWithoutExtension(path).ToUpperInvariant(), path, files));
    Console.WriteLine($"{Path.GetFileName(path)} : {files.Count} fichier(s)");
}

var json = JsonSerializer.Serialize(new Manifest(programs), new JsonSerializerOptions
{
    WriteIndented = true,
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
});
await File.WriteAllTextAsync(outputPath, json);
Console.WriteLine($"Manifeste écrit : {outputPath}");
return 0;

// ---------------------------------------------------------------------------------------------

static string? ArgValue(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

// À adapter : retourne la connexion de ton provider IBM i (iDB2Connection, ou autre DbConnection).
static DbConnection CreateConnection(string connectionString) =>
    throw new NotImplementedException(
        "Remplace par ex. : return new IBM.Data.Db2.iDB2Connection(connectionString); (selon ton package)");

// ---------------------------------------------------------------------------------------------
// Modèles

public sealed class TableRow
{
    public string SYSTEM_TABLE_SCHEMA { get; set; } = "";
    public string SYSTEM_TABLE_NAME { get; set; } = "";
    public string TABLE_TYPE { get; set; } = "";
}

public sealed class ColumnRow
{
    public string SYSTEM_TABLE_NAME { get; set; } = "";
    public string SYSTEM_COLUMN_NAME { get; set; } = "";
    public string COLUMN_NAME { get; set; } = "";
    public int ORDINAL_POSITION { get; set; }
    public string DATA_TYPE { get; set; } = "";
    public int LENGTH { get; set; }
    public int? NUMERIC_SCALE { get; set; }
    public string IS_NULLABLE { get; set; } = "";
}

public sealed record ColumnDef(
    string Name, string SystemName, string SqlType, string CsType, int Length, int? Scale, bool Nullable);

public sealed record FileUse(
    string Alias, string File, string? Schema, bool? IsLogical, string[] Modes, List<ColumnDef> Columns);

public sealed record ProgramDef(string Name, string SourcePath, List<FileUse> Files);

public sealed record Manifest(List<ProgramDef> Programs);

public sealed record TableRef(string Schema, bool IsLogical);

// ---------------------------------------------------------------------------------------------
// Parser COBOL

public sealed record ParsedSource(
    IReadOnlyDictionary<string, string> Files,          // alias COBOL -> nom du fichier AS400
    IReadOnlyList<(string Mode, string Alias)> Opens);

public static class CobolSourceParser
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    private static readonly Regex SelectRx = new(
        @"SELECT\s+(?<alias>[\w-]+)\s+ASSIGN\s+TO\s+DATABASE-(?<file>[\w-]+)",
        RegexOptions.IgnoreCase, Timeout);

    // (?<![\w-]) évite de matcher FILE-OPEN, WS-OPEN...
    private static readonly Regex OpenRx = new(
        @"(?<![\w-])OPEN\s+(?<body>[^.]+)",
        RegexOptions.IgnoreCase, Timeout);

    private static readonly HashSet<string> Modes = new(StringComparer.OrdinalIgnoreCase)
        { "INPUT", "OUTPUT", "I-O", "EXTEND" };

    public static ParsedSource Parse(IEnumerable<string> rawLines)
    {
        // Format fixe : on ignore les commentaires (col. 7 = * ou /) et on garde les colonnes 8-72.
        var text = string.Join(" ", rawLines
            .Where(l => l.Length < 7 || (l[6] != '*' && l[6] != '/'))
            .Select(l => l.Length <= 7 ? "" : l.Substring(7, Math.Min(l.Length, 72) - 7)));

        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in SelectRx.Matches(text))
            files[m.Groups["alias"].Value] = m.Groups["file"].Value;

        var opens = new List<(string Mode, string Alias)>();
        foreach (Match m in OpenRx.Matches(text))
        {
            string? mode = null;
            foreach (var raw in m.Groups["body"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var tok = raw.TrimEnd('.', ',');
                if (Modes.Contains(tok)) mode = tok.ToUpperInvariant();
                else if (mode is not null && files.ContainsKey(tok)) opens.Add((mode, tok));
                else break; // premier mot inconnu = fin du OPEN
            }
        }

        return new ParsedSource(files, opens);
    }
}

// ---------------------------------------------------------------------------------------------
// Lecture du catalogue DB2 for i

public sealed class CatalogReader(DbConnection conn, IReadOnlyList<string> libs)
{
    // Les requêtes utilisent des marqueurs positionnels "?" : l'ordre des membres des objets anonymes compte.
    // Si ton provider exige des paramètres nommés, adapte ici.

    public static async Task<List<string>> JobLibrariesAsync(DbConnection conn)
    {
        var rows = await conn.QueryAsync<string>(
            @"SELECT SCHEMA_NAME FROM QSYS2.LIBRARY_LIST_INFO
              WHERE TYPE IN ('USER', 'CURRENT') ORDER BY ORDINAL_POSITION");
        return rows.Select(r => r.Trim()).ToList();
    }

    public async Task<TableRef?> FindTableAsync(string file)
    {
        var rows = (await conn.QueryAsync<TableRow>(
            @"SELECT SYSTEM_TABLE_SCHEMA, SYSTEM_TABLE_NAME, TABLE_TYPE
              FROM QSYS2.SYSTABLES WHERE SYSTEM_TABLE_NAME = ?",
            new { file })).ToList();

        // Première bibliothèque de la liste qui contient le fichier.
        foreach (var lib in libs)
        {
            var hit = rows.FirstOrDefault(r =>
                r.SYSTEM_TABLE_SCHEMA.Trim().Equals(lib, StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
                return new TableRef(hit.SYSTEM_TABLE_SCHEMA.Trim(), hit.TABLE_TYPE.Trim() == "L");
        }
        return null;
    }

    public async Task<List<ColumnDef>> GetColumnsAsync(string schema, string file)
    {
        var rows = await conn.QueryAsync<ColumnRow>(
            @"SELECT SYSTEM_TABLE_NAME, SYSTEM_COLUMN_NAME, COLUMN_NAME, ORDINAL_POSITION,
                     DATA_TYPE, LENGTH, NUMERIC_SCALE, IS_NULLABLE
              FROM QSYS2.SYSCOLUMNS
              WHERE SYSTEM_TABLE_SCHEMA = ? AND SYSTEM_TABLE_NAME = ?
              ORDER BY ORDINAL_POSITION",
            new { schema, file });

        return rows.Select(c => new ColumnDef(
            Name: c.COLUMN_NAME.Trim(),
            SystemName: c.SYSTEM_COLUMN_NAME.Trim(),
            SqlType: c.DATA_TYPE.Trim(),
            CsType: TypeMapper.ToCsType(c),
            Length: c.LENGTH,
            Scale: c.NUMERIC_SCALE,
            Nullable: c.IS_NULLABLE.Trim() == "Y")).ToList();
    }
}

public static class TypeMapper
{
    public static string ToCsType(ColumnRow c)
    {
        var t = c.DATA_TYPE.Trim().ToUpperInvariant() switch
        {
            "CHAR" or "VARCHAR" or "CHARACTER" or "CHARACTER VARYING" => "string",
            "DECIMAL" or "NUMERIC" => "decimal",   // packed et zoned
            "INTEGER" => "int",
            "SMALLINT" => "short",
            "BIGINT" => "long",
            "DOUBLE" or "FLOAT" => "double",
            "REAL" => "float",
            "DATE" or "TIMESTAMP" => "DateTime",
            "TIME" => "TimeSpan",
            _ => "object"
        };
        return c.IS_NULLABLE.Trim() == "Y" && t != "string" ? t + "?" : t;
    }
}
