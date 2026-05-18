using System.Data.Common;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace Backend.Application.FunctionalTests.Infrastructure;

internal sealed class DatabaseResetter : IAsyncDisposable
{
    private readonly DbConnection _connection;
    private readonly Respawner _respawner;

    private DatabaseResetter(DbConnection connection, Respawner respawner)
    {
        _connection = connection;
        _respawner = respawner;
    }

    public static async Task<DatabaseResetter> CreateAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            TablesToIgnore = [new Table("__EFMigrationsHistory")],
        });

        return new DatabaseResetter(connection, respawner);
    }

    public Task ResetAsync() => _respawner.ResetAsync(_connection);

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
