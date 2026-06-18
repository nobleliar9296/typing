using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Services;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class SettingsActionExecutorTests
{
    [TestMethod]
    public async Task ExecuteAsync_WhenActionSucceeds_ReturnsTrueAndKeepsStatus()
    {
        var status = string.Empty;
        var executor = new SettingsActionExecutor((_, _) => { });

        var result = await executor.ExecuteAsync(
            () =>
            {
                status = "Database backup saved.";
                return Task.CompletedTask;
            },
            () => status,
            value => status = value,
            "Backup failed.",
            "test");

        Assert.IsTrue(result);
        Assert.AreEqual("Database backup saved.", status);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenIoExceptionIsThrown_SetsFallbackStatusAndLogs()
    {
        var status = "Database backup saved.";
        var logCount = 0;
        var executor = new SettingsActionExecutor((_, _) => logCount++);

        var result = await executor.ExecuteAsync(
            () => throw new IOException("access denied"),
            () => status,
            value => status = value,
            "Backup failed. Check the selected location and try again.",
            "test");

        Assert.IsFalse(result);
        Assert.AreEqual("Backup failed. Check the selected location and try again.", status);
        Assert.AreEqual(1, logCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenSqliteExceptionIsThrown_DoesNotEscape()
    {
        var status = string.Empty;
        var executor = new SettingsActionExecutor((_, _) => { });

        var result = await executor.ExecuteAsync(
            () => throw new SqliteException("bad database", 1),
            () => status,
            value => status = value,
            "Restore failed. Check the selected backup and try again.",
            "test");

        Assert.IsFalse(result);
        Assert.AreEqual("Restore failed. Check the selected backup and try again.", status);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenCanceled_ClearsInProgressStatusWithoutError()
    {
        var status = "Saving...";
        var logCount = 0;
        var executor = new SettingsActionExecutor((_, _) => logCount++);

        var result = await executor.ExecuteAsync(
            () => throw new OperationCanceledException(),
            () => status,
            value => status = value,
            "Save failed.",
            "test");

        Assert.IsFalse(result);
        Assert.AreEqual(string.Empty, status);
        Assert.AreEqual(0, logCount);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenActionSetsSpecificFailureStatus_PreservesIt()
    {
        var status = string.Empty;
        var executor = new SettingsActionExecutor((_, _) => { });

        var result = await executor.ExecuteAsync(
            () =>
            {
                status = "Import failed: The file is empty.";
                throw new InvalidDataException("empty file");
            },
            () => status,
            value => status = value,
            "Import failed. Check the selected file and try again.",
            "test");

        Assert.IsFalse(result);
        Assert.AreEqual("Import failed: The file is empty.", status);
    }
}
