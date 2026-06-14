using Microsoft.Data.Sqlite;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Repositories;

public sealed class PracticeSessionRepository : IPracticeSessionRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public PracticeSessionRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task SaveCompletedSessionAsync(
        StoredPracticeSession session,
        IReadOnlyList<StoredKeyEvent> events,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await InsertSessionAsync(connection, (SqliteTransaction)transaction, session, cancellationToken).ConfigureAwait(false);

        foreach (var keyEvent in events)
        {
            await InsertKeyEventAsync(connection, (SqliteTransaction)transaction, keyEvent, cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StoredPracticeSession>> GetRecentSessionsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<StoredPracticeSession>();
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, started_at_utc, ended_at_utc, mode, target_text, target_length,
                   raw_wpm, net_wpm, accuracy, consistency, total_keypresses,
                   correct_keypresses, incorrect_keypresses, corrected_errors,
                   uncorrected_errors, duration_ms
            FROM practice_sessions
            ORDER BY started_at_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var sessions = new List<StoredPracticeSession>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            sessions.Add(ReadSession(reader));
        }

        return sessions;
    }

    public async Task<StoredPracticeSession?> GetSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, started_at_utc, ended_at_utc, mode, target_text, target_length,
                   raw_wpm, net_wpm, accuracy, consistency, total_keypresses,
                   correct_keypresses, incorrect_keypresses, corrected_errors,
                   uncorrected_errors, duration_ms
            FROM practice_sessions
            WHERE id = $id;
            """;
        command.Parameters.AddWithValue("$id", sessionId.ToString());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadSession(reader)
            : null;
    }

    public async Task<IReadOnlyList<StoredKeyEvent>> GetSessionEventsAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, session_id, position, expected_char, actual_char, event_kind,
                   is_correct, was_correction, timestamp_ticks, elapsed_ms, delta_previous_ms
            FROM key_events
            WHERE session_id = $sessionId
            ORDER BY id ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());

        var events = new List<StoredKeyEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(ReadKeyEvent(reader));
        }

        return events;
    }

    public async Task DeleteAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (var deleteLearningCommand = connection.CreateCommand())
        {
            deleteLearningCommand.Transaction = (SqliteTransaction)transaction;
            deleteLearningCommand.CommandText = "DELETE FROM learning_items;";
            await deleteLearningCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var deleteEventsCommand = connection.CreateCommand())
        {
            deleteEventsCommand.Transaction = (SqliteTransaction)transaction;
            deleteEventsCommand.CommandText = "DELETE FROM key_events;";
            await deleteEventsCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var deleteSessionsCommand = connection.CreateCommand())
        {
            deleteSessionsCommand.Transaction = (SqliteTransaction)transaction;
            deleteSessionsCommand.CommandText = "DELETE FROM practice_sessions;";
            await deleteSessionsCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertSessionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        StoredPracticeSession session,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO practice_sessions (
                id, started_at_utc, ended_at_utc, mode, target_text, target_length,
                raw_wpm, net_wpm, accuracy, consistency, total_keypresses,
                correct_keypresses, incorrect_keypresses, corrected_errors,
                uncorrected_errors, duration_ms
            )
            VALUES (
                $id, $startedAtUtc, $endedAtUtc, $mode, $targetText, $targetLength,
                $rawWpm, $netWpm, $accuracy, $consistency, $totalKeypresses,
                $correctKeypresses, $incorrectKeypresses, $correctedErrors,
                $uncorrectedErrors, $durationMs
            );
            """;

        command.Parameters.AddWithValue("$id", session.Id.ToString());
        command.Parameters.AddWithValue("$startedAtUtc", session.StartedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$endedAtUtc", session.EndedAtUtc.ToString("O"));
        command.Parameters.AddWithValue("$mode", session.Mode);
        command.Parameters.AddWithValue("$targetText", session.TargetText);
        command.Parameters.AddWithValue("$targetLength", session.TargetLength);
        command.Parameters.AddWithValue("$rawWpm", session.RawWpm);
        command.Parameters.AddWithValue("$netWpm", session.NetWpm);
        command.Parameters.AddWithValue("$accuracy", session.Accuracy);
        command.Parameters.AddWithValue("$consistency", DbValue(session.Consistency));
        command.Parameters.AddWithValue("$totalKeypresses", session.TotalKeypresses);
        command.Parameters.AddWithValue("$correctKeypresses", session.CorrectKeypresses);
        command.Parameters.AddWithValue("$incorrectKeypresses", session.IncorrectKeypresses);
        command.Parameters.AddWithValue("$correctedErrors", session.CorrectedErrors);
        command.Parameters.AddWithValue("$uncorrectedErrors", session.UncorrectedErrors);
        command.Parameters.AddWithValue("$durationMs", session.DurationMs);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertKeyEventAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        StoredKeyEvent keyEvent,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO key_events (
                session_id, position, expected_char, actual_char, event_kind,
                is_correct, was_correction, timestamp_ticks, elapsed_ms, delta_previous_ms
            )
            VALUES (
                $sessionId, $position, $expectedChar, $actualChar, $eventKind,
                $isCorrect, $wasCorrection, $timestampTicks, $elapsedMs, $deltaPreviousMs
            );
            """;

        command.Parameters.AddWithValue("$sessionId", keyEvent.SessionId.ToString());
        command.Parameters.AddWithValue("$position", keyEvent.Position);
        command.Parameters.AddWithValue("$expectedChar", CharDbValue(keyEvent.ExpectedChar));
        command.Parameters.AddWithValue("$actualChar", CharDbValue(keyEvent.ActualChar));
        command.Parameters.AddWithValue("$eventKind", keyEvent.EventKind);
        command.Parameters.AddWithValue("$isCorrect", keyEvent.IsCorrect ? 1 : 0);
        command.Parameters.AddWithValue("$wasCorrection", keyEvent.WasCorrection ? 1 : 0);
        command.Parameters.AddWithValue("$timestampTicks", keyEvent.TimestampTicks);
        command.Parameters.AddWithValue("$elapsedMs", keyEvent.ElapsedMs);
        command.Parameters.AddWithValue("$deltaPreviousMs", DbValue(keyEvent.DeltaPreviousMs));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static StoredPracticeSession ReadSession(SqliteDataReader reader)
    {
        return new StoredPracticeSession(
            Guid.Parse(reader.GetString(0)),
            DateTimeOffset.Parse(reader.GetString(1)),
            DateTimeOffset.Parse(reader.GetString(2)),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetInt32(5),
            reader.GetDouble(6),
            reader.GetDouble(7),
            reader.GetDouble(8),
            reader.IsDBNull(9) ? null : reader.GetDouble(9),
            reader.GetInt32(10),
            reader.GetInt32(11),
            reader.GetInt32(12),
            reader.GetInt32(13),
            reader.GetInt32(14),
            reader.GetInt64(15));
    }

    private static StoredKeyEvent ReadKeyEvent(SqliteDataReader reader)
    {
        return new StoredKeyEvent(
            reader.GetInt64(0),
            Guid.Parse(reader.GetString(1)),
            reader.GetInt32(2),
            ReadNullableChar(reader, 3),
            ReadNullableChar(reader, 4),
            reader.GetString(5),
            reader.GetInt32(6) == 1,
            reader.GetInt32(7) == 1,
            reader.GetInt64(8),
            reader.GetDouble(9),
            reader.IsDBNull(10) ? null : reader.GetDouble(10));
    }

    private static object DbValue(double? value)
    {
        return value is null ? DBNull.Value : value.Value;
    }

    private static object CharDbValue(char? value)
    {
        return value is null ? DBNull.Value : value.Value.ToString();
    }

    private static char? ReadNullableChar(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal);
        return value.Length == 0 ? null : value[0];
    }
}
