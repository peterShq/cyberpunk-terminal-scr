using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace NexusCore
{
    /// <summary>
    /// Live preview surface for the settings dialog.
    ///
    /// It deliberately does NOT reuse SurfaceForm: that class is a top-level
    /// Form, and WinForms refuses to nest a top-level Form inside another form
    /// ("无法将顶级控件添加到控件") - which is exactly why an earlier attempt
    /// at an embedded SurfaceForm left the preview box black. This is a plain
    /// Panel that drives the very same Renderer / PanelLayout / PanelBox
    /// engine, so what you see here is what the saver draws.
    /// </summary>
    internal sealed class PreviewPanel : Panel
    {
        private readonly Timer _timer;
        private Renderer _renderer;
        private List<PanelBox> _panels;
        private Settings _settings;
        private Size _laidOutFor = new Size(0, 0);
        private bool _needsRelayout;

        public PreviewPanel(Settings settings)
        {
            _settings = settings ?? Config.Load();

            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Black;
            DoubleBuffered = true;

            _timer = new Timer();
            _timer.Interval = _settings.TickMs;
            _timer.Tick += delegate { Invalidate(); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _needsRelayout = true;
            _timer.Start();
            // Warm the panels up so the first visible frame is not empty.
            LayoutNow();
        }

        public void ApplySettings(Settings next)
        {
            if (next == null) return;
            _settings = next;
            _timer.Interval = _settings.TickMs;
            if (_panels != null)
            {
                for (int i = 0; i < _panels.Count; i++)
                {
                    _panels[i].SpeedMultiplier = _settings.SpeedScale;
                }
            }
            LayoutNow();
        }

        private void LayoutNow()
        {
            if (ClientSize.Width < 20 || ClientSize.Height < 20) return;

            _renderer = _renderer ?? new Renderer();
            double dpi = DeviceDpi / 96.0;
            _renderer.Layout(ClientSize, dpi, _settings);
            _laidOutFor = ClientSize;

            int rows = Math.Max(8, _renderer.Rows - 2);
            _panels = PanelLayout.Build(_renderer.Cols, rows, _settings);

            double t = (DateTime.Now.Ticks - _startedTicks) / (double)TimeSpan.TicksPerSecond;
            for (int i = 0; i < _panels.Count; i++) _panels[i].Update(t);

            _needsRelayout = false;
        }

        private readonly long _startedTicks = DateTime.Now.Ticks;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _needsRelayout = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_needsRelayout || _renderer == null || ClientSize != _laidOutFor)
            {
                LayoutNow();
            }
            if (_renderer == null || _panels == null) return;

            if (_panels.Count > 0)
            {
                double t = (DateTime.Now.Ticks - _startedTicks) / (double)TimeSpan.TicksPerSecond;
                _panels[0].Update(t);
            }

            RenderState st = new RenderState();
            st.Bounds = new Rectangle(Point.Empty, ClientSize);
            st.MonitorIndex = 0;
            st.MonitorCount = 1;
            st.DpiScale = DeviceDpi / 96.0;
            st.StartedTicks = _startedTicks;
            st.Frame = Environment.TickCount;
            st.Preview = true;

            _renderer.Paint(e.Graphics, st, _panels);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Stop();
                _timer.Dispose();
                if (_renderer != null) { _renderer.Dispose(); _renderer = null; }
            }
            base.Dispose(disposing);
        }
    }
}