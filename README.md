# TypingTrainer

A native Windows typing practice app focused on fast input response, local-first analytics, adaptive practice, and local content.

The current app implements the MVP typing loop, local SQLite persistence, a read-only analytics dashboard, adaptive lessons, built-in paragraph practice, local settings, streamed `.txt` import, native analytics graphs, a visual keyboard tutor, post-session review, and local coaching insights.

## Goals

- Native Windows UI with WinUI and the Windows App SDK.
- Fast keystroke feedback.
- Local-only data.
- Testable typing engine separate from the UI.
- A foundation for later analytics and adaptive lessons.

## Non-goals

- No global keyboard logging.
- No background key capture.
- No cloud account requirement.
- No telemetry.
- No remote content library.
- No Electron, WebView, or browser shell.

## Prerequisites

Install:

- .NET 8 SDK.
- Windows App SDK dependencies restored by NuGet.
- Windows Developer Mode.

If `dotnet --info` does not show an SDK, check whether the SDK was installed under `C:\Program Files (x86)\dotnet`. On this machine, the SDK is available there while `dotnet` on `PATH` still points at the x64 runtime-only host.

## Build

The WinUI app is configured for unpackaged development launch from VS Code or PowerShell.

From a developer terminal with the SDK available:

```powershell
dotnet restore TypingTrainer.sln
dotnet build TypingTrainer.sln
dotnet run --project .\src\TypingTrainer.App\TypingTrainer.App.csproj -c Debug -r win-x64
```

With the current x86 SDK / x64 .NET 8 runtime layout, use:

```powershell
& 'C:\Program Files (x86)\dotnet\dotnet.exe' restore TypingTrainer.sln
& 'C:\Program Files (x86)\dotnet\dotnet.exe' build TypingTrainer.sln --no-restore
& 'C:\Program Files (x86)\dotnet\dotnet.exe' run --project .\src\TypingTrainer.App\TypingTrainer.App.csproj -c Debug -r win-x64
```

VS Code tasks are available for `Restore`, `Build`, `Test`, and `Run App`.

## Test

```powershell
dotnet test TypingTrainer.sln
```

With the current x86 SDK / x64 .NET 8 runtime layout, use:

```powershell
& 'C:\Program Files (x86)\dotnet\dotnet.exe' test TypingTrainer.sln --arch x64
```

## Local Data

TypingTrainer stores completed practice sessions, key events, imported content packs, imported paragraphs, and app settings locally in SQLite.

Default development database location:

```text
%LOCALAPPDATA%\TypingTrainer\typingtrainer.db
```

The typing hot path remains in memory. Completed sessions are queued for background persistence after the lesson is complete.

The app does not use cloud sync, telemetry, networking, or background keyboard capture.

## Importing .txt Files

TypingTrainer can import large `.txt` files and split them into local practice paragraphs.

The importer streams the file and does not load the whole file into memory. Paragraphs are split on blank lines, oversized paragraphs are chunked, and imported rows are stored in SQLite batches.

Import cleanup settings can normalize text to ASCII, normalize whitespace, and optionally lowercase imported text. ASCII normalization is enabled by default to fix curly quotes, accents, unusual dashes, and similar characters before they become practice text.

Only import text you wrote, own, or have permission to use.

## Settings

Settings are stored locally in SQLite and include:

- default lesson mode
- lesson length
- practice lesson size override on the Practice screen
- backspace behavior
- auto-save behavior
- strict accuracy mode
- visual keyboard display, finger colors, and finger labels
- capitalization, number, and punctuation preferences
- imported and built-in content preferences
- import cleanup preferences for ASCII, whitespace, and lowercasing

## Strict Accuracy Mode

TypingTrainer supports an optional setting called "Require correct key to advance."

When enabled:

- wrong keypresses are counted as mistakes
- the cursor does not move forward
- the same target character remains active
- progress only continues when the correct key is pressed

This is useful for accuracy-first practice.

