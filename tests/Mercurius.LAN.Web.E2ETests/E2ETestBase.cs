using Npgsql;
using Xunit;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Per-test isolation for the shared E2E database.
///
/// All tables except EF's migration history are truncated before each test, so tests never see
/// rows another test left behind. The database must be the fixture-created isolated one
/// (<c>mercurius_e2e_*</c>); reset refuses to run against anything else so a development or
/// production database can never be wiped by a test run.
/// </summary>
public abstract class E2ETestBase : IAsyncLifetime
{
    /// <summary>Every fixture database name starts with this prefix (see IsolatedPostgresDatabase).</summary>
    private const string IsolatedDatabasePrefix = "mercurius_e2e_";

    protected E2ETestBase(PlaywrightE2EFixture app) => App = app;

    protected PlaywrightE2EFixture App { get; }

    public Task InitializeAsync() => ResetDatabaseAsync();

    /// <summary>
    /// Closes any browser context or page this test left open before the next test truncates the
    /// database, so no live Blazor circuit/SignalR connection can race a reset. Contexts the test
    /// already disposed are removed from the fixture's snapshot, so this is a no-op for them.
    /// </summary>
    public virtual async Task DisposeAsync()
    {
        List<Exception>? failures = null;
        try
        {
            await App.CloseAllContextsAsync();
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        try
        {
            App.DisposeCreatedApiClients();
        }
        catch (Exception exception)
        {
            (failures ??= []).Add(exception);
        }

        if (failures is not null)
            throw new AggregateException("One or more per-test E2E resources could not be disposed.", failures);
    }

    private async Task ResetDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(App.DatabaseConnectionString);
        await connection.OpenAsync();

        await using (var verify = connection.CreateCommand())
        {
            verify.CommandText = "SELECT current_database()";
            var databaseName = (string?)await verify.ExecuteScalarAsync() ?? string.Empty;
            if (!databaseName.StartsWith(IsolatedDatabasePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to reset '{databaseName}': only isolated fixture databases " +
                    $"('{IsolatedDatabasePrefix}*') may be truncated by the E2E suite.");
            }
        }

        var tables = new List<string>();
        await using (var readTables = connection.CreateCommand())
        {
            readTables.CommandText = """
                SELECT quote_ident(schemaname) || '.' || quote_ident(tablename)
                FROM pg_tables
                WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
                  AND tablename <> '__EFMigrationsHistory'
                ORDER BY schemaname, tablename
                """;
            await using var reader = await readTables.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        if (tables.Count == 0)
            return;

        await using var truncate = connection.CreateCommand();
        truncate.CommandText = $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE";
        await truncate.ExecuteNonQueryAsync();
    }
}
