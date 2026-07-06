# Mixed-DPI Testing Checklist

Use this checklist for manual verification on real Windows hardware. The app should rely on WinUI logical pixels and the app manifest's Per-Monitor V2 DPI awareness. Do not add monitor-specific scaling rules such as "4K means 175%".

## Current DPI Configuration

- App project: `src/TypingTrainer.App/TypingTrainer.App.csproj`
- Deployment mode: unpackaged WinUI 3 for the installer path, because `WindowsPackageType` is `None`.
- Manifest used by the project: `src/TypingTrainer.App/app.manifest`, referenced by `<ApplicationManifest>app.manifest</ApplicationManifest>`.
- DPI awareness: Per-Monitor V2 through:
  - legacy `dpiAware`: `true/pm`
  - modern `dpiAwareness`: `PerMonitorV2, PerMonitor`
- Packaged manifest: `src/TypingTrainer.App/Package.appxmanifest` remains present for WinUI/MSIX tooling and version metadata, but the current installer/publish flow is unpackaged.

## What To Test

1. Launch on a 27-inch 4K monitor at 150%.
2. Launch on a 27-inch 4K monitor at 175%.
3. Launch on a 14-inch 1080p monitor at 125%.
4. Launch on a 14-inch 1080p monitor at 150%.
5. Drag the app from the 4K monitor to the 1080p monitor.
6. Drag the app from the 1080p monitor to the 4K monitor.
7. Relaunch the app on each monitor after it was last closed on the other monitor.

## Pages And Flows

Check each item on Practice, Dashboard, Settings, and Session Detail where data exists.

- Text remains sharp after launch and after monitor movement.
- Button text is not clipped.
- ComboBox and NumberBox controls keep normal WinUI sizing.
- The top navigation bar stays stable.
- Practice text remains readable and cursor alignment stays under the active character.
- Visual keyboard keys stay crisp and do not overlap.
- Review popup fits inside the window and scrolls internally when needed.
- Dashboard charts remain sharp and labels do not overlap.
- Dashboard tables remain reachable with horizontal scrolling where needed.
- Settings content can scroll vertically on short windows.
- Imported-content preview remains readable.
- Session Detail summary cards and charts reflow without clipping.
- File picker dialogs still open on the active monitor.
- Any popup, flyout, tooltip, or review overlay appears near the active window and is not oversized.

## Minimum Window Checks

- Resize the app to a narrow desktop width around 760 logical px.
- Resize the app to a short desktop height around 620 logical px.
- Confirm content scrolls instead of disappearing.
- Confirm no page has unreachable bottom content.
- Confirm no fixed-width table pushes the whole page off screen without a horizontal scrollbar.

## Debug Verification

In Debug builds, open the Visual Studio Output window and watch for `[DPI]` messages while resizing and moving the app between monitors.

The log should include:

- current window DPI
- scale factor, where 96 DPI is 1.0
- window bounds in physical pixels
- display work area in physical pixels
- detected DPI awareness, expected to be `PerMonitorV2`
- a message when the DPI changes after moving monitors

## Expected Scale Reference

- 96 DPI = 1.0
- 120 DPI = 1.25
- 144 DPI = 1.5
- 168 DPI = 1.75
- 192 DPI = 2.0

## Remaining Human Verification

Mixed-DPI behavior cannot be fully proven by unit tests or single-monitor screenshots. The final pass must be done on real monitors with different Windows scale factors, especially 4K at 150%-175% and 1080p at 125%-150%.
