// Offline Screen Recorder 1607, v1.0.1
// WinForms/.NET Framework 4.x, Windows 10 version 1607 and later.
// FFmpeg gdigrab + optional DirectShow audio; H264/AAC recording to MKV,
// then safe MP4 remux. No Python, BLE, OCR engine or network is required.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace OfflineScreenRecorder1607
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new RecorderForm());
        }
    }

    internal enum RecorderState { Ready, Countdown, Recording, Finalizing }

    internal sealed class RecorderForm : Form
    {
        private const int HotkeyId = 6177;
        private const int WmHotkey = 0x0312;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        private TextBox txtFfmpeg, txtFolder, txtStatus, txtFile;
        private ComboBox cbAudio, cbResolution, cbFps, cbQuality, cbCountdown;
        private CheckBox chkCursor, chkMinimize;
        private Button btnRecord, btnStop, btnRefresh, btnTest, btnFolder, btnAudioLog;
        private Label lblClock;
        private readonly System.Windows.Forms.Timer uiTimer;
        private readonly object logLock = new object();
        private readonly Stopwatch elapsed = new Stopwatch();
        private RecorderState state = RecorderState.Ready;
        private Process captureProcess;
        private StreamWriter logWriter;
        private DateTime countdownEnd;
        private bool isHotkeyRegistered;
        private bool stopRequested;
        private bool finalizerStarted;
        private string outputMp4 = "", temporaryMkv = "", outputLog = "";
        private string lastError = "";
        private readonly string settingsPath;
        private readonly string audioLogPath;

        public RecorderForm()
        {
            Text = "Offline Screen Recorder - Windows 1607+";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(720, 560);
            MinimumSize = new Size(680, 550);
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoScroll = true;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                        "OfflineScreenRecorder1607", "preferences.ini");
            audioLogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                        "OfflineScreenRecorder1607", "AUDIO_DEVICE_SCAN_LAST.log");

            Label heading = MakeLabel("Screen + audio recording (offline)", 20, 15, 570, 28);
            heading.Font = new Font(Font.FontFamily, 14F, FontStyle.Bold);
            Controls.Add(heading);
            Controls.Add(MakeLabel("FFmpeg executable", 20, 58, 200, 20));
            txtFfmpeg = MakeText(20, 80, 550);
            Controls.Add(txtFfmpeg);
            Button browseFfmpeg = MakeButton("Browse...", 583, 79, 112);
            browseFfmpeg.Click += delegate { SelectFfmpeg(); };
            Controls.Add(browseFfmpeg);

            Controls.Add(MakeLabel("Output folder", 20, 119, 180, 20));
            txtFolder = MakeText(20, 141, 550);
            Controls.Add(txtFolder);
            Button browseFolder = MakeButton("Browse...", 583, 140, 112);
            browseFolder.Click += delegate { SelectFolder(); };
            Controls.Add(browseFolder);

            Controls.Add(MakeLabel("Audio source (select or type an exact device name)", 20, 180, 485, 20));
            btnAudioLog = MakeButton("Audio log", 596, 174, 99);
            btnAudioLog.Click += delegate { OpenAudioLog(); };
            Controls.Add(btnAudioLog);
            cbAudio = MakeCombo(20, 202, 405);
            cbAudio.DropDownStyle = ComboBoxStyle.DropDown;
            cbAudio.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            cbAudio.AutoCompleteSource = AutoCompleteSource.ListItems;
            cbAudio.Items.Add("No audio (silent)");
            cbAudio.SelectedIndex = 0;
            Controls.Add(cbAudio);
            btnRefresh = MakeButton("Refresh audio", 438, 201, 122);
            btnRefresh.Click += delegate { RefreshAudio(); };
            Controls.Add(btnRefresh);
            btnTest = MakeButton("Check FFmpeg", 573, 201, 122);
            btnTest.Click += delegate { TestFfmpeg(); };
            Controls.Add(btnTest);

            Controls.Add(MakeLabel("Output resolution", 20, 241, 155, 20));
            Controls.Add(MakeLabel("Frame rate", 208, 241, 145, 20));
            Controls.Add(MakeLabel("Quality", 362, 241, 145, 20));
            Controls.Add(MakeLabel("Start delay", 542, 241, 150, 20));
            cbResolution = MakeCombo(20, 264, 172);
            cbResolution.Items.AddRange(new object[] { "Original (best for OCR)", "1080p (1920 x 1080)", "720p (1280 x 720)", "480p (854 x 480)", "360p (640 x 360)" });
            cbResolution.SelectedIndex = 0;
            Controls.Add(cbResolution);
            cbFps = MakeCombo(208, 264, 138);
            cbFps.Items.AddRange(new object[] { "10 fps", "15 fps", "24 fps", "30 fps" });
            cbFps.SelectedIndex = 3;
            Controls.Add(cbFps);
            cbQuality = MakeCombo(362, 264, 164);
            cbQuality.Items.AddRange(new object[] { "High (CRF 18)", "Balanced (CRF 20)", "Compact (CRF 25)" });
            cbQuality.SelectedIndex = 1;
            Controls.Add(cbQuality);
            cbCountdown = MakeCombo(542, 264, 153);
            cbCountdown.Items.AddRange(new object[] { "0 seconds", "3 seconds", "5 seconds", "10 seconds" });
            cbCountdown.SelectedIndex = 1;
            Controls.Add(cbCountdown);

            chkCursor = new CheckBox();
            chkCursor.Text = "Include mouse cursor";
            chkCursor.Location = new Point(20, 312);
            chkCursor.Size = new Size(190, 25);
            chkCursor.Checked = true;
            Controls.Add(chkCursor);
            chkMinimize = new CheckBox();
            chkMinimize.Text = "Minimize window when recording starts";
            chkMinimize.Location = new Point(235, 312);
            chkMinimize.Size = new Size(325, 25);
            chkMinimize.Checked = true;
            Controls.Add(chkMinimize);

            btnRecord = MakeButton("START RECORDING", 20, 355, 230);
            btnRecord.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            btnRecord.Height = 40;
            btnRecord.Click += delegate { StartOrCancel(); };
            Controls.Add(btnRecord);
            btnStop = MakeButton("STOP / SAVE MP4", 263, 355, 220);
            btnStop.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            btnStop.Height = 40;
            btnStop.Click += delegate { StopCapture(); };
            Controls.Add(btnStop);
            lblClock = MakeLabel("00:00:00", 527, 359, 165, 35);
            lblClock.Font = new Font("Consolas", 15F, FontStyle.Bold);
            lblClock.TextAlign = ContentAlignment.MiddleRight;
            Controls.Add(lblClock);

            Controls.Add(MakeLabel("Status", 20, 405, 110, 20));
            txtStatus = MakeText(20, 427, 675);
            txtStatus.ReadOnly = true;
            Controls.Add(txtStatus);
            Controls.Add(MakeLabel("Video / recovery file", 20, 462, 180, 20));
            txtFile = MakeText(20, 484, 545);
            txtFile.ReadOnly = true;
            Controls.Add(txtFile);
            btnFolder = MakeButton("Open folder", 578, 483, 117);
            btnFolder.Click += delegate { OpenFolder(); };
            Controls.Add(btnFolder);
            Label hint = MakeLabel("Ctrl + Shift + F9: Start / Stop. MP4 finalizes after Stop; a recoverable MKV is kept on failure.", 20, 521, 665, 22);
            hint.ForeColor = Color.DimGray;
            Controls.Add(hint);

            uiTimer = new System.Windows.Forms.Timer();
            uiTimer.Interval = 200;
            uiTimer.Tick += delegate { TimerTick(); };
            uiTimer.Start();
            LoadPreferences();
            if (!File.Exists(txtFfmpeg.Text)) txtFfmpeg.Text = FindFfmpeg();
            if (String.IsNullOrWhiteSpace(txtFolder.Text))
                txtFolder.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ScreenRecorder");
            UpdateUi("Ready. Choose your audio source and press Start.");
            Shown += delegate { RegisterShortcut(); RefreshAudio(); };
        }

        private static Label MakeLabel(string text, int x, int y, int w, int h)
        {
            Label label = new Label(); label.Text = text; label.Location = new Point(x, y);
            label.Size = new Size(w, h); return label;
        }
        private static TextBox MakeText(int x, int y, int w)
        {
            TextBox box = new TextBox(); box.Location = new Point(x, y);
            box.Width = w; return box;
        }
        private static ComboBox MakeCombo(int x, int y, int w)
        {
            ComboBox c = new ComboBox(); c.Location = new Point(x, y); c.Width = w;
            c.DropDownStyle = ComboBoxStyle.DropDownList; return c;
        }
        private static Button MakeButton(string text, int x, int y, int w)
        {
            Button b = new Button(); b.Text = text; b.Location = new Point(x, y);
            b.Size = new Size(w, 29); return b;
        }

        private void RegisterShortcut()
        {
            isHotkeyRegistered = RegisterHotKey(Handle, HotkeyId, ModControl | ModShift, (uint)Keys.F9);
            if (!isHotkeyRegistered) UpdateUi("Ready. Ctrl+Shift+F9 is already in use; use the buttons.");
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WmHotkey && message.WParam.ToInt32() == HotkeyId)
            {
                if (state == RecorderState.Ready || state == RecorderState.Countdown) StartOrCancel();
                else if (state == RecorderState.Recording) StopCapture();
                return;
            }
            base.WndProc(ref message);
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (state == RecorderState.Recording)
            {
                e.Cancel = true;
                StopCapture();
                MessageBox.Show("Recording is stopping and the MP4 is being finalized. Close after the Ready status returns.",
                                "Finalizing video", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (state == RecorderState.Finalizing)
            {
                e.Cancel = true;
                MessageBox.Show("Please wait for finalization to complete before closing.", "Finalizing video",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            SavePreferences();
            if (isHotkeyRegistered) UnregisterHotKey(Handle, HotkeyId);
            uiTimer.Stop();
            base.OnFormClosing(e);
        }

        private void SelectFfmpeg()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "FFmpeg executable (ffmpeg.exe)|ffmpeg.exe|Executable files|*.exe";
                if (d.ShowDialog(this) == DialogResult.OK) { txtFfmpeg.Text = d.FileName; SavePreferences(); }
            }
        }
        private void SelectFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "Choose the video output folder";
                if (Directory.Exists(txtFolder.Text)) d.SelectedPath = txtFolder.Text;
                if (d.ShowDialog(this) == DialogResult.OK) { txtFolder.Text = d.SelectedPath; SavePreferences(); }
            }
        }
        private void OpenFolder()
        {
            try
            {
                string path = txtFolder.Text.Trim();
                if (!Directory.Exists(path)) { MessageBox.Show("Folder does not exist yet."); return; }
                Process.Start("explorer.exe", QuoteArg(path));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Open folder failed"); }
        }
        private static string FindFfmpeg()
        {
            string home = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidates = new string[] {
                Path.Combine(home, "ffmpeg.exe"),
                Path.Combine(home, "tools", "ffmpeg.exe"),
                @"C:\Tools\ffmpeg\bin\ffmpeg.exe"
            };
            foreach (string path in candidates) if (File.Exists(path)) return path;
            string env = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (string part in env.Split(';'))
            {
                try { string p = Path.Combine(part.Trim().Trim('"'), "ffmpeg.exe"); if (File.Exists(p)) return p; }
                catch { }
            }
            return Path.Combine(home, "ffmpeg.exe");
        }
        [DllImport("winmm.dll")]
        private static extern uint waveInGetNumDevs();

        private static string AudioSelection(string selection)
        {
            string name = (selection ?? "").Trim();
            if (name.Length == 0 || String.Equals(name, "No audio (silent)", StringComparison.OrdinalIgnoreCase))
                return null;
            return name;
        }

        private void OpenAudioLog()
        {
            try
            {
                if (!File.Exists(audioLogPath))
                {
                    MessageBox.Show("No audio scan log yet. Click Refresh audio first.", "Audio diagnostics");
                    return;
                }
                Process.Start("notepad.exe", QuoteArg(audioLogPath));
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Audio diagnostics"); }
        }

        private void RefreshAudio()
        {
            if (state != RecorderState.Ready || !btnRefresh.Enabled) return;
            string ffmpeg = txtFfmpeg.Text.Trim();
            if (!File.Exists(ffmpeg))
            {
                UpdateUi("FFmpeg not found; choose ffmpeg.exe before scanning for microphones.");
                return;
            }
            string old = cbAudio.Text;
            btnRefresh.Enabled = false;
            UpdateUi("Scanning FFmpeg DirectShow audio devices and Windows input count...");
            // UpdateUi enables buttons based on state. Disable AFTER it.
            btnRefresh.Enabled = false;
            ThreadPool.QueueUserWorkItem(delegate(object ignored)
            {
                string output = "", error = "";
                int code = -9999;
                uint windowsInputs = 0;
                string exception = "";
                try
                {
                    windowsInputs = waveInGetNumDevs();
                    // FFmpeg normally RETURNS NONZERO because 'dummy' is not a real input.
                    // The device listing is still printed to stderr and is valid.
                    code = RunHelper(ffmpeg, "-hide_banner -list_devices true -f dshow -i dummy",
                                     15000, out output, out error);
                }
                catch (Exception ex) { exception = ex.ToString(); }
                List<string> names = ParseAudioDevices(output + "\n" + error);
                StringBuilder log = new StringBuilder();
                log.AppendLine("OFFLINE SCREEN RECORDER 1607 - AUDIO SCAN v1.0.1");
                log.AppendLine("Time=" + DateTime.Now.ToString("o"));
                log.AppendLine("FFmpeg=" + ffmpeg);
                log.AppendLine("Windows waveIn input count=" + windowsInputs);
                log.AppendLine("FFmpeg exit=" + code + " (nonzero is normal for dummy input)");
                log.AppendLine("Matched audio devices=" + names.Count);
                foreach (string name in names) log.AppendLine("MATCH: " + name);
                if (exception.Length != 0) log.AppendLine("EXCEPTION: " + exception);
                log.AppendLine("----- FFmpeg stdout -----");
                log.AppendLine(output);
                log.AppendLine("----- FFmpeg stderr -----");
                log.AppendLine(error);
                log.AppendLine("----- End -----");
                string loggingProblem = "";
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(audioLogPath));
                    File.WriteAllText(audioLogPath, log.ToString(), new UTF8Encoding(false));
                }
                catch (Exception ex) { loggingProblem = ex.Message; }
                SafeUi(delegate
                {
                    if (state != RecorderState.Ready) return;
                    cbAudio.BeginUpdate();
                    cbAudio.Items.Clear();
                    cbAudio.Items.Add("No audio (silent)");
                    foreach (string name in names) cbAudio.Items.Add(name);
                    // A manually entered device name must survive a refresh.
                    if (AudioSelection(old) != null)
                    {
                        cbAudio.Text = old;
                    }
                    else if (names.Count > 0)
                    {
                        cbAudio.SelectedIndex = 1;
                    }
                    else cbAudio.SelectedIndex = 0;
                    cbAudio.EndUpdate();
                    btnRefresh.Enabled = true;
                    string status;
                    if (names.Count > 0)
                        status = "Found " + names.Count + " FFmpeg microphone input(s); select one to record audio.";
                    else if (windowsInputs > 0)
                        status = "Windows reports " + windowsInputs + " recording input(s), but FFmpeg found none. Open Audio log.";
                    else
                        status = "No recording input detected. Check Windows Recording devices, then Audio log.";
                    if (loggingProblem.Length != 0) status += " Could not save audio log: " + loggingProblem;
                    UpdateUi(status);
                });
            });
        }

        // The listing format differs between FFmpeg versions. The usual format
        // has an 'audio devices' section, but other builds add '(audio)' next to
        // device names. Do NOT enumerate alternative-name GUIDs as friendly names.
        private static List<string> ParseAudioDevices(string listing)
        {
            List<string> names = new List<string>();
            bool audioSection = false;
            foreach (string line in listing.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.IndexOf("DirectShow audio devices", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    audioSection = true;
                    continue;
                }
                if (line.IndexOf("DirectShow video devices", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    audioSection = false;
                    continue;
                }
                if (line.IndexOf("Alternative name", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                Match match = Regex.Match(line, "\"([^\"]+)\"(.*)$");
                if (!match.Success) continue;
                bool markedAudio = Regex.IsMatch(match.Groups[2].Value,
                                                 @"\(\s*audio\s*\)", RegexOptions.IgnoreCase);
                if (!audioSection && !markedAudio) continue;
                string friendlyName = match.Groups[1].Value.Trim();
                if (friendlyName.Length == 0) continue;
                if (!names.Exists(delegate(string item)
                                  { return String.Equals(item, friendlyName, StringComparison.OrdinalIgnoreCase); }))
                    names.Add(friendlyName);
            }
            return names;
        }

        private void TestFfmpeg()
        {
            if (state != RecorderState.Ready) return;
            string exe = txtFfmpeg.Text.Trim();
            if (!File.Exists(exe)) { MessageBox.Show("ffmpeg.exe was not found at the selected path."); return; }
            btnTest.Enabled = false;
            UpdateUi("Testing FFmpeg input devices and H.264/AAC encoders...");
            ThreadPool.QueueUserWorkItem(delegate(object ignored)
            {
                string stdout1, stderr1, stdout2, stderr2;
                int r1 = RunHelper(exe, "-hide_banner -devices", 10000, out stdout1, out stderr1);
                int r2 = RunHelper(exe, "-hide_banner -encoders", 10000, out stdout2, out stderr2);
                string devices = stdout1 + stderr1, encoders = stdout2 + stderr2;
                bool okay = r1 == 0 && r2 == 0 && devices.Contains("gdigrab") && devices.Contains("dshow") &&
                            encoders.Contains("libx264") && encoders.Contains("aac");
                SafeUi(delegate
                {
                    btnTest.Enabled = true;
                    string result = okay ? "PASS: gdigrab, dshow, libx264, AAC found." :
                                           "FAIL: Missing capture device/encoder or FFmpeg could not start.";
                    UpdateUi(result);
                    MessageBox.Show(result + "\n\nFFmpeg: " + exe + "\n" +
                                    "Devices exit: " + r1 + ", encoders exit: " + r2 +
                                    "\nSee the README for compatibility guidance.", "FFmpeg preflight",
                                    MessageBoxButtons.OK, okay ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                });
            });
        }

        private void StartOrCancel()
        {
            if (state == RecorderState.Countdown)
            {
                state = RecorderState.Ready; UpdateUi("Countdown canceled."); return;
            }
            if (state != RecorderState.Ready) return;
            if (!File.Exists(txtFfmpeg.Text.Trim()))
            {
                MessageBox.Show("FFmpeg executable was not found. Browse to ffmpeg.exe first.", "Missing FFmpeg"); return;
            }
            if (String.IsNullOrWhiteSpace(txtFolder.Text))
            {
                MessageBox.Show("Choose an output folder.", "Missing output folder"); return;
            }
            try
            {
                Directory.CreateDirectory(txtFolder.Text.Trim());
                string probe = Path.Combine(txtFolder.Text.Trim(), ".record_write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "ok"); File.Delete(probe);
            }
            catch (Exception ex) { MessageBox.Show("Cannot write to output folder:\n" + ex.Message); return; }
            SavePreferences();
            int[] seconds = new int[] { 0, 3, 5, 10 };
            int index = Math.Max(0, cbCountdown.SelectedIndex);
            int delay = seconds[Math.Min(seconds.Length - 1, index)];
            if (delay == 0) BeginCapture();
            else
            {
                countdownEnd = DateTime.Now.AddSeconds(delay);
                state = RecorderState.Countdown;
                UpdateUi("Recording begins in " + delay + " seconds. Press Start again to cancel.");
            }
        }
        private void TimerTick()
        {
            if (state == RecorderState.Countdown)
            {
                int remaining = (int)Math.Ceiling((countdownEnd - DateTime.Now).TotalSeconds);
                if (remaining <= 0) BeginCapture();
                else { lblClock.Text = "-" + remaining + " s"; txtStatus.Text = "Countdown: " + remaining + " seconds (Start cancels)"; }
            }
            else if (state == RecorderState.Recording) lblClock.Text = elapsed.Elapsed.ToString(@"hh\:mm\:ss");
            else if (state == RecorderState.Ready && !elapsed.IsRunning) { /* retain final elapsed until next run */ }
        }
        private void BeginCapture()
        {
            try
            {
                string folder = txtFolder.Text.Trim();
                string prefix = "SCREEN_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string stem = Path.Combine(folder, prefix);
                int suffix = 1;
                while (File.Exists(stem + ".mp4") || File.Exists(stem + ".capture.mkv") || File.Exists(stem + ".ffmpeg.log"))
                    stem = Path.Combine(folder, prefix + "_" + (suffix++).ToString("00"));
                outputMp4 = stem + ".mp4";
                temporaryMkv = stem + ".capture.mkv";
                outputLog = stem + ".ffmpeg.log";
                txtFile.Text = outputMp4;
                stopRequested = false; finalizerStarted = false; lastError = "";
                lock (logLock) logWriter = new StreamWriter(outputLog, false, new UTF8Encoding(false));
                WriteLog("START " + DateTime.Now.ToString("o"));
                WriteLog("FFmpeg=" + txtFfmpeg.Text.Trim());
                string args = BuildCaptureArguments(temporaryMkv);
                WriteLog("Command=" + QuoteArg(txtFfmpeg.Text.Trim()) + " " + args);
                captureProcess = new Process();
                captureProcess.StartInfo = new ProcessStartInfo(txtFfmpeg.Text.Trim(), args);
                captureProcess.StartInfo.UseShellExecute = false;
                captureProcess.StartInfo.CreateNoWindow = true;
                captureProcess.StartInfo.RedirectStandardInput = true;
                captureProcess.StartInfo.RedirectStandardOutput = true;
                captureProcess.StartInfo.RedirectStandardError = true;
                captureProcess.EnableRaisingEvents = true;
                captureProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null && e.Data.StartsWith("out_time=")) { /* progress available for later analysis */ }
                };
                captureProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null) WriteLog(e.Data);
                };
                captureProcess.Exited += delegate { SafeUi(delegate { OnCaptureExited(); }); };
                if (chkMinimize.Checked) WindowState = FormWindowState.Minimized;
                captureProcess.Start();
                captureProcess.BeginErrorReadLine();
                captureProcess.BeginOutputReadLine();
                elapsed.Reset(); elapsed.Start();
                state = RecorderState.Recording;
                UpdateUi("Recording. Press Stop or Ctrl+Shift+F9 to finish.");
            }
            catch (Exception ex)
            {
                elapsed.Stop();
                lastError = ex.ToString();
                WriteLog("START_FAIL " + lastError);
                CloseLog();
                state = RecorderState.Ready;
                WindowState = FormWindowState.Normal;
                UpdateUi("Could not start FFmpeg. See log: " + outputLog);
                MessageBox.Show(ex.Message, "Recording start failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private string BuildCaptureArguments(string destination)
        {
            List<string> a = new List<string>();
            int[] fpsList = new int[] { 10, 15, 24, 30 };
            int fpsIndex = Math.Max(0, Math.Min(cbFps.SelectedIndex, fpsList.Length - 1));
            Add(a, "-hide_banner", "-loglevel", "warning", "-y", "-thread_queue_size", "512",
                   "-f", "gdigrab", "-framerate", fpsList[fpsIndex],
                   "-draw_mouse", chkCursor.Checked ? "1" : "0", "-i", "desktop");
            string selectedAudio = AudioSelection(cbAudio.Text);
            bool hasAudio = selectedAudio != null;
            if (hasAudio)
                Add(a, "-thread_queue_size", "512", "-f", "dshow", "-i", "audio=" + selectedAudio);
            Add(a, "-map", "0:v:0");
            if (hasAudio) Add(a, "-map", "1:a:0");
            string filter = "pad=ceil(iw/2)*2:ceil(ih/2)*2";
            int[,] sizes = new int[,] { { 0, 0 }, { 1920, 1080 }, { 1280, 720 }, { 854, 480 }, { 640, 360 } };
            int ri = Math.Max(0, Math.Min(cbResolution.SelectedIndex, 4));
            if (ri > 0)
            {
                string w = sizes[ri, 0].ToString(); string h = sizes[ri, 1].ToString();
                filter = "scale=" + w + ":" + h + ":force_original_aspect_ratio=decrease:flags=lanczos," +
                         "pad=" + w + ":" + h + ":(ow-iw)/2:(oh-ih)/2,setsar=1";
            }
            int[] crfList = new int[] { 18, 20, 25 };
            int crfIndex = Math.Max(0, Math.Min(cbQuality.SelectedIndex, crfList.Length - 1));
            Add(a, "-vf", filter, "-c:v", "libx264", "-preset", "veryfast", "-crf",
                   crfList[crfIndex], "-pix_fmt", "yuv420p");
            if (hasAudio) Add(a, "-c:a", "aac", "-b:a", "128k", "-ar", "48000", "-af", "aresample=async=1:first_pts=0");
            Add(a, "-progress", "pipe:1", "-nostats", destination);
            StringBuilder sb = new StringBuilder();
            foreach (string p in a) { if (sb.Length > 0) sb.Append(' '); sb.Append(QuoteArg(p)); }
            return sb.ToString();
        }
        private static void Add(List<string> a, params object[] tokens)
        {
            foreach (object p in tokens) a.Add(Convert.ToString(p, System.Globalization.CultureInfo.InvariantCulture));
        }
        private void StopCapture()
        {
            if (state != RecorderState.Recording) return;
            state = RecorderState.Finalizing;
            stopRequested = true;
            UpdateUi("Stopping FFmpeg; waiting for video finalization...");
            Process p = captureProcess;
            try { p.StandardInput.WriteLine("q"); p.StandardInput.Flush(); WriteLog("STOP_REQUEST " + DateTime.Now.ToString("o")); }
            catch (Exception ex) { WriteLog("STOP_STDIN_FAIL " + ex.Message); }
            ThreadPool.QueueUserWorkItem(delegate(object ignored)
            {
                try
                {
                    if (p != null && !p.WaitForExit(30000))
                    {
                        WriteLog("STOP_TIMEOUT 30s: terminating FFmpeg, MKV recovery may be required");
                        p.Kill();
                    }
                }
                catch (Exception ex) { WriteLog("STOP_WAITER " + ex.Message); }
            });
        }
        private void OnCaptureExited()
        {
            if (finalizerStarted) return;
            finalizerStarted = true;
            elapsed.Stop();
            state = RecorderState.Finalizing;
            UpdateUi("FFmpeg closed. Checking capture and remuxing MP4...");
            Process p = captureProcess;
            string ff = txtFfmpeg.Text.Trim(), mkv = temporaryMkv, mp4 = outputMp4;
            ThreadPool.QueueUserWorkItem(delegate(object ignored)
            {
                int code = -9999;
                try { p.WaitForExit(); code = p.ExitCode; }
                catch (Exception ex) { WriteLog("EXIT_ERROR " + ex.Message); }
                WriteLog("FFMPEG_EXIT code=" + code + " stop_requested=" + stopRequested);
                string info = "", finalPath = mp4;
                bool success = false;
                if (code == 0 && File.Exists(mkv) && new FileInfo(mkv).Length > 1024)
                {
                    string staging = mp4 + ".partial.mp4", sout, serr;
                    if (File.Exists(staging)) { try { File.Delete(staging); } catch { } }
                    string args = "-hide_banner -nostdin -loglevel warning -y -i " + QuoteArg(mkv) +
                                  " -map 0 -c copy -movflags +faststart " + QuoteArg(staging);
                    WriteLog("REMUX " + args);
                    int r = RunHelper(ff, args, 600000, out sout, out serr);
                    WriteLog("REMUX_EXIT " + r + "\n" + sout + "\n" + serr);
                    if (r == 0 && File.Exists(staging) && new FileInfo(staging).Length > 1024)
                    {
                        try
                        {
                            File.Move(staging, mp4);
                            success = true;
                            try { File.Delete(mkv); } catch (Exception ex) { WriteLog("MKV_CLEANUP " + ex.Message); }
                            info = "Saved MP4: " + mp4;
                        }
                        catch (Exception ex) { info = "Could not move finalized MP4: " + ex.Message; finalPath = mkv; }
                    }
                    else { info = "MP4 remux failed (exit " + r + "); original MKV retained."; finalPath = mkv; }
                }
                else
                {
                    finalPath = mkv;
                    info = "Capture ended with FFmpeg exit code " + code + ". Review FFmpeg log; MKV kept if present.";
                }
                WriteLog((success ? "PASS " : "FAIL ") + info);
                CloseLog();
                SafeUi(delegate
                {
                    state = RecorderState.Ready;
                    captureProcess = null;
                    txtFile.Text = finalPath;
                    WindowState = FormWindowState.Normal;
                    Activate();
                    UpdateUi(info);
                    if (!success) MessageBox.Show(info + "\n\nLog: " + outputLog,
                                                  "Video recording result", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    try { p.Dispose(); } catch { }
                });
            });
        }

        private static int RunHelper(string file, string args, int timeout, out string stdout, out string stderr)
        {
            StringBuilder o = new StringBuilder(), e = new StringBuilder();
            object sync = new object();
            int exit = -9999;
            try
            {
                using (Process p = new Process())
                {
                    p.StartInfo = new ProcessStartInfo(file, args);
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.StartInfo.RedirectStandardError = true;
                    p.OutputDataReceived += delegate(object sender, DataReceivedEventArgs evt) { if (evt.Data != null) lock (sync) o.AppendLine(evt.Data); };
                    p.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs evt) { if (evt.Data != null) lock (sync) e.AppendLine(evt.Data); };
                    p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    if (!p.WaitForExit(timeout))
                    {
                        try { p.Kill(); } catch { }
                        p.WaitForExit();
                        lock (sync) e.AppendLine("Timed out after " + timeout + " ms");
                    }
                    else p.WaitForExit(); // finish async stdout/stderr handlers
                    exit = p.ExitCode;
                }
            }
            catch (Exception ex) { lock (sync) e.AppendLine("Helper failed: " + ex); }
            lock (sync) { stdout = o.ToString(); stderr = e.ToString(); }
            return exit;
        }
        private static string QuoteArg(string text)
        {
            if (text == null) return "\"\"";
            if (text.Length > 0 && text.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', '"' }) < 0) return text;
            StringBuilder b = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char c in text)
            {
                if (c == '\\') { backslashes++; continue; }
                if (c == '"') { b.Append('\\', backslashes * 2 + 1); b.Append('"'); backslashes = 0; continue; }
                b.Append('\\', backslashes); backslashes = 0; b.Append(c);
            }
            b.Append('\\', backslashes * 2); b.Append('"'); return b.ToString();
        }
        private void WriteLog(string msg)
        {
            lock (logLock)
            {
                try { if (logWriter != null) { logWriter.WriteLine(msg); logWriter.Flush(); } }
                catch { }
            }
        }
        private void CloseLog()
        {
            lock (logLock)
            {
                if (logWriter != null) { try { logWriter.Flush(); logWriter.Dispose(); } catch { } logWriter = null; }
            }
        }
        private void SafeUi(MethodInvoker action)
        {
            try { if (!IsDisposed && IsHandleCreated) BeginInvoke(action); }
            catch (InvalidOperationException) { }
        }
        private void UpdateUi(string message)
        {
            txtStatus.Text = message;
            btnRecord.Enabled = state == RecorderState.Ready || state == RecorderState.Countdown;
            btnRecord.Text = state == RecorderState.Countdown ? "CANCEL COUNTDOWN" : "START RECORDING";
            btnStop.Enabled = state == RecorderState.Recording;
            bool editable = state == RecorderState.Ready;
            cbAudio.Enabled = editable; cbResolution.Enabled = editable; cbFps.Enabled = editable;
            cbQuality.Enabled = editable; cbCountdown.Enabled = editable;
            chkCursor.Enabled = editable; chkMinimize.Enabled = editable;
            txtFfmpeg.Enabled = editable; txtFolder.Enabled = editable;
            btnRefresh.Enabled = editable; btnTest.Enabled = editable;
            if (state == RecorderState.Ready && !elapsed.IsRunning && elapsed.ElapsedTicks == 0) lblClock.Text = "00:00:00";
        }
        private void LoadPreferences()
        {
            if (!File.Exists(settingsPath)) return;
            try
            {
                foreach (string line in File.ReadAllLines(settingsPath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('='); if (eq <= 0) continue;
                    string key = line.Substring(0, eq), val = Uri.UnescapeDataString(line.Substring(eq + 1));
                    int idx;
                    switch (key)
                    {
                        case "ffmpeg": txtFfmpeg.Text = val; break;
                        case "folder": txtFolder.Text = val; break;
                        case "audio": if (!String.IsNullOrEmpty(val)) { cbAudio.Items.Add(val); cbAudio.Text = val; } break;
                        case "resolution": if (Int32.TryParse(val, out idx) && idx >= 0 && idx < cbResolution.Items.Count) cbResolution.SelectedIndex = idx; break;
                        case "fps": if (Int32.TryParse(val, out idx) && idx >= 0 && idx < cbFps.Items.Count) cbFps.SelectedIndex = idx; break;
                        case "quality": if (Int32.TryParse(val, out idx) && idx >= 0 && idx < cbQuality.Items.Count) cbQuality.SelectedIndex = idx; break;
                        case "countdown": if (Int32.TryParse(val, out idx) && idx >= 0 && idx < cbCountdown.Items.Count) cbCountdown.SelectedIndex = idx; break;
                        case "cursor": chkCursor.Checked = val == "true"; break;
                        case "minimize": chkMinimize.Checked = val == "true"; break;
                    }
                }
            }
            catch { /* ignore malformed preferences */ }
        }
        private void SavePreferences()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                string[] lines = new string[] {
                    "ffmpeg=" + Uri.EscapeDataString(txtFfmpeg.Text),
                    "folder=" + Uri.EscapeDataString(txtFolder.Text),
                    "audio=" + Uri.EscapeDataString(AudioSelection(cbAudio.Text) ?? ""),
                    "resolution=" + cbResolution.SelectedIndex,
                    "fps=" + cbFps.SelectedIndex,
                    "quality=" + cbQuality.SelectedIndex,
                    "countdown=" + cbCountdown.SelectedIndex,
                    "cursor=" + chkCursor.Checked.ToString().ToLowerInvariant(),
                    "minimize=" + chkMinimize.Checked.ToString().ToLowerInvariant()
                };
                File.WriteAllLines(settingsPath, lines, new UTF8Encoding(false));
            }
            catch { /* settings should not block capturing */ }
        }
    }
}