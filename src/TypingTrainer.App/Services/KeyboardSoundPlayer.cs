using Windows.Media.Core;
using Windows.Media.Playback;

namespace TypingTrainer.App.Services;

public sealed class KeyboardSoundPlayer : IDisposable
{
    internal const int DefaultInitialPlayerPoolSize = 24;
    internal const int DefaultMaxPlayerPoolSize = 48;
    private const string SoundPackPath = "Assets/Sounds/Keyboard/unicae_games_keyboard_soundpack_1/Single Keys";

    private readonly KeyboardSoundPlayerPool<MediaPlayer> _playerPool;
    private readonly string[] _keySounds;
    private readonly string[] _mistakeSounds;
    private readonly string[] _correctionSounds;

    private int _lastKeySoundIndex = -1;
    private int _lastMistakeSoundIndex = -1;
    private int _lastCorrectionSoundIndex = -1;
    private bool _isDisposed;

    public KeyboardSoundPlayer()
        : this(AppContext.BaseDirectory, DefaultInitialPlayerPoolSize, DefaultMaxPlayerPoolSize)
    {
    }

    internal KeyboardSoundPlayer(string baseDirectory, int playerPoolSize)
        : this(baseDirectory, playerPoolSize, Math.Max(playerPoolSize, DefaultMaxPlayerPoolSize))
    {
    }

    internal KeyboardSoundPlayer(string baseDirectory, int initialPlayerPoolSize, int maxPlayerPoolSize)
    {
        _playerPool = new KeyboardSoundPlayerPool<MediaPlayer>(
            initialPlayerPoolSize,
            maxPlayerPoolSize,
            CreatePlayer,
            IsBusy);

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

        _playerPool.Dispose();
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

        var player = _playerPool.Acquire();
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

    private static MediaPlayer CreatePlayer()
    {
        return new MediaPlayer
        {
            AutoPlay = false,
            AudioCategory = MediaPlayerAudioCategory.SoundEffects
        };
    }

    private static bool IsBusy(MediaPlayer player)
    {
        return player.PlaybackSession.PlaybackState is MediaPlaybackState.Opening
            or MediaPlaybackState.Buffering
            or MediaPlaybackState.Playing;
    }
}
