using Microsoft.Data.Sqlite;
using TypingTrainer.Data.Database;

namespace TypingTrainer.Data.Services;

public sealed class LocalDataBackupService : ILocalDataBackupService
{
    private static readonly string[] RequiredTables =
    [
        "schema_version",
        "practice_sessions",
        "key_events",
        "content_packs",
        "practice_content_items",
        "app_settings"
    ];

    private readonly IAppDatabasePath _databasePath;

    public LocalDataBackupService(IAppDatabasePath databasePath)
    {
        _databasePath = databasePath;
    }

    public Task BackupAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException("A backup destination path is required.", nameof(destinationPath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        SqliteConnection.ClearAllPools();
        File.Copy(_databasePath.GetDatabasePath(), destinationPath, overwrite: true);
        return Task.CompletedTask;
    }

    public async Task RestoreAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("A backup source path is required.", nameof(sourcePath));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The backup file was not found.", sourcePath);
        }

        await ValidateBackupAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        SqliteConnection.ClearAllPools();
        File.Copy(sourcePath, _databasePath.GetDatabasePath(), overwrite: true);
        SqliteConnection.ClearAllPools();
    }

    private static async Task ValidateBackupAsync(string sourcePath, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (var table in RequiredTables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = $name;
                """;
            command.Parameters.AddWithValue("$name", table);
            var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) == 1;
            if (!exists)
            {
                throw new InvalidOperationException($"The selected file is not a TypingTrainer database backup. Missing table: {table}.");
            }
        }
    }
}

