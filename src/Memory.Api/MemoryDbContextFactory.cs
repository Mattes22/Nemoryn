namespace Memory.Api;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Memory.Infrastructure.Persistence;
using Npgsql;
using Pgvector;
using Pgvector.EntityFrameworkCore;

internal sealed class MemoryDbContextFactory : IDesignTimeDbContextFactory<MemoryDbContext>
{
    public MemoryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(ResolveContentRoot())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("MemoryDatabase")
            ?? "Host=localhost;Port=5432;Database=memory;Username=memory_app";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();

        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseNpgsql(dataSourceBuilder.Build(), npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(MemoryDbContext).Assembly.FullName);
                npgsqlOptions.UseVector();
            })
            .Options;

        return new MemoryDbContext(options);
    }

    private static string ResolveContentRoot()
    {
        var current = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            current,
            AppContext.BaseDirectory,
            Path.Combine(current, "src", "Memory.Api")
        };

        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "appsettings.json")))
            ?? current;
    }
}
