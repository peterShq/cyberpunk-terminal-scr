using System;
using System.Collections.Generic;

namespace NexusCore
{
    internal sealed class ProgressTask
    {
        public string Name;
        public double Pct;
        public double Speed;
    }

    /// <summary>
    /// A single titled panel with its own scrolling log and widgets.
    /// Ported from panel.py's PanelBox class - same pacing maths, same pause and
    /// burst mechanics, so the motion feels identical.
    /// </summary>
    internal sealed class PanelBox
    {
        public string Title;
        public int X, Y, W, H;
        public string PanelType;
        public string ColorName;
        public bool Stationary;

        public readonly List<Line> Lines = new List<Line>();
        public int Tick;

        private int _burstCd;
        private double _pausedUntil;
        private double _lineBudget;
        private double _nextRefresh;
        private readonly double _refreshMin = 3.0;
        private readonly double _refreshMax = 8.0;
        private double _baseSpeed;

        /// <summary>Multiplied into the per-panel pacing; 1.0 = as designed.</summary>
        public double SpeedMultiplier = 1.0;

        public readonly List<double> ChartData = new List<double>();
        public string ChartTitle = "";
        public bool HasSpark;
        public bool HasBoxChart;
        public bool HasBarChart;
        public readonly Dictionary<string, double> BarData = new Dictionary<string, double>();
        public readonly List<string> BarKeys = new List<string>();

        public bool HasProgress;
        public readonly List<ProgressTask> ProgressTasks = new List<ProgressTask>();

        private static readonly Random Rng = new Random();

        public PanelBox(string title, int x, int y, int w, int h,
                     string panelType, string colorName, bool stationary)
        {
            Title = title;
            X = x; Y = y; W = w; H = h;
            PanelType = panelType;
            ColorName = colorName;
            Stationary = stationary;

            _baseSpeed = stationary ? 0.0 : Rng.NextDouble() * 0.48 + 0.12;

            for (int i = 0; i < 80; i++) ChartData.Add(Rng.NextDouble() * 60 + 20);
        }

        private static Line L(string text, string kind) { return new Line(text, kind); }

        /// <summary>Weighted choice among the generators for this panel type.</summary>
        private List<Line> GenLines()
        {
            double r = Rng.NextDouble();
            switch (PanelType)
            {
                case "build":
                    if (r < 0.35) return One(Content.GenCompile);
                    if (r < 0.50) return One(Content.GenCompileDetail);
                    if (r < 0.65) return One(Content.GenTest);
                    if (r < 0.75) return Two(Content.GenDiffHunk, Content.GenDiffAdd);
                    if (r < 0.82) return One(Content.GenDiffDel);
                    if (r < 0.90) return One(Content.GenLint);
                    if (r < 0.95) return One(Content.GenDep);
                    return One(Content.GenTestSummary);

                case "net":
                    if (r < 0.35) return One(Content.GenNetwork);
                    if (r < 0.55) return One(Content.GenK8s);
                    if (r < 0.70) return One(Content.GenMetric);
                    if (r < 0.80) return One(Content.GenApi);
                    if (r < 0.90) return One(Content.GenInlineSpark);
                    return One(Content.GenDeploy);

                case "claude":
                    if (r < 0.15) return Two(Content.GenClaudeSeparator, Content.GenClaudePrompt);
                    if (r < 0.30) return One(Content.GenClaudeToolRead);
                    if (r < 0.42) return One(Content.GenClaudeToolEdit);
                    if (r < 0.52) return One(Content.GenClaudeToolBash);
                    if (r < 0.65) return One(Content.GenClaudeToolResult);
                    if (r < 0.78) return One(Content.GenClaudeCodeSnippet);
                    if (r < 0.88) return One(Content.GenClaudeThink);
                    return One(Content.GenCompile);

                case "data":
                    if (r < 0.30) return One(Content.GenDb);
                    if (r < 0.45) return One(Content.GenDbDetail);
                    if (r < 0.60) return One(Content.GenMetric);
                    if (r < 0.75) return One(Content.GenInlineSpark);
                    if (r < 0.85) return One(Content.GenApi);
                    return One(Content.GenHex);

                case "deploy":
                    if (r < 0.30) return One(Content.GenDeploy);
                    if (r < 0.55) return One(Content.GenK8s);
                    if (r < 0.70) return One(Content.GenMetric);
                    if (r < 0.85) return One(Content.GenApi);
                    return One(Content.GenNetwork);

                case "hex":
                    {
                        // Two independent draws, exactly like the original:
                        // 60% hex, and of the rest half packet / half cipher.
                        if (Rng.NextDouble() < 0.6)
                        {
                            List<Line> many = new List<Line>();
                            int n = Rng.Next(1, 4);
                            for (int i = 0; i < n; i++) many.Add(Content.GenHex());
                            return many;
                        }
                        if (Rng.NextDouble() < 0.5) return One(Content.GenPacket);
                        return One(Content.GenCipher);
                    }

                case "infra":
                    if (r < 0.25) return One(Content.GenMetric);
                    if (r < 0.45) return One(Content.GenK8s);
                    if (r < 0.60) return One(Content.GenNetwork);
                    if (r < 0.75) return One(Content.GenInlineSpark);
                    if (r < 0.85) return One(Content.GenDb);
                    return One(Content.GenHex);

                default:
                    return One(Content.GenCompile);
            }
        }

