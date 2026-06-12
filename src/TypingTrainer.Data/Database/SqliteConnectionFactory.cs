using Microsoft.Data.Sqlite;

namespace TypingTrainer.Data.Database;

public sealed class SqliteConnectionFactory
{
    private readonly IAppDatabasePath _databasePath;

    public SqliteConnectionFactory(IAppDatabasePath databasePath)
    {
        _databasePath = databasePath;
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath.GetDatabasePath()
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return connection;
    }
}
