# Mixed-DPI Audit Notes

Date: 2026-07-06

## Scope

Surface: Typing Trainer WinUI 3 desktop app.

Goal: make mixed-DPI Windows behavior correct when launching on one monitor and moving the app to another monitor with a different scale factor.

Evidence source: source inspection, manifest inspection, asset inspection, build/test validation, and prior local responsive screenshots in `docs/audits/responsive-practice`. Real mixed-monitor screenshots were not captured in this run because the code agent cannot switch the Windows display scale or physically move the app between two monitors.

## Findings

1. Manifest DPI awareness
   - Health before: mostly good.
   - Evidence: `TypingTrainer.App.csproj` references `app.manifest` and uses `WindowsPackageType=None`. The manifest already declared modern `dpiAwareness` as `PerMonitorV2`.
   - Risk: older DPI-awareness consumers did not have the legacy `dpiAware` fallback, and the modern value lacked the explicit `PerMonitor` fallback.
   - Fix: added `dpiAware=true/pm` and changed modern awareness to `PerMonitorV2, PerMonitor`.

2. Packaged versus unpackaged setup
   - Health: clear after inspection.
   - Evidence: `Package.appxmanifest` exists for WinUI/MSIX tooling, but the app project sets `WindowsPackageType=None` and points `ApplicationManifest` at `app.manifest`.
   - Fix: applied DPI changes to `app.manifest`, not the packaged manifest.

3. Manual physical-pixel window sizing
   - Health: good.
   - Evidence: no `AppWindow.Resize`, `MoveAndResize`, `SetWindowPos`, or raw resize code was found in app source.
   - Risk: none currently proven.
   - Fix: no app sizing conversion was needed.

4. HWND and native interop
   - Health: low risk.
   - Evidence: current HWND use is for file picker ownership through `InitializeWithWindow`.
   - Risk: file picker positioning is owned by Windows and should follow the active window; no manual conversion is needed.
   - Fix: added reusable DPI helper APIs for diagnostics and future native-window work.

5. Custom rendering
   - Health: acceptable with manual verification.
   - Evidence: charts, keyboard, and practice text use XAML `Canvas`, `ActualWidth`, `ActualHeight`, and logical units.
   - Risk: custom renderers can still create cramped labels at extreme window sizes or display scales.
   - Fix: previous responsive work reflowed main pages and added tests. This pass adds DPI diagnostics and the manual checklist.

6. Assets and icons
   - Health: good.
   - Evidence: `AppIcon.ico` contains 16, 24, 32, 48, 64, 128, and 256 px entries. Tile/splash PNG dimensions match their nominal asset names.
   - Risk: no obvious low-resolution icon problem found.
   - Fix: no asset changes required.

## Accessibility And UX Risks

- Mixed-DPI behavior still needs real hardware verification at 125%, 150%, 175%, and 200%.
- Screenshot evidence cannot prove per-monitor DPI message handling.
- Debug logging verifies DPI changes in development, but production behavior should still be visually checked.

## Changes Made

- Strengthened manifest DPI awareness declaration in `src/TypingTrainer.App/app.manifest`.
- Added `DpiHelper` for DPI, scale, logical/physical conversion, physical window bounds, and DPI-awareness description.
- Added debug-only `DpiDebugLogger` attached from `MainWindow`.
- Added unit tests for DPI scale and conversion math.
- Added `docs/dpi-testing.md` with manual mixed-DPI test cases.

## Follow-Up

- Run the checklist on real mixed-DPI hardware.
- If a future change introduces `AppWindow.Resize`, `MoveAndResize`, `SetWindowPos`, custom title-bar hit regions, or bitmap render targets, use `DpiHelper` and label whether the API expects logical or physical pixels.