        private static List<Line> One(Func<Line> gen)
        {
            List<Line> l = new List<Line>();
            l.Add(gen());
            return l;
        }

        private static List<Line> Two(Func<Line> a, Func<Line> b)
        {
            List<Line> l = new List<Line>();
            l.Add(a());
            l.Add(b());
            return l;
        }

        private void RefreshStaticLines()
        {
            // Fill the panel rather than capping at ten lines: a stationary
            // panel has no scrolling to hide the emptiness, so a tall panel
            // with ten rows of text just looks broken.
            int target = Math.Max(6, H - 8);
            List<Line> lines = new List<Line>();
            while (lines.Count < target) lines.AddRange(GenLines());
            if (Rng.NextDouble() < 0.002)
            {
                lines.Add(L("  \u26a0 " + Content.Ominous[Rng.Next(Content.Ominous.Length)], "ominous"));
            }
            Lines.Clear();
            int from = Math.Max(0, lines.Count - target);
            for (int i = from; i < lines.Count; i++) Lines.Add(lines[i]);
        }

        public void Update(double t)
        {
            Tick++;

            if (Stationary)
            {
                if (t >= _nextRefresh || Lines.Count == 0)
                {
                    _nextRefresh = t + (_refreshMin + Rng.NextDouble() * (_refreshMax - _refreshMin));
                    RefreshStaticLines();
                }
                UpdateCharts();
                UpdateProgress();
                return;
            }

            // Occasional pause so fast panels still read as "working".
            if (t < _pausedUntil) { UpdateCharts(); return; }
            if (Rng.NextDouble() < 0.08)
            {
                _pausedUntil = t + (Rng.NextDouble() * 2.1 + 0.7);
                UpdateCharts();
                return;
            }

            _lineBudget += _baseSpeed * SpeedMultiplier;
            int count = (int)_lineBudget;
            _lineBudget -= count;

            if (_burstCd > 0)
            {
                _burstCd--;
                count += Rng.Next(1, 5);
            }
            else if (Rng.NextDouble() < 0.01)
            {
                _burstCd = Rng.Next(8, 19);
            }

            if (count <= 0 && Rng.NextDouble() < 0.08) count = 1;

            for (int i = 0; i < Math.Max(count, 0); i++)
            {
                List<Line> fresh = GenLines();
                if (Rng.NextDouble() < 0.003)
                {
                    fresh.Add(L("  \u26a0 " + Content.Ominous[Rng.Next(Content.Ominous.Length)], "ominous"));
                }
                Lines.AddRange(fresh);
            }

            UpdateCharts();
            UpdateProgress();

            int maxBuf = Math.Max(H * 4, 120);
            if (Lines.Count > maxBuf)
            {
                Lines.RemoveRange(0, Lines.Count - maxBuf);
            }
        }

        private void UpdateCharts()
        {
            if (HasSpark || HasBoxChart)
            {
                double last = ChartData[ChartData.Count - 1] + NextGaussian() * 2.5;
                ChartData.Add(Math.Max(0, Math.Min(100, last)));
                if (ChartData.Count > 200) ChartData.RemoveRange(0, ChartData.Count - 200);
            }
            if (HasBarChart)
            {
                List<string> keys = new List<string>(BarData.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    double v = BarData[keys[i]] + NextGaussian() * 1.2;
                    BarData[keys[i]] = Math.Max(0, Math.Min(100, v));
                }
            }
        }

