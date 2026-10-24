using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using PEPlugin.SDX;
using PEPlugin.View;

namespace PmxMcp
{
    /// <summary>Screenshots and camera control for the PmxView window.</summary>
    internal static class ViewTools
    {
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static void Register(ToolRegistry registry, Editor editor)
        {
            registry.Add(
                "capture_viewport",
                "截取 PmxView 窗口为 PNG 图片。可用于查看模型或检查编辑结果。",
                Schema.Object(Json.Obj(
                    "max_width", Schema.Int("缩放到最大为此宽度（默认 1024，0 表示保持原尺寸）"))),
                true,
                delegate(Dictionary<string, object> args) { return Capture(editor, args); });

            registry.Add(
                "get_camera",
                "读取 PmxView 相机：视点位置、目标和上方向向量。",
                Schema.None(),
                true,
                delegate(Dictionary<string, object> args) { return GetCamera(editor); });

            registry.Add(
                "set_camera",
                "移动 PmxView 相机。以 [x, y, z] 传入 position 和 target；up 默认为当前上方向向量。",
                Schema.Object(Json.Obj(
                    "position", Schema.NumArray("视点位置 [x, y, z]", 3),
                    "target", Schema.NumArray("注视点 [x, y, z]", 3),
                    "up", Schema.NumArray("上方向向量 [x, y, z]", 3)),
                    "position", "target"),
                false,
                delegate(Dictionary<string, object> args) { return SetCamera(editor, args); });
        }

        private static object Capture(Editor editor, Dictionary<string, object> args)
        {
            int maxWidth = Json.Int(args, "max_width", 1024);

            return editor.Ui<object>(delegate
            {
                // PMX Editor 0254 does not expose a viewport bitmap API (GetClientImage was
                // added later). PrintWindow grabs the view window directly, so overlapping
                // editor panels and other windows do not end up in the screenshot.
                Bitmap captured = CaptureViewWindow();
                if (captured == null)
                {
                    captured = CaptureScreenFallback(editor);
                }
                if (captured == null)
                {
                    throw new McpToolException("Could not capture PmxView; make sure the view window is open and visible.");
                }

                Bitmap scaled = null;
                try
                {
                    Bitmap source = captured;
                    if (maxWidth > 0 && captured.Width > maxWidth)
                    {
                        int height = (int)Math.Round(captured.Height * (double)maxWidth / captured.Width);
                        if (height < 1) height = 1;
                        scaled = new Bitmap(captured, new Size(maxWidth, height));
                        source = scaled;
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        source.Save(stream, ImageFormat.Png);
                        return new ImagePayload(Convert.ToBase64String(stream.ToArray()), "image/png");
                    }
                }
                finally
                {
                    if (scaled != null) scaled.Dispose();
                    captured.Dispose();
                }
            });
        }

        /// <summary>Finds the PmxView top-level window and prints its largest visible child.</summary>
        private static Bitmap CaptureViewWindow()
        {
            IntPtr view = FindWindowByTitle("PmxView");
            if (view == IntPtr.Zero) return null;

            IntPtr target = FindLargestVisibleChild(view);
            if (target == IntPtr.Zero) target = view;

            return PrintWindowBitmap(target);
        }

        private static Bitmap CaptureScreenFallback(Editor editor)
        {
            IPXPmxViewConnector view = editor.Connector.View.PmxView;
            Rectangle bounds = new Rectangle(view.Location, view.Size);
            if (bounds.Width <= 0 || bounds.Height <= 0) return null;

            view.Focus();
            Application.DoEvents();

            Bitmap captured = new Bitmap(bounds.Width, bounds.Height);
            try
            {
                using (Graphics graphics = Graphics.FromImage(captured))
                {
                    graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                }
                return captured;
            }
            catch
            {
                captured.Dispose();
                return null;
            }
        }

        private static IntPtr FindWindowByTitle(string titlePart)
        {
            uint pid = (uint)Process.GetCurrentProcess().Id;
            IntPtr found = IntPtr.Zero;

            EnumWindows(delegate(IntPtr hWnd, IntPtr lParam)
            {
                uint processId;
                GetWindowThreadProcessId(hWnd, out processId);
                if (processId != pid || !IsWindowVisible(hWnd)) return true;

                StringBuilder text = new StringBuilder(256);
                GetWindowText(hWnd, text, text.Capacity);
                if (text.ToString().IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);

            return found;
        }

        private static IntPtr FindLargestVisibleChild(IntPtr parent)
        {
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;

            EnumChildWindows(parent, delegate(IntPtr hWnd, IntPtr lParam)
            {
                if (!IsWindowVisible(hWnd)) return true;

                RECT rect;
                if (!GetWindowRect(hWnd, out rect)) return true;

                long area = (long)(rect.Right - rect.Left) * (rect.Bottom - rect.Top);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = hWnd;
                }
                return true;
            }, IntPtr.Zero);

            return best;
        }

        private static Bitmap PrintWindowBitmap(IntPtr hWnd)
        {
            RECT rect;
            if (!GetWindowRect(hWnd, out rect)) return null;

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            if (width <= 0 || height <= 0) return null;

            Bitmap bitmap = new Bitmap(width, height);
            bool printed;
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                IntPtr hdc = graphics.GetHdc();
                try
                {
                    printed = PrintWindow(hWnd, hdc, 2);
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            if (!printed)
            {
                bitmap.Dispose();
                return null;
            }
            return bitmap;
        }

        private static object GetCamera(Editor editor)
        {
            return editor.Ui<object>(delegate
            {
                IPXPmxViewConnector view = editor.Connector.View.PmxView;
                return Json.Obj(
                    "position", PmxUtil.Vec3(view.CameraPosition),
                    "target", PmxUtil.Vec3(view.CameraTarget),
                    "up", PmxUtil.Vec3(view.CameraUpVector),
                    "rotateCenter", PmxUtil.Vec3(view.CameraRotateCenter));
            });
        }

        private static object SetCamera(Editor editor, Dictionary<string, object> args)
        {
            editor.RequireWrite();

            float[] position = Json.Floats(args, "position", 3);
            float[] target = Json.Floats(args, "target", 3);
            float[] up = Json.Floats(args, "up", 3);

            if (position == null || target == null)
            {
                throw new McpToolException("position and target are both required");
            }

            return editor.Ui<object>(delegate
            {
                IPXPmxViewConnector view = editor.Connector.View.PmxView;

                V3 upVector = up != null
                    ? new V3(up[0], up[1], up[2])
                    : new V3(view.CameraUpVector);

                // SetCameraView takes (target, position, upVector).
                view.SetCameraView(
                    new V3(target[0], target[1], target[2]),
                    new V3(position[0], position[1], position[2]),
                    upVector);
                view.UpdateView();

                return Json.Obj(
                    "position", PmxUtil.Vec3(view.CameraPosition),
                    "target", PmxUtil.Vec3(view.CameraTarget),
                    "up", PmxUtil.Vec3(view.CameraUpVector));
            });
        }
    }
}
