<div align="center">

<img src="Installer/logo.png" alt="FolioDesk Logo" width="96" height="96" />

# FolioDesk

**Mobile-style app folders for your Windows desktop**

[![Release](https://img.shields.io/github/v/release/doka1203/FolioDesk?style=flat-square&color=4A90D9)](https://github.com/doka1203/FolioDesk/releases)
[![Downloads](https://img.shields.io/github/downloads/doka1203/FolioDesk/total?style=flat-square&color=4A90D9)](https://github.com/doka1203/FolioDesk/releases)
[![License](https://img.shields.io/github/license/doka1203/FolioDesk?style=flat-square&color=gray)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows-0078d4?style=flat-square&logo=windows)](https://github.com/doka1203/FolioDesk/releases)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)

[Download](#-download) · [Features](#-features) · [Usage](#-usage) · [Contributing](#-contributing) · [한국어](README.md)

---

</div>

## 📖 About

FolioDesk brings the iOS / Android app folder experience to your Windows desktop.

Group scattered desktop icons into folders just like on mobile, then open and close them with a single click.  
No complex setup required — works immediately after installation and leaves zero footprint when closed (no background process).

## 🎬 Demo

[![FolioDesk Demo](https://img.youtube.com/vi/fOiZs36iT4k/maxresdefault.jpg)](https://www.youtube.com/watch?v=fOiZs36iT4k)

## ✨ Features

- **Mobile-style app folders** — Group and organize apps into folders the same way iOS / Android does
- **Desktop shortcut** — Double-click a folder shortcut to open it as a popup at your cursor position
- **Drag-and-drop to add apps** — Drop any `.exe` or shortcut onto a folder shortcut to register it instantly
- **Reorder icons** — Drag icons within a folder to rearrange them freely
- **Extract back to desktop** — Drag an icon outside the folder to restore it to the desktop
- **Auto icon extraction** — App icons are extracted automatically and composited into a folder thumbnail
- **Multi-language** — Korean / English / Chinese / Japanese (switch with one click)
- **Lightweight** — No background process. Launches on demand and exits when the folder is closed

## 💾 Download

| Platform | Download |
|----------|---------|
| Windows 10 / 11 (64-bit) | [**Get the latest release →**](https://github.com/doka1203/FolioDesk/releases/latest) |

> No administrator privileges required. Installs to `%LocalAppData%\FolioDesk`.

## 🚀 Getting Started

1. Download `FolioDesk_Setup.exe` from the [latest release](https://github.com/doka1203/FolioDesk/releases/latest).
2. Run the installer. (no admin rights needed)
3. FolioDesk will launch automatically after installation.

## 📋 Usage

### Creating a folder

1. Open the FolioDesk main window and click **Create Folder**.
2. A folder shortcut will appear on your desktop.

### Adding apps

- **Drag and drop** any `.exe` file (or shortcut) from your desktop onto the folder shortcut.
- The app icon is extracted automatically and the app is registered in the folder.

### Opening a folder

- **Double-click** a folder shortcut — the folder popup opens at your cursor position.
- Click an app icon to launch it and close the folder.
- Click anywhere outside the folder to close it.

### Extracting / reordering apps

- **Drag an icon outside the folder** to move it back to the desktop.
- **Drag an icon onto another icon** inside the folder to reorder them.

### Changing the language

- Click the language toggle button in the main window to cycle through:  
  Korean → English → Chinese → Japanese.

## 🛠 Tech Stack

| Item | Detail |
|------|--------|
| Language | C# |
| Framework | .NET 10, WPF |
| Data storage | JSON (`%LocalAppData%\FolioDesk\folio.json`) |
| Installer | Inno Setup |
| Icon extraction | Win32 API (Shell32, ExtractIconEx) |

## 🤝 Contributing

Bug reports, feature requests, and pull requests are all welcome!

1. Fork this repository.
2. Create a new branch (`git checkout -b feature/amazing-feature`).
3. Commit your changes (`git commit -m 'Add amazing feature'`).
4. Push to the branch (`git push origin feature/amazing-feature`).
5. Open a Pull Request.

For bugs or suggestions, please open an [Issue](https://github.com/doka1203/FolioDesk/issues).

## 📄 License


This project is licensed under the GNU General Public License v3.0 - see the [LICENSE](LICENSE) file for details.

Copyright (c) 2026 doka1203