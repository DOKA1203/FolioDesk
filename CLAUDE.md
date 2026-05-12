# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**FolioDesk** is a Windows desktop application (WPF, .NET 10, C#) that brings iOS/Android-style app folders to the Windows desktop. Users organize desktop shortcuts into folders; clicking a folder shortcut shows a popup grid of the apps inside.

## Build & Run

```powershell
# Build
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe' 'FolioDesk\FolioDesk.csproj' /t:Restore,Build /p:Configuration=Release /m

# Run (main manager UI)
dotnet run --project FolioDesk/FolioDesk.csproj

# Publish (self-contained for release)
dotnet publish FolioDesk/FolioDesk.csproj -c Release -r win-x64 --self-contained false
```

There are no automated tests. The app is Windows-only (WPF + Win32 P/Invoke).

## Launch Modes

`App.xaml.cs` routes startup based on command-line arguments:
- **No args** → `MainWindow` (folder manager UI)
- **One arg** (folder ID) → `FolioFolderWindow` at cursor position (folder popup)
- **Two args** (folder ID + exe path) → silently adds app to folder and exits

Desktop shortcuts are `.lnk` files that call `FolioDesk.exe <folderId>`.

## Architecture

### Layers

| Layer | Key Files | Role |
|---|---|---|
| UI (WPF, code-behind) | `MainWindow`, `FolioFolderWindow`, `IconSettingsWindow` | All windows use XAML + code-behind, **not MVVM** |
| Data | `Models/FolioData.cs` | `FolioDataManager` (singleton via `App.DataManager`) — JSON CRUD |
| Icons | `Icons/IconExtractor.cs`, `Icons/IconGenerator.cs` | Win32 icon extraction; composite folder icon generation |
| Services | `Services/LocalizationService.cs` | Runtime XAML ResourceDictionary swapping for i18n |
| Shortcuts | `ShortCuts/ShortCutManager.cs` | Creates/updates `.lnk` files on the desktop via COM Interop |

### Data Persistence

All data lives in `%LocalAppData%\FolioDesk\`:
- `folio.json` — all `FolioFolder` / `FolioItem` records
- `language.cfg` — user language preference
- `icons/{folderId}/` — generated `.ico` files and cached app icon PNGs

### Icon Extraction (`IconExtractor.cs`)

This is the most complex module (~640 lines). It handles:
- `.exe`/`.dll` — Win32 Shell image list (JUMBO → EXTRALARGE → LARGE fallback)
- `.lnk` shortcuts — resolves target, then recurses
- UWP/Store apps — registry lookup → `AppxManifest.xml` → scale-variant PNG
- Launcher-based apps (e.g. Valorant) — `IconLocation` field parsing

### Folder Icon Generation (`IconGenerator.cs`)

Creates a 256×256 PNG with up to 4 app icons on a rounded-rect colored background, then wraps it in an `.ico` container. GUID-based filenames; cleans up old `.ico` files on regeneration.

### Localization

Four languages (Korean default, English, Chinese, Japanese) via XAML resource dictionaries in `Resources/Strings/`. `LocalizationService.Get(key)` is a static helper. Language persists to `language.cfg`.

## Key Conventions

- **No MVVM** — all UI logic lives in code-behind (`.xaml.cs`) files.
- **Singleton data manager** — always access via `App.DataManager`.
- **Win32 P/Invoke** — shell integration uses `Shell32`, `GDI+`, and `IWshRuntimeLibrary` COM; treat these as platform-specific and Windows-only.
- **Installer** — `FolioDesk.iss` (Inno Setup 6.x); installs to `%LocalAppData%\FolioDesk` with lowest (non-admin) privileges and depends on .NET 10 runtime.
- **Naming** — private fields use `_camelCase`; constants use `UPPER_CASE`.
