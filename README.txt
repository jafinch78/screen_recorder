OFFLINE SCREEN RECORDER 1607 - v1.0.1
=====================================
Windows 10 Anniversary Update (1607, build 14393) and later, x86/x64.
Standalone offline WinForms screen recorder (video + optional audio).

SCOPE
-----
This is the *recorder*, not an OCR recognizer. It creates screen-capture
video suitable for later OCR processing. It does not require Techstream,
BLE, Python, Internet connectivity, or a CAN logger.

MICROPHONE SELECTION / v1.0.1 AUDIO DIAGNOSTICS
------------------------------------------------
- The Audio source box supports both choosing a discovered microphone and
  typing the exact FFmpeg DirectShow device name.
- Click Refresh audio to scan available microphones.
- Click Audio log to see FFmpeg's raw input listing, device-name matches,
  its exit status, Windows legacy waveIn input count, and launch path.
  The audio scan log is saved at:
  %LOCALAPPDATA%\OfflineScreenRecorder1607\AUDIO_DEVICE_SCAN_LAST.log
- The No audio (silent) option records only video.
- If none appear: open Windows Sound > Recording using Win+R, mmsys.cpl.
  Check the input is present and enabled. On Windows 1607, also check
  Settings > Privacy > Microphone. Reconnect/re-enable USB audio if needed.
- You can type a device name manually if FFmpeg enumeration did not
  populate the dropdown. It must match a valid DirectShow capture name.
- If the mic is visible in Windows but not FFmpeg, share the audio scan
  log so the raw FFmpeg output can identify why it was omitted.
- Microphone capture is NOT automatically system/playback audio; Stereo
  Mix requires the operating system/driver to expose an input by that name.
- Windows 1607 microphone enumeration/audio capture still requires field
  validation. This package was not compiled on a Windows 1607 PC here.

BASELINE REUSED FROM TOYOTA TECHSTREAM CAPTURE
--------------------------------------------
FFmpeg gdigrab desktop input, DirectShow (dshow) optional audio input,
H.264/libx264 CRF20 (default), veryfast preset, AAC 128 kbps, and an
FFmpeg diagnostic log. All CAN/BLE sync, telemetry, and capture-session
coupling have been removed.

INSTALL / FIRST RUN
-------------------
1. Extract ZIP to a writable folder on the Windows 1607 or newer PC.
2. Close any older recorder EXE before updating. Run BUILD_WIN1607.bat
   once, or double click RUN_OFFLINE_RECORDER.bat. The launcher rebuilds
   if an older v1.0.0 EXE is found. This uses the C# compiler with .NET
   on Windows. No Visual Studio or Python needed.
3. The build script will copy C:\Tools\ffmpeg\bin\ffmpeg.exe (if present)
   or one on PATH to the same folder. Alternatively manually copy your
   Windows ffmpeg.exe into this folder (or tools\ffmpeg.exe).
   This package DOES NOT include FFmpeg binary or external dependencies.
   The GUI also accepts a custom path to FFmpeg.
4. Open OfflineScreenRecorder1607.exe. Choose output folder and audio
   device. Click "Check FFmpeg" to verify gdigrab, dshow and encoders.
   Click Refresh audio, then Audio log if no microphone appears.
5. Select resolution, frame rate, quality, countdown and optional cursor.
6. Press START RECORDING. During countdown press Start again to cancel.
   Recording can be stopped with Ctrl+Shift+F9 even if minimized.
7. Press STOP / SAVE MP4. Do not close the GUI while finalizing.
   The final video is SCREEN_YYYYMMDD_HHMMSS.mp4.

OUTPUTS
-------
SCREEN_YYYYMMDD_HHMMSS.mp4          final video, H.264 + optional AAC audio
SCREEN_YYYYMMDD_HHMMSS.ffmpeg.log   command, event and encoder diagnostic log
SCREEN_YYYYMMDD_HHMMSS.capture.mkv  temporary recording; automatically
                                   deleted only after successful MP4 remux
*.mp4.partial.mp4                   staging file if interrupted in remux

WHY MKV FIRST?
--------------
A crash during ordinary MP4 recording can leave an unusable video header.
MKV is more resilient. After a normal stop, FFmpeg remuxes to MP4 without
lossy re-encoding, then deletes the MKV only when MP4 creation succeeded.
If FFmpeg fails or MP4 remux fails, the MKV is retained for recovery.

GUI OPTIONS
-----------
Resolution: Original, 1920x1080, 1280x720, 854x480, 640x360.
            Predefined sizes preserve the source's aspect ratio with padding.
            Original is the best option for later OCR of small text.
FPS:        10, 15, 24, 30 fps (30 default).
Quality:    High CRF18, Balanced CRF20 (default), Compact CRF25.
Countdown:  0, 3, 5, 10 seconds before recording starts.
Audio:      select a DirectShow microphone, Stereo Mix (if driver exposes
            it), or No audio. A microphone does NOT automatically capture
            application / speaker audio. Selecting "Stereo Mix" captures
            playback only on machines/drivers that offer it.
Other:      capture cursor, minimize at recording start, Ctrl+Shift+F9 hotkey.
            Audio is AAC 128 kbps; frame output uses H.264 yuv420p.

LIMITATIONS AND KNOWN COMPATIBILITY ITEMS
-----------------------------------------
- Fully offline after the FFmpeg executable is made available locally.
- This archive is SOURCE + ONE-TIME LOCAL BUILD SCRIPTS. It is NOT a
  precompiled Windows EXE. BUILD_WIN1607.bat creates the EXE on Windows.
- A Windows FFmpeg build that supports gdigrab, dshow, libx264, and AAC
  must be supplied. The previous Techstream installation used FFmpeg 9.0.1
  Essentials, verified by the user on Windows; the app can use that build.
- If screen is multiple monitors, gdigrab desktop captures the entire
  Windows virtual desktop, not just the active monitor.
- This version does not offer pausing, selectable crop region, dual-source
  audio mixing, system-audio loopback without Stereo Mix, or OCR recognition.
- Protected video / DRM content and Windows secure desktop/UAC prompts
  are not reliably capturable with GDI.
- Preset output dimensions can upscale a lower-resolution desktop.
- If FFmpeg fails quickly, inspect the *.ffmpeg.log beside the attempted file.
- Tested here by static inspection and synthetic audio-listing fixtures,
  not by running on Windows 1607. A real-device audio test is needed.

COPYING A PREBUILT PORTABLE PACKAGE TO ANOTHER PC
------------------------------------------------
After running the build once, copy OfflineScreenRecorder1607.exe and
ffmpeg.exe together to any writable folder on a Windows 1607+ PC.
No installation, Python runtime, Visual Studio or Internet is needed
for normal operation. Recording preferences are stored per-user in
%APPDATA%\OfflineScreenRecorder1607\preferences.ini.

BUILD TROUBLESHOOTING
---------------------
BUILD_WIN1607_LAST.log has the first error and C# compiler diagnostics.
The build uses:
  %WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
or falls back to Framework\v4.0.30319\csc.exe.
If an EXE has a compiler error, share BUILD_WIN1607_LAST.log.
No changes are made to other Techstream/Evidence Builder installations.