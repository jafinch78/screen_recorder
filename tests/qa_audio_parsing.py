"""Synthetic FFmpeg device-list fixtures model expected C# parsing behavior.
They do not substitute for Windows 1607 device discovery testing.
"""
import re
from pathlib import Path
src = (Path(__file__).parent.parent/'source'/'Program.cs').read_text()
assert 'private static List<string> ParseAudioDevices' in src
assert 'Alternative name' in src

def parse_devices(text):
    names = []
    audio = False
    for line in text.splitlines():
        if 'directshow audio devices' in line.lower():
            audio = True
            continue
        if 'directshow video devices' in line.lower():
            audio = False
            continue
        if 'alternative name' in line.lower():
            continue
        m = re.search(r'"([^"]+)"(.*)$', line)
        if not m:
            continue
        if not audio and not re.search(r'\(\s*audio\s*\)', m.group(2), re.I):
            continue
        name = m.group(1).strip()
        if name and name.lower() not in {n.lower() for n in names}:
            names.append(name)
    return names

samples = [
    ('''[dshow @ 000001234] DirectShow video devices (some may be both video and audio devices)
[dshow @ 000001234]  "Integrated Webcam"
[dshow @ 000001234]   Alternative name "@device_pnp_\\?\\usb#vid_..."
[dshow @ 000001234] DirectShow audio devices
[dshow @ 000001234]  "Microphone (Realtek(R) Audio)"
[dshow @ 000001234]   Alternative name "@device_cm_{33D9A762}\\wave_{...}"
[dshow @ 000001234]  "Microphone Array (Intel Smart Sound)"
Error opening input file dummy.''', ['Microphone (Realtek(R) Audio)', 'Microphone Array (Intel Smart Sound)']),
    ('''[dshow @ a1] "Webcam USB" (video)
[dshow @ a1] "Microphone USB" (audio)
[dshow @ a1]   Alternative name "@device_cm_abc"''', ['Microphone USB']),
    ('''[dshow @ a1] DirectShow audio devices
[dshow @ a1]  "Microphone (USB Audio Device)"
[dshow @ a1]  "Microphone (USB Audio Device)"
[dshow @ a1]   Alternative name "@device_cm_abc"''', ['Microphone (USB Audio Device)']),
    ('[dshow @ a1] DirectShow audio devices\n[dshow @ a1] Could not enumerate audio devices', []),
]
for i, (sample, expected) in enumerate(samples):
    result = parse_devices(sample)
    assert result == expected, f'case {i}: {result} != {expected}'
    print('PASS: parser fixture', i)
print('PASS: synthetic FFmpeg audio enumeration fixtures; live Windows verification pending')