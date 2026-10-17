using System;
using Microsoft.Win32;

namespace NexusCore
{
    /// <summary>
    /// User preferences. Per-user and stored as plain values under
    /// HKCU\Software\NexusCoreSaver - nothing here is secret, so no DPAPI and
    /// no elevation needed. See 02-需求设计文档.md §4.
    /// </summary>
    internal sealed class Settings
    {
        /// <summary>"all" or "primary".</summary>
        public string DisplayScope = "all";

        /// <summary>Point size; 0 means "derive from the screen height".</summary>
        public int FontSize;

        public string FontName = "Consolas";

        /// <summary>"slow" / "normal" / "fast".</summary>
        public string Speed = "normal";

        public int Fps = 30;

        /// <summary>"low" / "med" / "high" - controls how many panels tile.</summary>
        public string Density = "high";

        public double SpeedScale
        {
            get
            {
                if (Speed == "slow") return 0.5;
                if (Speed == "fast") return 2.0;
                return 1.0;
            }
        }

        public int MaxDepth
        {
            get
            {
                if (Density == "low") return 1;
                if (Density == "med") return 2;
                return 3;
            }
        }

        public int TickMs
        {
            get { return Fps <= 15 ? 66 : (Fps >= 60 ? 16 : 33); }
        }

        public static string[] DisplayScopes = { "all", "primary" };
        public static string[] Speeds = { "slow", "normal", "fast" };
        public static string[] Densities = { "low", "med", "high" };
        public static string[] Fonts = { "Consolas", "Cascadia Mono", "Lucida Console", "Courier New" };

        public static string Describe(string[] options, string value)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == value) return options[i];
            }
            return options[0];
        }
    }

    internal static class Config
    {
        private const string KeyPath = @"Software\NexusCoreSaver";

        public static Settings Load()
        {
            Settings s = new Settings();
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (key == null) return s;

                    s.DisplayScope = ReadString(key, "DisplayScope", s.DisplayScope);
                    s.FontName = ReadString(key, "FontName", s.FontName);
                    s.Speed = ReadString(key, "Speed", s.Speed);
                    s.Density = ReadString(key, "Density", s.Density);
                    s.FontSize = ReadInt(key, "FontSize", s.FontSize);
                    s.Fps = ReadInt(key, "Fps", s.Fps);
                }
            }
            catch (Exception)
            {
                // A locked-down or redirected registry must never stop the
                // saver - fall through with the defaults.
            }

            // Clamp anything a hand-edited registry could have put out of range.
            if (s.Fps != 15 && s.Fps != 30 && s.Fps != 60) s.Fps = 30;
            if (s.FontSize < 0 || s.FontSize > 48) s.FontSize = 0;
            s.DisplayScope = Settings.Describe(Settings.DisplayScopes, s.DisplayScope);
            s.Speed = Settings.Describe(Settings.Speeds, s.Speed);
            s.Density = Settings.Describe(Settings.Densities, s.Density);
            return s;
        }

        public static void Save(Settings s)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath, true))
                {
                    if (key == null) return;
                    key.SetValue("DisplayScope", s.DisplayScope, RegistryValueKind.String);
                    key.SetValue("FontSize", s.FontSize, RegistryValueKind.DWord);
                    key.SetValue("FontName", s.FontName, RegistryValueKind.String);
                    key.SetValue("Speed", s.Speed, RegistryValueKind.String);
                    key.SetValue("Fps", s.Fps, RegistryValueKind.DWord);
                    key.SetValue("Density", s.Density, RegistryValueKind.String);
                }
            }
            catch (Exception)
            {
                // Read-only registry: keep running with the current values.
            }
        }

        private static string ReadString(RegistryKey key, string name, string fallback)
        {
            object v = key.GetValue(name);
            return v as string ?? fallback;
        }

        private static int ReadInt(RegistryKey key, string name, int fallback)
        {
            object v = key.GetValue(name);
            if (v == null) return fallback;
            try { return Convert.ToInt32(v); }
            catch (Exception) { return fallback; }
        }
    }
}