When disabled, the app keeps the normal behavior where wrong characters advance the cursor and can be corrected with Backspace.

## Visual Keyboard

The Practice screen includes a native visual QWERTY keyboard.

It shows:

- finger-based key colors
- the current expected key
- required Shift key for uppercase letters and shifted symbols
- Space, Enter, Backspace, and punctuation keys

The visual keyboard is only a teaching aid. Keyboard input is still captured only while the practice surface is focused.

## Startup Logs

If the WinUI app fails before showing the main window, startup exceptions are written to:

```text
%LOCALAPPDATA%\TypingTrainer\logs\startup.log
```

## Export

Saved sessions can be exported to JSON from the Settings page.

## Analytics Dashboard

The dashboard reads local SQLite practice history and shows:

- recent sessions
- local insight cards
- mode filtering
- typing goal recommendations
- net WPM graph
- accuracy graph
- practice time graph
- WPM trends
- accuracy trends
- practice time
- weak keys
- slow keys
- weak bigrams
- slow bigrams

Analytics are calculated locally. No telemetry, cloud sync, or network access is used.

The dashboard can filter analytics by lesson mode and shows a concise local recommendation based on current goals, weak keys, slow bigrams, and weekly practice time.

## Data Model Note

Milestone 3 computes analytics from saved `practice_sessions` and `key_events`.
Derived character and bigram stats tables are intentionally deferred until later milestones.

## Adaptive Lessons

TypingTrainer can generate local adaptive lessons based on saved practice history.

The generator uses:

- unlocked keyboard stages
- weak character detection
- slow character detection
- weak bigram detection
- slow bigram detection
- local-only skill profiles

No cloud service, telemetry, or network access is used.

## Content Library

TypingTrainer separates typing, lesson generation, and content:

- The typing engine tracks whatever target text it receives.
- Lesson generators choose useful text based on mode and skill profile.
- The content library provides built-in words, original built-in paragraphs, and imported local paragraphs.

Built-in paragraphs are local, original practice text. Imported paragraphs stay local in SQLite. Imported word lists and richer content management are deferred.

## Lesson Modes

- Adaptive: balanced practice focused on current weak spots.
- Paragraph: prefers enabled imported paragraphs, then falls back to built-in paragraph text.
- Weak Keys: emphasizes the user's weakest unlocked characters.
- Weak Bigrams: emphasizes slow or inaccurate two-character transitions.
- Review: balanced practice from unlocked characters, plus a post-session "Practice These Mistakes" flow after completed lessons.
- Fixed: the original fixed sample sentence.

## Long Practice

The Practice screen has a lesson size selector:

- Small: the current short paragraph-sized practice.
- Medium: about 250 words.
- Long: about 1000 words.

Long paragraph lessons are assembled from multiple imported or built-in paragraph chunks when available. The practice text area scrolls inside the typing surface so the visual keyboard stays below it.

## Manual Checks

- The app launches to the practice screen.
- The practice surface receives focus on load.
- Typing advances through the selected lesson.
- Correct characters are shown in green.
- Mistakes are shown in red.
- The current character is underlined.
- Backspace removes the previous typed character and allows correction.
- WPM, accuracy, elapsed time, and unresolved errors update live.
- `Ctrl+R` restarts the current lesson.
- `Esc` pauses or resumes the active lesson.
- Pressing `Esc` twice within one second ends the active incomplete session without saving it.
- After a completed or stopped session, typing a printable key restarts the current lesson and processes that key.
- Next Lesson regenerates the selected lesson mode.
- Completed sessions show a review table and a Practice These Mistakes option.
- Settings opens and persists local preferences.
- The visual keyboard appears below the practice text and highlights the current expected key.
- Importing a `.txt` file shows progress and creates a local content pack.
- Paragraph mode can use imported content and falls back to built-in paragraph content.
- Dashboard graphs update with the selected range.

## Safety Check

Keyboard input is handled only by the focused WinUI practice surface. The app does not use global hooks, background listeners, or raw OS-wide keyboard capture APIs.
