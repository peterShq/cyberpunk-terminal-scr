using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NexusCore
{
    /// <summary>
    /// `/s` mode: Windows starts exactly one screensaver process, so covering
    /// the desired displays is our job - one borderless topmost window each.
    /// </summary>
    internal sealed class SaverHost
    {
        public static void Run()
        {
            Settings settings = Config.Load();
            Screen[] screens = Screen.AllScreens;
            if (screens.Length == 0)
            {
                // Nothing to draw on - exit cleanly rather than spin.
                return;
            }

            List<Screen> targets = new List<Screen>();
            if (settings.DisplayScope == "primary")
            {
                targets.Add(Screen.PrimaryScreen);
            }
            else
            {
                foreach (Screen s in screens) targets.Add(s);
            }

            List<SurfaceForm> forms = new List<SurfaceForm>();
            for (int i = 0; i < targets.Count; i++)
            {
                Screen s = targets[i];
                forms.Add(new SurfaceForm("NEXUS-CORE-" + i, s.Bounds, i,
                                          targets.Count, false, settings));
            }

            Program.Log("saver: starting, scope=" + settings.DisplayScope
                        + ", " + forms.Count + " monitor(s), bounds="
                        + targets[0].Bounds.Width + "x" + targets[0].Bounds.Height
                        + ", fps=" + settings.Fps);

            try
            {
                foreach (SurfaceForm f in forms) f.Show();
                Application.Run();
            }
            finally
            {
                foreach (SurfaceForm f in forms)
                {
                    if (!f.IsDisposed) f.Close();
                }
            }
        }
    }
}