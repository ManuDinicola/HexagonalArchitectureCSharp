using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MyApp.Application.Ports.Out;

namespace MyApp.Api.Tests;

/// <summary>The real application (real pipeline, real EF Core) on a throw-away SQLite file.</summary>
internal sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseFile = Path.Combine(Path.GetTempPath(), $"myapp-tests-{Guid.CreateVersion7()}.db");

    /// <summary>When set, saving fails with this result instead of reaching the database.</summary>
    public IUnitOfWork? UnitOfWorkOverride { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databaseFile}");

        if (UnitOfWorkOverride is not null)
        {
            builder.ConfigureServices(services => services.AddScoped(_ => UnitOfWorkOverride));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(_databaseFile);
        }
    }
}
