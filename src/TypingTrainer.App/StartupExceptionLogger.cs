namespace TypingTrainer.App;

internal static class StartupExceptionLogger
{
    private static readonly object SyncRoot = new();
    private static bool _globalHandlersRegistered;

    public static void RegisterGlobalHandlers()
    {
        if (_globalHandlersRegistered)
        {
            return;
        }

        _globalHandlersRegistered = true;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log("AppDomain.UnhandledException", args.ExceptionObject);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log("TaskScheduler.UnobservedTaskException", args.Exception);
        };
    }

    public static void Log(string source, object? error)
    {
        try
        {
            lock (SyncRoot)
            {
                var logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TypingTrainer",
                    "logs");
                Directory.CreateDirectory(logDirectory);

                var logPath = Path.Combine(logDirectory, "startup.log");
                var message = error switch
                {
                    Exception exception => exception.ToString(),
                    null => "(null)",
                    _ => error.ToString() ?? "(unknown error)"
                };

                File.AppendAllText(
                    logPath,
                    $"[{DateTimeOffset.UtcNow:O}] {source}{Environment.NewLine}{message}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch
        {
            // Startup logging must never become the reason startup fails.
        }
    }
}
