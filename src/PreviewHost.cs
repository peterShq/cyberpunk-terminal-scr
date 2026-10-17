using System;
using System.Drawing;
using System.Windows.Forms;

namespace NexusCore
{
    /// <summary>
    /// `/p &lt;HWND&gt;` mode. Per KB 182383 the preview must become a CHILD of
    /// the window handle we were handed and cover its whole client area, or
    /// the system's 预览(V) button shows black.
    /// </summary>
    internal sealed class PreviewForm : SurfaceForm
    {
        private readonly IntPtr _parent;

        public PreviewForm(IntPtr parent)
            : base("PREVIEW", ClientAreaOf(parent), 0, 1, true)
        {
            _parent = parent;
            // No TopMost here: a child window has no business being topmost,
            // and it can end up stranded above its own parent dialog.
        }

        protected override bool IsSaverMode { get { return false; } }

        private static Rectangle ClientAreaOf(IntPtr parent)
        {
            Native.RECT r;
            if (parent != IntPtr.Zero && Native.GetClientRect(parent, out r) && r.Width > 0)
            {
                return new Rectangle(0, 0, r.Width, r.Height);
            }
            return new Rectangle(0, 0, 320, 180);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            AttachToParent();
        }

        private void AttachToParent()
        {
            if (_parent == IntPtr.Zero || !Native.IsWindow(_parent)) return;

            Native.SetParent(Handle, _parent);

            int style = Native.GetWindowLong(Handle, Native.GWL_STYLE);
            style &= ~Native.WS_POPUP;
            style |= Native.WS_CHILD;
            Native.SetWindowLong(Handle, Native.GWL_STYLE, style);

            Native.RECT r;
            if (Native.GetClientRect(_parent, out r))
            {
                // SWP_NOZORDER alone left the child buried under sibling
                // controls in the host dialog - the system preview looked
                // blank. Raise it within the parent and force it visible.
                Native.SetWindowPos(Handle, IntPtr.Zero, 0, 0, r.Width, r.Height,
                                    Native.SWP_SHOWWINDOW | Native.SWP_NOACTIVATE);
                Native.ShowWindow(Handle, Native.SW_SHOW);
            }

            Program.Log("preview: attached to parent " + _parent.ToInt64()
                        + " client=" + r.Width + "x" + r.Height);
        }

        protected override void OnTick(object sender, EventArgs e)
        {
            // The settings dialog closed - nothing left to draw into.
            if (_parent != IntPtr.Zero && !Native.IsWindow(_parent))
            {
                Close();
                return;
            }
            base.OnTick(sender, e);
        }
    }

    internal static class PreviewHost
    {
        public static void Run(IntPtr parent)
        {
            Application.Run(new PreviewForm(parent));
        }
    }
}