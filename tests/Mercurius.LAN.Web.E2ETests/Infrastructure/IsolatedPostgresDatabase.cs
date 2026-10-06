using Mercurius.LAN.API.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mercurius.LAN.Web.E2ETests.Infrastructure;

internal sealed class IsolatedPostgresDatabase : IAsyncDisposable
{
    private readonly string _databaseName;
    private readonly string _adminConnectionString;
    private int _disposed;

    private IsolatedPostgresDatabase(string databaseName, string adminConnectionString, string connectionString)
    {
        _databaseName = databaseName;
        _adminConnectionString = adminConnectionString;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<IsolatedPostgresDatabase> CreateAsync(CancellationToken cancellationToken = default)
    {
        var configured = Environment.GetEnvironmentVariable("TEST_POSTGRES_CONNECTION")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Timeout=5;Command Timeout=30";
        var databaseName = $"mercurius_e2e_{Environment.ProcessId}_{Guid.NewGuid():N}";
        var admin = new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" };

        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE \"{databaseName}\"";
        await command.ExecuteNonQueryAsync(cancellationToken);

        var testDatabase = new NpgsqlConnectionStringBuilder(admin.ConnectionString) { Database = databaseName };
        return new IsolatedPostgresDatabase(databaseName, admin.ConnectionString, testDatabase.ConnectionString);
    }

    public MercuriusDBContext CreateDbContext() => new(new DbContextOptionsBuilder<MercuriusDBContext>()
        .UseNpgsql(ConnectionString)
        .Options);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
