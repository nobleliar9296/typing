namespace TypingTrainer.Data.Database;

public sealed class DatabaseInitializer
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly MigrationRunner _migrationRunner;

    public DatabaseInitializer(SqliteConnectionFactory connectionFactory, MigrationRunner migrationRunner)
    {
        _connectionFactory = connectionFactory;
        _migrationRunner = migrationRunner;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await _migrationRunner.RunAsync(connection, cancellationToken).ConfigureAwait(false);
    }
}
