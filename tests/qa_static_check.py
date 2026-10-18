"""Development-only static checks for the provided source/package (runs without Windows)."""
from pathlib import Path
root = Path(__file__).resolve().parent.parent
s = (root / 'source' / 'Program.cs').read_text(encoding='utf-8')
checks = {
 'WinForms': 'Application.Run(new RecorderForm())' in s,
 'gdigrab': '"gdigrab"' in s,
 'dshow': '"dshow"' in s,
 'editable audio selector': 'cbAudio.DropDownStyle = ComboBoxStyle.DropDown' in s,
 'manual audio wired to encoder': 'AudioSelection(cbAudio.Text)' in s and 'audio=" + selectedAudio' in s,
 'audio diagnostics': 'AUDIO_DEVICE_SCAN_LAST.log' in s and 'OpenAudioLog()' in s,
 'native windows audio count': 'waveInGetNumDevs()' in s,
 'H264': '"libx264"' in s,
 'AAC': '"aac"' in s,
 '128kbps audio': '"128k"' in s,
 'Original + 4 resolution presets': all(x in s for x in ('1920, 1080','1280, 720','854, 480','640, 360')),
 'countdown options': '0, 3, 5, 10' in s,
 'source supports preflight': 'private void TestFfmpeg()' in s,
 'partial MP4 and MKV recovery': '.capture.mkv' in s and '.partial.mp4' in s,
 'global record hotkey': 'RegisterHotKey' in s and 'Keys.F9' in s,
 'local config folder': 'preferences.ini' in s,
 'no network access': not any(p in s for p in ('HttpWebRequest','WebClient','HttpClient','Socket(')),
 'one-time Windows compiler script': 'csc.exe' in (root / 'BUILD_WIN1607.bat').read_text(),
}
for k, v in checks.items(): print(('PASS' if v else 'FAIL'),k)
if not all(checks.values()): raise SystemExit(1)