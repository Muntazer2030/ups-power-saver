// UPS Power Saver - tray app: toggle a low-power mode (CPU + GPU) with Ctrl+Alt+U,
// live power overlay, and a stats window (Ctrl+Alt+M). Single exe, runs as admin.
// Compiled with the .NET Framework C# compiler (C# 5). See Build.ps1.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using LibreHardwareMonitor.Hardware;

namespace UpsPowerSaver
{
    static class Program
    {
        static readonly Dictionary<string, Assembly> Loaded = new Dictionary<string, Assembly>();

        [STAThread]
        static void Main(string[] args)
        {
            // LibreHardwareMonitor and its dependencies are embedded in this exe as resources.
            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            bool created;
            using (Mutex m = new Mutex(true, "UPSPowerSaverApp", out created))
            {
                if (!created)
                {
                    try { EventWaitHandle.OpenExisting("UPSPowerSaverShow").Set(); } catch { }
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                RunApp(args);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void RunApp(string[] args)
        {
            bool trayOnly = args.Any(a => a.Equals("-tray", StringComparison.OrdinalIgnoreCase));
            Application.Run(new AppContext(trayOnly));
        }

        static Assembly Resolve(object sender, ResolveEventArgs e)
        {
            string name = new AssemblyName(e.Name).Name;
            lock (Loaded)
            {
                Assembly a;
                if (Loaded.TryGetValue(name, out a)) return a;
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("dll_" + name + ".dll"))
                {
                    if (s == null) return null;
                    byte[] b = new byte[s.Length];
                    int off = 0;
                    while (off < b.Length) off += s.Read(b, off, b.Length - off);
                    a = Assembly.Load(b);
                    Loaded[name] = a;
                    return a;
                }
            }
        }
    }

    static class Diag
    {
        public static void Log(string msg)
        {
            try { File.AppendAllText(Path.Combine(Settings.Dir, "app.log"), DateTime.Now.ToString("s") + "  " + msg + "\r\n"); } catch { }
        }
    }

    // ------------------------------------------------------------------ settings
    class Settings
    {
        public int CpuMax = 40, CpuMin = 5, DisplayOff = 120, GpuMaxClock = 1200, GpuMinClock = 210, GpuWatts = 0;
        public int OverlayOpacity = 85, OverlayFont = 11;
        public bool DisableBoost = true, ShowOverlay = true, AutoStart = true;
        public string Corner = "TopLeft";
        public bool ShowFps = true, AlwaysOverlay = false;
        // FPS is never shown for these programs (browsers, chat, editors...). Comma-separated exe names; edit in settings.ini.
        public string FpsIgnore = "claude.exe,chrome.exe,msedge.exe,firefox.exe,brave.exe,opera.exe,vivaldi.exe,code.exe,cursor.exe,devenv.exe,unity.exe,unityhub.exe,blender.exe,discord.exe,spotify.exe,teams.exe,ms-teams.exe,slack.exe,telegram.exe,whatsapp.exe,notepad.exe,windowsterminal.exe,powershell.exe,cmd.exe,explorer.exe,applicationframehost.exe,steamwebhelper.exe,taskmgr.exe,vlc.exe,mpv.exe,winword.exe,excel.exe,powerpnt.exe,onenote.exe,mspaint.exe,ups power saver.exe";
        public string ToggleHotkey = "Ctrl+Alt+U", StatsHotkey = "Ctrl+Alt+W", OverlayHotkey = "Ctrl+Alt+O";
        public string OverlayMode = "Advanced";   // Simple = total watts only, Advanced = CPU/GPU details

        public static string Dir
        {
            get
            {
                string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UPSPowerSaver");
                Directory.CreateDirectory(d);
                return d;
            }
        }
        public static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i < 1 || line.TrimStart().StartsWith("#")) continue;
                    string k = line.Substring(0, i).Trim(), v = line.Substring(i + 1);
                    int hash = v.IndexOf('#');                 // allow trailing "# comment"
                    if (hash >= 0) v = v.Substring(0, hash);
                    v = v.Trim();
                    int n;
                    bool isInt = int.TryParse(v, out n);
                    switch (k)
                    {
                        case "CpuMaxPercent": if (isInt) s.CpuMax = n; break;
                        case "CpuMinPercent": if (isInt) s.CpuMin = n; break;
                        case "DisplayOffSeconds": if (isInt) s.DisplayOff = n; break;
                        case "GpuMaxClockMHz": if (isInt) s.GpuMaxClock = n; break;
                        case "GpuMinClockMHz": if (isInt) s.GpuMinClock = n; break;
                        case "GpuPowerWatts": if (isInt) s.GpuWatts = n; break;
                        case "OverlayOpacityPercent": if (isInt) s.OverlayOpacity = n; break;
                        case "OverlayFontSize": if (isInt) s.OverlayFont = n; break;
                        case "DisableTurboBoost": s.DisableBoost = v == "1"; break;
                        case "ShowOverlay": s.ShowOverlay = v == "1"; break;
                        case "OverlayCorner": s.Corner = v; break;
                        case "AutoStart": s.AutoStart = v == "1"; break;
                        case "OverlayMode": if (v == "Simple" || v == "Advanced") s.OverlayMode = v; break;
                        case "ToggleHotkey": if (HotkeyDef.Parse(v) != null) s.ToggleHotkey = v; break;
                        case "StatsHotkey": if (HotkeyDef.Parse(v) != null) s.StatsHotkey = v; break;
                        case "OverlayHotkey": if (HotkeyDef.Parse(v) != null) s.OverlayHotkey = v; break;
                        case "ShowFps": s.ShowFps = v == "1"; break;
                        case "FpsIgnore": s.FpsIgnore = v; break;
                        case "AlwaysShowOverlay": s.AlwaysOverlay = v == "1"; break;
                    }
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine("# UPS Power Saver settings (the Settings window edits this file)");
            b.AppendLine("CpuMaxPercent=" + CpuMax);
            b.AppendLine("CpuMinPercent=" + CpuMin);
            b.AppendLine("DisableTurboBoost=" + (DisableBoost ? 1 : 0));
            b.AppendLine("DisplayOffSeconds=" + DisplayOff);
            b.AppendLine("GpuMaxClockMHz=" + GpuMaxClock);
            b.AppendLine("GpuMinClockMHz=" + GpuMinClock);
            b.AppendLine("GpuPowerWatts=" + GpuWatts + "   # 0 = GPU's minimum allowed");
            b.AppendLine("ShowOverlay=" + (ShowOverlay ? 1 : 0));
            b.AppendLine("OverlayCorner=" + Corner + "   # TopLeft TopRight BottomLeft BottomRight");
            b.AppendLine("OverlayOpacityPercent=" + OverlayOpacity);
            b.AppendLine("OverlayFontSize=" + OverlayFont);
            b.AppendLine("OverlayMode=" + OverlayMode + "   # Simple (total watts only) or Advanced");
            b.AppendLine("AutoStart=" + (AutoStart ? 1 : 0) + "   # start with Windows");
            b.AppendLine("ToggleHotkey=" + ToggleHotkey);
            b.AppendLine("StatsHotkey=" + StatsHotkey);
            b.AppendLine("OverlayHotkey=" + OverlayHotkey + "   # show/hide the overlay any time, even with saver mode OFF");
            b.AppendLine("AlwaysShowOverlay=" + (AlwaysOverlay ? 1 : 0));
            b.AppendLine("ShowFps=" + (ShowFps ? 1 : 0) + "   # needs the bundled PresentMon tool");
            b.AppendLine("FpsIgnore=" + FpsIgnore);
            File.WriteAllText(FilePath, b.ToString());
        }
    }

