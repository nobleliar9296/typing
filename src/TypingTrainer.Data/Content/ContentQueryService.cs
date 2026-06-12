using Microsoft.Data.Sqlite;
using TypingTrainer.Core.Content;
using TypingTrainer.Data.Database;
using TypingTrainer.Data.Models;

namespace TypingTrainer.Data.Content;

public sealed class ContentQueryService : IContentQueryService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly IPracticeContentProvider _builtInContentProvider;

    public ContentQueryService(
        SqliteConnectionFactory connectionFactory,
        IPracticeContentProvider builtInContentProvider)
    {
        _connectionFactory = connectionFactory;
        _builtInContentProvider = builtInContentProvider;
    }

    public async Task<PracticeContentItem?> GetNextParagraphAsync(
        ParagraphPracticeQuery query,
        CancellationToken cancellationToken = default)
    {
        return (await GetParagraphsAsync(query, cancellationToken).ConfigureAwait(false)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<PracticeContentItem>> GetParagraphsAsync(
        ParagraphPracticeQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UseImportedContent)
        {
            var imported = await GetImportedParagraphsAsync(query, cancellationToken).ConfigureAwait(false);
            if (HasEnoughText(imported, query.TargetCharacters) || !query.UseBuiltInContent)
            {
                return imported;
            }

            var combined = imported.ToList();
            combined.AddRange(GetBuiltInParagraphs(query, query.TargetCharacters - CountJoinedCharacters(combined)));
            return combined;
        }

        return query.UseBuiltInContent
            ? GetBuiltInParagraphs(query, query.TargetCharacters)
            : Array.Empty<PracticeContentItem>();
    }

    public async Task<IReadOnlyList<ContentPackRow>> GetContentPacksAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, name, source_path, source_file_name, file_size_bytes,
                   created_at_utc, paragraph_count, enabled
            FROM content_packs
            ORDER BY created_at_utc DESC;
            """;

        var packs = new List<ContentPackRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            packs.Add(new ContentPackRow(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt64(4),
                DateTimeOffset.Parse(reader.GetString(5)),
                reader.GetInt32(6),
                reader.GetInt32(7) == 1));
        }

        return packs;
    }

    public async Task<bool> HasEnabledImportedContentAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1
                FROM content_packs
                WHERE enabled = 1
                  AND paragraph_count > 0
            );
            """;
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result) == 1;
    }

    public async Task DeleteContentPackAsync(
        Guid packId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM content_packs WHERE id = $packId;";
        command.Parameters.AddWithValue("$packId", packId.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<PracticeContentItem>> GetImportedParagraphsAsync(
        ParagraphPracticeQuery query,
        CancellationToken cancellationToken)
    {
        await using var connection = await _connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            SELECT item.id, item.pack_id, item.kind, item.title, item.text, item.language, item.source,
                   item.character_count, item.word_count, item.character_set,
                   item.contains_capital_letters, item.contains_numbers, item.contains_punctuation,
                   item.average_word_length, item.difficulty_score, item.created_at_utc,
                   item.last_used_at_utc, item.use_count
            FROM practice_content_items item
            INNER JOIN content_packs pack ON pack.id = item.pack_id
            WHERE pack.enabled = 1
              AND item.kind = 'Paragraph'
              AND ($allowCapitalLetters = 1 OR item.contains_capital_letters = 0)
              AND ($allowNumbers = 1 OR item.contains_numbers = 0)
              AND ($allowPunctuation = 1 OR item.contains_punctuation = 0)
            ORDER BY
              item.use_count ASC,
              CASE WHEN item.last_used_at_utc IS NULL THEN 0 ELSE 1 END ASC,
              ABS(item.character_count - $targetCharacters) ASC,
              item.difficulty_score ASC,
              item.created_at_utc ASC
            LIMIT 80;
            """;
        command.Parameters.AddWithValue("$allowCapitalLetters", query.AllowCapitalLetters ? 1 : 0);
        command.Parameters.AddWithValue("$allowNumbers", query.AllowNumbers ? 1 : 0);
        command.Parameters.AddWithValue("$allowPunctuation", query.AllowPunctuation ? 1 : 0);
        command.Parameters.AddWithValue("$targetCharacters", query.TargetCharacters);

        var candidates = new List<PracticeContentItem>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
        {
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                candidates.Add(ReadContentItem(reader));
            }
        }

        var selected = SelectUntilTarget(candidates, query.TargetCharacters);
        foreach (var item in selected)
        {
            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = (SqliteTransaction)transaction;
            updateCommand.CommandText = """
                UPDATE practice_content_items
                SET last_used_at_utc = $lastUsedAtUtc,
                    use_count = use_count + 1
                WHERE id = $id;
                """;
            updateCommand.Parameters.AddWithValue("$lastUsedAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            updateCommand.Parameters.AddWithValue("$id", item.Id);
            await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return selected;
    }

    private IReadOnlyList<PracticeContentItem> GetBuiltInParagraphs(ParagraphPracticeQuery query, int targetCharacters)
    {
        var candidates = _builtInContentProvider
            .GetContentItems()
            .Where(item => item.Kind == PracticeContentKind.Paragraph)
            .Where(item => IsAllowed(item, query))
            .OrderBy(item => Math.Abs(item.CharacterCount - query.TargetCharacters))
            .ThenBy(item => item.DifficultyScore)
            .ToArray();

        if (candidates.Length == 0)
        {
            return Array.Empty<PracticeContentItem>();
        }

        var selected = new List<PracticeContentItem>();
        var totalCharacters = 0;
        var index = 0;
        var safeTarget = Math.Max(20, targetCharacters);

        while (totalCharacters < safeTarget && selected.Count < 100)
        {
            var item = candidates[index % candidates.Length];
            selected.Add(item);
            totalCharacters += item.CharacterCount + 2;
            index++;

            if (safeTarget <= candidates[0].CharacterCount)
            {
                break;
            }
        }

        return selected;
    }

    private static bool IsAllowed(PracticeContentItem item, ParagraphPracticeQuery query)
    {
        return (query.AllowCapitalLetters || !item.ContainsCapitalLetters)
            && (query.AllowNumbers || !item.ContainsNumbers)
            && (query.AllowPunctuation || !item.ContainsPunctuation);
    }

    private static IReadOnlyList<PracticeContentItem> SelectUntilTarget(
        IReadOnlyList<PracticeContentItem> candidates,
        int targetCharacters)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<PracticeContentItem>();
        }

        var selected = new List<PracticeContentItem>();
        var totalCharacters = 0;
        var safeTarget = Math.Max(20, targetCharacters);

        foreach (var candidate in candidates)
        {
            selected.Add(candidate);
            totalCharacters += candidate.CharacterCount + 2;

            if (totalCharacters >= safeTarget)
            {
                break;
            }
        }

        return selected;
    }

    private static bool HasEnoughText(IReadOnlyList<PracticeContentItem> items, int targetCharacters)
    {
        return CountJoinedCharacters(items) >= Math.Max(20, targetCharacters);
    }

    private static int CountJoinedCharacters(IReadOnlyList<PracticeContentItem> items)
    {
        if (items.Count == 0)
        {
            return 0;
        }

        return items.Sum(item => item.CharacterCount) + ((items.Count - 1) * 2);
    }

    private static PracticeContentItem ReadContentItem(SqliteDataReader reader)
    {
        return new PracticeContentItem(
            reader.GetString(0),
            Enum.Parse<PracticeContentKind>(reader.GetString(2)),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            "Imported",
            Array.Empty<string>(),
            reader.GetString(9).ToHashSet(),
            reader.GetInt32(8),
            reader.GetInt32(7),
            reader.GetInt32(10) == 1,
            reader.GetInt32(11) == 1,
            reader.GetInt32(12) == 1,
            reader.GetDouble(13),
            reader.GetDouble(14),
            reader.GetString(1),
            DateTimeOffset.Parse(reader.GetString(15)),
            reader.IsDBNull(16) ? null : DateTimeOffset.Parse(reader.GetString(16)),
            reader.GetInt32(17));
    }
}
