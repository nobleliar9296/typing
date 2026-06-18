namespace TypingTrainer.App.Services;

internal sealed class KeyboardSoundPlayerPool<TPlayer> : IDisposable
    where TPlayer : IDisposable
{
    private readonly Func<TPlayer> _createPlayer;
    private readonly Func<TPlayer, bool> _isBusy;
    private readonly List<TPlayer> _players;
    private int _nextPlayerIndex;
    private bool _isDisposed;

    public KeyboardSoundPlayerPool(
        int initialPlayerPoolSize,
        int maxPlayerPoolSize,
        Func<TPlayer> createPlayer,
        Func<TPlayer, bool> isBusy)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(initialPlayerPoolSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPlayerPoolSize);
        if (initialPlayerPoolSize > maxPlayerPoolSize)
        {
            throw new ArgumentOutOfRangeException(nameof(initialPlayerPoolSize), "Initial player pool size cannot exceed maximum player pool size.");
        }

        _createPlayer = createPlayer;
        _isBusy = isBusy;
        MaxPlayerPoolSize = maxPlayerPoolSize;
        _players = Enumerable.Range(0, initialPlayerPoolSize)
            .Select(_ => createPlayer())
            .ToList();
    }

    public int MaxPlayerPoolSize { get; }

    public int Count => _players.Count;

    public TPlayer Acquire()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        for (var attempt = 0; attempt < _players.Count; attempt++)
        {
            var index = (_nextPlayerIndex + attempt) % _players.Count;
            var candidate = _players[index];
            if (!_isBusy(candidate))
            {
                _nextPlayerIndex = (index + 1) % _players.Count;
                return candidate;
            }
        }

        if (_players.Count < MaxPlayerPoolSize)
        {
            var newPlayer = _createPlayer();
            _players.Add(newPlayer);
            _nextPlayerIndex = 0;
            return newPlayer;
        }

        var fallback = _players[_nextPlayerIndex];
        _nextPlayerIndex = (_nextPlayerIndex + 1) % _players.Count;
        return fallback;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        foreach (var player in _players)
        {
            player.Dispose();
        }

        _isDisposed = true;
    }
}
