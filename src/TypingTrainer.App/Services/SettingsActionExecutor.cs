namespace TypingTrainer.App.Services;

public sealed class SettingsActionExecutor
{
    private readonly Action<string, Exception> _logException;

    public SettingsActionExecutor(Action<string, Exception>? logException = null)
    {
        _logException = logException ?? StartupExceptionLogger.Log;
    }

    public async Task<bool> ExecuteAsync(
        Func<Task> operation,
        Func<string> getStatus,
        Action<string> setStatus,
        string fallbackFailureStatus,
        string logSource)
    {
        var previousStatus = getStatus();

        try
        {
            await operation().ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            if (ShouldReplaceStatus(previousStatus, getStatus()))
            {
                setStatus(string.Empty);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logException(logSource, ex);
            var currentStatus = getStatus();
            if (ShouldReplaceStatus(previousStatus, currentStatus))
            {
                setStatus(fallbackFailureStatus);
            }

            return false;
        }
    }

    private static bool ShouldReplaceStatus(string previousStatus, string currentStatus)
    {
        if (string.Equals(currentStatus, previousStatus, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(currentStatus))
        {
            return true;
        }

        return IsInProgressStatus(currentStatus);
    }

    private static bool IsInProgressStatus(string status)
    {
        return status.Contains("Saving", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Starting", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Loading", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Exporting", StringComparison.OrdinalIgnoreCase)
            || status.Contains("Importing", StringComparison.OrdinalIgnoreCase);
    }
}
