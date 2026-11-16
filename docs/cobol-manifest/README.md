# CobolManifest: COBOL programs to a typed JSON manifest

A standalone command-line sample. It is **not** part of `MyApp.slnx`, so `dotnet build` and `dotnet test` at the repository root do not build it.

It reads COBOL sources that use physical files (no `LINKAGE SECTION`), asks the IBM i (DB2 for i) catalog for the layout of each file, and writes a JSON manifest. The manifest is meant to feed two generators: `CREATE PROCEDURE` statements and a C# source generator that wraps the COBOL programs.

## What it does

1. Parses each source (fixed format, columns 8-72, comment lines skipped):
   - `SELECT alias ASSIGN TO DATABASE-FILE` in `FILE-CONTROL`;
   - `OPEN INPUT | OUTPUT | I-O | EXTEND ...`, including several files and several modes in one statement, over several lines. An `OPEN` ends at the first word that is neither a mode nor a known file alias.
2. Finds each file in `QSYS2.SYSTABLES` (first library of the library list that contains it; `L` means logical file).
3. Reads its columns from `QSYS2.SYSCOLUMNS` and maps the SQL type to a C# type (`DECIMAL` and `NUMERIC` both become `decimal`).
4. Writes `programs -> files -> columns` as indented camelCase JSON, without timestamp so diffs stay clean.

## Use

`CreateConnection` in `Program.cs` throws `NotImplementedException`: return the connection of your IBM i provider.

```bash
export IBMI_CONN="<connection string>"
dotnet run -- ./sources manifest.json --libs MYLIB,OTHERLIB
```

`--libs` is optional; without it the job's library list (`QSYS2.LIBRARY_LIST_INFO`) is used.

## Known limits

- The queries use positional `?` markers. If your provider needs named parameters, adjust `CatalogReader`.
- Column names of the `QSYS2` views were written from memory: if one is missing on your release, run `SELECT * ... FETCH FIRST 1 ROW ONLY` and adapt the row classes.
- Not covered by the parser: `COPY` members, `ASSIGN TO` other than `DATABASE-`, free-format source.
- This sample was compiled against a stub of Dapper and its parser was tested on an example source. The catalog queries were not run against a real IBM i, and the project was not built with the .NET 11 SDK.
- Several types share `Program.cs` on purpose (sample code). Split them one per file before moving it into `src/`.
