using System;
using System.Drawing;
using System.Windows.Forms;

namespace NexusCore
{
    /// <summary>
    /// Invoked with no arguments (or /c) when the user presses 设置(T) in the
    /// Windows 屏幕保护程序 dialog. The black box is a real SurfaceForm parented
    /// into this dialog, so every change is visible immediately.
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly PreviewPanel _preview;
        private Settings _pending;

        private ComboBox _display;
        private ComboBox _font;
        private ComboBox _speed;
        private ComboBox _density;
        private ComboBox _fps;
        private NumericUpDown _fontSize;
        private CheckBox _fontSizeAuto;
        private Label _previewNote;

        public SettingsForm()
        {
            _pending = Config.Load();

            Text = "Cyberpunk Terminal - 设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(660, 600);
            BackColor = Color.FromArgb(24, 24, 28);
            ForeColor = Color.FromArgb(220, 220, 220);
            Font = new Font("Microsoft YaHei UI", 9f);

            Controls.Add(MakeHeader("NEXUS CORE  ·  屏幕保护程序设置"));

            int y = 56;
            const int ColW = 306;

            _display = MakeCombo("显示器范围", 20, y, ColW,
                                  new string[] { "所有显示器", "仅主显示器" },
                                  _pending.DisplayScope == "primary" ? "仅主显示器" : "所有显示器");
            _display.SelectedIndexChanged += delegate { Push(); };

            _font = MakeCombo("字体", 334, y, ColW,
                               WithFallback(Settings.Fonts, _pending.FontName),
                               _pending.FontName);
            _font.SelectedIndexChanged += delegate { Push(); };

            y += 36;
            Controls.Add(MakeLabel("字号", 20, y + 4, 68));
            _fontSize = new NumericUpDown();
            _fontSize.Location = new Point(92, y);
            _fontSize.Size = new Size(88, 24);
            _fontSize.Minimum = 0;
            _fontSize.Maximum = 48;
            _fontSize.Value = _pending.FontSize < 0 ? 0 : (_pending.FontSize > 48 ? 48 : _pending.FontSize);
            _fontSize.ValueChanged += delegate { Push(); };
            Controls.Add(_fontSize);

            _fontSizeAuto = new CheckBox();
            _fontSizeAuto.Text = "自动";
            _fontSizeAuto.Location = new Point(186, y + 2);
            _fontSizeAuto.Size = new Size(70, 22);
            _fontSizeAuto.FlatStyle = FlatStyle.Flat;
            _fontSizeAuto.ForeColor = Color.FromArgb(200, 200, 200);
            _fontSizeAuto.Checked = _pending.FontSize == 0;
            _fontSizeAuto.CheckedChanged += delegate
            {
                _fontSize.Enabled = !_fontSizeAuto.Checked;
                Push();
            };
            Controls.Add(_fontSizeAuto);
            _fontSize.Enabled = !_fontSizeAuto.Checked;

            Hint at254 = new Hint();
            at254.Location = new Point(254, y + 4);
            at254.Size = new Size(280, 20);
            at254.Text = "0 = 按屏幕高度自适应（约 60 行）";
            Controls.Add(at254);

            y += 36;
            _speed = MakeCombo("动画速度", 20, y, ColW,
                                new string[] { "慢", "正常", "快" },
                                SpeedLabel(_pending.Speed));
            _speed.SelectedIndexChanged += delegate { Push(); };

            _density = MakeCombo("内容密度", 334, y, ColW,
                                  new string[] { "低", "中", "高" },
                                  DensityLabel(_pending.Density));
            _density.SelectedIndexChanged += delegate { Push(); };

            y += 36;
            _fps = MakeCombo("帧率上限", 20, y, ColW,
                              new string[] { "15 FPS", "30 FPS", "60 FPS" },
                              _pending.Fps + " FPS");
            _fps.SelectedIndexChanged += delegate { Push(); };

            Controls.Add(MakeLabel("预览（实时）", 334, y + 4, 160));

            _preview = new PreviewPanel(_pending);
            _preview.Location = new Point(20, y + 36);
            // Tall enough for the recursive split to actually subdivide, so the
            // preview shows a representative panel layout rather than one big
            // box - a 170px strip always collapses to a single panel.
            _preview.Size = new Size(620, 280);
            _preview.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(_preview);

            _previewNote = MakeLabel("", 20, _preview.Bottom + 6, 620);
            _previewNote.ForeColor = Color.FromArgb(150, 200, 200);

            int by = _preview.Bottom + 30;
            Label tip = MakeLabel(
                "提示：屏保运行时任意键鼠操作即退出；系统的「在恢复时显示登录屏幕」可正常勾选，不影响本屏保。",
                20, by, 620);
            tip.ForeColor = Color.FromArgb(150, 150, 150);
            Controls.Add(tip);

            Controls.Add(MakeButton("应用", 20, by + 26, 108, delegate { SaveNow(); }));
            Controls.Add(MakeButton("确定", 138, by + 26, 108, delegate { SaveNow(); Close(); }));
            Controls.Add(MakeButton("取消", 256, by + 26, 108, delegate { Close(); }));

            UpdateNote();
        }

