# Packaging

Typing Trainer is a WinUI/.NET 8 desktop app. WinUI self-contained publish output normally contains many DLLs, WinMD files, PRI files, runtime files, and localization folders. Do not remove those files from the publish folder; they are required for the app to run on another machine.

The installer bundles that full publish folder into one setup executable. Users only receive `TypingTrainerSetup.exe`; during install, the setup program copies the published app files under `Program Files\Typing Trainer`, creates shortcuts, and registers the uninstaller.

## Prerequisite

Install Inno Setup 6 if it is not already installed:

https://jrsoftware.org/isinfo.php

The build script looks for the compiler at:

- `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`
- `C:\Program Files\Inno Setup 6\ISCC.exe`

## Build The Installer

From PowerShell, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

The script can be launched from any working directory. It resolves paths from the repository root, reads version and publisher metadata from `src\TypingTrainer.App\TypingTrainer.App.csproj`, publishes the app in `Release` for `win-x64`, and then compiles the Inno Setup script.

The publish output is created at:

```text
artifacts\publish\TypingTrainer.App
```

The final setup executable is created at:

```text
artifacts\installer\TypingTrainerSetup.exe
```

## Full Release Build

For a release candidate, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1
```

The release script regenerates icons, builds the solution in `Release x64`, runs tests, publishes the WinUI app, and creates the installer.

## Code Signing

The scripts support signing both `TypingTrainer.App.exe` and `TypingTrainerSetup.exe` when a signing certificate is configured.

To sign with a certificate installed in the Windows certificate store:

```powershell
$env:TYPINGTRAINER_SIGNING_THUMBPRINT = "<certificate thumbprint>"
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1
```

To sign with a PFX file:

```powershell
$env:TYPINGTRAINER_SIGNING_PFX = "C:\path\to\certificate.pfx"
$env:TYPINGTRAINER_SIGNING_PFX_PASSWORD = "<pfx password>"
powershell -ExecutionPolicy Bypass -File .\scripts\release.ps1
```

If no signing configuration is present, signing is skipped and the installer is still produced. A real trusted code signing certificate is required to reduce Windows SmartScreen warnings for users outside your own machines.

## Regenerate The Inno Script

The checked-in Inno Setup script can be regenerated with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\generate-installer-script.ps1
```

## Regenerate App Icons

The app icon PNGs and `Assets\AppIcon.ico` can be regenerated with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\generate-app-icons.ps1
```

## Notes

The script cleans only packaging artifacts under `artifacts\publish` and `artifacts\installer`. It does not remove source code or change app behavior.

The installer uses `installer\TypingTrainer.iss` and targets x64 Windows. It installs to `{autopf}\Typing Trainer`, uses `Assets\AppIcon.ico` for the setup icon, includes publisher URLs pointing to `https://gundeepsidhu.dev`, creates a Start Menu shortcut, offers an optional Desktop shortcut, registers an uninstall entry, and offers to launch Typing Trainer after installation.

When installing over an existing copy, the installer uses the same stable `AppId`, asks Windows to close `TypingTrainer.App.exe` if it is running, cleans the old files under `{app}`, and then copies the newly published folder. This keeps stale DLLs or removed dependency folders from lingering between releases. User data is stored under LocalAppData, not Program Files, so install-folder cleanup should not remove typing history or settings.

## Clean Install Checklist

Before sharing a release broadly, test `artifacts\installer\TypingTrainerSetup.exe` on a clean Windows x64 machine or VM:

1. Install with the default Program Files path.
2. Confirm the Start Menu shortcut launches Typing Trainer.
3. Install again over the existing install and confirm it upgrades in place.
4. Confirm typing history/settings survive the upgrade.
5. Uninstall from Windows Settings and confirm the app entry is removed.
6. Reinstall with the optional Desktop shortcut checked and confirm the shortcut icon appears correctly.
