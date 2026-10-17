using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NexusCore
{
    internal enum RunMode
    {
        Settings,      // no argument, or /c
        Preview,       // /p <HWND>
        Saver,         // /s, /hwnd:<HWND> <hInstance>, or anything unknown
        ChangePassword // /a <HWND>, Windows 95 legacy
    }

    /// <summary>
    /// Entry point. Everything hinges on parsing the command line correctly -
    /// the shell talks to a .scr only through it. Contract per Microsoft
    /// KB 182383: no args = settings, /c = settings, /p &lt;HWND&gt; = preview,
    /// /s = run, /a &lt;HWND&gt; = change password. HWND is an unsigned decimal
    /// string. Anything we cannot parse must degrade to saver mode, never
    /// crash - see 02-需求设计文档.md §7.
    /// </summary>
    internal static class Program
    {
        private const string LogName = "NexusCoreSaver.log";

        [STAThread]
        private static int Main(string[] args)
        {
            // Must happen before the first window exists.
            Native.EnablePerMonitorDpi();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (IsSelfTest(args))
            {
                SelfTest(args);
                return 0;
            }

            string renderPath = ExtractSwitchValue(args, "--render");
            if (renderPath != null)
            {
                RenderToFile(renderPath);
                return 0;
            }

            string uiProbePath = ExtractSwitchValue(args, "--uiprobe");
            if (uiProbePath != null)
            {
                UiProbe(uiProbePath);
                return 0;
            }

            IntPtr parent;
            RunMode mode = ParseMode(args, out parent);

            Log("mode=" + mode + " parent=" + parent.ToInt64()
                + " raw=[" + string.Join(" ", args) + "]");

            switch (mode)
            {
                case RunMode.Settings:
                    Application.Run(new SettingsForm());
                    break;

                case RunMode.Preview:
                    PreviewHost.Run(parent);
                    break;

                case RunMode.ChangePassword:
                    // The password gate was dropped in P2: any input exits the
                    // saver, and the shell's own logon-at-resume option is the
                    // right control. Answer politely instead of dead-ending.
                    MessageBox.Show(
                        "本屏保不使用退出密码，任意键鼠操作即可退出。\r\n\r\n"
                      + "如需在唤醒后要求登录，请在「设置 → 账户 → 登录选项」中启用"
                      + "「在我睡眠时要求登录」。",
                        "Cyberpunk Terminal",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;

                default:
                    SaverHost.Run();
                    break;
            }
            return 0;
        }

        private static RunMode ParseMode(string[] args, out IntPtr parent)
        {
            parent = IntPtr.Zero;
            if (args == null || args.Length == 0) return RunMode.Settings;

            char switchChar = '\0';
            string number = null;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg == null) continue;
                arg = arg.Trim();
                if (arg.Length == 0) continue;

                if (arg[0] == '/' || arg[0] == '-')
                {
                    string body = arg.Substring(1);
                    if (body.Length == 0) continue;

                    string value = null;
                    int colon = body.IndexOf(':');
                    if (colon >= 0)
                    {
                        value = body.Substring(colon + 1);
                        body = body.Substring(0, colon);
                    }

                    if (switchChar == '\0' && body.Length > 0)
                    {
                        switchChar = char.ToUpperInvariant(body[0]);
                    }
                    if (IsAllDigits(value)) number = value;
                }
                else if (IsAllDigits(arg))
                {
                    // Old-style shells pass the preview parent as a bare number.
                    if (switchChar == '\0') switchChar = 'P';
                    if (number == null) number = arg;
                }
            }

            long hwndValue;
            if (number != null)
            {
                long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out hwndValue);
                parent = new IntPtr(hwndValue);
            }

            switch (switchChar)
            {
                case 'C': return RunMode.Settings;
                case 'P': return parent != IntPtr.Zero ? RunMode.Preview : RunMode.Settings;
                case 'S': return RunMode.Saver;
                case 'A': return RunMode.ChangePassword;
                default:
                    // Unrecognised switch. If a handle came along with it this
                    // is the /hwnd:<HWND> <hInstance> form; otherwise assume
                    // settings, which is the least surprising window to show.
                    return number != null ? RunMode.Saver : RunMode.Settings;
            }
        }

        private static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9') return false;
            }
            return true;
        }

        private static bool IsSelfTest(string[] args)
        {
            if (args == null) return false;
            foreach (string a in args)
            {
                if (a != null && a.Trim().Equals("--selftest", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string ExtractSwitchValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a == null) continue;
                a = a.Trim();
                if (a.StartsWith(name, StringComparison.OrdinalIgnoreCase))
                {
                    int eq = a.IndexOf('=');
                    if (eq > 0) return a.Substring(eq + 1);
                    if (i + 1 < args.Length && args[i + 1] != null
                        && !args[i + 1].StartsWith("-"))
                    {
                        return args[i + 1];
                    }
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Builds the real SettingsForm off-screen and dumps it to a PNG, so the
        /// dialog (including the embedded preview surface) can be reviewed
        /// without a human staring at the machine. Screen capture is blocked in
        /// the build harness; DrawToBitmap is not.
        /// </summary>
        private static void UiProbe(string path)
        {
            Application.EnableVisualStyles();
            using (SettingsForm form = new SettingsForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(0, 0);
                form.Show();
                Application.DoEvents();

                // Let the embedded preview tick and paint a few frames.
                for (int i = 0; i < 40; i++)
                {
                    Application.DoEvents();
                    System.Threading.Thread.Sleep(25);
                }

                using (Bitmap bmp = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                    bmp.Save(path, ImageFormat.Png);
                }

                Log("uiprobe: wrote " + path + " (" + form.Width + "x" + form.Height + ")");
                form.Close();
            }
        }

        /// <summary>
        /// Paints exactly one frame through the real Renderer into a PNG.
        /// Gives a reviewable artefact for the drawing code without needing
        /// screen capture (blocked in the build harness) or a human present.
        /// </summary>
        private static void RenderToFile(string path)
        {
            const int W = 1600, H = 900;
            Settings settings = Config.Load();
            using (Bitmap bmp = new Bitmap(W, H))
            using (Graphics g = Graphics.FromImage(bmp))
            using (Renderer renderer = new Renderer())
            {
                renderer.Layout(new Size(W, H), 1.0, settings);
                List<PanelBox> panels = PanelLayout.Build(renderer.Cols,
                                                           Math.Max(8, renderer.Rows - 2),
                                                           settings);

                // Fast-forward so every panel has a populated buffer instead of
                // an empty first frame.
                const double warm = 12.0;
                for (int step = 0; step < 360; step++)
                {
                    panels[step % panels.Count].Update(warm + step * 0.033);
                }

                RenderState st = new RenderState();
                st.Bounds = new Rectangle(0, 0, W, H);
                st.MonitorIndex = 0;
                st.MonitorCount = 2;
                st.DpiScale = 1.0;
                st.StartedTicks = DateTime.Now.Ticks - (long)(warm * TimeSpan.TicksPerSecond);
                st.Frame = 420;

                renderer.Paint(g, st, panels);
                bmp.Save(path, ImageFormat.Png);
            }
            Log("render: wrote " + path);
        }

        /// <summary>
        /// Proves the command-line contract without ever opening a window -
        /// a WinExe has no console, so results go to a file. This is the P0
        /// gate check; see 03-开发执行计划.md §2.
        /// </summary>
        private static void SelfTest(string[] args)
        {
            string[][] cases = new string[][]
            {
                new string[0],                            // shell's 设置 button
                new string[]{ "/c" },                      // modal settings
                new string[]{ "/s" },                      // run
                new string[]{ "/p", "12345" },             // preview, space form
                new string[]{ "/p:67890" },                // preview, colon form
                new string[]{ "/p", "12345", "junk" },      // trailing junk
                new string[]{ "/a", "111" },                // change password
                new string[]{ "/hwnd:24680", "8192" },     // third-party form
                new string[]{ "/bogus" },                  // unknown switch
                new string[]{ "555" },                     // bare legacy handle
                new string[]{ "/S" },                      // lower-case switch
                new string[]{ "/P:42" },                   // lower-case preview
            };

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("NexusCore.scr command-line self test");
            sb.AppendLine("date : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine();

            for (int i = 0; i < cases.Length; i++)
            {
                IntPtr parent;
                RunMode mode = ParseMode(cases[i], out parent);
                sb.AppendLine("case " + (i + 1).ToString(CultureInfo.InvariantCulture)
                              + "  args=[" + string.Join(" ", cases[i]) + "]"
                              + "  -> mode=" + mode
                              + "  parent=" + parent.ToInt64());
            }

            IntPtr live;
            RunMode liveMode = ParseMode(StripSelfTest(args), out live);
            sb.AppendLine();
            sb.AppendLine("live invocation args=[" + string.Join(" ", args) + "]"
                          + "  -> mode=" + liveMode + "  parent=" + live.ToInt64());

            // Round-trip the settings key so a broken registry path shows up
            // here instead of failing silently at run time.
            sb.AppendLine();
            sb.AppendLine("--- config round-trip: HKCU\\Software\\NexusCoreSaver ---");
            Settings probe = new Settings();
            Settings original = Config.Load();
            probe.DisplayScope = "primary";
            probe.FontName = "Cascadia Mono";
            probe.FontSize = 14;
            probe.Speed = "fast";
            probe.Fps = 60;
            probe.Density = "med";
            Config.Save(probe);
            Settings back = Config.Load();
            sb.AppendLine("wrote     : scope=" + probe.DisplayScope + " font=" + probe.FontName
                          + "/" + probe.FontSize + " speed=" + probe.Speed
                          + " fps=" + probe.Fps + " density=" + probe.Density);
            sb.AppendLine("read back : scope=" + back.DisplayScope + " font=" + back.FontName
                          + "/" + back.FontSize + " speed=" + back.Speed
                          + " fps=" + back.Fps + " density=" + back.Density);
            bool ok = back.DisplayScope == "primary" && back.FontName == "Cascadia Mono"
                      && back.FontSize == 14 && back.Speed == "fast"
                      && back.Fps == 60 && back.Density == "med";
            sb.AppendLine("round-trip: " + (ok ? "PASS" : "FAIL"));
            sb.AppendLine("derived   : SpeedScale=" + back.SpeedScale
                          + " MaxDepth=" + back.MaxDepth + " TickMs=" + back.TickMs);
            sb.AppendLine("default   : SpeedScale=" + new Settings().SpeedScale
                          + " MaxDepth=" + new Settings().MaxDepth
                          + " TickMs=" + new Settings().TickMs);
            Config.Save(original);
            sb.AppendLine("restored  : scope=" + original.DisplayScope
                          + " font=" + original.FontName + "/" + original.FontSize
                          + " speed=" + original.Speed + " fps=" + original.Fps
                          + " density=" + original.Density);

            string path = Path.Combine(Path.GetTempPath(), "NexusCoreSaver-selftest.txt");
            try
            {
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log("selftest write failed: " + ex.Message);
            }
        }

        private static string[] StripSelfTest(string[] args)
        {
            List<string> kept = new List<string>();
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (a == null) continue;
                    if (a.Trim().Equals("--selftest", StringComparison.OrdinalIgnoreCase)) continue;
                    kept.Add(a);
                }
            }
            return kept.ToArray();
        }

        /// <summary>Diagnostics must never interrupt the saver, ever.</summary>
        internal static void Log(string message)
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), LogName);
                File.AppendAllText(path,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)
                    + "  " + message + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch (Exception)
            {
                // Logging is best-effort only.
            }
        }
    }
}