        /// <summary>ComboBox with a real item list - no empty dropdowns.</summary>
        private ComboBox MakeCombo(string label, int x, int y, int w,
                                   string[] items, string selected)
        {
            Controls.Add(MakeLabel(label, x, y + 4, 72));

            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Location = new Point(x + 74, y);
            c.Size = new Size(w - 74, 24);
            c.FlatStyle = FlatStyle.Flat;
            c.BackColor = Color.FromArgb(40, 40, 46);
            c.ForeColor = Color.White;
            c.Items.AddRange(items);

            int idx = Array.IndexOf(items, selected);
            c.SelectedIndex = idx >= 0 ? idx : 0;
            Controls.Add(c);
            return c;
        }

        private static string[] WithFallback(string[] options, string value)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == value) return options;
            }
            string[] copy = new string[options.Length + 1];
            Array.Copy(options, copy, options.Length);
            copy[options.Length] = value;
            return copy;
        }

        private static string SpeedLabel(string v)
        {
            return v == "slow" ? "慢" : (v == "fast" ? "快" : "正常");
        }

        private static string DensityLabel(string v)
        {
            return v == "low" ? "低" : (v == "med" ? "中" : "高");
        }

        private static string SpeedValue(string label)
        {
            if (label == "慢") return "slow";
            if (label == "快") return "fast";
            return "normal";
        }

        private static string DensityValue(string label)
        {
            if (label == "低") return "low";
            if (label == "中") return "med";
            return "high";
        }

        private void Push()
        {
            Settings s = new Settings();
            s.DisplayScope = (_display.SelectedItem != null &&
                              _display.SelectedItem.ToString() == "仅主显示器") ? "primary" : "all";
            s.FontName = _font.SelectedItem != null ? _font.SelectedItem.ToString() : "Consolas";
            s.Speed = SpeedValue(_speed.SelectedItem == null ? "正常" : _speed.SelectedItem.ToString());
            s.Density = DensityValue(_density.SelectedItem == null ? "高" : _density.SelectedItem.ToString());
            s.Fps = _fps.SelectedIndex <= 0 ? 15 : (_fps.SelectedIndex == 1 ? 30 : 60);
            s.FontSize = _fontSizeAuto.Checked ? 0 : (int)_fontSize.Value;

            bool changed = s.DisplayScope != _pending.DisplayScope
                           || s.FontName != _pending.FontName
                           || s.FontSize != _pending.FontSize
                           || s.Speed != _pending.Speed
                           || s.Fps != _pending.Fps
                           || s.Density != _pending.Density;
            _pending = s;
            if (changed && _preview != null && !_preview.IsDisposed)
            {
                // Font, size and density all change the character grid, so the
                // preview has to re-layout - not just repaint.
                _preview.ApplySettings(s);
            }
            UpdateNote();
        }

        private void UpdateNote()
        {
            if (_previewNote == null) return;
            _previewNote.Text = "当前：" + _pending.FontName
                                + (_pending.FontSize == 0 ? " / 自动字号" : " / " + _pending.FontSize + " px")
                                + " / 密度 " + DensityLabel(_pending.Density)
                                + " / 速度 " + SpeedLabel(_pending.Speed)
                                + " / " + _pending.Fps + " FPS";
        }

        private void SaveNow()
        {
            Push();
            Config.Save(_pending);
            Program.Log("settings: saved scope=" + _pending.DisplayScope
                        + " font=" + _pending.FontName + "/" + _pending.FontSize
                        + " speed=" + _pending.Speed + " fps=" + _pending.Fps
                        + " density=" + _pending.Density);
        }

        private Label MakeLabel(string text, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = text;
            l.Location = new Point(x, y);
            l.Size = new Size(w, 20);
            l.ForeColor = Color.FromArgb(180, 180, 180);
            return l;
        }

        private Label MakeHeader(string text)
        {
            Label l = MakeLabel(text, 20, 18, 620);
            l.Font = new Font("Consolas", 12f, FontStyle.Bold);
            l.ForeColor = Color.FromArgb(0, 220, 220);
            return l;
        }

        private Button MakeButton(string text, int x, int y, int w, EventHandler onClick)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(x, y);
            b.Size = new Size(w, 30);
            b.BackColor = Color.FromArgb(40, 40, 46);
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(0, 220, 220);
            b.Click += onClick;
            return b;
        }

        /// <summary>Dimmed inline note next to a control.</summary>
        private sealed class Hint : Label
        {
        }
    }
}