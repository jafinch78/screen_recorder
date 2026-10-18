# Offline Screen Recorder 1607

Portable, offline **Windows 10 version 1607 (build 14393) and later** screen recorder with optional microphone audio. The GUI is written in C# WinForms and is built locally using the C# compiler included with the Windows .NET Framework installation. **No Python, Visual Studio, BLE, or Internet connection is required to run the recorder.** A compatible Windows FFmpeg executable is required separately.

This app was derived from the FFmpeg desktop/audio recording approach used in the Toyota Techstream Capture workflow, but is a standalone **screen-and-audio recorder**: it does not perform OCR, access vehicle hardware, or collect CAN telemetry.

## Recording options

| Control | Choices |
| --- | --- |
| Output resolution | Original, 1920×1080, 1280×720, 854×480, 640×360 |
| Frame rate | 10 / 15 / 24 / 30 fps |
| Quality | High (CRF 18), Balanced (CRF 20), Compact (CRF 25) |
| Audio | DirectShow microphone/source, manually typed device name, or silent |
| Countdown | 0 / 3 / 5 / 10 seconds |
| Output | User-selected directory, H.264 MP4, optional AAC audio |
| Other | Include cursor, minimize on start, Ctrl+Shift+F9 start/stop |

For later OCR processing, **Original** resolution preserves small text most accurately. Preset output sizes preserve source aspect ratio using padding.

## Setup on Windows 1607+

1. Download or clone the repository to a **writable** directory.
2. Provide a Windows `ffmpeg.exe` supporting `gdigrab`, `dshow`, `libx264`, and `aac`: put it beside the build scripts, in `tools\\ffmpeg.exe`, or select its absolute path in the GUI. The build script can also find it at `C:\\Tools\\ffmpeg\\bin\\ffmpeg.exe` or on `PATH`.
3. Run `RUN_OFFLINE_RECORDER.bat`. This automatically invokes `BUILD_WIN1607.bat` if the v1.0.1 executable hasn't been built yet. Alternatively run `BUILD_WIN1607.bat` first and launch the resulting `OfflineScreenRecorder1607.exe`.
4. Use **Check FFmpeg** and **Refresh audio**. Choose the screen-capture settings and an output folder.
5. Press **START RECORDING**, then **STOP / SAVE MP4** or use Ctrl+Shift+F9.

Once built, `OfflineScreenRecorder1607.exe` with a working `ffmpeg.exe` can be copied to another compatible Windows machine and run without development tools or connectivity.

**The repository does not include FFmpeg binaries or a precompiled Windows EXE.** FFmpeg is a separate third-party project subject to its own distribution and licensing terms.

## Audio/microphone troubleshooting

The source selector is **editable**. Click **Refresh audio** to enumerate FFmpeg DirectShow inputs. If no microphone is listed, open **Audio log** to inspect FFmpeg's unmodified device enumeration results along with a Windows `waveInGetNumDevs()` count. Diagnostic output:

`%LOCALAPPDATA%\OfflineScreenRecorder1607\AUDIO_DEVICE_SCAN_LAST.log`

You can enter an exact DirectShow device name manually. To view devices that Windows exposes, run `mmsys.cpl` and inspect **Recording**. Recording a microphone does **not** automatically capture system playback audio; it requires an input such as Stereo Mix when supported.

## Safe recording and files

- FFmpeg captures the Windows virtual desktop using `gdigrab` and optionally microphone input using `dshow`.
- A `.capture.mkv` file is recorded first, then remuxed to `.mp4` on normal stop without re-encoding. If remuxing fails, the MKV is retained for recovery.
- An adjacent `.ffmpeg.log` records encoder diagnostics.
- Preferences are saved per-user under `%APPDATA%\OfflineScreenRecorder1607\preferences.ini`.
- Build diagnostics are written to `BUILD_WIN1607_LAST.log`.

## Limitations and test status

- Single desktop capture (all monitors as a virtual desktop), not a specific window or cropped region.
- No native system-audio loopback, audio mixing, pause/resume, or OCR recognition.
- Secure desktops/UAC prompts and protected video may be inaccessible to GDI capture.
- Static source tests and synthetic FFmpeg audio-device-list parsing tests passed for v1.0.1; resolution filters were exercised with synthetic video. Full Windows 1607 field testing was **not independently performed by the package author**. A user subsequently reported the updated GUI was working, but exact OS build and complete microphone/MP4 playback validation were not documented.

For detailed behavior, see [README.txt](README.txt) and [tests/QA_SUMMARY.txt](tests/QA_SUMMARY.txt). Source is [source/Program.cs](source/Program.cs).

## Project licensing

No source-code license has been selected for this repository yet. Public availability on GitHub does not itself grant a reuse license. FFmpeg and any other third-party binaries are **not** included.