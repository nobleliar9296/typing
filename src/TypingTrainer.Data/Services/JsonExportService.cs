using System.Text.Json;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;

namespace TypingTrainer.Data.Services;

public sealed class JsonExportService : IJsonExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly IPracticeSessionRepository _practiceSessionRepository;

    public JsonExportService(IPracticeSessionRepository practiceSessionRepository)
    {
        _practiceSessionRepository = practiceSessionRepository;
    }

    public async Task ExportAllSessionsAsync(
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _practiceSessionRepository.GetRecentSessionsAsync(int.MaxValue, cancellationToken)
            .ConfigureAwait(false);

        var exportSessions = new List<ExportSession>();

        foreach (var session in sessions)
        {
            var events = await _practiceSessionRepository.GetSessionEventsAsync(session.Id, cancellationToken)
                .ConfigureAwait(false);

            exportSessions.Add(new ExportSession(
                session.Id,
                session.StartedAtUtc,
                session.EndedAtUtc,
                session.Mode,
                session.TargetText,
                session.RawWpm,
                session.NetWpm,
                session.Accuracy,
                events));
        }

        var export = new ExportDocument(
            DateTimeOffset.UtcNow,
            "TypingTrainer",
            exportSessions);

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(stream, export, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private sealed record ExportDocument(
        DateTimeOffset ExportedAtUtc,
        string App,
        IReadOnlyList<ExportSession> Sessions);

    private sealed record ExportSession(
        Guid Id,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset EndedAtUtc,
        string Mode,
        string TargetText,
        double RawWpm,
        double NetWpm,
        double Accuracy,
        IReadOnlyList<StoredKeyEvent> Events);
}
