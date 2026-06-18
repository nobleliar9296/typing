using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Services;
using TypingTrainer.Core.Content;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Review;
using TypingTrainer.Core.Skill;
using TypingTrainer.Data.Content;
using TypingTrainer.Data.Models;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class LessonServiceTests
{
    [TestMethod]
    public async Task GenerateNextLessonAsync_WhenAdaptiveSucceeds_ReturnsGeneratedLesson()
    {
        var adaptive = new FakeLessonGenerator();
        var service = CreateService(adaptive: adaptive);

        var lesson = await service.GenerateNextLessonAsync(LessonMode.Adaptive);

        Assert.AreEqual("adaptive text", lesson.Text);
        Assert.AreEqual("adaptive reason", lesson.Reason);
        Assert.AreEqual(1, adaptive.GenerateCount);
    }

    [TestMethod]
    public async Task GenerateNextLessonAsync_WhenParagraphContentIsEmpty_UsesAdaptiveFallbackWithoutLogging()
    {
        var logCount = 0;
        var adaptive = new FakeLessonGenerator { Text = "adaptive fallback" };
        var service = CreateService(
            adaptive: adaptive,
            content: new FakeContentQueryService { Paragraphs = Array.Empty<PracticeContentItem>() },
            logException: (_, _) => logCount++);

        var lesson = await service.GenerateNextLessonAsync(LessonMode.Paragraph);

        Assert.AreEqual("adaptive fallback", lesson.Text);
        Assert.AreEqual(1, adaptive.GenerateCount);
        Assert.AreEqual(0, logCount);
    }

    [TestMethod]
    public async Task GenerateNextLessonAsync_WhenContentThrows_LogsAndReturnsExplicitFallback()
    {
        var logCount = 0;
        var service = CreateService(
            content: new FakeContentQueryService { ParagraphError = new InvalidOperationException("content failed") },
            logException: (_, _) => logCount++);

        var lesson = await service.GenerateNextLessonAsync(LessonMode.Paragraph);

        Assert.AreEqual("The lesson could not be generated. A fallback lesson was loaded.", lesson.Reason);
        Assert.AreEqual("Fallback lesson", lesson.ContentTitle);
        Assert.AreEqual(1, logCount);
    }

    [TestMethod]
    public async Task GenerateNextLessonAsync_WhenAdaptiveThrows_LogsAndReturnsExplicitFallback()
    {
        var logCount = 0;
        var service = CreateService(
            adaptive: new FakeLessonGenerator { Error = new InvalidOperationException("adaptive failed") },
            logException: (_, _) => logCount++);

        var lesson = await service.GenerateNextLessonAsync(LessonMode.Adaptive);

        Assert.AreEqual("The lesson could not be generated. A fallback lesson was loaded.", lesson.Reason);
        Assert.AreEqual(1, logCount);
    }

    [TestMethod]
    public async Task GenerateNextLessonAsync_WhenCanceled_DoesNotSwallowCancellation()
    {
        var service = CreateService(settings: new FakeSettingsRepository { GetError = new OperationCanceledException() });

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => service.GenerateNextLessonAsync(LessonMode.Adaptive));
    }

    [TestMethod]
    public async Task GenerateReviewLessonAsync_WhenAdaptiveThrows_LogsAndReturnsExplicitFallback()
    {
        var logCount = 0;
        var service = CreateService(
            adaptive: new FakeLessonGenerator { Error = new InvalidOperationException("review failed") },
            logException: (_, _) => logCount++);

        var lesson = await service.GenerateReviewLessonAsync(CreateReview(), 120);

        Assert.AreEqual("The review lesson could not be generated. A fallback lesson was loaded.", lesson.Reason);
        Assert.AreEqual("Fallback lesson", lesson.ContentTitle);
        Assert.AreEqual(1, logCount);
    }

    private static LessonService CreateService(
        FakeSettingsRepository? settings = null,
        FakeSkillProfileQueryService? skills = null,
        FakeContentQueryService? content = null,
        FakeLessonGenerator? adaptive = null,
        Action<string, Exception>? logException = null)
    {
        return new LessonService(
            skills ?? new FakeSkillProfileQueryService(),
            settings ?? new FakeSettingsRepository(),
            content ?? new FakeContentQueryService(),
            adaptive ?? new FakeLessonGenerator(),
            new FakeLessonGenerator(),
            logException ?? ((_, _) => { }));
    }

    private static SessionReview CreateReview()
    {
        return new SessionReview(
            CorrectedErrors: 1,
            UncorrectedErrors: 0,
            [new SessionReviewKeyRow('a', "a", Samples: 2, CorrectCount: 1, IncorrectCount: 1, Accuracy: 0.5, MedianLatencyMs: 120, WeaknessScore: 0.5)],
            Array.Empty<SessionReviewKeyRow>(),
            Array.Empty<SessionReviewBigramRow>(),
            Array.Empty<string>());
    }

    private sealed class FakeSettingsRepository : IAppSettingsRepository
    {
        public Exception? GetError { get; set; }

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            if (GetError is not null)
            {
                throw GetError;
            }

            return Task.FromResult(AppSettings.Defaults);
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeSkillProfileQueryService : ISkillProfileQueryService
    {
        public Task<UserSkillProfile> GetUserSkillProfileAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(SkillProfileDefaults.Empty());
    }

    private sealed class FakeContentQueryService : IContentQueryService
    {
        public IReadOnlyList<PracticeContentItem> Paragraphs { get; set; } = Array.Empty<PracticeContentItem>();

        public Exception? ParagraphError { get; set; }

        public Task<PracticeContentItem?> GetNextParagraphAsync(ParagraphPracticeQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult<PracticeContentItem?>(Paragraphs.FirstOrDefault());

        public Task<IReadOnlyList<PracticeContentItem>> GetParagraphsAsync(ParagraphPracticeQuery query, CancellationToken cancellationToken = default)
        {
            if (ParagraphError is not null)
            {
                throw ParagraphError;
            }

            return Task.FromResult(Paragraphs);
        }

        public Task<IReadOnlyList<ContentPackRow>> GetContentPacksAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ContentPackRow>>(Array.Empty<ContentPackRow>());

        public Task<IReadOnlyList<PracticeContentItem>> GetContentPackPreviewAsync(Guid packId, int limit = 5, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PracticeContentItem>>(Array.Empty<PracticeContentItem>());

        public Task RenameContentPackAsync(Guid packId, string name, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SetContentPackEnabledAsync(Guid packId, bool enabled, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> HasEnabledImportedContentAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteContentPackAsync(Guid packId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeLessonGenerator : ILessonGenerator
    {
        public string Text { get; set; } = "adaptive text";

        public Exception? Error { get; set; }

        public int GenerateCount { get; private set; }

        public LessonGenerationResult Generate(UserSkillProfile skillProfile, LessonGenerationOptions options)
        {
            GenerateCount++;
            if (Error is not null)
            {
                throw Error;
            }

            return new LessonGenerationResult(
                Text,
                Text.ToHashSet(),
                Array.Empty<char>(),
                Array.Empty<string>(),
                "adaptive reason");
        }
    }
}
