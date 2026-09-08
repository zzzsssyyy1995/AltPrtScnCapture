# Changelog

## 1.2.0 - 2026-09-08

- Combined administrator restart and Print Screen capture into one capture mode.
- Default to Alt + PrtScn for new installations.
- Selecting administrator Print Screen mode requests UAC and automatically starts capture after the previous process exits.
- Cancelling UAC preserves the original mode and monitoring state.
- Removed the standalone administrator restart menu; tray tooltip reports actual privilege and monitoring state.
- Wait for PDF merging to finish before restarting.
- Fixed resource argument quoting for PowerShell 7 builds.

## 1.1.2 - 2026-08-13

- Added an on-demand **Restart as administrator** tray command.
- Added a visible `管理员模式 ✓` status when the current instance is elevated.
- Kept the existing instance running when the UAC prompt is cancelled or elevation fails.
- Cleanly unregisters keyboard hooks before the non-elevated instance exits.

## 1.1.1 - 2026-08-13

- Replaced bare `PrtScn` hotkey registration with a low-level keyboard hook.
- Suppressed the Windows snipping overlay while `PrtScn` monitoring is enabled.
- Kept modified Print Screen combinations available to Windows.
- Kept the keyboard hook callback lightweight by posting capture work to the application message loop.

## 1.1.0 - 2026-08-13

- Changed the default capture hotkey to `PrtScn`.
- Added a tray submenu for switching between `PrtScn` and `Alt + PrtScn`.
- Added persistent hotkey selection and checked-state feedback.
- Added live hotkey switching with automatic rollback on registration failure.
- Added an `Alt + PrtScn` fallback prompt when `PrtScn` cannot be registered.

## 1.0.0 - 2026-08-11

- Added opt-in global `Alt + PrtScn` monitoring.
- Added foreground-window PNG capture with timestamp filenames.
- Added configurable and persistent screenshot directory.
- Added filename-sorted PNG/JPG/JPEG to PDF merging.
- Added a Windows tray menu and notification feedback.
- Added embedded multi-resolution application and tray icons.
- Added Chinese and English project documentation.
