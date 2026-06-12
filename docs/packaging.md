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

The script can be launched from any working directory. It resolves paths from the repository root, publishes the app in `Release` for `win-x64`, and then compiles the Inno Setup script.

The publish output is created at:

```text
artifacts\publish\TypingTrainer.App
```

The final setup executable is created at:

```text
artifacts\installer\TypingTrainerSetup.exe
```

## Regenerate The Inno Script

The checked-in Inno Setup script can be regenerated with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\generate-installer-script.ps1
```

## Notes

The script cleans only packaging artifacts under `artifacts\publish` and `artifacts\installer`. It does not remove source code or change app behavior.

The installer uses `installer\TypingTrainer.iss` and targets x64 Windows. It installs to `{autopf}\Typing Trainer`, creates a Start Menu shortcut, offers an optional Desktop shortcut, registers an uninstall entry, and offers to launch Typing Trainer after installation.

When installing over an existing copy, the installer uses the same stable `AppId`, asks Windows to close `TypingTrainer.App.exe` if it is running, cleans the old files under `{app}`, and then copies the newly published folder. This keeps stale DLLs or removed dependency folders from lingering between releases.
