# Keyboard Sound Assets

The app uses the bundled `unicae_games_keyboard_soundpack_1` WAV files for
typing-exercise sound feedback.

Active mapping:

- Normal accepted keys: `Single Keys/keypress-001.wav` through `keypress-028.wav`
- Mistakes and strict-mode blocked input: `Single Keys/keypress-029.wav` and `keypress-030.wav`
- Accepted Backspace corrections: `Single Keys/keypress-031.wav` and `keypress-032.wav`

Each playback randomly chooses a clip, avoids immediately repeating the same
clip within that category, and applies slight volume plus playback-rate
variation so repeated typing sounds less mechanical.

Supported copied formats are `.wav`, `.mp3`, `.m4a`, and `.wma`.
Files in this folder are copied to local build output and publish output.
