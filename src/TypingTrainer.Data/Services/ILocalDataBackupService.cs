namespace TypingTrainer.Data.Services;

public interface ILocalDataBackupService
{
    Task BackupAsync(string destinationPath, CancellationToken cancellationToken = default);

    Task RestoreAsync(string sourcePath, CancellationToken cancellationToken = default);
}

