# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

**FolioDesk** is a Windows desktop application (WPF, .NET 10, C#) that brings iOS/Android-style app folders to the Windows desktop. Users organize desktop shortcuts into folders; clicking a folder shortcut shows a popup grid of the apps inside.

## Build & Run

```powershell
# Build
dotnet build FolioDesk/FolioDesk.csproj -c Release

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
| UI (WPF, code-behind) | `MainWindow`, `FolioFolderWindow`, `IconSettingsWindow` | XAML + code-behind for view-specific mouse, animation, and Win32 behavior; **not full MVVM** |
| Application | `Application/*Service.cs` | User operations, compensation, and orchestration across persistence, files, icons, and shortcuts |
| Domain | `Models/FolioData.cs` | Persistence-neutral `FolioData`, `FolioFolder`, and `FolioItem` records |
| Infrastructure | `Infrastructure/` | Fresh-read JSON repository, local item storage, and cross-process mutation lock |
| Icons | `Icons/IconExtractor.cs`, `Icons/IconGenerator.cs`, `Icons/WindowsIconService.cs` | Win32 icon extraction and composite folder icon generation behind `IIconService` |
| Services | `Services/LocalizationService.cs` | Runtime XAML ResourceDictionary swapping for i18n |
| Shortcuts | `ShortCuts/ShortCutManager.cs` | Creates/updates `.lnk` files through the `WScript.Shell` COM adapter |

`AppComposition` manually creates services and adapters. There is no DI container or assembly scanning. Constructors do not load JSON or initialize COM, preserving the launch path's cold-start characteristics.

### Data Persistence

All data lives in `%LocalAppData%\FolioDesk\`:
- `folio.json` — all `FolioFolder` / `FolioItem` records
- `language.cfg` — user language preference
- `icons/{folderId}/` — generated `.ico` files and cached app icon PNGs

`JsonFolioRepository` reads the latest JSON for each operation rather than caching it for the process lifetime. Mutations acquire a named mutex so separate FolioDesk launch-mode processes cannot overwrite each other's stale snapshots. Saves use a unique same-directory temporary file followed by atomic replacement and backup.

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

- **Selective presentation separation** — keep mouse coordinates, drag ghosts, animations, focus, and Win32 window behavior in code-behind. Put multi-resource user operations in Application Services; do not add ViewModels to trivial windows solely for consistency.
- **Manual composition** — create dependencies through `AppComposition`; do not introduce a runtime-scanning DI container without a measured need.
- **No eager persistence** — repository and adapter constructors must remain free of file reads, COM creation, or other startup I/O.
- **Application boundary** — windows call Application Services and queries, not JSON, file storage, icon generation, or shortcut adapters directly.
- **Win32 P/Invoke** — shell integration uses `Shell32`, GDI+, and late-bound `WScript.Shell` COM; treat these as platform-specific and Windows-only.
- **Installer** — `FolioDesk.iss` (Inno Setup 6.x); installs to `%LocalAppData%\FolioDesk` with lowest (non-admin) privileges and depends on .NET 10 runtime.
- **Naming** — private fields use `_camelCase`; constants use `UPPER_CASE`.