        private void UpdateProgress()
        {
            if (!HasProgress) return;
            List<ProgressTask> next = new List<ProgressTask>();
            for (int i = 0; i < ProgressTasks.Count; i++)
            {
                ProgressTask t = ProgressTasks[i];
                t.Pct += t.Speed;
                if (t.Pct >= 100)
                {
                    Lines.Add(L("  \u2713 " + t.Name + " complete", "pass"));
                    ProgressTask p = new ProgressTask();
                    p.Name = Content.ProgressTasks[Rng.Next(Content.ProgressTasks.Length)];
                    p.Pct = 0;
                    p.Speed = Rng.NextDouble() * 0.55 + 0.05;
                    next.Add(p);
                }
                else
                {
                    next.Add(t);
                }
            }
            ProgressTasks.Clear();
            ProgressTasks.AddRange(next);
        }

        private static double NextGaussian()
        {
            // Box-Muller, clamped so a stray tail cannot spike a chart.
            double u1 = 1.0 - Rng.NextDouble();
            double u2 = 1.0 - Rng.NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return Math.Max(-2.5, Math.Min(2.5, z));
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  LAYOUT - varied panel sizes, like a tiling window manager
    // ══════════════════════════════════════════════════════════════════

    internal sealed class PanelConfig
    {
        public string Title;
        public string Type;
        public string Color;
        public bool Spark;
        public bool Box;
        public bool Bar;
        public bool Progress;
        public string ChartTitle;
        public bool Stationary;

        public PanelConfig(string title, string type, string color, bool spark, bool box,
                           bool bar, bool progress, string chartTitle, bool stationary)
        {
            Title = title; Type = type; Color = color;
            Spark = spark; Box = box; Bar = bar; Progress = progress;
            ChartTitle = chartTitle; Stationary = stationary;
        }
    }

    internal struct Rect
    {
        public int X, Y, W, H;
        public Rect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }
    }

    internal static class PanelLayout
    {
        private static readonly Random Rng = new Random();

        public static readonly PanelConfig[] Configs = {
            new PanelConfig("CLAUDE CODE :: AGENT-0", "claude",  "magenta", true,  false, false, true,  "inference latency", false),
            new PanelConfig("CLAUDE CODE :: AGENT-1", "claude",  "magenta", false, false, false, false, "", false),
            new PanelConfig("NEXUS-CORE :: BUILD",    "build",   "cyan",    true,  false, false, false, "build throughput", false),
            new PanelConfig("PHANTOM :: NETWORK",     "net",     "green",   true,  false, false, false, "network I/O (MB/s)", false),
            new PanelConfig("VAULT-SIGMA :: DATABASE","data",    "blue",    false, false, true,  false, "", true),
            new PanelConfig("SPECTRE-9 :: DEPLOY",    "deploy",  "cyan",    true,  false, false, true,  "pod health", false),
            new PanelConfig("OBSIDIAN :: INFRA",      "infra",   "yellow",  false, true,  false, false, "", true),
            new PanelConfig("CIPHER :: INTERCEPT",    "hex",     "red",     false, false, false, false, "", false),
            new PanelConfig("ECHO :: TELEMETRY",      "infra",   "green",   true,  false, false, false, "entropy (\u03c3)", true),
            new PanelConfig("DARK-FIBER :: RELAY",    "net",     "cyan",    false, false, false, false, "", false),
            new PanelConfig("UMBRA :: CLASSIFIER",    "data",    "magenta", false, true,  false, false, "anomaly score", false),
            new PanelConfig("GHOST :: PROTOCOL",      "deploy",  "yellow",  false, false, false, true,  "", false),
            new PanelConfig("KRONOS :: ENGINE",       "build",   "red",     true,  false, false, false, "compile rate", false),
            new PanelConfig("CLAUDE CODE :: AGENT-2", "claude",  "magenta", false, false, false, false, "", false),
            new PanelConfig("HYDRA :: CLUSTER",       "infra",   "blue",    false, false, true,  false, "cpu cluster", true),
            new PanelConfig("ZONE-49 :: ANALYSIS",    "data",    "green",   false, true,  false, false, "prediction conf", false),
        };

        private const int MinW = 36;
        private const int MinH = 9;

