using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mercurius.LAN.Web.E2ETests;

/// <summary>
/// Temporarily hides an EF-mapped table from the API so a normal page load performs a real,
/// server-side call that fails against the real database. The browser never talks to the API
/// directly, so Playwright route interception cannot provoke these server-side failures.
///
/// PostgreSQL follows relations by OID, so foreign keys that reference the renamed table keep
/// working and no constraint rewriting is needed. Disposing restores the original name in a
/// <c>finally</c>, and only isolated fixture databases (<c>mercurius_e2e_*</c>) may be faulted.
/// </summary>
internal sealed class DatabaseReadFault : IAsyncDisposable
{
    private const string IsolatedDatabasePrefix = "mercurius_e2e_";

    private readonly NpgsqlConnection _connection;
    private readonly string _schema;
    private readonly string _table;
    private readonly string _backupTable;
    private bool _restored;

    private DatabaseReadFault(NpgsqlConnection connection, string schema, string table, string backupTable)
    {
        _connection = connection;
        _schema = schema;
        _table = table;
        _backupTable = backupTable;
    }

    /// <param name="mappedTableName">
    /// The physical table EF maps, for example <c>tournaments</c> or <c>teams</c>. Matching on the
    /// mapped table keeps the fault tied to the stable database contract rather than a CLR type name.
    /// </param>
    public static async Task<DatabaseReadFault> InstallAsync(PlaywrightE2EFixture app, string mappedTableName)
    {
        await using var context = app.CreateDbContext();
        var entityType = context.Model.GetEntityTypes()
            .First(type => string.Equals(type.GetTableName(), mappedTableName, StringComparison.OrdinalIgnoreCase));
        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"The '{mappedTableName}' entity has no mapped table name.");
        var schema = entityType.GetSchema() ?? "public";

        var connection = new NpgsqlConnection(app.DatabaseConnectionString);
        var ownershipTransferred = false;
        try
        {
            await connection.OpenAsync();
            // A short lock timeout keeps a hung ALTER from stalling the whole run; the rename itself is
            // instantaneous on an idle fixture database.
            await ExecuteAsync(connection, "SET lock_timeout = '15s'");

            var databaseName = (string?)await ExecuteScalarAsync(connection, "SELECT current_database()");
            if (databaseName is null || !databaseName.StartsWith(IsolatedDatabasePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to fault '{databaseName}': only isolated fixture databases " +
                    $"('{IsolatedDatabasePrefix}*') may be mutated by the E2E suite.");
            }

            var backupTable = $"{tableName}_e2e_fault_{Guid.NewGuid():N}";
            if (backupTable.Length > 63)
                backupTable = backupTable[..63];

            await ExecuteAsync(connection, $"ALTER TABLE {Qualified(schema, tableName)} RENAME TO {QuoteIdentifier(backupTable)}");
            var fault = new DatabaseReadFault(connection, schema, tableName, backupTable);
            ownershipTransferred = true;
            return fault;
        }
        finally
        {
            if (!ownershipTransferred)
                await connection.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_restored)
            return;

        try
        {
            await ExecuteAsync(_connection, $"ALTER TABLE {Qualified(_schema, _backupTable)} RENAME TO {QuoteIdentifier(_table)}");
        }
        finally
        {
            _restored = true;
            await _connection.DisposeAsync();
        }
    }

    private static string Qualified(string schema, string table) =>
        $"{QuoteIdentifier(schema)}.{QuoteIdentifier(table)}";

    private static string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ExecuteScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }
}
