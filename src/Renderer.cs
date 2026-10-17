using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace NexusCore
{
    /// <summary>Everything the renderer needs to draw one surface.</summary>
    internal sealed class RenderState
    {
        public Rectangle Bounds;
        public int MonitorIndex;
        public int MonitorCount;
        public int Frame;
        public double DpiScale;
        public long StartedTicks;
        public bool Preview;
    }

    /// <summary>
    /// GDI+ painter for the panel layout ported from panel.py.
    ///
    /// Chrome (frames, bars, sparklines) is drawn with primitives rather than
    /// box-drawing glyphs: Consolas has no reliable coverage for the rounded
    /// corner block, and primitives stay crisp at any DPI. Only real text goes
    /// through the font.
    /// </summary>
    internal sealed class Renderer : IDisposable
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly Color Cyan = Color.FromArgb(0, 220, 220);
        private static readonly Color Green = Color.FromArgb(60, 220, 130);
        private static readonly Color Magenta = Color.FromArgb(230, 80, 220);
        private static readonly Color Blue = Color.FromArgb(0, 150, 200);
        private static readonly Color Yellow = Color.FromArgb(240, 200, 80);
        private static readonly Color Red = Color.FromArgb(240, 90, 90);
        private static readonly Color White = Color.FromArgb(220, 220, 220);
        private static readonly Color Grey = Color.FromArgb(110, 110, 110);

        private readonly Dictionary<string, Color> _palette = new Dictionary<string, Color>();
        private readonly Dictionary<string, Color> _kindColor = new Dictionary<string, Color>();
        private readonly HashSet<string> _boldKinds = new HashSet<string>();
        private readonly HashSet<string> _dimKinds = new HashSet<string>();

        private Font _font;
        private Font _fontBold;
        private StringFormat _fmt;
        private StringFormat _fmtRight;

        private float _cellW = 8f;
        private float _cellH = 16f;
        private int _cols;
        private int _rows;

        private readonly Random _rnd = new Random();

        public int Cols { get { return _cols; } }
        public int Rows { get { return _rows; } }

        public Renderer()
        {
            _palette["cyan"] = Cyan;
            _palette["green"] = Green;
            _palette["magenta"] = Magenta;
            _palette["blue"] = Blue;
            _palette["yellow"] = Yellow;
            _palette["red"] = Red;
            _palette["white"] = White;

            _kindColor["normal"] = Cyan;
            _kindColor["dim"] = Color.FromArgb(0, 130, 130);
            _kindColor["keyword"] = Magenta;
            _kindColor["string"] = Green;
            _kindColor["comment"] = Color.FromArgb(0, 110, 150);
            _kindColor["pass"] = Green;
            _kindColor["warn"] = Yellow;
            _kindColor["error"] = Red;
            _kindColor["ominous"] = Red;
            _kindColor["claude"] = Magenta;
            _kindColor["claude_tool"] = Cyan;
            _kindColor["claude_output"] = Color.FromArgb(50, 170, 110);
            _kindColor["claude_think"] = Color.FromArgb(150, 60, 150);
            _kindColor["value"] = White;
            _kindColor["heading"] = Cyan;
            _kindColor["progress"] = Cyan;
            _kindColor["diff_add"] = Green;
            _kindColor["diff_del"] = Red;
            _kindColor["diff_hunk"] = Blue;

            _boldKinds.Add("ominous");
            _boldKinds.Add("claude");
            _boldKinds.Add("keyword");
            _boldKinds.Add("heading");
            _boldKinds.Add("claude_prompt");

            _dimKinds.Add("dim");
            _dimKinds.Add("comment");

            _fmt = new StringFormat(StringFormat.GenericTypographic);
            _fmt.FormatFlags |= StringFormatFlags.NoWrap;
            _fmt.Trimming = StringTrimming.None;

            _fmtRight = new StringFormat(StringFormat.GenericTypographic);
            _fmtRight.FormatFlags |= StringFormatFlags.NoWrap;
            _fmtRight.Alignment = StringAlignment.Far;
        }

        // ── metrics ──
        private string _family = "Consolas";

        /// <summary>
        /// Chain starts at the configured family and falls back through the
        /// usual suspects, so a missing font can never stop the saver.
        /// </summary>
        private Font SafeFont(float size, FontStyle style)
        {
            string[] order = _family == "Consolas"
                ? new string[] { "Consolas", "Cascadia Mono", "Lucida Console", "Courier New" }
                : new string[] { _family, "Consolas", "Cascadia Mono", "Courier New" };
            for (int i = 0; i < order.Length; i++)
            {
                try { return new Font(order[i], size, style, GraphicsUnit.Point); }
                catch (ArgumentException) { }
            }
            return new Font(FontFamily.GenericMonospace, size, style, GraphicsUnit.Point);
        }

        /// <summary>
        /// Derive the character grid from measured font metrics. An explicit
        /// FontSize in the settings wins; otherwise the size is solved so the
        /// grid lands near 62 rows on any panel height.
        /// </summary>
        public void Layout(Size client, double dpi, Settings st)
        {
            _family = string.IsNullOrEmpty(st.FontName) ? "Consolas" : st.FontName;

            float points;
            if (st.FontSize > 0)
            {
                points = st.FontSize;
            }
            else
            {
                float targetCell = client.Height / 62f;
                if (targetCell < 13f) targetCell = 13f;
                if (targetCell > 26f) targetCell = 26f;
                using (Font probe = SafeFont(12f, FontStyle.Regular))
                {
                    float ratio = probe.Height / 12f;   // px per point at this DPI
                    points = targetCell / ratio;
                }
            }
            if (points < 7f) points = 7f;
            if (points > 28f) points = 28f;

            if (_font != null) { _font.Dispose(); _font = null; }
            if (_fontBold != null) { _fontBold.Dispose(); _fontBold = null; }
            _font = SafeFont(points, FontStyle.Regular);
            _fontBold = SafeFont(points, FontStyle.Bold);

            using (Bitmap scratch = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(scratch))
            {
                g.PageUnit = GraphicsUnit.Pixel;
                _cellH = Math.Max(12f, _font.Height);
                _cellW = MeasureCharWidth(g);
                if (_cellW < 4f) _cellW = 7f;
            }

            _cols = Math.Max(40, (int)(client.Width / _cellW));
            // Rows must not exceed what the surface can physically hold: an
            // inflated row count makes panels lay out taller than the window
            // and the last panel spills under the status bar (very visible in
            // the small settings preview). Never inflate above the real fit.
            int fitRows = (int)(client.Height / _cellH);
            _rows = fitRows < 8 ? 8 : fitRows;
        }

        private float MeasureCharWidth(Graphics g)
        {
            // Measure 100 chars and divide: far more stable than a single glyph.
            float w = g.MeasureString(new string('M', 100), _font, new SizeF(4000f, 4000f), _fmt).Width;
            return w / 100f;
        }

        private Color Base(string name)
        {
            Color c;
            if (name != null && _palette.TryGetValue(name, out c)) return c;
            return Cyan;
        }

        private Color KindColor(string kind)
        {
            Color c;
            if (kind != null && _kindColor.TryGetValue(kind, out c)) return c;
            return White;
        }

        /// <summary>
        /// Brush cache. Earlier this deliberately returned a fresh SolidBrush
        /// every time, because every call site wraps the result in `using` and a
        /// shared instance got disposed by the first panel (the next one then
        /// threw "Parameter not valid" from GDI+).
        ///
        /// Now that the render loop is measurable at 60 FPS, the per-frame
        /// allocation matters, so the cache is back - with the ownership fixed
        /// at the source: `Owned()` hands out an un-owned brush the caller may
        /// dispose, `Cached()` hands out a shared one the caller must not.
        /// A GDI+ SolidBrush is immutable once created apart from being freed,
        /// so sharing is safe as long as nobody disposes it.
        /// </summary>
        private readonly Dictionary<int, SolidBrush> _brushCache =
            new Dictionary<int, SolidBrush>();

        private SolidBrush Brush(Color c)
        {
            int key = c.ToArgb();
            SolidBrush b;
            if (_brushCache.TryGetValue(key, out b) && b != null) return b;
            b = new SolidBrush(c);
            _brushCache[key] = b;
            return b;
        }

        /// <summary>A brush the caller owns and must dispose.</summary>
        private SolidBrush Owned(Color c)
        {
            return new SolidBrush(c);
        }

        private float X(int cell) { return cell * _cellW; }
        private float Y(int row) { return row * _cellH; }

        // ── frame ───────────────────────────────────────────────────────
        public void Paint(Graphics g, RenderState st, List<PanelBox> panels)
        {
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.Black);

            double elapsed = (DateTime.Now.Ticks - st.StartedTicks) / (double)TimeSpan.TicksPerSecond;
            int fps = elapsed > 0.2 ? (int)(st.Frame / elapsed) : st.Frame;

            for (int i = 0; i < panels.Count; i++) DrawPanel(g, panels[i]);

            DrawStatusBar(g, st, panels, elapsed, fps);
        }

        private void DrawPanel(Graphics g, PanelBox p)
        {
            float px = X(p.X), py = Y(p.Y);
            float pw = p.W * _cellW, ph = p.H * _cellH;
            if (pw < 24 || ph < 20) return;

            Color baseColor = Base(p.ColorName);

            using (Pen border = new Pen(Dim(baseColor, 0.45f), 1f))
            using (SolidBrush corner = Owned(Dim(baseColor, 0.7f)))
            {
                g.DrawRectangle(border, px + 0.5f, py + 0.5f, pw - 1, ph - 1);
                // Small corner ticks read as "panel" without box glyphs.
                const float t = 5f;
                g.FillRectangle(corner, px, py, t, 1f);
                g.FillRectangle(corner, px + pw - t, py, t, 1f);
                g.FillRectangle(corner, px, py + ph - 1f, t, 1f);
                g.FillRectangle(corner, px + pw - t, py + ph - 1f, t, 1f);
            }

            // Title
            string title = " " + p.Title + " ";
            int maxChars = p.W - 8;
            if (title.Length > maxChars) title = title.Substring(0, Math.Max(0, maxChars));
            DrawText(g, title, px + _cellW, py + 1f, Brush(baseColor), true);

            // Spinner in the top-right corner
            string spin = p.Stationary ? "\u00b7" : Braille(p.Tick);
            SizeF sz = g.MeasureString(spin, _fontBold, new SizeF(4000f, 4000f), _fmt);
            DrawText(g, spin, px + pw - _cellW * 2f - 4f, py + 1f, Brush(Magenta), true);

            // Content area
            float cx = px + _cellW * 0.5f;
            float cy = py + _cellH + 2f;
            float cw = pw - _cellW;
            float ch = ph - _cellH - 6f;
            if (cw < 12 || ch < 8) return;

            GraphicsState saved = g.Save();
            g.SetClip(new RectangleF(cx, cy, cw, ch));

            int drawn = 0;
            if (p.HasSpark && ch > 6 * _cellH)
            {
                DrawSparkline(g, p, cx, cy, cw, ch, ref drawn);
            }
            if (p.HasBoxChart && ch > 10 * _cellH && !p.HasSpark)
            {
                DrawBoxChart(g, p, cx, cy, cw, ch, ref drawn);
            }
            if (p.HasBarChart && ch > 10 * _cellH && cw > 30 * _cellW)
            {
                DrawBarChart(g, p, cx, cy, cw, ch, ref drawn);
            }
            if (p.HasProgress && p.ProgressTasks.Count > 0)
            {
                DrawProgress(g, p, cx, cy, cw, ch, ref drawn);
            }

            int maxLines = (int)(ch / _cellH) - drawn;
            if (maxLines > 0)
            {
                int from = Math.Max(0, p.Lines.Count - maxLines);
                for (int i = from; i < p.Lines.Count; i++)
                {
                    Line ln = p.Lines[i];
                    float ly = cy + (drawn + (i - from)) * _cellH;
                    if (ly > py + ph - _cellH) break;
                    Color c = KindColor(ln.Kind);
                    if (_dimKinds.Contains(ln.Kind)) c = Dim(c, 0.62f);
                    DrawText(g, ln.Text, cx, ly, Brush(c), _boldKinds.Contains(ln.Kind));
                }
            }

            g.Restore(saved);
        }

        private static string Braille(int tick)
        {
            string[] frames = { "\u28fe", "\u28fd", "\u28fb", "\u28bf", "\u287f", "\u285f", "\u28ef", "\u28f7" };
            return frames[((tick % frames.Length) + frames.Length) % frames.Length];
        }

        private static Color Dim(Color c, float factor)
        {
            return Color.FromArgb(c.A,
                (int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor));
        }

        private void DrawText(Graphics g, string text, float x, float y, SolidBrush brush, bool bold)
        {
            g.DrawString(text, bold ? _fontBold : _font, brush, x, y, _fmt);
        }

        // ── widgets ─────────────────────────────────────────────────────
        private void DrawSparkline(Graphics g, PanelBox p, float cx, float cy, float cw, float ch, ref int drawn)
        {
            string label = " " + p.ChartTitle;
            DrawText(g, label, cx, cy + drawn * _cellH, Brush(Dim(Base(p.ColorName), 0.8f)), false);
            drawn++;

            int slots = (int)(cw / Math.Max(2f, _cellW * 0.62f));
            if (slots < 4) slots = 4;
            List<double> data = p.ChartData;
            int start = data.Count - slots;
            if (start < 0) start = 0;

            double mn = double.MaxValue, mx = double.MinValue;
            for (int i = start; i < data.Count; i++)
            {
                if (data[i] < mn) mn = data[i];
                if (data[i] > mx) mx = data[i];
            }
            double range = mx - mn;
            if (range <= 0) range = 1;

            float plotH = _cellH * 1.6f;
            float by = cy + (drawn + 1) * _cellH;
            float bw = cw / slots;
            using (SolidBrush b = Owned(Dim(Base(p.ColorName), 1f)))
            {
                for (int i = 0; i < data.Count - start; i++)
                {
                    double frac = (data[start + i] - mn) / range;
                    float h = (float)(frac * plotH);
                    if (h < 1f) h = 1f;
                    g.FillRectangle(b, cx + i * bw, by + (plotH - h), Math.Max(1f, bw - 1f), h);
                }
            }

            string val = data[data.Count - 1].ToString("F1", Inv);
            DrawText(g, val, cx, by + _cellH * 0.3f, Brush(White), true);
            drawn += 2;

            using (Pen line = new Pen(Dim(Base(p.ColorName), 0.35f), 1f))
            {
                g.DrawLine(line, cx, by + plotH + 3f, cx + cw, by + plotH + 3f);
            }
            drawn++;
        }

        private void DrawBoxChart(Graphics g, PanelBox p, float cx, float cy, float cw, float ch, ref int drawn)
        {
            int plotRows = Math.Min(6, (int)(ch / _cellH / 2));
            if (plotRows < 3) plotRows = 3;

            DrawText(g, " " + p.ChartTitle.ToUpperInvariant(), cx, cy + drawn * _cellH,
                     Brush(Dim(Base(p.ColorName), 0.9f)), true);
            drawn++;

            double mn = double.MaxValue, mx = double.MinValue;
            for (int i = 0; i < p.ChartData.Count; i++)
            {
                if (p.ChartData[i] < mn) mn = p.ChartData[i];
                if (p.ChartData[i] > mx) mx = p.ChartData[i];
            }
            double range = mx - mn;
            if (range <= 0) range = 1;

            // Cap the column count: past ~44 the bars turn into noise.
            int cols = Math.Min(44, (int)(cw / Math.Max(2f, _cellW * 0.6f)));
            if (cols < 6) cols = 6;
            int start = Math.Max(0, p.ChartData.Count - cols);
            int n = p.ChartData.Count - start;

            float areaH = plotRows * _cellH;
            float areaY = cy + drawn * _cellH;
            float colW = cw / cols;

            using (Pen grid = new Pen(Color.FromArgb(28, 0, 80, 80), 1f))
            using (SolidBrush b = Owned(Dim(Base(p.ColorName), 0.95f)))
            {
                g.DrawRectangle(grid, cx, areaY, cw, areaH);
                for (int i = 0; i < n; i++)
                {
                    double frac = (p.ChartData[start + i] - mn) / range;
                    float h = (float)(frac * (areaH - 2));
                    if (h < 1f) h = 1f;
                    g.FillRectangle(b, cx + 1 + i * colW, areaY + areaH - 1 - h,
                                    Math.Max(1f, colW - 1f), h);
                }
            }
            drawn += plotRows + 1;
        }

        private void DrawBarChart(Graphics g, PanelBox p, float cx, float cy, float cw, float ch, ref int drawn)
        {
            string[] keys = p.BarKeys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                if (drawn >= (int)(ch / _cellH) - 1) break;
                double val;
                if (!p.BarData.TryGetValue(keys[i], out val)) continue;

                float y = cy + drawn * _cellH;
                DrawText(g, keys[i].PadLeft(8), cx, y, Brush(White), false);

                float labelW = _cellW * 8f;
                float barX = cx + labelW + _cellW * 0.5f;
                float barMax = cw - labelW - _cellW * 6f;
                if (barMax < 20) barMax = 20;
                float barW = (float)(val / 100.0 * barMax);
                float barH = _cellH * 0.72f;

                using (SolidBrush track = Owned(Color.FromArgb(30, 30, 34)))
                using (SolidBrush fill = Owned(Base(p.ColorName)))
                {
                    g.FillRectangle(track, barX, y + (_cellH - barH) / 2f, barMax, barH);
                    g.FillRectangle(fill, barX, y + (_cellH - barH) / 2f, Math.Max(1f, barW), barH);
                }

                string pct = val.ToString("F1", Inv) + "%";
                DrawText(g, pct, barX + barMax + _cellW * 0.4f, y, Brush(White), false);
                drawn++;
            }
            if (drawn > 0) drawn++;
        }

        private void DrawProgress(Graphics g, PanelBox p, float cx, float cy, float cw, float ch, ref int drawn)
        {
            for (int i = 0; i < p.ProgressTasks.Count; i++)
            {
                if (drawn >= (int)(ch / _cellH) - 1) break;
                ProgressTask t = p.ProgressTasks[i];
                float y = cy + drawn * _cellH;

                string spin = t.Pct >= 100 ? "\u2713" : SpinFrames(p.Tick + i);
                DrawText(g, " " + spin + " ", cx, y, Brush(Base(p.ColorName)), false);

                int nameChars = Math.Max(6, (int)(cw / _cellW) - 26);
                string name = t.Name.Length > nameChars ? t.Name.Substring(0, nameChars) : t.Name;
                DrawText(g, name, cx + _cellW * 3f, y, Brush(White), false);

                float barX = cx + _cellW * (3f + nameChars + 1f);
                float barMax = cw - (barX - cx) - _cellW * 6f;
                if (barMax < 24) barMax = 24;
                float barH = _cellH * 0.7f;
                float frac = (float)(t.Pct / 100.0);

                using (SolidBrush track = Owned(Color.FromArgb(30, 30, 34)))
                using (SolidBrush fill = Owned(Green))
                {
                    g.FillRectangle(track, barX, y + (_cellH - barH) / 2f, barMax, barH);
                    g.FillRectangle(fill, barX, y + (_cellH - barH) / 2f, Math.Max(1f, barMax * frac), barH);
                }

                DrawText(g, t.Pct.ToString("F0", Inv) + "%", barX + barMax + _cellW * 0.4f, y, Brush(White), false);
                drawn++;
            }
            if (drawn > 0) drawn++;
        }

        private static readonly string[] Spin = { "\u280b", "\u2819", "\u2839", "\u2838", "\u283c", "\u2834", "\u2826", "\u2827", "\u2807", "\u280f" };

        private static string SpinFrames(int i)
        {
            return Spin[((i % Spin.Length) + Spin.Length) % Spin.Length];
        }

        // ── chrome ──────────────────────────────────────────────────────
        private void DrawStatusBar(Graphics g, RenderState st, List<PanelBox> panels, double elapsed, int fps)
        {
            float h = _cellH * 1.25f;
            float y = st.Bounds.Height - h - 1f;
            float w = st.Bounds.Width - 8f;

            using (SolidBrush back = Owned(Color.FromArgb(0, 30, 34)))
            {
                g.FillRectangle(back, 4f, y, w, h);
            }
            using (Pen edge = new Pen(Color.FromArgb(0, 80, 85), 1f))
            {
                g.DrawLine(edge, 4f, y, 4f + w, y);
            }

            int uh = (int)elapsed / 3600;
            int um = ((int)elapsed % 3600) / 60;
            int us = (int)elapsed % 60;
            string left = string.Format(Inv,
                " {0} NEXUS UNIFIED COMMAND v4.7.2 | {1:00}:{2:00}:{3:00} | {4} fps | NODES {5}/52",
                SpinFrames(st.Frame), uh, um, us, fps, _rnd.Next(47, 53));

            DrawText(g, left, 8f, y + 1f, Brush(Color.Black), false);

            string right = string.Format(Inv, "MEM {0}% | CLEARANCE: COSMIC//NOFORN | THREAT: {1} ",
                _rnd.Next(62, 88),
                (_rnd.NextDouble() < 0.07) ? "ELEVATED " : "NOMINAL");
            DrawTextRight(g, right, 4f + w - 4f, y + 1f, Brush(Color.Black));
        }

        private void DrawTextRight(Graphics g, string text, float right, float y, SolidBrush brush)
        {
            g.DrawString(text, _font, brush, right - 4000f, y, _fmtRight);
        }

        public void Dispose()
        {
            foreach (SolidBrush b in _brushCache.Values)
            {
                if (b != null) b.Dispose();
            }
            _brushCache.Clear();

            if (_font != null) _font.Dispose();
            if (_fontBold != null) _fontBold.Dispose();
            if (_fmt != null) _fmt.Dispose();
            if (_fmtRight != null) _fmtRight.Dispose();
        }
    }
}