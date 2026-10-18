# Changelog

## 1.0.1 - 2026-10-18
- Added editable DirectShow audio source for manual microphone selection.
- Improved microphone discovery and saved raw audio enumeration diagnostics.
- Added legacy Windows `waveInGetNumDevs()` count in audio diagnostics.
- Added `Audio log` display and reliable rebuilding of older GUI versions.

## 1.0.0 - 2026-10-18
- Initial portable WinForms recorder based on FFmpeg `gdigrab` and optional `dshow`.
- Resolution, frame rate, encoding quality, countdown, cursor and output folder settings.
- Hotkey and crash-tolerant MKV recording followed by MP4 remux.