    // ------------------------------------------------------------------ power plan + GPU limits
    static class Power
    {
        public const string PlanName = "UPS Saver";
        const string Balanced = "381b4222-f694-41f0-9685-ff5bb260df2e";
        static readonly Regex GuidRx = new Regex("[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
        static string PrevFile { get { return Path.Combine(Settings.Dir, "previous-plan.txt"); } }

        public static string Run(string exe, string args, out int exitCode)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args);
            psi.UseShellExecute = false; psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
            try
            {
                using (Process p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                    p.WaitForExit(15000);
                    exitCode = p.HasExited ? p.ExitCode : -1;
                    return o;
                }
            }
            catch (Exception ex) { exitCode = -2; return ex.Message; }
        }
        public static string Run(string exe, string args) { int c; return Run(exe, args, out c); }

        static string FirstGuid(string text) { Match m = GuidRx.Match(text); return m.Success ? m.Value : null; }
        public static string Active() { return FirstGuid(Run("powercfg", "/getactivescheme")); }

        public static string UpsGuid()
        {
            foreach (string line in Run("powercfg", "/list").Split('\n'))
                if (line.Contains("(" + PlanName + ")")) return FirstGuid(line);
            return null;
        }
        public static bool IsOn() { string u = UpsGuid(); return u != null && string.Equals(u, Active(), StringComparison.OrdinalIgnoreCase); }

        static void SetVal(string g, string sub, string setting, int v)
        {
            Run("powercfg", "/setacvalueindex " + g + " " + sub + " " + setting + " " + v);
            Run("powercfg", "/setdcvalueindex " + g + " " + sub + " " + setting + " " + v);
        }

        static string EnsurePlan(Settings s)
        {
            string g = UpsGuid();
            if (g == null)
            {
                g = FirstGuid(Run("powercfg", "/duplicatescheme " + Active()));
                if (g == null) throw new Exception("Could not create the power plan.");
                Run("powercfg", "/changename " + g + " \"" + PlanName + "\" \"Reduced CPU/GPU power for running on a UPS\"");
            }
            SetVal(g, "SUB_PROCESSOR", "PROCTHROTTLEMAX", s.CpuMax);
            SetVal(g, "SUB_PROCESSOR", "PROCTHROTTLEMIN", s.CpuMin);
            SetVal(g, "SUB_PROCESSOR", "PERFBOOSTMODE", s.DisableBoost ? 0 : 2);
            SetVal(g, "SUB_PROCESSOR", "PERFEPP", 100);
            if (s.DisplayOff > 0) SetVal(g, "SUB_VIDEO", "VIDEOIDLE", s.DisplayOff);
            return g;
        }

        static bool Smi(string args, out string output)
        {
            int code; output = Run("nvidia-smi", args, out code);
            return code == 0;
        }

        static string GpuLimit(Settings s)
        {
            string o;
            int w = s.GpuWatts;
            if (w <= 0)
            {
                double d;
                Smi("--query-gpu=power.min_limit --format=csv,noheader,nounits", out o);
                w = double.TryParse(o.Trim().Split('\n')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) ? (int)Math.Ceiling(d) : 0;
            }
            bool ok1 = w <= 0 || Smi("-pl " + w, out o);
            bool ok2 = Smi("-lgc " + s.GpuMinClock + "," + s.GpuMaxClock, out o);
            return ok1 && ok2 ? "GPU " + w + "W / " + s.GpuMaxClock + "MHz" : "GPU limit failed";
        }

        static void GpuReset()
        {
            string o;
            Smi("-rgc", out o);
            Smi("--query-gpu=power.default_limit --format=csv,noheader,nounits", out o);
            double d;
            if (double.TryParse(o.Trim().Split('\n')[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d))
                Smi("-pl " + (int)Math.Floor(d), out o);
        }

        // Returns a one-line description of what was applied.
        public static string TurnOn(Settings s)
        {
            string active = Active(), ups = UpsGuid();
            if (active != null && !string.Equals(active, ups, StringComparison.OrdinalIgnoreCase))
                File.WriteAllText(PrevFile, active);
            string g = EnsurePlan(s);
            Run("powercfg", "/setactive " + g);
            return "CPU max " + s.CpuMax + "%" + (s.DisableBoost ? ", boost off" : "") + ". " + GpuLimit(s);
        }

        public static void TurnOff()
        {
            string prev = null;
            try { prev = File.ReadAllText(PrevFile).Trim(); } catch { }
            string ups = UpsGuid();
            if (string.IsNullOrEmpty(prev) || string.Equals(prev, ups, StringComparison.OrdinalIgnoreCase)) prev = Balanced;
            Run("powercfg", "/setactive " + prev);
            GpuReset();
        }

        // Re-apply changed settings while ON.
        public static string Reapply(Settings s) { string g = EnsurePlan(s); Run("powercfg", "/setactive " + g); return GpuLimit(s); }
    }

    // ------------------------------------------------------------------ sensors (LibreHardwareMonitor)
    class Sn
    {
        public string HW, HType, Type, Name, Key;
        public ISensor S;
        public double? Val { get { return S.Value.HasValue ? (double?)S.Value.Value : null; } }
    }

    class Snap
    {
        public double? CpuW, CpuV, CpuT, CpuClk, CpuLoad, GpuW, GpuV, GpuT, GpuClk, GpuLoad;
        public double? Fps, FrameMs;
        public string FpsApp;
    }

