namespace TypingTrainer.Data.Database;

public sealed class LocalAppDataDatabasePath : IAppDatabasePath
{
    private const string AppFolderName = "TypingTrainer";
    private const string DatabaseFileName = "typingtrainer.db";

    public string GetDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var directory = Path.Combine(localAppData, AppFolderName);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, DatabaseFileName);
    }
}