        private static void SplitRect(int x, int y, int w, int h, int depth, int maxDepth, List<Rect> acc)
        {
            if (depth >= maxDepth || (w < MinW * 2 && h < MinH * 2))
            {
                acc.Add(new Rect(x, y, w, h));
                return;
            }

            double aspect = w / (double)Math.Max(h, 1);
            bool splitH;
            if (aspect > 2.2) splitH = false;
            else if (aspect < 1.0) splitH = true;
            else splitH = Rng.NextDouble() < 0.45;

            double ratio = Rng.NextDouble() * 0.4 + 0.30;

            if (splitH)
            {
                int h1 = Math.Max(MinH, (int)(h * ratio));
                int h2 = h - h1;
                if (h2 < MinH) { acc.Add(new Rect(x, y, w, h)); return; }
                SplitRect(x, y, w, h1, depth + 1, maxDepth, acc);
                SplitRect(x, y + h1, w, h2, depth + 1, maxDepth, acc);
            }
            else
            {
                int w1 = Math.Max(MinW, (int)(w * ratio));
                int w2 = w - w1;
                if (w2 < MinW) { acc.Add(new Rect(x, y, w, h)); return; }
                SplitRect(x, y, w1, h, depth + 1, maxDepth, acc);
                SplitRect(x + w1, y, w2, h, depth + 1, maxDepth, acc);
            }
        }

        /// <summary>
        /// Guarantee enough stationary panels. Static ones are what make the
        /// screen look deliberate instead of uniformly noisy.
        /// </summary>
        private static List<PanelConfig> EnsureStationary(List<PanelConfig> configs, int useCount, int target)
        {
            if (target <= 0) return configs;

            List<PanelConfig> stationary = new List<PanelConfig>();
            for (int i = 0; i < configs.Count; i++)
            {
                if (configs[i].Stationary) stationary.Add(configs[i]);
            }
            if (stationary.Count == 0) return configs;

            List<PanelConfig> used = configs.GetRange(0, useCount);
            int existing = 0;
            for (int i = 0; i < used.Count; i++) if (used[i].Stationary) existing++;
            if (existing >= target) return configs;

            int need = target - existing;
            List<int> replaceable = new List<int>();
            for (int i = 0; i < used.Count; i++) if (!used[i].Stationary) replaceable.Add(i);

            int n = Math.Min(need, replaceable.Count);
            for (int i = 0; i < n; i++) used[replaceable[i]] = stationary[i % stationary.Count];

            List<PanelConfig> result = new List<PanelConfig>(used);
            result.AddRange(configs.GetRange(useCount, configs.Count - useCount));
            return result;
        }

        public static List<PanelBox> Build(int cols, int rows, Settings st)
        {
            int maxDepth = st.MaxDepth;
            List<Rect> rects = new List<Rect>();
            SplitRect(0, 0, cols, rows, 0, maxDepth, rects);

            List<PanelConfig> configs = new List<PanelConfig>(Configs);
            Shuffle(configs);
            configs = EnsureStationary(configs, rects.Count, Math.Max(1, rects.Count / 2));

            List<PanelBox> panels = new List<PanelBox>();
            for (int i = 0; i < rects.Count; i++)
            {
                Rect r = rects[i];
                PanelConfig cfg = configs[i % configs.Count];

                PanelBox p = new PanelBox(cfg.Title, r.X, r.Y, r.W, r.H, cfg.Type, cfg.Color, cfg.Stationary);
                p.SpeedMultiplier = st.SpeedScale;
                p.HasSpark = cfg.Spark && r.H > 9;
                p.HasBoxChart = cfg.Box && r.H > 12 && r.W > 25;
                p.HasBarChart = cfg.Bar && r.H > 12 && r.W > 30;
                p.ChartTitle = cfg.ChartTitle;
                p.HasProgress = cfg.Progress && !cfg.Stationary;

                if (cfg.Bar)
                {
                    string[] pool = { "cpu", "mem", "gpu", "net", "disk", "cache", "queue", "latency" };
                    List<string> keys = new List<string>(pool);
                    for (int k = 0; k < 5; k++)
                    {
                        int idx = Rng.Next(keys.Count);
                        string key = keys[idx];
                        keys.RemoveAt(idx);
                        p.BarData[key] = Rng.NextDouble() * 80 + 10;
                        p.BarKeys.Add(key);
                    }
                }

                if (cfg.Progress)
                {
                    int n = Rng.Next(2, 5);
                    for (int k = 0; k < n; k++)
                    {
                        ProgressTask t = new ProgressTask();
                        t.Name = Content.ProgressTasks[Rng.Next(Content.ProgressTasks.Length)];
                        t.Pct = Rng.NextDouble() * 70;
                        t.Speed = Rng.NextDouble() * 0.46 + 0.04;
                        p.ProgressTasks.Add(t);
                    }
                }

                panels.Add(p);
            }
            return panels;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}