using Windows.Media.Core;
using Windows.Media.Playback;

namespace TypingTrainer.App.Services;

public sealed class KeyboardSoundPlayer : IDisposable
{
    private const int DefaultPlayerPoolSize = 10;
    private const string SoundPackPath = "Assets/Sounds/Keyboard/unicae_games_keyboard_soundpack_1/Single Keys";

    private readonly MediaPlayer[] _players;
    private readonly string[] _keySounds;
    private readonly string[] _mistakeSounds;
    private readonly string[] _correctionSounds;

    private int _nextPlayerIndex;
    private int _lastKeySoundIndex = -1;
    private int _lastMistakeSoundIndex = -1;
    private int _lastCorrectionSoundIndex = -1;
    private bool _isDisposed;

    public KeyboardSoundPlayer()
        : this(AppContext.BaseDirectory, DefaultPlayerPoolSize)
    {
    }

    internal KeyboardSoundPlayer(string baseDirectory, int playerPoolSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(playerPoolSize);

        _players = Enumerable.Range(0, playerPoolSize)
            .Select(_ => new MediaPlayer { AutoPlay = false })
            .ToArray();

        var soundDirectory = Path.Combine(baseDirectory, Path.Combine(SoundPackPath.Split('/')));
        _keySounds = BuildSoundList(soundDirectory, 1, 28);
        _mistakeSounds = BuildSoundList(soundDirectory, 29, 30);
        _correctionSounds = BuildSoundList(soundDirectory, 31, 32);
    }

    public void PlayKey()
    {
        Play(_keySounds, ref _lastKeySoundIndex, minVolume: 0.54, maxVolume: 0.70);
    }

    public void PlayMistake()
    {
        Play(_mistakeSounds, ref _lastMistakeSoundIndex, minVolume: 0.68, maxVolume: 0.80);
    }

    public void PlayCorrection()
    {
        Play(_correctionSounds, ref _lastCorrectionSoundIndex, minVolume: 0.51, maxVolume: 0.65);
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

    private static string[] BuildSoundList(string soundDirectory, int firstIndex, int lastIndex)
    {
        if (!Directory.Exists(soundDirectory))
        {
            return [];
        }

        return Enumerable.Range(firstIndex, lastIndex - firstIndex + 1)
            .Select(index => Path.Combine(soundDirectory, $"keypress-{index:000}.wav"))
            .Where(File.Exists)
            .ToArray();
    }

    private void Play(string[] sounds, ref int lastSoundIndex, double minVolume, double maxVolume)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (sounds.Length == 0)
        {
            throw new InvalidOperationException("No keyboard sound assets were found.");
        }

        var soundIndex = PickSoundIndex(sounds.Length, lastSoundIndex);
        lastSoundIndex = soundIndex;

        var player = NextPlayer();
        player.Source = MediaSource.CreateFromUri(new Uri(sounds[soundIndex]));
        player.Volume = Random.Shared.NextDouble() * (maxVolume - minVolume) + minVolume;
        player.PlaybackSession.PlaybackRate = Random.Shared.NextDouble() * 0.06 + 0.97;
        player.Play();
    }

    private static int PickSoundIndex(int soundCount, int lastSoundIndex)
    {
        if (soundCount == 1)
        {
            return 0;
        }

        int soundIndex;
        do
        {
            soundIndex = Random.Shared.Next(soundCount);
        }
        while (soundIndex == lastSoundIndex);

        return soundIndex;
    }

    private MediaPlayer NextPlayer()
    {
        var player = _players[_nextPlayerIndex];
        _nextPlayerIndex = (_nextPlayerIndex + 1) % _players.Length;
        return player;
    }
}
