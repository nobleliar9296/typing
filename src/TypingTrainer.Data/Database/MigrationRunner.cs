using Microsoft.Data.Sqlite;

namespace TypingTrainer.Data.Database;

public sealed class MigrationRunner
{
    public async Task RunAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await ExecuteAsync(connection, transaction, """
            CREATE TABLE IF NOT EXISTS schema_version (
                version INTEGER PRIMARY KEY,
                applied_at_utc TEXT NOT NULL
            );
            """, cancellationToken).ConfigureAwait(false);

        var currentVersion = await GetCurrentVersionAsync(connection, transaction, cancellationToken).ConfigureAwait(false);

        if (currentVersion < 1)
        {
            await ExecuteAsync(connection, transaction, """
                CREATE TABLE IF NOT EXISTS practice_sessions (
                    id TEXT PRIMARY KEY,
                    started_at_utc TEXT NOT NULL,
                    ended_at_utc TEXT NOT NULL,
                    mode TEXT NOT NULL,
                    target_text TEXT NOT NULL,
                    target_length INTEGER NOT NULL,
                    raw_wpm REAL NOT NULL,
                    net_wpm REAL NOT NULL,
                    accuracy REAL NOT NULL,
                    consistency REAL,
                    total_keypresses INTEGER NOT NULL,
                    correct_keypresses INTEGER NOT NULL,
                    incorrect_keypresses INTEGER NOT NULL,
                    corrected_errors INTEGER NOT NULL,
                    uncorrected_errors INTEGER NOT NULL,
                    duration_ms INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS key_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    session_id TEXT NOT NULL,
                    position INTEGER NOT NULL,
                    expected_char TEXT,
                    actual_char TEXT,
                    event_kind TEXT NOT NULL,
                    is_correct INTEGER NOT NULL,
                    was_correction INTEGER NOT NULL,
                    timestamp_ticks INTEGER NOT NULL,
                    elapsed_ms REAL NOT NULL,
                    delta_previous_ms REAL,
                    FOREIGN KEY(session_id) REFERENCES practice_sessions(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_key_events_session_id
                ON key_events(session_id);

                CREATE INDEX IF NOT EXISTS idx_key_events_expected_char
                ON key_events(expected_char);

                CREATE INDEX IF NOT EXISTS idx_practice_sessions_started
                ON practice_sessions(started_at_utc);
                """, cancellationToken).ConfigureAwait(false);

            await using var insertVersionCommand = connection.CreateCommand();
            insertVersionCommand.Transaction = (SqliteTransaction)transaction;
            insertVersionCommand.CommandText = """
                INSERT OR IGNORE INTO schema_version (version, applied_at_utc)
                VALUES (1, $appliedAtUtc);
                """;
            insertVersionCommand.Parameters.AddWithValue("$appliedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            await insertVersionCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 2)
        {
            await ExecuteAsync(connection, transaction, """
                CREATE TABLE IF NOT EXISTS content_packs (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    source_path TEXT,
                    source_file_name TEXT,
                    file_size_bytes INTEGER,
                    created_at_utc TEXT NOT NULL,
                    paragraph_count INTEGER NOT NULL,
                    enabled INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS practice_content_items (
                    id TEXT PRIMARY KEY,
                    pack_id TEXT NOT NULL,
                    kind TEXT NOT NULL,
                    title TEXT NOT NULL,
                    text TEXT NOT NULL,
                    language TEXT NOT NULL,
                    source TEXT NOT NULL,
                    character_count INTEGER NOT NULL,
                    word_count INTEGER NOT NULL,
                    character_set TEXT NOT NULL,
                    contains_capital_letters INTEGER NOT NULL,
                    contains_numbers INTEGER NOT NULL,
                    contains_punctuation INTEGER NOT NULL,
                    average_word_length REAL NOT NULL,
                    difficulty_score REAL NOT NULL,
                    created_at_utc TEXT NOT NULL,
                    last_used_at_utc TEXT,
                    use_count INTEGER NOT NULL,
                    FOREIGN KEY(pack_id) REFERENCES content_packs(id) ON DELETE CASCADE
                );

                CREATE INDEX IF NOT EXISTS idx_practice_content_items_pack_id
                ON practice_content_items(pack_id);

                CREATE INDEX IF NOT EXISTS idx_practice_content_items_kind
                ON practice_content_items(kind);

                CREATE INDEX IF NOT EXISTS idx_practice_content_items_difficulty
                ON practice_content_items(difficulty_score);

                CREATE TABLE IF NOT EXISTS app_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL
                );
                """, cancellationToken).ConfigureAwait(false);

            await using var insertVersionCommand = connection.CreateCommand();
            insertVersionCommand.Transaction = (SqliteTransaction)transaction;
            insertVersionCommand.CommandText = """
                INSERT OR IGNORE INTO schema_version (version, applied_at_utc)
                VALUES (2, $appliedAtUtc);
                """;
            insertVersionCommand.Parameters.AddWithValue("$appliedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            await insertVersionCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (currentVersion < 3)
        {
            await ExecuteAsync(connection, transaction, """
                CREATE TABLE IF NOT EXISTS learning_items (
                    target_type TEXT NOT NULL,
                    target TEXT NOT NULL,
                    exposure_count INTEGER NOT NULL,
                    correct_count INTEGER NOT NULL,
                    incorrect_count INTEGER NOT NULL,
                    accuracy REAL NOT NULL,
                    median_latency_ms REAL,
                    weakness_score REAL NOT NULL,
                    stability_score REAL NOT NULL,
                    mastery_state TEXT NOT NULL,
                    interval_days INTEGER NOT NULL,
                    ease_factor REAL NOT NULL,
                    last_seen_utc TEXT NOT NULL,
                    next_due_utc TEXT NOT NULL,
                    primary_mistake_cause TEXT NOT NULL,
                    cause_counts_json TEXT NOT NULL,
                    PRIMARY KEY (target_type, target)
                );

                CREATE INDEX IF NOT EXISTS idx_learning_items_due
                ON learning_items(next_due_utc);

                CREATE INDEX IF NOT EXISTS idx_learning_items_mastery
                ON learning_items(mastery_state);
                """, cancellationToken).ConfigureAwait(false);

            await using var insertVersionCommand = connection.CreateCommand();
            insertVersionCommand.Transaction = (SqliteTransaction)transaction;
            insertVersionCommand.CommandText = """
                INSERT OR IGNORE INTO schema_version (version, applied_at_utc)
                VALUES (3, $appliedAtUtc);
                """;
            insertVersionCommand.Parameters.AddWithValue("$appliedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            await insertVersionCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> GetCurrentVersionAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "SELECT COALESCE(MAX(version), 0) FROM schema_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result);
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        System.Data.Common.DbTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
