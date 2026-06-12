using TypingTrainer.Data.Database;
using TypingTrainer.Data.Content;
using TypingTrainer.Data.Repositories;
using TypingTrainer.Data.Services;
using TypingTrainer.Core.Content;
using TypingTrainer.Core.Lessons;
using TypingTrainer.Core.Skill;

namespace TypingTrainer.App.Services;

public sealed class AppServices
{
    private AppServices(
        IPracticeSessionRepository practiceSessionRepository,
        IJsonExportService jsonExportService,
        IAnalyticsQueryService analyticsQueryService,
        IKeyboardHeatmapQueryService keyboardHeatmapQueryService,
        ISessionDetailQueryService sessionDetailQueryService,
        ISkillProfileQueryService skillProfileQueryService,
        IContentQueryService contentQueryService,
        ITextFileImportService textFileImportService,
        ILocalDataBackupService localDataBackupService,
        IAppSettingsRepository appSettingsRepository,
        ILessonService lessonService,
        ISessionPersistenceQueue sessionPersistenceQueue)
    {
        PracticeSessionRepository = practiceSessionRepository;
        JsonExportService = jsonExportService;
        AnalyticsQueryService = analyticsQueryService;
        KeyboardHeatmapQueryService = keyboardHeatmapQueryService;
        SessionDetailQueryService = sessionDetailQueryService;
        SkillProfileQueryService = skillProfileQueryService;
        ContentQueryService = contentQueryService;
        TextFileImportService = textFileImportService;
        LocalDataBackupService = localDataBackupService;
        AppSettingsRepository = appSettingsRepository;
        LessonService = lessonService;
        SessionPersistenceQueue = sessionPersistenceQueue;
    }

    public IPracticeSessionRepository PracticeSessionRepository { get; }

    public IJsonExportService JsonExportService { get; }

    public IAnalyticsQueryService AnalyticsQueryService { get; }

    public IKeyboardHeatmapQueryService KeyboardHeatmapQueryService { get; }

    public ISessionDetailQueryService SessionDetailQueryService { get; }

    public ISkillProfileQueryService SkillProfileQueryService { get; }

    public IContentQueryService ContentQueryService { get; }

    public ITextFileImportService TextFileImportService { get; }

    public ILocalDataBackupService LocalDataBackupService { get; }

    public IAppSettingsRepository AppSettingsRepository { get; }

    public ILessonService LessonService { get; }

    public ISessionPersistenceQueue SessionPersistenceQueue { get; }

    public static async Task<AppServices> CreateAsync(CancellationToken cancellationToken = default)
    {
        var databasePath = new LocalAppDataDatabasePath();
        var connectionFactory = new SqliteConnectionFactory(databasePath);
        var migrationRunner = new MigrationRunner();
        var databaseInitializer = new DatabaseInitializer(connectionFactory, migrationRunner);
        await databaseInitializer.InitializeAsync(cancellationToken).ConfigureAwait(false);

        var practiceSessionRepository = new PracticeSessionRepository(connectionFactory);
        var appSettingsRepository = new AppSettingsRepository(connectionFactory);
        var jsonExportService = new JsonExportService(practiceSessionRepository);
        var analyticsQueryService = new AnalyticsQueryService(connectionFactory);
        var keyboardHeatmapQueryService = new KeyboardHeatmapQueryService(connectionFactory);
        var sessionDetailQueryService = new SessionDetailQueryService(practiceSessionRepository);
        var localDataBackupService = new LocalDataBackupService(databasePath);
        var skillProfileQueryService = new SkillProfileQueryService(connectionFactory);
        var wordListProvider = new BuiltInWordListProvider();
        var paragraphProvider = new BuiltInParagraphProvider();
        var contentImportRepository = new ContentImportRepository(connectionFactory);
        var textFileImportService = new TextFileImportService(contentImportRepository);
        var contentQueryService = new ContentQueryService(connectionFactory, paragraphProvider);
        var unlockPlanner = new CharacterUnlockPlanner();
        var adaptiveLessonGenerator = new AdaptiveLessonGenerator(
            wordListProvider,
            unlockPlanner);
        var paragraphLessonGenerator = new ParagraphLessonGenerator(
            paragraphProvider,
            wordListProvider,
            unlockPlanner);
        var lessonService = new LessonService(
            skillProfileQueryService,
            appSettingsRepository,
            contentQueryService,
            adaptiveLessonGenerator,
            paragraphLessonGenerator);
        var sessionPersistenceQueue = new SessionPersistenceQueue(practiceSessionRepository);

        return new AppServices(
            practiceSessionRepository,
            jsonExportService,
            analyticsQueryService,
            keyboardHeatmapQueryService,
            sessionDetailQueryService,
            skillProfileQueryService,
            contentQueryService,
            textFileImportService,
            localDataBackupService,
            appSettingsRepository,
            lessonService,
            sessionPersistenceQueue);
    }
}