    // ------------------------------------------------------------------ FPS (Intel PresentMon, bundled)
    // PresentMon watches the Windows present events through ETW (no injection into games, anti-cheat safe).
    class FpsReader
    {
        class Stat { public Queue<double> Ms = new Queue<double>(); public double Sum; public string App; public long LastTick; }

        Process proc;
        readonly Dictionary<int, Stat> stats = new Dictionary<int, Stat>();
        readonly object lk = new object();
        long retryAfter;
        const string Session = "UPSPowerSaver";
        static readonly string[] Ignore = { "dwm.exe", "explorer.exe", "searchhost.exe", "textinputhost.exe", "shellexperiencehost.exe", "startmenuexperiencehost.exe" };

        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        static string ExePath { get { return Path.Combine(Settings.Dir, "PresentMon.exe"); } }

        public bool Running { get { try { return proc != null && !proc.HasExited; } catch { return false; } } }

        static byte[] Embedded()
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("tool_PresentMon.exe"))
            {
                if (s == null) return null;
                byte[] b = new byte[s.Length]; int off = 0;
                while (off < b.Length) off += s.Read(b, off, b.Length - off);
                return b;
            }
        }

        // The tool runs elevated, so only ever run a file that is byte-identical to the one embedded in this exe.
        static bool EnsureTool()
        {
            byte[] emb = Embedded();
            if (emb == null) return false;
            try
            {
                bool same = File.Exists(ExePath) && File.ReadAllBytes(ExePath).SequenceEqual(emb);
                if (!same) File.WriteAllBytes(ExePath, emb);
                return true;
            }
            catch (Exception ex) { Diag.Log("PresentMon extract failed: " + ex.Message); return false; }
        }

        public void Start()
        {
            if (Running || Environment.TickCount < retryAfter) return;
            retryAfter = Environment.TickCount + 30000;
            if (!EnsureTool()) return;
            KillStale();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(ExePath,
                    "--output_stdout --no_console_stats --v1_metrics --stop_existing_session --session_name " + Session + " --no_track_gpu --no_track_input --exclude dwm.exe");
                psi.UseShellExecute = false; psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true; psi.RedirectStandardError = true;
                proc = Process.Start(psi);
                Process p = proc;
                lock (lk) stats.Clear();
                Thread t = new Thread(delegate() { Read(p); }); t.IsBackground = true; t.Start();
                Thread e = new Thread(delegate() { try { p.StandardError.ReadToEnd(); } catch { } }); e.IsBackground = true; e.Start();
                Diag.Log("PresentMon started");
            }
            catch (Exception ex) { Diag.Log("PresentMon start failed: " + ex.Message); proc = null; }
        }

        // A copy left running by an earlier instance that was killed (e.g. from Task Manager).
        static void KillStale()
        {
            foreach (Process p in Process.GetProcessesByName("PresentMon"))
            {
                try { if (string.Equals(p.MainModule.FileName, ExePath, StringComparison.OrdinalIgnoreCase)) p.Kill(); } catch { }
            }
        }

        public void Stop()
        {
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
            proc = null;
            if (File.Exists(ExePath))
                Power.Run(ExePath, "--session_name " + Session + " --terminate_existing_session");   // end the ETW session
            lock (lk) stats.Clear();
        }

        void Read(Process p)
        {
            try
            {
                int iApp = -1, iPid = -1, iMs = -1;
                string line;
                while ((line = p.StandardOutput.ReadLine()) != null)
                {
                    string[] f = line.Split(',');
                    if (iPid < 0)
                    {
                        for (int i = 0; i < f.Length; i++)
                        {
                            string n = f[i].Trim().ToLowerInvariant();
                            if (n == "application") iApp = i; else if (n == "processid") iPid = i; else if (n == "msbetweenpresents") iMs = i;
                        }
                        if (iPid >= 0 && iMs >= 0 && iApp >= 0) continue;
                        iPid = -1; continue;
                    }
                    int pid; double ms;
                    if (f.Length <= Math.Max(iMs, Math.Max(iApp, iPid))) continue;
                    if (!int.TryParse(f[iPid], out pid) || !double.TryParse(f[iMs], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out ms) || ms <= 0) continue;
                    lock (lk)
                    {
                        Stat s;
                        if (!stats.TryGetValue(pid, out s)) { s = new Stat(); stats[pid] = s; }
                        s.App = f[iApp]; s.LastTick = Environment.TickCount;
                        s.Ms.Enqueue(ms); s.Sum += ms;
                        while (s.Ms.Count > 2 && s.Sum - s.Ms.Peek() >= 1000) s.Sum -= s.Ms.Dequeue();
                    }
                }
            }
            catch (Exception ex) { Diag.Log("PresentMon reader ended: " + ex.Message); }
        }

        // Frame rate of the foreground app, if it is presenting frames and is not on the ignore list
        // (browsers, chat apps, editors...). Nothing else is ever reported, so background apps never show up.
        public void Fill(Snap s, string ignoreList)
        {
            uint fg = 0; GetWindowThreadProcessId(GetForegroundWindow(), out fg);
            lock (lk)
            {
                Stat f;
                if (!stats.TryGetValue((int)fg, out f) || Environment.TickCount - f.LastTick >= 2000 || f.Ms.Count < 2) return;
                string app = (f.App ?? "").ToLowerInvariant();
                if (Ignore.Contains(app)) return;
                foreach (string n in (ignoreList ?? "").Split(','))
                    if (n.Trim().Length > 0 && n.Trim().ToLowerInvariant() == app) return;
                s.FrameMs = f.Sum / f.Ms.Count;
                s.Fps = 1000.0 / s.FrameMs.Value;
                s.FpsApp = f.App;
            }
        }
    }

    class Sensors
    {
        Computer pc;
        List<IHardware> hw = new List<IHardware>();
        public List<Sn> All = new List<Sn>();

        public Sensors()
        {
            pc = new Computer();
            pc.IsCpuEnabled = true; pc.IsGpuEnabled = true; pc.IsMotherboardEnabled = true;
            pc.Open();
            foreach (IHardware h in pc.Hardware) Add(h);
            Update(); Thread.Sleep(300); Update();
            foreach (IHardware h in hw)
                foreach (ISensor s in h.Sensors)
                    All.Add(new Sn { HW = h.Name, HType = h.HardwareType.ToString(), Type = s.SensorType.ToString(), Name = s.Name, S = s, Key = s.Identifier.ToString() });
        }
        void Add(IHardware h) { hw.Add(h); foreach (IHardware s in h.SubHardware) Add(s); }
        public void Update() { foreach (IHardware h in hw) { try { h.Update(); } catch { } } }
        public void Close() { try { pc.Close(); } catch { } }

        Sn Find(string ht, string st, string rx)
        {
            return All.FirstOrDefault(x => Regex.IsMatch(x.HType, "^" + ht) && (st == null || x.Type == st) && Regex.IsMatch(x.Name, rx));
        }
        static double? V(Sn x) { return x == null ? null : x.Val; }
        static double? Avg(IEnumerable<double?> l) { List<double> v = l.Where(d => d.HasValue).Select(d => d.Value).ToList(); return v.Count > 0 ? (double?)v.Average() : null; }

        public Snap Get()
        {
            Snap o = new Snap();
            o.CpuW = V(Find("Cpu", "Power", "^CPU Package$"));
            double? vcore = V(Find("SuperIO", "Voltage", "^(Vcore|CPU VCore|CPU Core)$"));
            o.CpuV = vcore ?? Avg(All.Where(x => x.HType == "Cpu" && x.Type == "Voltage").Select(x => x.Val).Where(d => d > 0));
            Sn t = Find("Cpu", "Temperature", "^CPU Package$") ?? Find("Cpu", "Temperature", "Core Max");
            o.CpuT = V(t);
            o.CpuClk = Avg(All.Where(x => x.HType == "Cpu" && x.Type == "Clock" && !x.Name.Contains("Bus")).Select(x => x.Val));
            o.CpuLoad = V(Find("Cpu", "Load", "^CPU Total$"));
            Sn gw = Find("Gpu", null, "^GPU Board Power$") ?? Find("Gpu", null, "^GPU (Package )?Power$");
            o.GpuW = V(gw);
            if (!o.GpuW.HasValue)
            {
                double d;
                string r = Power.Run("nvidia-smi", "--query-gpu=power.draw --format=csv,noheader,nounits").Trim().Split('\n')[0];
                if (double.TryParse(r, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d)) o.GpuW = d;
            }
            o.GpuV = V(Find("Gpu", "Voltage", "Core"));
            o.GpuT = V(Find("Gpu", "Temperature", "^GPU Core$"));
            o.GpuClk = V(Find("Gpu", "Clock", "^GPU Core$"));
            o.GpuLoad = V(Find("Gpu", "Load", "^GPU Core$"));
            return o;
        }
    }

    // ------------------------------------------------------------------ windows
    class ClickThroughForm : Form
    {
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x20 | 0x8 | 0x80 | 0x08000000; // layered, click-through, topmost, toolwindow, no-activate
                return cp;
            }
        }
        protected override bool ShowWithoutActivation { get { return true; } }
    }

    class DbListView : ListView { public DbListView() { DoubleBuffered = true; } }

    // "Ctrl+Alt+U" <-> modifier flags + virtual key
    class HotkeyDef
    {
        public uint Mods, Vk;

        public static HotkeyDef Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            HotkeyDef d = new HotkeyDef();
            bool haveKey = false;
            foreach (string raw in text.Split('+'))
            {
                string t = raw.Trim();
                if (t.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) d.Mods |= 2;
                else if (t.Equals("Alt", StringComparison.OrdinalIgnoreCase)) d.Mods |= 1;
                else if (t.Equals("Shift", StringComparison.OrdinalIgnoreCase)) d.Mods |= 4;
                else if (t.Equals("Win", StringComparison.OrdinalIgnoreCase)) d.Mods |= 8;
                else if (t.Length == 1 && char.IsLetterOrDigit(t[0])) { d.Vk = (uint)char.ToUpper(t[0]); haveKey = true; }
                else
                {
                    Keys k;
                    if (!Enum.TryParse<Keys>(t, true, out k) || k == Keys.None) return null;
                    d.Vk = (uint)k; haveKey = true;
                }
            }
            return haveKey && (d.Mods & 7) != 0 ? d : null;
        }

        // Text for a key press; null while only modifiers are held.
        public static string Format(Keys modifiers, Keys key)
        {
            if (key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu || key == Keys.LWin || key == Keys.RWin) return null;
            StringBuilder b = new StringBuilder();
            if ((modifiers & Keys.Control) != 0) b.Append("Ctrl+");
            if ((modifiers & Keys.Alt) != 0) b.Append("Alt+");
            if ((modifiers & Keys.Shift) != 0) b.Append("Shift+");
            string k = key.ToString();
            if (k.Length == 2 && k[0] == 'D' && char.IsDigit(k[1])) k = k.Substring(1);
            return b + k;
        }
    }

    // Click it, then press the shortcut you want.
    class HotkeyBox : TextBox
    {
        public HotkeyBox() { ReadOnly = true; ShortcutsEnabled = false; BackColor = Color.White; }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            e.Handled = true; e.SuppressKeyPress = true;
            string t = HotkeyDef.Format(e.Modifiers, e.KeyCode);
            if (t != null && HotkeyDef.Parse(t) != null) Text = t;
        }
        protected override bool IsInputKey(Keys keyData) { return true; }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (Focused) { OnKeyDown(new KeyEventArgs(keyData)); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    class HotkeyForm : Form
    {
        public event Action<int> Hotkey;
        [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
        public HotkeyForm() { IntPtr unused = Handle; }
        protected override void SetVisibleCore(bool value) { base.SetVisibleCore(false); }
        // 0 = ok, -1 = not a valid shortcut, otherwise the Win32 error (1409 = already used by another program)
        public int Register(int id, string text)
        {
            HotkeyDef d = HotkeyDef.Parse(text);
            if (d == null) return -1;
            return RegisterHotKey(Handle, id, d.Mods | 0x4000, d.Vk) ? 0 : Marshal.GetLastWin32Error();
        }
        public void UnregisterAll() { UnregisterHotKey(Handle, 1); UnregisterHotKey(Handle, 2); UnregisterHotKey(Handle, 3); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x312 && Hotkey != null) Hotkey(m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }

    class SettingsForm : Form
    {
        NumericUpDown cpuMax, cpuMin, gpuMax, gpuMin, gpuW, dispOff, opac, font;
        CheckBox boost, overlay, autostart;
        ComboBox corner, mode;
        HotkeyBox toggleKey, statsKey, overlayKey;
        CheckBox alwaysOverlay, showFps;
        public Settings Result;

        HotkeyBox Key(string label, int y, string value)
        {
            Controls.Add(new Label { Text = label, Left = 14, Top = y + 3, Width = 220 });
            HotkeyBox k = new HotkeyBox { Left = 240, Top = y, Width = 120, Text = value };
            Controls.Add(k);
            Controls.Add(new Label { Text = "click, then press keys", Left = 366, Top = y + 3, Width = 140, ForeColor = Color.Gray });
            return k;
        }

        NumericUpDown Num(string label, int y, int min, int max, int val, string hint)
        {
            Label l = new Label { Text = label, Left = 14, Top = y + 3, Width = 220 };
            NumericUpDown n = new NumericUpDown { Left = 240, Top = y, Width = 80, Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, val)) };
            Label h = new Label { Text = hint, Left = 326, Top = y + 3, Width = 170, ForeColor = Color.Gray };
            Controls.Add(l); Controls.Add(n); Controls.Add(h);
            return n;
        }

        public SettingsForm(Settings s)
        {
            Text = "UPS Power Saver - Settings"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(510, 420);
            int y = 14;
            toggleKey = Key("Shortcut: toggle saver mode", y, s.ToggleHotkey); y += 32;
            statsKey = Key("Shortcut: open window", y, s.StatsHotkey); y += 32;
            overlayKey = Key("Shortcut: show/hide overlay", y, s.OverlayHotkey); y += 32;
            autostart = new CheckBox { Text = "Start automatically with Windows", Left = 14, Top = y, Width = 360, Checked = s.AutoStart }; Controls.Add(autostart); y += 40;
            cpuMax = Num("CPU max speed (%)", y, 5, 100, s.CpuMax, "lower = less power"); y += 32;
            cpuMin = Num("CPU min speed (%)", y, 0, 100, s.CpuMin, ""); y += 32;
            boost = new CheckBox { Text = "Turn off Turbo Boost (big CPU saver)", Left = 14, Top = y, Width = 360, Checked = s.DisableBoost }; Controls.Add(boost); y += 32;
            gpuMax = Num("GPU max clock (MHz)", y, 300, 4000, s.GpuMaxClock, "stock boost ~3000"); y += 32;
            gpuMin = Num("GPU min clock (MHz)", y, 100, 2000, s.GpuMinClock, ""); y += 32;
            gpuW = Num("GPU power limit (W, 0 = lowest)", y, 0, 600, s.GpuWatts, "0 = GPU minimum"); y += 32;
            dispOff = Num("Screen off after (seconds)", y, 0, 3600, s.DisplayOff, "0 = leave alone"); y += 38;
            overlay = new CheckBox { Text = "Show power overlay while ON", Left = 14, Top = y, Width = 360, Checked = s.ShowOverlay }; Controls.Add(overlay); y += 30;
            Controls.Add(new Label { Text = "Overlay corner", Left = 14, Top = y + 3, Width = 220 });
            corner = new ComboBox { Left = 240, Top = y, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            corner.Items.AddRange(new object[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" });
            corner.SelectedItem = s.Corner; if (corner.SelectedIndex < 0) corner.SelectedIndex = 0;
            Controls.Add(corner); y += 32;
            alwaysOverlay = new CheckBox { Text = "Always show overlay (even when saver mode is OFF)", Left = 14, Top = y, Width = 480, Checked = s.AlwaysOverlay }; Controls.Add(alwaysOverlay); y += 28;
            showFps = new CheckBox { Text = "Show FPS in the overlay (games must be borderless/windowed)", Left = 14, Top = y, Width = 480, Checked = s.ShowFps }; Controls.Add(showFps); y += 32;
            Controls.Add(new Label { Text = "Overlay shows", Left = 14, Top = y + 3, Width = 220 });
            mode = new ComboBox { Left = 240, Top = y, Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            mode.Items.AddRange(new object[] { "Simple", "Advanced" });
            mode.SelectedItem = s.OverlayMode; if (mode.SelectedIndex < 0) mode.SelectedIndex = 1;
            Controls.Add(mode);
            Controls.Add(new Label { Text = "Simple = total watts only", Left = 366, Top = y + 3, Width = 140, ForeColor = Color.Gray });
            y += 32;
            opac = Num("Overlay opacity (%)", y, 20, 100, s.OverlayOpacity, ""); y += 32;
            font = Num("Overlay font size", y, 8, 28, s.OverlayFont, ""); y += 40;
            Button ok = new Button { Text = "Save", Left = 330, Top = y, Width = 80, DialogResult = DialogResult.OK };
            Button cancel = new Button { Text = "Cancel", Left = 416, Top = y, Width = 80, DialogResult = DialogResult.Cancel };
            Controls.Add(ok); Controls.Add(cancel); AcceptButton = ok; CancelButton = cancel;
            ClientSize = new Size(510, y + 44);
            ok.Click += delegate
            {
                Settings r = Settings.Load();
                r.CpuMax = (int)cpuMax.Value; r.CpuMin = (int)cpuMin.Value; r.DisableBoost = boost.Checked;
                r.GpuMaxClock = (int)gpuMax.Value; r.GpuMinClock = (int)gpuMin.Value; r.GpuWatts = (int)gpuW.Value;
                r.DisplayOff = (int)dispOff.Value; r.ShowOverlay = overlay.Checked; r.Corner = (string)corner.SelectedItem;
                r.OverlayOpacity = (int)opac.Value; r.OverlayFont = (int)font.Value;
                r.OverlayMode = (string)mode.SelectedItem; r.AutoStart = autostart.Checked;
                if (HotkeyDef.Parse(toggleKey.Text) != null) r.ToggleHotkey = toggleKey.Text;
                if (HotkeyDef.Parse(statsKey.Text) != null) r.StatsHotkey = statsKey.Text;
                if (HotkeyDef.Parse(overlayKey.Text) != null) r.OverlayHotkey = overlayKey.Text;
                r.AlwaysOverlay = alwaysOverlay.Checked; r.ShowFps = showFps.Checked;
                Result = r;
            };
        }
    }

    // ------------------------------------------------------------------ app
    class AppContext : ApplicationContext
    {
        Settings cfg = Settings.Load();
        Sensors sensors;
        NotifyIcon tray = new NotifyIcon();
        Icon iconOn, iconOff;
        HotkeyForm hot = new HotkeyForm();
        EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "UPSPowerSaverShow");
        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        int tick;
        bool on;
        double peak;
        string err = "";

        // overlay
        ClickThroughForm overlay = new ClickThroughForm();
        Label ovLabel = new Label();

        // main window
        Form main;
        Label summary, status;
        Button toggleBtn;
        DbListView lv;
        Dictionary<string, ListViewItem> items = new Dictionary<string, ListViewItem>();
        Dictionary<string, Sn> itemSensor = new Dictionary<string, Sn>();
        Dictionary<string, double[]> minmax = new Dictionary<string, double[]>();
        ToolStripMenuItem startupItem, modeItem;

        static readonly string Deg = "°";
        readonly FpsReader fps = new FpsReader();
        bool overlayPinned;     // overlay was switched on by hand, so it stays when saver mode goes OFF

        public AppContext(bool trayOnly)
        {
            try { sensors = new Sensors(); }
            catch (Exception ex) { err = ex.Message; }

            iconOff = MakeIcon(Color.FromArgb(120, 120, 130));
            iconOn = MakeIcon(Color.FromArgb(60, 220, 110));
            tray.Icon = iconOff; tray.Text = "UPS Power Saver"; tray.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Open window", null, delegate { ShowMain(); });
            menu.Items.Add("Toggle saver mode", null, delegate { Toggle(); });
            menu.Items.Add("Show / hide overlay", null, delegate { ToggleOverlay(); });
            menu.Items.Add("Settings...", null, delegate { OpenSettings(); });
            modeItem = new ToolStripMenuItem("Overlay: simple (total watts only)");
            modeItem.Click += delegate { ToggleOverlayMode(); };
            menu.Items.Add(modeItem);
            startupItem = new ToolStripMenuItem("Start with Windows");
            startupItem.Click += delegate { cfg = Settings.Load(); cfg.AutoStart = !cfg.AutoStart; cfg.Save(); SyncStartup(); };
            menu.Items.Add(startupItem);
            menu.Items.Add("Exit (restores normal power)", null, delegate { Quit(); });
            menu.Opening += delegate
            {
                startupItem.Checked = cfg.AutoStart;
                modeItem.Checked = cfg.OverlayMode == "Simple";
            };
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { ShowMain(); };

            SetupOverlay();
            BuildMain();

            hot.Hotkey += OnHotkey;
            RegisterHotkeys();
            if (!File.Exists(Settings.FilePath)) cfg.Save();
            SyncStartup();

            on = Power.IsOn();          // e.g. app was closed/crashed while ON
            RefreshState();
            if (cfg.AlwaysOverlay || (on && cfg.ShowOverlay)) ShowOverlay();

            timer.Interval = 250;
            timer.Tick += OnTick;
            timer.Start();
            if (!trayOnly) ShowMain();
        }

        // ---- icon
        static Icon MakeIcon(Color c)
        {
            using (Bitmap b = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(b))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (Brush br = new SolidBrush(c)) g.FillEllipse(br, 1, 1, 30, 30);
                Point[] bolt = { new Point(18, 4), new Point(8, 18), new Point(15, 18), new Point(13, 28), new Point(24, 13), new Point(17, 13) };
                g.FillPolygon(Brushes.White, bolt);
                return Icon.FromHandle(b.GetHicon());
            }
        }

        // ---- overlay
        void SetupOverlay()
        {
            overlay.FormBorderStyle = FormBorderStyle.None; overlay.ShowInTaskbar = false; overlay.TopMost = true;
            overlay.StartPosition = FormStartPosition.Manual; overlay.BackColor = Color.FromArgb(12, 12, 12);
            ovLabel.AutoSize = true; ovLabel.Location = new Point(8, 6);
            ovLabel.ForeColor = Color.FromArgb(120, 255, 140);
            overlay.Controls.Add(ovLabel);
        }
        void ShowOverlay()
        {
            overlay.Opacity = Math.Max(0.2, Math.Min(1.0, cfg.OverlayOpacity / 100.0));
            ovLabel.Font = new Font("Consolas", cfg.OverlayFont, FontStyle.Bold);
            peak = 0;
            UpdateOverlay();
            overlay.Show();
        }
        // Manual show/hide (shortcut, button, tray). Works whether or not saver mode is ON.
        void ToggleOverlay()
        {
            if (overlay.Visible) { overlay.Hide(); overlayPinned = false; }
            else { ShowOverlay(); overlayPinned = true; }
        }

        Snap ReadSnap()
        {
            Snap s = sensors.Get();
            if (cfg.ShowFps) fps.Fill(s, cfg.FpsIgnore);
            return s;
        }

        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        // Games sometimes grab the top-most slot; take it back.
        void KeepOverlayOnTop() { SetWindowPos(overlay.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x2 | 0x1 | 0x10 | 0x40); }
        void PlaceOverlay()
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int w = ovLabel.Width + 16, h = ovLabel.Height + 12, m = 12;
            int x = cfg.Corner.EndsWith("Right") ? wa.Right - w - m : wa.Left + m;
            int y = cfg.Corner.StartsWith("Bottom") ? wa.Bottom - h - m : wa.Top + m;
            overlay.SetBounds(x, y, w, h);
        }
        void UpdateOverlay()
        {
            if (sensors == null) ovLabel.Text = "sensors unavailable";
            else
            {
                Snap s = ReadSnap();
                string adv = SummaryText(s);                      // also tracks the peak
                string simple = string.Format("{0:0.0} W", (s.CpuW ?? 0) + (s.GpuW ?? 0));
                if (cfg.ShowFps && s.Fps.HasValue) simple += "  " + s.Fps.Value.ToString("0") + " FPS";
                // saver mode indicator: green text + "SAVER ON", amber text + "SAVER OFF"
                string tag = on ? "SAVER ON" : "SAVER OFF";
                ovLabel.ForeColor = on ? Color.FromArgb(120, 255, 140) : Color.FromArgb(255, 190, 80);
                ovLabel.Text = cfg.OverlayMode == "Simple" ? simple + "  " + tag : tag + "\r\n" + adv;
            }
            PlaceOverlay();
        }

        void ToggleOverlayMode()
        {
            cfg = Settings.Load();
            cfg.OverlayMode = cfg.OverlayMode == "Simple" ? "Advanced" : "Simple";
            cfg.Save();
            if (overlay.Visible) UpdateOverlay();
        }

        void RegisterHotkeys()
        {
            hot.UnregisterAll();
            string failed = "";
            string[] keys = { cfg.ToggleHotkey, cfg.StatsHotkey, cfg.OverlayHotkey };
            if (keys.Distinct().Count() < 3) failed += "(two shortcuts are the same) ";
            else
            {
                string log = "register";
                for (int i = 0; i < 3; i++)
                {
                    int e = hot.Register(i + 1, keys[i]);
                    log += " " + keys[i] + "->" + e;
                    if (e != 0) failed += keys[i] + " ";
                }
                Diag.Log(log);
            }
            if (failed != "") tray.ShowBalloonTip(5000, "UPS Power Saver", "Shortcut not available (used by another program): " + failed + "- change it in Settings.", ToolTipIcon.Warning);
            if (status != null)
                status.Text = cfg.ToggleHotkey + " = toggle saver mode    " + cfg.OverlayHotkey + " = show/hide overlay\r\n" + cfg.StatsHotkey + " = open this window"
                    + (failed != "" ? "\r\nNOT AVAILABLE (used by another program): " + failed + "- change it in Settings"
                                    : "\r\nClosing this window keeps the app in the tray. Use \"Exit app\" to quit.");
        }

        void OnHotkey(int id)
        {
            Diag.Log("hotkey pressed id=" + id);
            try { if (id == 1) Toggle(); else if (id == 2) ShowMain(); else if (id == 3) ToggleOverlay(); }
            catch (Exception ex) { Diag.Log("hotkey handler failed: " + ex); }
        }

        static string F(double? v, string fmt, string unit) { return v.HasValue ? v.Value.ToString(fmt) + unit : "--"; }

        string SummaryText(Snap s, bool withApp = false)
        {
            double tot = (s.CpuW ?? 0) + (s.GpuW ?? 0);
            if (tot > peak) peak = tot;
            string l1 = string.Format("CPU {0,7} {1,7} {2,5} {3,8} {4,5}", F(s.CpuW, "0.0", "W"), F(s.CpuV, "0.00", "V"), F(s.CpuT, "0", Deg), F(s.CpuClk, "0", "MHz"), F(s.CpuLoad, "0", "%"));
            string l2 = string.Format("GPU {0,7} {1,7} {2,5} {3,8} {4,5}", F(s.GpuW, "0.0", "W"), F(s.GpuV, "0.00", "V"), F(s.GpuT, "0", Deg), F(s.GpuClk, "0", "MHz"), F(s.GpuLoad, "0", "%"));
            string l3 = string.Format("CPU+GPU {0:0.0}W   peak {1:0.0}W", tot, peak);
            string l4 = "";
            if (cfg.ShowFps)
                l4 = "\r\n" + (s.Fps.HasValue ? string.Format("FPS {0,5:0} {1,7:0.0}ms{2}", s.Fps.Value, s.FrameMs.Value, withApp ? "  " + s.FpsApp : "")
                                              : "FPS    --" + (withApp ? "  (no game in the foreground)" : ""));
            return l1 + "\r\n" + l2 + "\r\n" + l3 + l4;
        }

        // ---- main window
        void BuildMain()
        {
            main = new Form();
            main.Text = "UPS Power Saver"; main.Icon = iconOff; main.ClientSize = new Size(680, 700); main.StartPosition = FormStartPosition.CenterScreen;
            main.BackColor = Color.FromArgb(24, 24, 28); main.ForeColor = Color.White;

            Panel top = new Panel { Dock = DockStyle.Top, Height = 240 };
            toggleBtn = new Button { Left = 14, Top = 12, Width = 200, Height = 52, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
            toggleBtn.FlatAppearance.BorderSize = 0;
            toggleBtn.Click += delegate { Toggle(); };
            Button settingsBtn = new Button { Text = "Settings", Left = 224, Top = 12, Width = 90, Height = 52, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 58) };
            settingsBtn.FlatAppearance.BorderSize = 0;
            settingsBtn.Click += delegate { OpenSettings(); };
            Button ovBtn = new Button { Text = "Overlay on/off", Left = 320, Top = 12, Width = 100, Height = 52, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 58) };
            ovBtn.FlatAppearance.BorderSize = 0;
            ovBtn.Click += delegate { ToggleOverlay(); };
            Button modeBtn = new Button { Text = "Simple / Advanced", Left = 426, Top = 12, Width = 120, Height = 52, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(50, 50, 58) };
            modeBtn.FlatAppearance.BorderSize = 0;
            modeBtn.Click += delegate { ToggleOverlayMode(); };
            Button exitBtn = new Button { Text = "Exit app", Left = 552, Top = 12, Width = 110, Height = 52, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(120, 45, 45) };
            exitBtn.FlatAppearance.BorderSize = 0;
            exitBtn.Click += delegate { Quit(); };
            status = new Label { Left = 14, Top = 72, Width = 650, Height = 50, ForeColor = Color.Silver };
            summary = new Label { Left = 14, Top = 126, Width = 650, Height = 110, Font = new Font("Consolas", 11, FontStyle.Bold), ForeColor = Color.FromArgb(120, 255, 140) };
            top.Controls.AddRange(new Control[] { toggleBtn, settingsBtn, ovBtn, modeBtn, exitBtn, status, summary });

            lv = new DbListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, HeaderStyle = ColumnHeaderStyle.Nonclickable, BorderStyle = BorderStyle.None };
            lv.BackColor = Color.FromArgb(30, 30, 36); lv.ForeColor = Color.White; lv.Font = new Font("Consolas", 10);
            lv.Columns.Add("Sensor", 340); lv.Columns.Add("Value", 90); lv.Columns.Add("Min", 90); lv.Columns.Add("Max", 90);
            main.Controls.Add(lv); main.Controls.Add(top); lv.BringToFront();

            if (sensors != null)
            {
                string[] order = { "Power", "Voltage", "Current", "Temperature", "Clock", "Load", "Fan", "Data", "SmallData" };
                var list = sensors.All.Where(x => order.Contains(x.Type) && !Regex.IsMatch(x.Name, "^D3D|Distance to TjMax|Thread #"))
                    .OrderBy(x => Array.IndexOf(order, x.Type)).ThenBy(x => x.HType.StartsWith("Cpu") ? 0 : x.HType.StartsWith("Gpu") ? 1 : 2);
                foreach (Sn x in list)
                {
                    string shortName = x.HType.StartsWith("Cpu") ? "CPU" : x.HType.StartsWith("Gpu") ? "GPU" : "BOARD";
                    ListViewItem it = new ListViewItem(string.Format("{0,-5} {1,-11} {2}", shortName, x.Type, x.Name));
                    it.SubItems.Add("--"); it.SubItems.Add("--"); it.SubItems.Add("--");
                    lv.Items.Add(it); items[x.Key] = it; itemSensor[x.Key] = x;
                }
            }
            main.FormClosing += delegate(object s, FormClosingEventArgs e) { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; main.Hide(); } };
        }

        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);

        void ShowMain()
        {
            Diag.Log("ShowMain visible=" + main.Visible + " state=" + main.WindowState);
            if (main.WindowState == FormWindowState.Minimized) main.WindowState = FormWindowState.Normal;
            main.Show();
            ShowWindow(main.Handle, 9);                       // SW_RESTORE
            main.TopMost = true; main.TopMost = false;        // jump above other windows
            SetForegroundWindow(main.Handle); main.Activate();
            if (sensors != null) sensors.Update();
            UpdateMain();
        }

        static string Fmt(string type, double? v)
        {
            if (!v.HasValue) return "--";
            double d = v.Value;
            switch (type)
            {
                case "Power": return d.ToString("0.0") + " W";
                case "Voltage": return d.ToString("0.000") + " V";
                case "Current": return d.ToString("0.00") + " A";
                case "Temperature": return d.ToString("0.0") + " " + Deg + "C";
                case "Clock": return d.ToString("0") + " MHz";
                case "Load": return d.ToString("0.0") + " %";
                case "Fan": return d.ToString("0") + " RPM";
                case "Data": return d.ToString("0.0") + " GB";
                case "SmallData": return d.ToString("0") + " MB";
                default: return d.ToString("0.0");
            }
        }

        void UpdateMain()
        {
            if (sensors == null) { summary.Text = "Sensor library failed to load:\r\n" + err; return; }
            Snap s = ReadSnap();
            string note = "";
            if (s.CpuW.HasValue && s.CpuW.Value == 0) note = "\r\n(CPU watts = 0: is the PawnIO driver installed? Restart Windows once.)";
            summary.Text = SummaryText(s, true) + note;
            foreach (KeyValuePair<string, Sn> kv in itemSensor)
            {
                double? v = kv.Value.Val;
                ListViewItem it = items[kv.Key];
                if (v.HasValue)
                {
                    double[] mm;
                    if (!minmax.TryGetValue(kv.Key, out mm)) { mm = new double[] { v.Value, v.Value }; minmax[kv.Key] = mm; }
                    if (v.Value < mm[0]) mm[0] = v.Value;
                    if (v.Value > mm[1]) mm[1] = v.Value;
                    it.SubItems[2].Text = Fmt(kv.Value.Type, mm[0]); it.SubItems[3].Text = Fmt(kv.Value.Type, mm[1]);
                }
                it.SubItems[1].Text = Fmt(kv.Value.Type, v);
            }
        }

        // ---- state
        void RefreshState()
        {
            tray.Icon = on ? iconOn : iconOff;
            tray.Text = on ? "UPS Power Saver - ON" : "UPS Power Saver - OFF";
            toggleBtn.Text = on ? "Saver mode: ON" : "Saver mode: OFF";
            toggleBtn.BackColor = on ? Color.FromArgb(40, 160, 85) : Color.FromArgb(90, 90, 100);
            toggleBtn.ForeColor = Color.White;
        }

        void Toggle()
        {
            string msg;
            try
            {
                if (!on)
                {
                    cfg = Settings.Load();
                    msg = Power.TurnOn(cfg);
                    on = true;
                    if (cfg.ShowOverlay) ShowOverlay();
                    tray.ShowBalloonTip(3000, "UPS saver mode: ON", msg, ToolTipIcon.Info);
                }
                else
                {
                    Power.TurnOff();
                    on = false;
                    if (!overlayPinned && !cfg.AlwaysOverlay) overlay.Hide();   // stays if you switched it on by hand
                    tray.ShowBalloonTip(3000, "UPS saver mode: OFF", "Normal power restored (CPU + GPU).", ToolTipIcon.Info);
                }
            }
            catch (Exception ex) { tray.ShowBalloonTip(5000, "UPS Power Saver", ex.Message, ToolTipIcon.Error); }
            RefreshState();
            if (overlay.Visible) UpdateOverlay();       // flip the SAVER ON/OFF indicator right away
        }

        void OpenSettings()
        {
            using (SettingsForm f = new SettingsForm(Settings.Load()))
            {
                if (f.ShowDialog() == DialogResult.OK && f.Result != null)
                {
                    cfg = f.Result; cfg.Save();
                    RegisterHotkeys(); SyncStartup();
                    if (on) Power.Reapply(cfg);
                    if (cfg.AlwaysOverlay || overlayPinned || (on && cfg.ShowOverlay) || overlay.Visible) ShowOverlay(); else overlay.Hide();
                    tray.ShowBalloonTip(2500, "UPS Power Saver", on ? "Settings saved and applied." : "Settings saved. They apply when you switch ON.", ToolTipIcon.Info);
                }
            }
        }

        // ---- start with windows (elevated logon task, no UAC prompt)
        static bool StartupEnabled() { int c; Power.Run("schtasks", "/query /tn \"UPS Power Saver\"", out c); return c == 0; }
        // Make the Windows logon task match the AutoStart setting (also re-points it if the exe moved).
        void SyncStartup()
        {
            if (cfg.AutoStart)
                Power.Run("schtasks", "/create /tn \"UPS Power Saver\" /tr \"\\\"" + Application.ExecutablePath + "\\\" -tray\" /sc onlogon /rl highest /f");
            else if (StartupEnabled())
                Power.Run("schtasks", "/delete /tn \"UPS Power Saver\" /f");
        }

        // ---- loop
        void OnTick(object sender, EventArgs e)
        {
            try
            {
                if (showEvent.WaitOne(0)) ShowMain();
                if (overlay.Visible) KeepOverlayOnTop();
                tick++;
                if (tick % 4 != 0) return;
                bool mainOn = main.Visible && main.WindowState != FormWindowState.Minimized;
                bool needFps = cfg.ShowFps && (overlay.Visible || mainOn);   // only trace frames while something shows them
                if (needFps) fps.Start(); else if (fps.Running) fps.Stop();
                if (!overlay.Visible && !mainOn) return;
                if (sensors != null) sensors.Update();
                if (overlay.Visible) UpdateOverlay();
                if (mainOn) UpdateMain();
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(Settings.Dir, "error.log"), DateTime.Now + " " + ex + "\r\n"); } catch { }
            }
        }

        void Quit()
        {
            try
            {
                timer.Stop();
                fps.Stop();
                if (on) Power.TurnOff();
                hot.UnregisterAll();
                tray.Visible = false; tray.Dispose();
            }
            catch { }
            Environment.Exit(0);   // also closes any open Settings dialog; the OS releases the sensor driver handle
        }
    }
}
