using Microsoft.Data.Sqlite;
using TypingTrainer.Core.Content;
using TypingTrainer.Data.Database;

namespace TypingTrainer.Data.Content;

public sealed class ContentImportRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ContentImportRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<Guid> CreatePackAsync(
        string name,
        string sourcePath,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var packId = Guid.NewGuid();
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO content_packs (
                id, name, source_path, source_file_name, file_size_bytes,
                created_at_utc, paragraph_count, enabled
            )
            VALUES (
                $id, $name, $sourcePath, $sourceFileName, $fileSizeBytes,
                $createdAtUtc, 0, 1
            );
            """;
        command.Parameters.AddWithValue("$id", packId.ToString());
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$sourcePath", sourcePath);
        command.Parameters.AddWithValue("$sourceFileName", Path.GetFileName(sourcePath));
        command.Parameters.AddWithValue("$fileSizeBytes", fileSizeBytes);
        command.Parameters.AddWithValue("$createdAtUtc", DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return packId;
    }

    public async Task InsertParagraphBatchAsync(
        Guid packId,
        IReadOnlyList<PracticeContentItem> paragraphs,
        CancellationToken cancellationToken = default)
    {
        if (paragraphs.Count == 0)
        {
            return;
        }

        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var paragraph in paragraphs)
        {
            await InsertParagraphAsync(connection, (SqliteTransaction)transaction, packId, paragraph, cancellationToken)
                .ConfigureAwait(false);
        }

        await using (var updatePackCommand = connection.CreateCommand())
        {
            updatePackCommand.Transaction = (SqliteTransaction)transaction;
            updatePackCommand.CommandText = """
                UPDATE content_packs
                SET paragraph_count = paragraph_count + $count
                WHERE id = $packId;
                """;
            updatePackCommand.Parameters.AddWithValue("$count", paragraphs.Count);
            updatePackCommand.Parameters.AddWithValue("$packId", packId.ToString());
            await updatePackCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeletePackAsync(Guid packId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM content_packs WHERE id = $packId;";
        command.Parameters.AddWithValue("$packId", packId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertParagraphAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid packId,
        PracticeContentItem paragraph,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO practice_content_items (
                id, pack_id, kind, title, text, language, source,
                character_count, word_count, character_set,
                contains_capital_letters, contains_numbers, contains_punctuation,
                average_word_length, difficulty_score, created_at_utc,
                last_used_at_utc, use_count
            )
            VALUES (
                $id, $packId, $kind, $title, $text, $language, $source,
                $characterCount, $wordCount, $characterSet,
                $containsCapitalLetters, $containsNumbers, $containsPunctuation,
                $averageWordLength, $difficultyScore, $createdAtUtc,
                NULL, 0
            );
            """;
        command.Parameters.AddWithValue("$id", paragraph.Id);
        command.Parameters.AddWithValue("$packId", packId.ToString());
        command.Parameters.AddWithValue("$kind", paragraph.Kind.ToString());
        command.Parameters.AddWithValue("$title", paragraph.Title);
        command.Parameters.AddWithValue("$text", paragraph.Text);
        command.Parameters.AddWithValue("$language", paragraph.Language);
        command.Parameters.AddWithValue("$source", paragraph.Source);
        command.Parameters.AddWithValue("$characterCount", paragraph.CharacterCount);
        command.Parameters.AddWithValue("$wordCount", paragraph.WordCount);
        command.Parameters.AddWithValue("$characterSet", new string(paragraph.CharacterSet.OrderBy(character => character).ToArray()));
        command.Parameters.AddWithValue("$containsCapitalLetters", paragraph.ContainsCapitalLetters ? 1 : 0);
        command.Parameters.AddWithValue("$containsNumbers", paragraph.ContainsNumbers ? 1 : 0);
        command.Parameters.AddWithValue("$containsPunctuation", paragraph.ContainsPunctuation ? 1 : 0);
        command.Parameters.AddWithValue("$averageWordLength", paragraph.AverageWordLength);
        command.Parameters.AddWithValue("$difficultyScore", paragraph.DifficultyScore);
        command.Parameters.AddWithValue("$createdAtUtc", (paragraph.CreatedAtUtc ?? DateTimeOffset.UtcNow).ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
