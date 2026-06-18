using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypingTrainer.App.Services;

namespace TypingTrainer.App.Tests;

[TestClass]
public sealed class KeyboardSoundPlayerPoolTests
{
    [TestMethod]
    public void DefaultProductionPoolSizes_RemainExpected()
    {
        Assert.AreEqual(24, KeyboardSoundPlayer.DefaultInitialPlayerPoolSize);
        Assert.AreEqual(48, KeyboardSoundPlayer.DefaultMaxPlayerPoolSize);
    }

    [TestMethod]
    public void Constructor_WithCustomSmallInitialSize_PreallocatesRequestedCount()
    {
        var pool = CreatePool(initial: 1, max: 3);

        Assert.AreEqual(1, pool.Count);
        Assert.AreEqual(3, pool.MaxPlayerPoolSize);
    }

    [TestMethod]
    public void Acquire_WhenPlayersAreBusy_GrowsOnlyToMaximum()
    {
        var players = new List<FakePlayer>();
        var pool = CreatePool(initial: 1, max: 2, players);
        foreach (var player in players)
        {
            player.IsBusy = true;
        }

        var first = pool.Acquire();
        first.IsBusy = true;
        var second = pool.Acquire();
        second.IsBusy = true;
        var fallback = pool.Acquire();

        Assert.AreEqual(2, pool.Count);
        Assert.IsTrue(ReferenceEquals(first, fallback) || ReferenceEquals(second, fallback));
    }

    [TestMethod]
    public void Constructor_WithInvalidSizes_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => CreatePool(initial: -1, max: 1));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => CreatePool(initial: 0, max: 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => CreatePool(initial: 2, max: 1));
    }

    [TestMethod]
    public void Dispose_DisposesAllPlayersOnce()
    {
        var players = new List<FakePlayer>();
        var pool = CreatePool(initial: 2, max: 3, players);

        pool.Dispose();
        pool.Dispose();

        Assert.IsTrue(players.All(player => player.DisposeCount == 1));
    }

    private static KeyboardSoundPlayerPool<FakePlayer> CreatePool(
        int initial,
        int max,
        List<FakePlayer>? players = null)
    {
        return new KeyboardSoundPlayerPool<FakePlayer>(
            initial,
            max,
            () =>
            {
                var player = new FakePlayer();
                players?.Add(player);
                return player;
            },
            player => player.IsBusy);
    }

    private sealed class FakePlayer : IDisposable
    {
        public bool IsBusy { get; set; }

        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
