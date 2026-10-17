using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NexusCore
{
    /// <summary>
    /// One painted surface: borderless window, fixed-tick repaint, keyboard
    /// capture. Shared by the real screensaver and by the settings preview.
    /// </summary>
    internal class SurfaceForm : Form
    {
        private static int _cursorHidden;

        protected readonly RenderState State;
        private Settings _settings;
        private Renderer _renderer;
        private List<PanelBox> _panels;
        private Size _laidOutFor = new Size(0, 0);
        private int _lastCols, _lastRows;
        private readonly Timer _timer;
        private long _startTicks = DateTime.Now.Ticks;
        private Timer _cursorTimer;
        private bool _activityHandled;
        private bool _needsRelayout;
        private bool _started;

        // A screensaver window is activated *underneath* the pointer, which
        // makes Windows deliver a synthetic WM_MOUSEMOVE the instant it opens.
        // Treating that as "the user moved the mouse" made the saver exit
        // before it was ever visible, so input is ignored for a short grace
        // period and sub-3px jitter is discarded.
        private const int InputGraceMs = 2000;
        private readonly DateTime _inputOpenAt;
        private bool _mouseBaselineSet;
        private Point _lastMouse;
        private readonly bool _ignoreInput;

        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int WM_SETTINGCHANGE = 0x001A;
        private const int WM_POWERBROADCAST = 0x0218;
        private const int WM_DEVICECHANGE = 0x0219;
        private const int PBT_APMSUSPEND = 0x0004;
        private const int PBT_APMRESUMESUSPEND = 0x0007;
        private const int PBT_APMRESUMEAUTOMATIC = 0x0012;

        public SurfaceForm(string title, Rectangle bounds, int monitorIndex,
                           int monitorCount, bool preview)
            : this(title, bounds, monitorIndex, monitorCount, preview, null)
        {
        }

        public SurfaceForm(string title, Rectangle bounds, int monitorIndex,
                           int monitorCount, bool preview, Settings settings)
        {
            State = new RenderState();
            _settings = settings ?? Config.Load();

            State.Bounds = bounds;
            State.MonitorIndex = monitorIndex;
            State.MonitorCount = monitorCount;
            State.Preview = preview;
            State.StartedTicks = _startTicks;
            _inputOpenAt = DateTime.Now;
            _ignoreInput = Environment.GetEnvironmentVariable("NEXUSCORE_IGNORE_INPUT") == "1";
            if (_ignoreInput)
            {
                Program.Log("saver: NEXUSCORE_IGNORE_INPUT=1, input is disabled");
            }

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            BackColor = Color.Black;
            ForeColor = Color.White;
            ShowInTaskbar = false;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer, true);

            _timer = new Timer();
            _timer.Interval = _settings.TickMs;
            _timer.Tick += OnTick;
        }

        protected Settings Settings { get { return _settings; } }

        /// <summary>Live-apply edited settings (used by the settings preview).</summary>
        public void ApplySettings(Settings next)
        {
            if (next == null) return;
            _settings = next;
            _timer.Interval = _settings.TickMs;
            _needsRelayout = true;
            if (_panels != null)
            {
                for (int i = 0; i < _panels.Count; i++) _panels[i].SpeedMultiplier = _settings.SpeedScale;
            }
            if (IsHandleCreated) Invalidate();
        }

        /// <summary>Push the edited settings through a full re-layout.</summary>
        public void RebuildFor(Settings next)
        {
            ApplySettings(next);
            if (ClientSize.Width <= 0) return;
            _renderer = _renderer ?? new Renderer();
            _renderer.Layout(ClientSize, DeviceDpi / 96.0, _settings);
            _laidOutFor = ClientSize;
            _lastCols = _renderer.Cols;
            _lastRows = _renderer.Rows;
            _panels = PanelLayout.Build(_lastCols, Math.Max(8, _lastRows - 2), _settings);
            if (IsHandleCreated) Invalidate();
        }

        /// <summary>
        /// Kick off animation. Split out of OnLoad because the settings dialog
        /// embeds this class as a plain child control, and a child control
        /// never raises Form.OnLoad - without this the preview stays black.
        /// </summary>
        public void StartTicking()
        {
            if (_started) return;
            _started = true;
            _timer.Interval = _settings.TickMs;
            _needsRelayout = true;
            if (_cursorTimer == null && IsSaverMode)
            {
                _cursorTimer = new Timer();
                _cursorTimer.Interval = 400;
                _cursorTimer.Tick += delegate
                {
                    _cursorTimer.Stop();
                    _cursorTimer.Dispose();
                    _cursorTimer = null;
                    HideCursor();
                };
                _cursorTimer.Start();
            }
            _timer.Start();
        }

        /// <summary>Preview must not raise itself or steal the cursor.</summary>
        protected virtual bool IsSaverMode { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (IsSaverMode) Native.MakeInvisibleToShell(Handle);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Native.MakeInvisibleToShell(Handle);

            if (IsSaverMode)
            {
                TopMost = true;
                // Hide early: the longer the pointer sits over the window, the
                // more spurious motion events we generate.
                StartTicking();
            }
            else
            {
                // Embedded preview: no cursor handling, but still needs the
                // repaint timer running.
                StartTicking();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!IsSaverMode) return;
            try { Activate(); Focus(); }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// Power / display topology events. After a resume the surface must come
        /// back cleanly (re-layout against whatever resolution we woke up to),
        /// and "show logon screen on resume" must not stop the saver running.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST)
            {
                int reason = m.WParam.ToInt32();
                if (reason == PBT_APMSUSPEND)
                {
                    _timer.Stop();
                    Program.Log("power: suspending");
                }
                else if (reason == PBT_APMRESUMESUSPEND || reason == PBT_APMRESUMEAUTOMATIC)
                {
                    _needsRelayout = true;
                    if (IsHandleCreated && !_timer.Enabled) _timer.Start();
                    Program.Log("power: resumed, scheduling relayout");
                }
            }
            else if (m.Msg == WM_DISPLAYCHANGE || m.Msg == WM_DEVICECHANGE
                     || m.Msg == WM_SETTINGCHANGE)
            {
                _needsRelayout = true;
            }
            base.WndProc(ref m);
        }

        protected virtual void OnTick(object sender, EventArgs e)
        {
            State.Frame++;
            if (_panels != null)
            {
                double t = (DateTime.Now.Ticks - State.StartedTicks) / (double)TimeSpan.TicksPerSecond;
                for (int i = 0; i < _panels.Count; i++) _panels[i].Update(t);
            }
            Invalidate();
        }

        // ---- user activity -------------------------------------------------
        // Windows' own convention, and ours: any real input ends the
        // screensaver. No password gate (P2 scope change) - the shell's
        // "log on at resume" option covers the unattended case.
        private void HandleActivity()
        {
            if (!IsSaverMode || _activityHandled) return;
            // Diagnostic hook: NEXUSCORE_IGNORE_INPUT=1 makes the saver
            // completely inert to input, so automated tests can prove the
            // window stays up and renders without a human in the loop.
            if (_ignoreInput) return;
            // Swallow the input burst generated while the window appears.
            if ((DateTime.Now - _inputOpenAt).TotalMilliseconds < InputGraceMs) return;

            _activityHandled = true;
            Program.Log("saver: exiting, user input");
            RequestExit();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            HandleActivity();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            HandleActivity();
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            // Mouse moves come in floods and Windows synthesises one the moment
            // the window opens under the pointer. Require real movement.
            if (!_mouseBaselineSet)
            {
                _mouseBaselineSet = true;
                _lastMouse = e.Location;
                return;
            }
            if (Math.Abs(e.X - _lastMouse.X) < 3 && Math.Abs(e.Y - _lastMouse.Y) < 3)
            {
                base.OnMouseMove(e);
                return;
            }
            _lastMouse = e.Location;
            HandleActivity();
            base.OnMouseMove(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            double dpi = e.Graphics.DpiX / 96.0;
            if (_needsRelayout || _renderer == null || ClientSize != _laidOutFor)
            {
                if (_renderer == null) _renderer = new Renderer();

                if (_needsRelayout || ClientSize != _laidOutFor)
                {
                    _renderer.Layout(ClientSize, dpi, _settings);
                    _laidOutFor = ClientSize;
                    _needsRelayout = false;

                    // Keep the panel grid when only pixels changed but the
                    // character grid did not - avoids a jarring re-shuffle.
                    if (_renderer.Cols != _lastCols || _renderer.Rows != _lastRows
                        || _panels == null)
                    {
                        Relayout(dpi);
                    }
                }
            }

            _renderer.Paint(e.Graphics, State, _panels);
        }

        private void Relayout(double dpi)
        {
            _renderer.Layout(ClientSize, dpi, _settings);
            _lastCols = _renderer.Cols;
            _lastRows = _renderer.Rows;
            _laidOutFor = ClientSize;
            _needsRelayout = false;
            // Bottom row is reserved for the status bar.
            int rows = Math.Max(8, _lastRows - 2);
            _panels = PanelLayout.Build(_lastCols, rows, _settings);
            Program.Log("layout: " + _lastCols + "x" + rows + " -> " + _panels.Count + " panels");
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (IsSaverMode && keyData != Keys.None)
            {
                HandleActivity();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected virtual void RequestExit()
        {
            _timer.Stop();
            RestoreCursor();
            Application.Exit();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            if (_cursorTimer != null)
            {
                _cursorTimer.Stop();
                _cursorTimer.Dispose();
                _cursorTimer = null;
            }
            RestoreCursor();
            if (_renderer != null) { _renderer.Dispose(); _renderer = null; }
            base.OnFormClosed(e);
        }

        private static void HideCursor()
        {
            if (_cursorHidden++ == 0) Native.ShowCursor(false);
        }

        private static void RestoreCursor()
        {
            if (_cursorHidden > 0 && --_cursorHidden == 0) Native.ShowCursor(true);
        }
    }
}