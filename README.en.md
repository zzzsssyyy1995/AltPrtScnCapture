<div align="center">
  <img src="app-icon.png" width="128" height="128" alt="AltPrtScnCapture icon">
  <h1>AltPrtScnCapture</h1>
  <p>A lightweight, portable and privacy-friendly active-window capture tool for Windows.</p>
  <p>
    <a href="https://github.com/zzzsssyyy1995/AltPrtScnCapture/releases/latest"><strong>Download the latest release</strong></a>
    ·
    <a href="README.md">简体中文</a>
  </p>

  ![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
  ![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet&logoColor=white)
  ![Release](https://img.shields.io/github/v/release/zzzsssyyy1995/AltPrtScnCapture?display_name=tag)
</div>

## Features

- Capture the foreground window with the global `Alt + PrtScn` hotkey.
- Save PNG files automatically using timestamp-based names.
- Start and stop monitoring from the system tray; monitoring is off at startup.
- Choose and remember the screenshot directory.
- Merge PNG/JPG/JPEG files by filename into a multi-page A4 PDF.
- Run as a single EXE with no administrator privileges or third-party runtime packages.
- Keep all screenshots and PDF processing local—nothing is uploaded.

## Quick start

1. Download the Windows archive from [Releases](https://github.com/zzzsssyyy1995/AltPrtScnCapture/releases/latest).
2. Extract it and run `AltPrtScnCapture.exe`.
3. Select a screenshot folder on first launch, or cancel to use the Pictures folder.
4. Right-click the blue tray icon and choose **开始检测** (Start monitoring).
5. Press `Alt + PrtScn` to capture the active window.

> The executable is not commercially code-signed. Windows SmartScreen may show an “Unknown publisher” warning on first launch.

## Tray menu

| Item | Action |
| --- | --- |
| 开始检测 | Register the global hotkey |
| 停止检测 | Unregister the hotkey |
| 设置保存目录 | Choose the output directory |
| 合并为PDF | Merge supported images by filename |
| 退出 | Close the application |

## Build from source

Requirements: Windows 10/11 and .NET Framework 4.8.

```powershell
git clone https://github.com/zzzsssyyy1995/AltPrtScnCapture.git
cd AltPrtScnCapture
.\build.ps1
```

The executable is written to `dist\AltPrtScnCapture.exe`. The build script uses the .NET Framework C# compiler included with Windows and does not download dependencies.

## Notes and limitations

- A conflicting application may prevent registration of `Alt + PrtScn`.
- Secure desktops, lock screens and protected content may not be capturable.
- Screen-pixel capture can include another window if it overlaps the target window.
- Settings are stored at `%LOCALAPPDATA%\AltPrtScnCapture\settings.txt`.

Please report bugs through [GitHub Issues](https://github.com/zzzsssyyy1995/AltPrtScnCapture/issues), without attaching screenshots that contain sensitive information.
