// Snowglobe – leichter Schneefall für den Windows-Desktop.
// Ein transparentes, durchklickbares Overlay pro Monitor. Schnee bleibt auf
// Fensteroberkanten, Desktop-Icons und am Bildschirmboden liegen. Dazu eine
// Schaufel und ein Schneeroboter zum Räumen.
// Build: build.cmd (nutzt den in Windows enthaltenen C#-Compiler, kein SDK nötig)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("Snowglobe")]
[assembly: System.Reflection.AssemblyProduct("Snowglobe")]
[assembly: System.Reflection.AssemblyVersion("1.1.0.0")]

namespace Snowglobe
{
    // ------------------------------------------------------------------ Win32

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public int biSize, biWidth, biHeight;
        public short biPlanes, biBitCount;
        public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)] struct MSLLHOOKSTRUCT { public POINT pt; public uint mouseData, flags, time; public IntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public POINT pt; }

    static class N
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr v);
        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr sec, uint off);
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref POINT pt, ref SIZE sz, IntPtr src, ref POINT ptSrc, int key, ref BLENDFUNCTION bf, int flags);
        [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr h, out uint key, out byte alpha, out uint flags);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr h, uint cmd);
        public delegate bool EnumProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int sz);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out int v, int sz);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr p, IntPtr after, string c, string t);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint acc, bool inh, uint pid);
        [DllImport("kernel32.dll")] public static extern IntPtr VirtualAllocEx(IntPtr p, IntPtr a, UIntPtr sz, uint t, uint prot);
        [DllImport("kernel32.dll")] public static extern bool VirtualFreeEx(IntPtr p, IntPtr a, UIntPtr sz, uint t);
        [DllImport("kernel32.dll")] public static extern bool ReadProcessMemory(IntPtr p, IntPtr a, out RECT r, IntPtr sz, out IntPtr read);
        [DllImport("kernel32.dll")] public static extern bool WriteProcessMemory(IntPtr p, IntPtr a, ref RECT r, IntPtr sz, out IntPtr written);
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr SendMessageTimeout(IntPtr h, uint msg, IntPtr w, IntPtr l, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll")] public static extern int MapWindowPoints(IntPtr from, IntPtr to, ref RECT r, int n);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint a);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr mon, int type, out uint dx, out uint dy);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr h, uint flags);
        public delegate IntPtr LLProc(int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, LLProc fn, IntPtr mod, uint tid);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern int GetMessage(out MSG m, IntPtr h, uint min, uint max);
        [DllImport("user32.dll")] public static extern bool PostThreadMessage(uint tid, uint msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

        public static string ClassOf(IntPtr h)
        {
            var sb = new StringBuilder(128);
            GetClassName(h, sb, sb.Capacity);
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------ Overlay

    // Transparentes, durchklickbares Fenster mit eigenem 32-bit-Puffer (premultiplied ARGB).
    unsafe sealed class Overlay : NativeWindow, IDisposable
    {
        public readonly int X, Y, W, H;
        public readonly uint* Bits;
        IntPtr memDC, dib, oldObj;

        public Overlay(Rectangle b)
        {
            X = b.X; Y = b.Y; W = b.Width; H = b.Height;
            var cp = new CreateParams();
            cp.Caption = "Snowglobe";
            cp.X = X; cp.Y = Y; cp.Width = W; cp.Height = H;
            cp.Style = unchecked((int)0x80000000);                 // WS_POPUP
            cp.ExStyle = 0x00080000 | 0x00000020 | 0x00000080      // LAYERED | TRANSPARENT | TOOLWINDOW
                       | 0x00000008 | 0x08000000;                  // TOPMOST | NOACTIVATE
            CreateHandle(cp);

            var bi = new BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = W; bi.biHeight = -H; bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr screen = N.GetDC(IntPtr.Zero);
            memDC = N.CreateCompatibleDC(screen);
            IntPtr bits;
            dib = N.CreateDIBSection(screen, ref bi, 0, out bits, IntPtr.Zero, 0);
            N.ReleaseDC(IntPtr.Zero, screen);
            oldObj = N.SelectObject(memDC, dib);
            Bits = (uint*)bits;
        }

        public void SetExcludeFromCapture(bool on)
        {
            N.SetWindowDisplayAffinity(Handle, on ? 0x11u : 0u);
        }

        public void Present()
        {
            POINT dst = new POINT(); dst.X = X; dst.Y = Y;
            SIZE sz = new SIZE(); sz.cx = W; sz.cy = H;
            POINT src = new POINT();
            BLENDFUNCTION bf = new BLENDFUNCTION(); bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
            N.UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref sz, memDC, ref src, 0, ref bf, 2);
        }

        public void Clear()
        {
            long n = (long)W * H;
            for (long i = 0; i < n; i++) Bits[i] = 0;
        }

        public void SetShown(bool shown)
        {
            N.ShowWindow(Handle, shown ? 4 : 0); // SW_SHOWNOACTIVATE / SW_HIDE
        }

        const uint SWP = 0x0001 | 0x0002 | 0x0010; // NOSIZE|NOMOVE|NOACTIVATE

        public void KeepOnTop()
        {
            N.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, SWP);
        }

        // Direkt über dem Desktop einsortieren: hinter allen normalen Fenstern.
        public void PlaceAbove(IntPtr deskHost)
        {
            N.SetWindowPos(Handle, new IntPtr(-2), 0, 0, 0, 0, SWP);  // NOTOPMOST
            IntPtr prev = N.GetWindow(deskHost, 3);                   // GW_HWNDPREV
            if (prev != IntPtr.Zero && prev != Handle)
                N.SetWindowPos(Handle, prev, 0, 0, 0, 0, SWP);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0084) { m.Result = new IntPtr(-1); return; } // WM_NCHITTEST -> HTTRANSPARENT
            if (m.Msg == 0x0021) { m.Result = new IntPtr(3); return; }  // WM_MOUSEACTIVATE -> NOACTIVATE
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (memDC != IntPtr.Zero)
            {
                N.SelectObject(memDC, oldObj);
                N.DeleteObject(dib);
                N.DeleteDC(memDC);
                memDC = IntPtr.Zero;
            }
            DestroyHandle();
        }
    }

    // ------------------------------------------------------------ Sprites

    // Vorgerenderte, weiche Flocken (premultiplied ARGB, mit dezentem Schatten für helle
    // Hintergründe) in 20 Größen × 4×4 Subpixel-Positionen.
    static class Sprites
    {
        public const int Classes = 20;
        public const float RMin = 0.5f, RStep = 0.25f;
        public static uint[][] Data;
        public static int[] Size, Off;

        public static void Build()
        {
            Data = new uint[Classes * 16][];
            Size = new int[Classes];
            Off = new int[Classes];
            for (int c = 0; c < Classes; c++)
            {
                float r = RMin + c * RStep;
                int off = (int)Math.Ceiling(r + 3f);
                int S = off * 2 + 2;
                Size[c] = S; Off[c] = off;
                float shOff = 0.35f + r * 0.15f;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float cx = off + sx * 0.25f + 0.125f, cy = off + sy * 0.25f + 0.125f;
                    var a = new uint[S * S];
                    for (int py = 0; py < S; py++)
                    for (int px = 0; px < S; px++)
                    {
                        float fa = 0, sa = 0;
                        for (int j = 0; j < 3; j++)
                        for (int i = 0; i < 3; i++)
                        {
                            float x = px + (i + 0.5f) / 3f, y = py + (j + 0.5f) / 3f;
                            float dx = x - cx, dy = y - cy;
                            float d = (float)Math.Sqrt(dx * dx + dy * dy);
                            float core = Math.Max(0f, Math.Min(1f, r + 0.5f - d));
                            float glow = 0.22f * (float)Math.Exp(-(d * d) / (r * r * 2.0f + 1.0f));
                            fa += core + glow * (1f - core);
                            float ex = x - cx - shOff * 0.4f, ey = y - cy - shOff;
                            float ds = (float)Math.Sqrt(ex * ex + ey * ey);
                            sa += 0.30f * Math.Max(0f, Math.Min(1f, (r + 1.3f - ds) / 1.6f));
                        }
                        fa /= 9f; sa /= 9f;
                        float oa = fa + sa * (1f - fa);
                        float sw = sa * (1f - fa);
                        int A = (int)(oa * 255f + 0.5f);
                        int R = (int)((246f * fa + 60f * sw) + 0.5f);
                        int G = (int)((250f * fa + 68f * sw) + 0.5f);
                        int Bc = (int)((255f * fa + 88f * sw) + 0.5f);
                        if (A > 255) A = 255; if (R > A) R = A; if (G > A) G = A; if (Bc > A) Bc = A;
                        a[py * S + px] = ((uint)A << 24) | ((uint)R << 16) | ((uint)G << 8) | (uint)Bc;
                    }
                    Data[c * 16 + sy * 4 + sx] = a;
                }
            }
        }

        public static int ClassFor(float r)
        {
            int c = (int)Math.Round((r - RMin) / RStep);
            return c < 0 ? 0 : (c >= Classes ? Classes - 1 : c);
        }
    }

    // ------------------------------------------------------------ Art (Roboter, Schaufel)

    // Mit GDI+ gezeichnete Grafik als premultiplied-Pixel; (AX, AY) ist der Ankerpunkt.
    sealed class Art
    {
        public uint[] Px;
        public int W, H, AX, AY;
        public float LX, LY; // Position der Antennen-Lampe (nur Roboter)

        public delegate void Painter(Graphics g);

        public static Art Build(int w, int h, int ax, int ay, bool mirror, Painter paint)
        {
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.Clear(Color.Transparent);
                    if (mirror) { g.TranslateTransform(w, 0); g.ScaleTransform(-1, 1); }
                    paint(g);
                }
                var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                var tmp = new int[w * h];
                for (int y = 0; y < h; y++)
                    Marshal.Copy(new IntPtr(data.Scan0.ToInt64() + (long)y * data.Stride), tmp, y * w, w);
                bmp.UnlockBits(data);
                var px = new uint[w * h];
                Buffer.BlockCopy(tmp, 0, px, 0, tmp.Length * 4);
                var a = new Art();
                a.Px = px; a.W = w; a.H = h; a.AX = mirror ? w - ax : ax; a.AY = ay;
                return a;
            }
        }

        static GraphicsPath Round(float x, float y, float w, float h, float r)
        {
            var p = new GraphicsPath();
            float d = r * 2;
            p.AddArc(x, y, d, d, 180, 90);
            p.AddArc(x + w - d, y, d, d, 270, 90);
            p.AddArc(x + w - d, y + h - d, d, d, 0, 90);
            p.AddArc(x, y + h - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        // Roboter: [richtung*4 + schild*2 + frame], richtung 0 = rechts, 1 = links.
        public static Art[] Robot(float u, out float front, out float back)
        {
            int w = (int)Math.Ceiling(46 * u), h = (int)Math.Ceiling(35 * u);
            int ax = (int)(21 * u);
            front = 22 * u; back = 16 * u;
            var res = new Art[8];
            for (int dir = 0; dir < 2; dir++)
            for (int blade = 0; blade < 2; blade++)
            for (int frame = 0; frame < 2; frame++)
            {
                bool bl = blade == 1; int fr = frame;
                float hb = h - 0.5f;
                var a = Build(w, h, ax, h, dir == 1, delegate (Graphics g)
                {
                    // Raupenketten
                    using (var p = Round(6 * u, hb - 8 * u, 26 * u, 8 * u, 3.8f * u))
                    using (var b = new SolidBrush(Color.FromArgb(255, 46, 50, 60))) g.FillPath(b, p);
                    using (var pen = new Pen(Color.FromArgb(255, 90, 96, 110), 1.1f * u))
                        for (int k = 0; k < 7; k++)
                        {
                            float x = 8 * u + ((k * 4 + fr * 2) % 26) * u;
                            if (x < 30 * u) g.DrawLine(pen, x, hb - 7.6f * u, x, hb - 6.6f * u);
                        }
                    using (var b = new SolidBrush(Color.FromArgb(255, 150, 156, 168)))
                        for (int k = 0; k < 3; k++) g.FillEllipse(b, (10 + k * 8) * u - 2.6f * u, hb - 4 * u - 2.6f * u, 5.2f * u, 5.2f * u);
                    using (var b = new SolidBrush(Color.FromArgb(255, 60, 64, 74)))
                        for (int k = 0; k < 3; k++) g.FillEllipse(b, (10 + k * 8) * u - 1f * u, hb - 4 * u - 1f * u, 2f * u, 2f * u);
                    // Körper
                    var body = new RectangleF(5 * u, hb - 19 * u, 28 * u, 12 * u);
                    using (var p = Round(body.X, body.Y, body.Width, body.Height, 3 * u))
                    using (var b = new LinearGradientBrush(body, Color.FromArgb(255, 255, 162, 52), Color.FromArgb(255, 222, 102, 22), 90f))
                        g.FillPath(b, p);
                    using (var b = new SolidBrush(Color.FromArgb(255, 60, 60, 66))) g.FillRectangle(b, 6 * u, hb - 11.5f * u, 26 * u, 1.6f * u);
                    using (var b = new SolidBrush(Color.FromArgb(255, 255, 214, 60)))
                        for (int k = 0; k < 6; k++) g.FillRectangle(b, (7 + k * 4.2f) * u, hb - 11.5f * u, 2f * u, 1.6f * u);
                    // Kopf mit Visier
                    using (var p = Round(11 * u, hb - 28 * u, 16 * u, 10 * u, 3 * u))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(255, 238, 242, 248))) g.FillPath(b, p);
                        using (var pen = new Pen(Color.FromArgb(255, 170, 180, 198), 0.8f * u)) g.DrawPath(pen, p);
                    }
                    using (var p = Round(13 * u, hb - 26 * u, 13 * u, 5 * u, 2.2f * u))
                    using (var b = new SolidBrush(Color.FromArgb(255, 24, 34, 64))) g.FillPath(b, p);
                    using (var b = new SolidBrush(Color.FromArgb(255, 96, 236, 255)))
                    {
                        g.FillEllipse(b, 18 * u - 1.3f * u, hb - 23.5f * u - 1.3f * u, 2.6f * u, 2.6f * u);
                        g.FillEllipse(b, 23 * u - 1.3f * u, hb - 23.5f * u - 1.3f * u, 2.6f * u, 2.6f * u);
                    }
                    // Schneemütze
                    using (var b = new SolidBrush(Color.FromArgb(255, 252, 253, 255)))
                        g.FillEllipse(b, 12 * u, hb - 29.6f * u, 14 * u, 3.4f * u);
                    // Antenne
                    using (var pen = new Pen(Color.FromArgb(255, 120, 126, 140), 1.1f * u))
                        g.DrawLine(pen, 15 * u, hb - 29 * u, 13 * u, hb - 32.3f * u);
                    // Schild
                    float lift = bl ? 0 : 6 * u;
                    using (var pen = new Pen(Color.FromArgb(255, 70, 74, 84), 2 * u))
                        g.DrawLine(pen, 32 * u, hb - 12 * u, 37 * u, hb - 8 * u - lift);
                    var pts = new PointF[] {
                        new PointF(36 * u, hb - 15 * u - lift), new PointF(40.5f * u, hb - 16.5f * u - lift),
                        new PointF(44 * u, hb - lift), new PointF(36 * u, hb - lift) };
                    using (var b = new LinearGradientBrush(new RectangleF(36 * u, hb - 17 * u - lift, 8 * u, 17 * u),
                        Color.FromArgb(255, 210, 218, 230), Color.FromArgb(255, 140, 150, 166), 0f))
                        g.FillPolygon(b, pts);
                    using (var pen = new Pen(Color.FromArgb(255, 96, 104, 120), 0.8f * u)) g.DrawPolygon(pen, pts);
                });
                a.LX = dir == 1 ? w - 13 * u : 13 * u;
                a.LY = hb - 32.6f * u;
                res[dir * 4 + blade * 2 + frame] = a;
            }
            return res;
        }

        // Schaufel: [0] schiebt nach rechts, [1] nach links. Anker = Mitte der Schaufelkante.
        public static Art[] Shovel(float u)
        {
            int w = (int)Math.Ceiling(46 * u), h = (int)Math.Ceiling(46 * u);
            int ax = (int)(22 * u);
            var res = new Art[2];
            for (int dir = 0; dir < 2; dir++)
            {
                float hb = h - 0.5f;
                res[dir] = Build(w, h, ax, h, dir == 1, delegate (Graphics g)
                {
                    // Stiel
                    using (var pen = new Pen(Color.FromArgb(255, 150, 98, 52), 2.8f * u))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 18 * u, hb - 4 * u, 6 * u, hb - 36 * u);
                    }
                    using (var pen = new Pen(Color.FromArgb(255, 196, 140, 84), 1f * u))
                        g.DrawLine(pen, 17.4f * u, hb - 5 * u, 5.6f * u, hb - 35 * u);
                    // D-Griff
                    using (var pen = new Pen(Color.FromArgb(255, 40, 40, 46), 1.8f * u))
                    {
                        g.DrawLine(pen, 2 * u, hb - 37 * u, 10 * u, hb - 37 * u);
                        g.DrawLine(pen, 2 * u, hb - 37 * u, 4 * u, hb - 42 * u);
                        g.DrawLine(pen, 10 * u, hb - 37 * u, 8 * u, hb - 42 * u);
                        g.DrawLine(pen, 4 * u, hb - 42 * u, 8 * u, hb - 42 * u);
                    }
                    // Blatt (Boden + Stirnwand)
                    var pan = new PointF[] { new PointF(11 * u, hb - 2.4f * u), new PointF(32 * u, hb - 2.4f * u), new PointF(33 * u, hb), new PointF(12 * u, hb) };
                    var wall = new PointF[] { new PointF(29.5f * u, hb - 13 * u), new PointF(33 * u, hb - 14.5f * u), new PointF(34 * u, hb), new PointF(31 * u, hb) };
                    using (var b = new SolidBrush(Color.FromArgb(255, 70, 132, 210))) { g.FillPolygon(b, pan); g.FillPolygon(b, wall); }
                    using (var pen = new Pen(Color.FromArgb(255, 30, 70, 130), 0.8f * u)) { g.DrawPolygon(pen, pan); g.DrawPolygon(pen, wall); }
                    using (var pen = new Pen(Color.FromArgb(255, 200, 206, 216), 1.2f * u)) g.DrawLine(pen, 31.2f * u, hb - 0.4f * u, 33.6f * u, hb - 0.4f * u);
                });
            }
            return res;
        }

        // Zielmarkierung für „Roboter hinschicken".
        public static Art Marker(float u)
        {
            int s = (int)Math.Ceiling(22 * u);
            return Build(s, s, s / 2, s / 2, false, delegate (Graphics g)
            {
                float c = s / 2f;
                using (var pen = new Pen(Color.FromArgb(230, 20, 30, 50), 3.2f * u)) g.DrawEllipse(pen, c - 7 * u, c - 7 * u, 14 * u, 14 * u);
                using (var pen = new Pen(Color.FromArgb(255, 255, 170, 50), 1.8f * u))
                {
                    g.DrawEllipse(pen, c - 7 * u, c - 7 * u, 14 * u, 14 * u);
                    g.DrawLine(pen, c, c - 10 * u, c, c - 4 * u);
                    g.DrawLine(pen, c, c + 4 * u, c, c + 10 * u);
                    g.DrawLine(pen, c - 10 * u, c, c - 4 * u, c);
                    g.DrawLine(pen, c + 4 * u, c, c + 10 * u, c);
                }
            });
        }

        public static Art Lamp(float u, Color col)
        {
            int s = (int)Math.Ceiling(7 * u) | 1;
            return Build(s, s, s / 2, s / 2, false, delegate (Graphics g)
            {
                float c = s / 2f;
                using (var b = new SolidBrush(Color.FromArgb(90, col))) g.FillEllipse(b, c - 3.3f * u, c - 3.3f * u, 6.6f * u, 6.6f * u);
                using (var b = new SolidBrush(col)) g.FillEllipse(b, c - 1.5f * u, c - 1.5f * u, 3f * u, 3f * u);
            });
        }
    }

    // ------------------------------------------------------------ Simulation

    struct Flake { public float X, Y, Vy, R, Phase, Freq, Amp, Alpha, Drift; public int Cls; }
    struct Debris { public float X, Y, Vx, Vy, R, Alpha; public int Cls; }
    struct WinInfo { public IntPtr H; public RECT R; public bool Surface, Big; }

    // Eine horizontale Kante, auf der Schnee liegen bleibt (Fenster, Icon, Boden).
    sealed class Surface
    {
        public readonly long Key;
        public readonly int Kind, X0, X1, Y, N;     // Kind: 0 Boden, 1 Fenster, 2 Icon
        public readonly float MaxH;
        public readonly float[] H, Cap, Cap2, ClipY;
        public readonly bool[] Vis;
        public readonly int[] Cover;
        public int Z;
        public bool Seen;
        public float Peak;

        public Surface(long key, int kind, int x0, int x1, int y, float maxH, bool taper)
        {
            Key = key; Kind = kind; X0 = x0; X1 = x1; Y = y; MaxH = maxH; N = x1 - x0;
            H = new float[N]; Cap = new float[N]; Cap2 = new float[N]; ClipY = new float[N];
            Vis = new bool[N]; Cover = new int[N];
            float max2 = maxH * 2.5f;
            for (int j = 0; j < N; j++)
            {
                float edge = Math.Min(j, N - 1 - j) + 1;
                Cap[j] = taper ? Math.Min(maxH, edge * 0.45f) : maxH;
                Cap2[j] = taper ? Math.Min(max2, edge * 0.6f) : max2;   // Grenze für geschobenen Schnee
                Vis[j] = true;
                ClipY[j] = -1e9f;
                Cover[j] = -1;
            }
        }

        public float Top(float x)
        {
            int j = (int)x - X0;
            if (j < 0) j = 0; else if (j >= N) j = N - 1;
            return Y - H[j];
        }

        public float Sum(int from, int to)
        {
            float s = 0;
            for (int j = Math.Max(0, from); j < Math.Min(N, to); j++) s += H[j];
            return s;
        }

        public void Deposit(int i, float area)
        {
            int spread = Math.Max(3, (int)(Math.Sqrt(area) * 2.2));
            float sum = 0;
            for (int k = -spread; k <= spread; k++)
            {
                int j = i + k;
                if (j >= 0 && j < N) sum += Bump(k, spread);
            }
            if (sum <= 0) return;
            for (int k = -spread; k <= spread; k++)
            {
                int j = i + k;
                if (j < 0 || j >= N || H[j] >= Cap[j]) continue;
                float v = H[j] + area * Bump(k, spread) / sum;
                H[j] = v > Cap[j] ? Cap[j] : v;
            }
            if (H[i] > Peak) Peak = H[i];
        }

        static float Bump(int k, int spread)
        {
            float t = (float)k / (spread + 1);
            return 0.5f + 0.5f * (float)Math.Cos(t * Math.PI);
        }

        // Böschungswinkel: zu steile Stufen rutschen ab; dazu langsames Tauen.
        public void Step(float dt, float melt, bool flip)
        {
            if (Peak <= 0f) return;
            // geschobene Haufen (höher als natürlicher Schnee) rutschen schneller ab
            int passes = Peak > MaxH * 1.05f ? 4 : 1;
            for (int p = 0; p < passes; p++)
            {
                if (flip ^ (p & 1) == 1) { for (int j = 0; j < N - 1; j++) Relax(j, j + 1); }
                else { for (int j = N - 2; j >= 0; j--) Relax(j + 1, j); }
            }
            float m = melt * dt, peak = 0f;
            for (int j = 0; j < N; j++)
            {
                float v = H[j] - m;
                if (v < 0f) v = 0f;
                H[j] = v;
                if (v > peak) peak = v;
            }
            Peak = peak;
        }

        void Relax(int a, int b)
        {
            const float lim = 0.35f;
            float d = H[a] - H[b];
            if (d > lim) Move(a, b, (d - lim) * 0.45f);
            else if (d < -lim) Move(b, a, (-d - lim) * 0.45f);
        }

        void Move(int from, int to, float m)
        {
            float room = Cap2[to] - H[to];
            if (m > room) m = room;
            if (m <= 0) return;
            H[from] -= m; H[to] += m;
        }
    }

    sealed class Robot
    {
        public const int Idle = 0, Travel = 1, Push = 2, Hop = 3, Fall = 4;
        public Surface Surf;
        public float X, Yb, Load, Vy, IdleT, Anim, Blink;
        public int Dir = 1, State = Fall;
        public bool Blade;
        public float TargetX, ParkX = float.NaN;
        public bool PushAfter, Spill;
        public Run Cur;
        public readonly List<Run> Runs = new List<Run>();
        public float H0x, H0y, H1x, H1y, HT, HDur, HArc;
        public Surface HopSurf;
        public bool CleanAfter;
        public float CleanHeap = float.NaN;
        public bool PendingClean;
        public float PendingHeap = float.NaN;
        public bool Busy { get { return State == Travel || State == Push || State == Hop || Runs.Count > 0; } }
    }

    struct Run { public float Start, End; public int Dir; }

    // Ein Monitor: Overlay, Flocken, Schneehaufen, ggf. Roboter und Schaufel.
    unsafe sealed class World : IDisposable
    {
        public static float DepositBoost = 1f;
        public static bool RobotAuto;

        public readonly Rectangle B, Work;
        public readonly float S;
        public readonly Overlay Ov;
        public bool Hidden, Covered;
        public Robot Bot;
        public bool ShovelOn, MarkerOn;
        float shX, shY, shLoad, mkX, mkY;
        int shDir = 1;

        readonly int W, Hh;
        Flake[] fl = new Flake[0];
        int nfl;
        readonly Debris[] db = new Debris[8000];
        int ndb;
        readonly List<Surface> surfs = new List<Surface>();
        readonly Dictionary<long, Surface> map = new Dictionary<long, Surface>();
        readonly Surface ground;
        readonly Random rnd;
        List<Rectangle> dirty = new List<Rectangle>(2048), dirtyPrev = new List<Rectangle>(2048);
        bool groundFade, flip;
        float time;

        readonly Art[] robotArt, shovelArt;
        readonly Art markerArt, lampRed, lampAmber;
        readonly float RF, RB;
        public readonly float GroundHeapX;

        public World(Rectangle bounds, Rectangle work, float scale, int seed)
        {
            B = bounds; Work = work; S = scale;
            W = B.Width; Hh = B.Height;
            rnd = new Random(seed);
            Ov = new Overlay(B);
            int gy = Math.Min(Hh, Work.Bottom - B.Top);
            ground = new Surface(-1, 0, 0, W, gy, 26f * S, false);
            surfs.Add(ground);
            robotArt = Art.Robot(S, out RF, out RB);
            shovelArt = Art.Shovel(S);
            markerArt = Art.Marker(S);
            lampRed = Art.Lamp(S, Color.FromArgb(255, 255, 60, 50));
            lampAmber = Art.Lamp(S, Color.FromArgb(255, 255, 180, 30));
            GroundHeapX = 90f * S;
        }

        float Rnd() { return (float)rnd.NextDouble(); }

        public void SetCount(int n)
        {
            var nf = new Flake[n];
            for (int i = 0; i < n; i++)
            {
                if (i < nfl) nf[i] = fl[i];
                else Spawn(ref nf[i], true);
            }
            fl = nf; nfl = n;
        }

        void Spawn(ref Flake f, bool anywhere)
        {
            float depth = (float)Math.Pow(Rnd(), 2.0);               // 0 = fern, 1 = nah
            f.R = (0.7f + 2.4f * depth) * S;
            f.Cls = Sprites.ClassFor(f.R);
            f.X = Rnd() * (W + 120) - 60;
            f.Y = anywhere ? Rnd() * Hh : -f.R * 3f - Rnd() * 40f;
            f.Vy = (20f + 45f * depth) * S * (0.85f + 0.3f * Rnd());
            f.Amp = (5f + 14f * Rnd()) * S;
            f.Freq = 0.35f + Rnd() * 0.9f;
            f.Phase = Rnd() * 6.2832f;
            f.Alpha = (0.45f + 0.55f * depth) * (0.8f + 0.2f * Rnd());
            f.Drift = 0.5f + 0.8f * depth;
        }

        bool TryLand(float x, float yPrev, float y, float area)
        {
            int xi = (int)x;
            if (xi < 0 || xi >= W) return false;
            Surface best = null;
            float bestTop = float.MaxValue;
            int bi = 0;
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                if (xi < s.X0 || xi >= s.X1) continue;
                int i = xi - s.X0;
                if (!s.Vis[i]) continue;
                float top = s.Y - s.H[i];
                if (yPrev <= top && y >= top && top < bestTop && top >= s.ClipY[i])
                {
                    best = s; bestTop = top; bi = i;
                }
            }
            if (best == null) return false;
            best.Deposit(bi, area);
            return true;
        }

        public void Step(float dt, float t)
        {
            time = t;
            float wind = S * (float)(11 * Math.Sin(t * 0.07) + 6 * Math.Sin(t * 0.19 + 1.3) + 3 * Math.Sin(t * 0.43 + 2.1));

            for (int i = 0; i < nfl; i++)
            {
                Flake f = fl[i];
                float yPrev = f.Y;
                f.Y += f.Vy * dt;
                f.X += (wind * f.Drift + f.Amp * (float)Math.Sin(t * f.Freq + f.Phase)) * dt;
                if (f.X < -60) f.X += W + 120; else if (f.X > W + 60) f.X -= W + 120;
                float area = (2.6f * f.R * f.R + 0.6f * S) * DepositBoost;
                if (TryLand(f.X, yPrev, f.Y, area) || f.Y > Hh + 10) Spawn(ref f, false);
                fl[i] = f;
            }

            float g = 520f * S, term = 280f * S;
            for (int i = 0; i < ndb; )
            {
                Debris d = db[i];
                float yPrev = d.Y;
                d.Vy = Math.Min(term, d.Vy + g * dt);
                d.Vx *= 1f - 1.5f * dt;
                d.X += d.Vx * dt;
                d.Y += d.Vy * dt;
                bool dead = d.Y > Hh + 10 || d.X < -20 || d.X > W + 20
                         || TryLand(d.X, yPrev, d.Y, 2.2f * d.R * d.R);
                if (dead) { db[i] = db[--ndb]; continue; }
                db[i] = d;
                i++;
            }

            if (Bot != null) StepRobot(dt);

            flip = !flip;
            float melt = 0.006f * S;
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                s.Step(dt, s == ground && groundFade ? 25f * S : melt, flip);
            }
            if (groundFade && ground.Peak <= 0f) groundFade = false;
        }

        // ---- Schnee bewegen

        // Schnee oberhalb der Höhe h0 im Spaltenbereich [wx0, wx1) aufnehmen; was über
        // "keep" hinausgeht, wird vor dem Schild in Richtung dir abgelegt (Schneepflug).
        void Plow(Surface s, float wx0, float wx1, float h0, int dir, ref float load, float keep)
        {
            int c0 = Math.Max(0, (int)wx0 - s.X0), c1 = Math.Min(s.N, (int)wx1 - s.X0);
            for (int j = c0; j < c1; j++)
            {
                float cut = s.H[j] - h0;
                if (cut > 0) { s.H[j] = h0; load += cut; }
            }
            if (dir == 0 || load <= keep) return;
            float amt = load - keep;
            int f = dir > 0 ? c1 : c0 - 1;
            int w = Math.Max(3, Math.Min(40, (int)Math.Sqrt(amt * 2.5f) + 2));
            float sum = w * (w + 1) / 2f, left = 0, spill = 0;
            for (int k = 0; k < w; k++)
            {
                int j = f + k * dir;
                float part = amt * (w - k) / sum;
                if (j < 0 || j >= s.N) { spill += part; continue; }
                float room = Math.Max(0f, s.Cap2[j] - s.H[j]);
                float add = Math.Min(room, part);
                s.H[j] += add;
                if (s.H[j] > s.Peak) s.Peak = s.H[j];
                left += part - add;
            }
            load = keep + left;
            if (spill > 0)
            {
                float ex = dir > 0 ? s.X1 : s.X0 - 1;
                Spill(ex, s.Y - 3 * S, spill, dir * 45f * S, -20f * S);
            }
        }

        // Restladung von innen nach außen um eine Spalte herum aufschütten (bis Cap2).
        void Fill(Surface s, float wx, ref float load)
        {
            int c = Math.Max(0, Math.Min(s.N - 1, (int)wx - s.X0));
            for (int r = 0; r < s.N && load > 0.01f; r++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (r == 0 && side > 0) continue;
                    int j = c + side * r;
                    if (j < 0 || j >= s.N) continue;
                    float add = Math.Min(load, Math.Max(0f, s.Cap2[j] - s.H[j]) * 0.5f);
                    s.H[j] += add; load -= add;
                    if (s.H[j] > s.Peak) s.Peak = s.H[j];
                }
            }
        }

        // Schnee als herabfallende Brocken freisetzen.
        void Spill(float x, float y, float amount, float vx, float vy)
        {
            int n = Math.Min(900, (int)(amount / (3.4f * S * S)) + 1);
            for (int i = 0; i < n && ndb < db.Length; i++)
            {
                Debris d;
                d.X = x + (Rnd() - 0.5f) * 8f * S;
                d.Y = y - Rnd() * 6f * S;
                d.Vx = vx + (Rnd() - 0.5f) * 40f * S;
                d.Vy = vy + (Rnd() - 0.6f) * 40f * S;
                d.R = (0.8f + Rnd() * 0.9f) * S;
                d.Alpha = 0.85f + 0.15f * Rnd();
                d.Cls = Sprites.ClassFor(d.R);
                db[ndb++] = d;
            }
        }

        // ---- Schaufel

        public void ShovelStep(float x, float y, float px, float py, bool down, ref float load)
        {
            ShovelOn = true; shX = x; shY = y;
            if (x - px > 0.5f) shDir = 1; else if (x - px < -0.5f) shDir = -1;
            if (down)
            {
                float dist = Math.Max(Math.Abs(x - px), Math.Abs(y - py));
                int steps = Math.Max(1, (int)(dist / 3f));
                int sdx = x > px + 0.3f ? 1 : (x < px - 0.3f ? -1 : 0);
                float bw = 10f * S, cap = 320f * S * S;
                for (int i = 1; i <= steps; i++)
                {
                    float qx = px + (x - px) * i / steps, qy = py + (y - py) * i / steps;
                    for (int k = 0; k < surfs.Count; k++)
                    {
                        var s = surfs[k];
                        int col = (int)qx - s.X0;
                        if (col < 0 || col >= s.N || !s.Vis[col]) continue;
                        float top = s.Y - s.H[col];
                        if (qy < top - 1f || qy > s.Y + 8f * S) continue;
                        float h0 = s.Y - qy;
                        if (h0 < 3f * S) h0 = 0f;   // Blatt am Boden: sauber bis unten räumen
                        Plow(s, qx - bw, qx + bw, h0, sdx, ref load, cap);
                    }
                }
            }
            shLoad = load;
        }

        public void DropLoad(float x, float y, ref float load, float vx, float vy)
        {
            if (load > 0.5f) Spill(x, y - 3f * S, load, vx, vy);
            load = 0; shLoad = 0;
        }

        public void SetMarker(float x, float y) { MarkerOn = true; mkX = x; mkY = y; }

        // ---- Roboter

        bool RobotSurface(Surface s)
        {
            return s == ground || (s.Kind == 1 && s.N >= (RF + RB) * 3);
        }

        public void RobotEnter(Robot r, float x, bool clean, float heap)
        {
            Bot = r;
            r.Surf = null; r.State = Robot.Fall; r.Vy = 0;
            r.X = Math.Max(RB + 2, Math.Min(W - RF - 2, x));
            r.Yb = -2f;
            r.Runs.Clear(); r.Load = 0; r.Blade = false;
            r.PendingClean = clean; r.PendingHeap = heap;
        }

        public void RobotPlace(Robot r)
        {
            Bot = r;
            r.Surf = ground; r.State = Robot.Idle;
            r.X = W * 0.18f; r.Yb = ground.Y;
        }

        public bool RobotVisible()
        {
            var b = Bot;
            if (b == null) return false;
            if (b.Surf == null || b.State == Robot.Hop || b.State == Robot.Fall) return true;
            int j = (int)b.X - b.Surf.X0;
            return j < 0 || j >= b.Surf.N || b.Surf.Vis[j];
        }

        public Rectangle RobotRect()
        {
            var b = Bot;
            var a = robotArt[0];
            int m = (int)(4 * S);
            return new Rectangle(B.X + (int)b.X - a.AX - m, B.Y + (int)b.Yb - a.H - m, a.W + 2 * m, a.H + 2 * m);
        }

        public void Poke()
        {
            var b = Bot;
            if (b == null || b.Surf == null || b.State == Robot.Hop || b.State == Robot.Fall) return;
            HopTo(b.Surf, b.X, true, b.Surf == ground ? b.X : float.NaN);
        }

        // Roboter zu einer angeklickten Stelle schicken; dort entsteht der Haufen.
        public void SendTo(float x, float y)
        {
            var b = Bot;
            Surface best = null;
            float bd = float.MaxValue;
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                if (!RobotSurface(s)) continue;
                int col = (int)x - s.X0;
                if (col < 0 || col >= s.N || !s.Vis[col]) continue;
                float top = s.Y - s.H[col];
                if (y < top - 50f * S || y > s.Y + 30f * S) continue;
                float d = Math.Abs(y - top);
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null) best = ground;
            if (b.State == Robot.Fall || b.Surf == null) { b.PendingClean = true; b.PendingHeap = x; return; }
            HopTo(best, x, true, x);
        }

        void HopTo(Surface s, float x, bool clean, float heap)
        {
            var b = Bot;
            if (b.Load > 0.5f) { Spill(b.X + b.Dir * RF, b.Yb - 3 * S, b.Load, 0, 0); b.Load = 0; }
            b.H0x = b.X; b.H0y = b.Yb;
            b.H1x = Math.Max(s.X0 + RB + 1, Math.Min(s.X1 - RF - 1, x));
            b.H1y = s.Top(b.H1x);
            float dist = Math.Abs(b.H1x - b.H0x) + Math.Abs(b.H1y - b.H0y);
            b.HDur = 0.45f + dist / (1500f * S);
            b.HArc = 40f * S + Math.Max(0f, b.H0y - b.H1y) * 0.6f + dist * 0.05f;
            b.HT = 0;
            b.HopSurf = s;
            b.CleanAfter = clean; b.CleanHeap = heap;
            if (Math.Abs(b.H1x - b.H0x) > 1) b.Dir = b.H1x > b.H0x ? 1 : -1;
            b.State = Robot.Hop; b.Blade = false;
            b.Runs.Clear();
        }

        void StartClean(Surface s, float heap)
        {
            var b = Bot;
            b.Surf = s;
            b.Runs.Clear();
            b.Spill = float.IsNaN(heap);
            float lo = s.X0 + RB + 1, hi = s.X1 - RF - 1;
            b.ParkX = float.NaN;
            if (hi > lo)
            {
                if (b.Spill)
                {
                    var r = new Run();
                    if (b.X - s.X0 < s.X1 - b.X) { r.Start = hi; r.End = lo; r.Dir = -1; }
                    else { r.Start = lo; r.End = hi; r.Dir = 1; }
                    b.Runs.Add(r);
                }
                else
                {
                    float hx = Math.Max(s.X0, Math.Min(s.X1, heap));
                    var runs = new List<Run>();
                    int first = -1, last = -1;
                    for (int j = 0; j < s.N; j++)
                        if (s.H[j] > 0.6f) { if (first < 0) first = j; last = j; }
                    if (first >= 0 && s.X0 + first < hx - 6 * S)
                    {
                        var r = new Run();
                        r.Start = Math.Max(lo, s.X0 + first - RF);
                        r.End = Math.Min(hi, hx - RF);
                        r.Dir = 1;
                        if (r.End > r.Start + 3) runs.Add(r);
                    }
                    if (last >= 0 && s.X0 + last > hx + 6 * S)
                    {
                        var r = new Run();
                        r.Start = Math.Min(hi, s.X0 + last + RF);
                        r.End = Math.Max(lo, hx + RF);
                        r.Dir = -1;
                        if (r.Start > r.End + 3) runs.Add(r);
                    }
                    if (runs.Count == 2 && Math.Abs(runs[1].Start - b.X) < Math.Abs(runs[0].Start - b.X)) runs.Reverse();
                    b.Runs.AddRange(runs);
                    float park = hx - 70f * S;
                    if (park < lo) park = hx + 70f * S;
                    b.ParkX = Math.Max(lo, Math.Min(hi, park));
                }
            }
            NextRun();
        }

        void NextRun()
        {
            var b = Bot;
            if (b.Runs.Count > 0)
            {
                b.Cur = b.Runs[0];
                b.Runs.RemoveAt(0);
                b.TargetX = b.Cur.Start; b.PushAfter = true;
                b.State = Robot.Travel; b.Blade = false;
            }
            else if (!float.IsNaN(b.ParkX))
            {
                b.TargetX = b.ParkX; b.ParkX = float.NaN; b.PushAfter = false;
                b.State = Robot.Travel; b.Blade = false;
            }
            else
            {
                b.State = Robot.Idle; b.IdleT = 0; b.Blade = false;
            }
        }

        void StepRobot(float dt)
        {
            var b = Bot;
            b.Blink += dt;
            if (b.Surf != null && !surfs.Contains(b.Surf))
            {
                if (b.Load > 0.5f) { Spill(b.X, b.Yb - 4 * S, b.Load, 0, 0); b.Load = 0; }
                b.Surf = null; b.State = Robot.Fall; b.Vy = 0; b.Runs.Clear(); b.Blade = false;
            }
            float follow = Math.Min(1f, dt * 14f);
            switch (b.State)
            {
                case Robot.Idle:
                    b.IdleT += dt;
                    b.Yb += (b.Surf.Top(b.X) - b.Yb) * follow;
                    if (RobotAuto && b.IdleT > 5f) AutoJob();
                    break;

                case Robot.Travel:
                {
                    float d = b.TargetX - b.X, step = 115f * S * dt;
                    if (Math.Abs(d) > 0.5f) b.Dir = d > 0 ? 1 : -1;
                    if (Math.Abs(d) <= step)
                    {
                        b.X = b.TargetX;
                        if (b.PushAfter) { b.Dir = b.Cur.Dir; b.State = Robot.Push; b.Blade = true; }
                        else { b.State = Robot.Idle; b.IdleT = 0; }
                    }
                    else { b.X += Math.Sign(d) * step; b.Anim += step; }
                    b.Yb += (b.Surf.Top(b.X) - b.Yb) * follow;
                    break;
                }

                case Robot.Push:
                {
                    var s = b.Surf;
                    float step = 55f * S * dt;
                    b.X += b.Dir * step; b.Anim += step;
                    bool reached = b.Dir > 0 ? b.X >= b.Cur.End : b.X <= b.Cur.End;
                    if (reached) b.X = b.Cur.End;
                    float front = b.X + b.Dir * RF, back = b.X - b.Dir * RB;
                    Plow(s, Math.Min(front, back), Math.Max(front, back), 0f, b.Dir, ref b.Load, 0f);
                    b.Yb += (s.Y - b.Yb) * follow;
                    if (reached)
                    {
                        if (b.Spill)
                        {
                            // Rest über die Fensterkante schieben
                            float amt = b.Load;
                            int c0 = b.Dir > 0 ? (int)front - s.X0 : 0, c1 = b.Dir > 0 ? s.N : (int)front - s.X0;
                            for (int j = Math.Max(0, c0); j < Math.Min(s.N, c1); j++) { amt += s.H[j]; s.H[j] = 0; }
                            Spill(b.Dir > 0 ? s.X1 : s.X0 - 1, s.Y - 3 * S, amt, b.Dir * 50f * S, -15f * S);
                            b.Load = 0;
                        }
                        else if (b.Load > 0.01f) Fill(s, front + b.Dir * 4f * S, ref b.Load);
                        NextRun();
                    }
                    break;
                }

                case Robot.Hop:
                {
                    b.HT += dt / b.HDur;
                    float t = Math.Min(1f, b.HT);
                    b.X = b.H0x + (b.H1x - b.H0x) * t;
                    b.Yb = b.H0y + (b.HopSurf.Top(b.H1x) - b.H0y) * t - b.HArc * 4f * t * (1f - t);
                    if (t >= 1f)
                    {
                        if (!surfs.Contains(b.HopSurf)) { b.State = Robot.Fall; b.Vy = 0; b.Surf = null; break; }
                        b.Surf = b.HopSurf;
                        b.X = b.H1x;
                        if (b.CleanAfter) StartClean(b.Surf, b.CleanHeap);
                        else { b.State = Robot.Idle; b.IdleT = 0; }
                    }
                    break;
                }

                case Robot.Fall:
                {
                    float yPrev = b.Yb;
                    b.Vy = Math.Min(900f * S, b.Vy + 1400f * S * dt);
                    b.Yb += b.Vy * dt;
                    Surface landed = null;
                    float bt = float.MaxValue;
                    int xi = (int)b.X;
                    for (int k = 0; k < surfs.Count; k++)
                    {
                        var s = surfs[k];
                        if (!RobotSurface(s) || xi < s.X0 || xi >= s.X1 || !s.Vis[xi - s.X0]) continue;
                        float top = s.Y - s.H[xi - s.X0];
                        if (yPrev <= top + 1 && b.Yb >= top && top < bt) { bt = top; landed = s; }
                    }
                    if (landed == null && b.Yb > Hh + 40) { landed = ground; bt = ground.Top(b.X); }
                    if (landed != null)
                    {
                        b.Surf = landed;
                        b.Yb = bt;
                        b.X = Math.Max(landed.X0 + RB + 1, Math.Min(landed.X1 - RF - 1, b.X));
                        b.State = Robot.Idle; b.IdleT = 0;
                        if (b.PendingClean)
                        {
                            b.PendingClean = false;
                            float heap = b.PendingHeap;
                            if (float.IsNaN(heap)) heap = landed == ground ? b.X : float.NaN;
                            StartClean(landed, heap);
                        }
                    }
                    break;
                }
            }
        }

        // Automatik: die Fläche mit dem meisten Schnee räumen.
        void AutoJob()
        {
            var b = Bot;
            b.IdleT = 0;
            Surface best = null;
            float bestAmt = 0;
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                if (!RobotSurface(s) || !s.Vis[s.N / 2]) continue;
                float amt;
                if (s == ground)
                {
                    int hx = (int)GroundHeapX, r = (int)(160 * S);
                    amt = s.Sum(0, hx - r) + s.Sum(hx + r, s.N);
                }
                else amt = s.Sum(0, s.N);
                if (amt < 2.2f * S * s.N) continue;
                if (amt > bestAmt) { bestAmt = amt; best = s; }
            }
            if (best == null) return;
            float heap = best == ground ? GroundHeapX : float.NaN;
            if (best == b.Surf) StartClean(best, heap);
            else HopTo(best, (best.X0 + best.X1) / 2f, true, heap);
        }

        // ---- Oberflächen aus Fenster-/Icon-Liste aktualisieren

        public void UpdateSurfaces(List<WinInfo> wins, HashSet<IntPtr> fresh, List<RECT> icons, float iconTop, float iconPx)
        {
            // alte Sichtbarkeit merken, um neu verdeckten Schnee nach oben zu schieben
            var oldVis = new Dictionary<Surface, bool[]>();
            for (int k = 0; k < surfs.Count; k++)
                if (surfs[k].Peak > 0.3f) oldVis[surfs[k]] = (bool[])surfs[k].Vis.Clone();

            for (int k = 0; k < surfs.Count; k++) surfs[k].Seen = false;
            ground.Seen = true;
            int workBottom = Work.Bottom - B.Top;

            for (int i = 0; i < wins.Count; i++)
            {
                var w = wins[i];
                if (!w.Surface) continue;
                int y = w.R.Top - B.Top;
                int x0 = Math.Max(0, w.R.Left - B.Left + 1);
                int x1 = Math.Min(W, w.R.Right - B.Left - 1);
                if (y < 24 * S || y >= workBottom - 4 || x1 - x0 < 40) continue;
                Upsert(w.H.ToInt64(), 1, x0, x1, y, 14f * S, i, oldVis);
            }

            for (int i = 0; i < icons.Count; i++)
            {
                RECT r = icons[i];
                int cx = (r.Left + r.Right) / 2 - B.Left;
                int half = (int)(iconPx * 0.42f);
                int x0 = cx - half, x1 = cx + half;
                int y = r.Top - B.Top + (int)iconTop;
                if (x0 < 0 || x1 > W || y < 10 || y >= workBottom || x1 - x0 < 8) continue;
                long key = (2L << 56) ^ ((long)(r.Left & 0xFFFFF) << 32) ^ ((long)(r.Top & 0xFFFFF) << 12) ^ (r.Right & 0xFFF);
                Upsert(key, 2, x0, x1, y, 7f * S, int.MaxValue, oldVis);
            }

            for (int k = surfs.Count - 1; k >= 0; k--)
            {
                var s = surfs[k];
                if (s.Seen) continue;
                Release(s);
                surfs.RemoveAt(k);
                map.Remove(s.Key);
            }

            for (int k = 0; k < surfs.Count; k++) ComputeVis(surfs[k], wins);

            // Ein neu geöffnetes/bewegtes Fenster schiebt den Schnee, den es verdeckt, auf seine Oberkante.
            foreach (var kv in oldVis)
            {
                var s = kv.Key;
                if (!map.ContainsKey(s.Key) && s != ground) continue;
                bool[] old = kv.Value;
                if (old.Length != s.N) continue;
                for (int j = 0; j < s.N; j++)
                {
                    if (!old[j] || s.Vis[j] || s.H[j] < 0.3f) continue;
                    int ci = s.Cover[j];
                    if (ci < 0 || !fresh.Contains(wins[ci].H)) continue;
                    Surface t;
                    if (!map.TryGetValue(wins[ci].H.ToInt64(), out t) || t == s) continue;
                    int x = s.X0 + j, tc = x - t.X0;
                    float h = s.H[j];
                    s.H[j] = 0;
                    if (tc < 0 || tc >= t.N || !t.Vis[tc]) { Spill(x, wins[ci].R.Top - B.Top - 2, h, 0, -30 * S); continue; }
                    float add = Math.Min(h, Math.Max(0f, t.Cap2[tc] - t.H[tc]));
                    t.H[tc] += add;
                    if (t.H[tc] > t.Peak) t.Peak = t.H[tc];
                    if (h - add > 0.2f) Spill(x, t.Y - t.H[tc] - 1, h - add, 0, -20 * S);
                }
            }
        }

        void Upsert(long key, int kind, int x0, int x1, int y, float maxH, int z, Dictionary<Surface, bool[]> oldVis)
        {
            Surface s;
            if (map.TryGetValue(key, out s))
            {
                if (s.X0 == x0 && s.X1 == x1 && s.Y == y) { s.Seen = true; s.Z = z; return; }
                surfs.Remove(s);
                map.Remove(key);
                if (s.X0 == x0 && s.X1 == x1)
                {
                    // nur vertikal bewegt (z.B. Popup klappt auf): Schnee fährt mit
                    var n = new Surface(key, kind, x0, x1, y, maxH, true);
                    for (int j = 0; j < n.N; j++) n.H[j] = Math.Min(n.Cap2[j], s.H[j]);
                    n.Peak = s.Peak; n.Z = z; n.Seen = true;
                    bool[] ov;
                    if (oldVis.TryGetValue(s, out ov)) { oldVis.Remove(s); oldVis[n] = ov; }
                    surfs.Add(n); map[key] = n;
                    if (Bot != null && Bot.Surf == s) Bot.Surf = n;
                    if (Bot != null && Bot.HopSurf == s) Bot.HopSurf = n;
                    return;
                }
                Release(s);
                oldVis.Remove(s);
            }
            s = new Surface(key, kind, x0, x1, y, maxH, true);
            s.Z = z; s.Seen = true;
            surfs.Add(s);
            map[key] = s;
        }

        // Verdeckung durch Fenster, die in der Z-Reihenfolge darüber liegen.
        void ComputeVis(Surface s, List<WinInfo> wins)
        {
            for (int j = 0; j < s.N; j++) { s.Vis[j] = true; s.ClipY[j] = -1e9f; s.Cover[j] = -1; }
            bool isGround = s == ground;
            int lim = isGround ? wins.Count : Math.Min(s.Z, wins.Count);
            int line = s.Y - 1;
            for (int i = 0; i < lim; i++)
            {
                if (isGround && wins[i].Big) continue;   // maximierte Fenster verdecken den Boden-Schnee nicht
                RECT r = wins[i].R;
                int ox0 = r.Left - B.Left, ox1 = r.Right - B.Left, oy0 = r.Top - B.Top, oy1 = r.Bottom - B.Top;
                int a = Math.Max(ox0, s.X0), b = Math.Min(ox1, s.X1);
                if (a >= b) continue;
                if (oy0 <= line && oy1 > line)
                {
                    for (int x = a; x < b; x++)
                    {
                        int j = x - s.X0;
                        s.Vis[j] = false;
                        if (s.Cover[j] < 0) s.Cover[j] = i;
                    }
                }
                else if (oy1 <= line && oy1 > s.Y - s.MaxH * 2.5f - 2)
                {
                    for (int x = a; x < b; x++)
                        if (s.ClipY[x - s.X0] < oy1) s.ClipY[x - s.X0] = oy1;
                }
            }
        }

        // Schnee eines verschwundenen/bewegten Fensters fällt herunter.
        void Release(Surface s)
        {
            if (s.Peak <= 0.3f) return;
            for (int i = 0; i < s.N; i += 2)
            {
                if (!s.Vis[i]) continue;
                float h = s.H[i];
                int k = (int)(h / (2.2f * S) + Rnd());
                for (int j = 0; j < k && ndb < db.Length; j++)
                {
                    Debris d;
                    d.X = s.X0 + i + Rnd() * 2f;
                    d.Y = s.Y - Rnd() * h;
                    d.Vx = (Rnd() - 0.5f) * 40f * S;
                    d.Vy = Rnd() * 30f * S;
                    d.R = (0.8f + Rnd() * 0.9f) * S;
                    d.Alpha = 0.85f + 0.15f * Rnd();
                    d.Cls = Sprites.ClassFor(d.R);
                    db[ndb++] = d;
                }
            }
        }

        public void ShakeOff()
        {
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                if (s == ground) continue;
                Release(s);
                Array.Clear(s.H, 0, s.N);
                s.Peak = 0;
            }
            groundFade = true;
        }

        // ---- Rendering

        public void Render()
        {
            uint* bits = Ov.Bits;
            // vorherigen Frame nur dort löschen, wo gezeichnet wurde
            for (int k = 0; k < dirtyPrev.Count; k++)
            {
                Rectangle r = dirtyPrev[k];
                int x0 = Math.Max(0, r.X), x1 = Math.Min(W, r.Right), y0 = Math.Max(0, r.Y), y1 = Math.Min(Hh, r.Bottom);
                for (int y = y0; y < y1; y++)
                {
                    uint* p = bits + (long)y * W + x0;
                    for (int x = x0; x < x1; x++) *p++ = 0;
                }
            }
            var tmp = dirtyPrev; dirtyPrev = dirty; dirty = tmp; dirty.Clear();

            for (int k = 0; k < surfs.Count; k++) DrawPile(bits, surfs[k]);
            for (int i = 0; i < ndb; i++) Blit(bits, db[i].X, db[i].Y, db[i].Cls, db[i].Alpha);
            for (int i = 0; i < nfl; i++) Blit(bits, fl[i].X, fl[i].Y, fl[i].Cls, fl[i].Alpha);
            if (Bot != null && RobotVisible()) DrawRobot(bits);
            if (ShovelOn) DrawShovel(bits);
            if (MarkerOn) DrawArt(bits, markerArt, (int)mkX, (int)mkY);

            Ov.Present();
        }

        void DrawRobot(uint* bits)
        {
            var b = Bot;
            int frame = ((int)(b.Anim / (3f * S))) & 1;
            var a = robotArt[(b.Dir > 0 ? 0 : 4) + (b.Blade ? 2 : 0) + frame];
            int x = (int)Math.Round(b.X), y = (int)Math.Round(b.Yb);
            DrawArt(bits, a, x, y);
            bool busy = b.Busy;
            float period = busy ? 0.5f : 1.6f;
            if ((b.Blink % period) < period * 0.35f)
                DrawArt(bits, busy ? lampAmber : lampRed, x - a.AX + (int)a.LX, y - a.AY + (int)a.LY);
        }

        void DrawShovel(uint* bits)
        {
            var a = shovelArt[shDir > 0 ? 0 : 1];
            int x = (int)Math.Round(shX), y = (int)Math.Round(shY);
            DrawArt(bits, a, x, y);
            if (shLoad > 1f)
            {
                // Schnee auf dem Schaufelblatt
                float mh = Math.Min(11f * S, (float)Math.Sqrt(shLoad) * 0.7f);
                float hw = 9f * S;
                int cx = x + (int)(shDir * 0.5f * S), by = y - (int)(2.4f * S);
                int x0 = (int)(cx - hw), x1 = (int)(cx + hw);
                for (int px = x0; px <= x1; px++)
                {
                    if (px < 0 || px >= W) continue;
                    float t = (px - cx) / hw;
                    float h = mh * (float)Math.Sqrt(Math.Max(0f, 1f - t * t));
                    float top = by - h;
                    for (int py = (int)Math.Floor(top); py < by; py++)
                    {
                        if (py < 0 || py >= Hh) continue;
                        float cov = Math.Min(1f, py + 1 - top);
                        if (cov <= 0) continue;
                        int al = (int)(cov * 250);
                        int c = 250 - (int)(20f * (py - top) / Math.Max(1f, h));
                        bits[(long)py * W + px] = ((uint)al << 24) | ((uint)(c * al / 255) << 16) | ((uint)((c + 3) * al / 255) << 8) | (uint)(255 * al / 255);
                    }
                }
                dirty.Add(new Rectangle(x0, (int)(by - mh) - 2, x1 - x0 + 1, (int)mh + 3));
            }
        }

        void DrawArt(uint* bits, Art a, int x, int y)
        {
            Blend(bits, a.Px, a.W, a.H, x - a.AX, y - a.AY, 256);
        }

        void DrawPile(uint* bits, Surface s)
        {
            if (s.Peak < 0.15f) return;
            int maxY = Math.Min(Hh, s.Y);
            for (int i = 0; i < s.N; i++)
            {
                if (!s.Vis[i]) continue;
                float h = s.H[i];
                if (h < 0.15f) continue;
                int x = s.X0 + i;
                float top = s.Y - h;
                int y0 = (int)Math.Floor(top);
                int yc = (int)Math.Ceiling(s.ClipY[i]);
                if (y0 < yc) y0 = yc;
                if (y0 < 0) y0 = 0;
                uint hsh = (uint)x * 2654435761u;
                int tex = (int)((hsh >> 27) & 7) - 3;
                // weicher Schatten über der Oberkante: Kontur auf hellem Hintergrund
                int ys = (int)Math.Floor(top) - 1;
                if (ys >= 0 && ys >= yc && ys < maxY)
                {
                    float sa = Math.Min(1f, h) * 38f * (1f - Math.Max(0f, Math.Min(1f, ys + 1 - top)));
                    int a = (int)sa;
                    if (a > 0 && bits[(long)ys * W + x] == 0)
                        bits[(long)ys * W + x] = ((uint)a << 24) | ((uint)(70 * a / 255) << 16) | ((uint)(80 * a / 255) << 8) | (uint)(100 * a / 255);
                }
                for (int y = y0; y < maxY; y++)
                {
                    float cov = y + 1 - top;
                    if (cov <= 0f) continue;
                    if (cov > 1f) cov = 1f;
                    float d = (y + 0.5f - top) / Math.Max(h, 1f);
                    if (d < 0f) d = 0f; else if (d > 1f) d = 1f;
                    float sh = d * d;
                    int cr = 255 - (int)(44 * sh) + tex, cg = 255 - (int)(32 * sh) + tex, cb = 255 - (int)(12 * sh);
                    if (y == s.Y - 1 && h > 1.5f) { cr -= 18; cg -= 14; cb -= 6; }
                    if (cr > 255) cr = 255; if (cg > 255) cg = 255; if (cb > 255) cb = 255;
                    int al = (int)(cov * 245f);
                    bits[(long)y * W + x] = ((uint)al << 24) | ((uint)(cr * al / 255) << 16) | ((uint)(cg * al / 255) << 8) | (uint)(cb * al / 255);
                }
            }
            int ry = (int)(s.Y - s.Peak) - 2;
            dirty.Add(new Rectangle(s.X0, ry, s.N, s.Y - ry));
        }

        void Blit(uint* bits, float fx, float fy, int cls, float alpha)
        {
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            int sx = (int)((fx - ix) * 4f), sy = (int)((fy - iy) * 4f);
            if (sx > 3) sx = 3; if (sy > 3) sy = 3;
            int S2 = Sprites.Size[cls], off = Sprites.Off[cls];
            Blend(bits, Sprites.Data[cls * 16 + sy * 4 + sx], S2, S2, ix - off, iy - off, (int)(alpha * 256f));
        }

        // premultiplied "over"-Blending eines Sprites in den Puffer
        void Blend(uint* bits, uint[] spr, int sw, int sh, int dx, int dy, int al)
        {
            if (dx >= W || dy >= Hh || dx + sw <= 0 || dy + sh <= 0) return;
            int px0 = Math.Max(0, -dx), py0 = Math.Max(0, -dy);
            int px1 = Math.Min(sw, W - dx), py1 = Math.Min(sh, Hh - dy);
            for (int py = py0; py < py1; py++)
            {
                uint* row = bits + (long)(dy + py) * W + dx;
                int so = py * sw;
                for (int px = px0; px < px1; px++)
                {
                    uint s = spr[so + px];
                    int a = (int)(s >> 24) * al >> 8;
                    if (a == 0) continue;
                    int sr = (int)((s >> 16) & 255) * al >> 8, sg = (int)((s >> 8) & 255) * al >> 8, sb = (int)(s & 255) * al >> 8;
                    uint d = row[px];
                    if (d == 0 || a >= 255)
                    {
                        row[px] = ((uint)a << 24) | ((uint)sr << 16) | ((uint)sg << 8) | (uint)sb;
                    }
                    else
                    {
                        int ia = 255 - a;
                        uint na = (uint)(a + (((int)(d >> 24) * ia) >> 8));
                        uint nr = (uint)(sr + (((int)((d >> 16) & 255) * ia) >> 8));
                        uint ng = (uint)(sg + (((int)((d >> 8) & 255) * ia) >> 8));
                        uint nb = (uint)(sb + (((int)(d & 255) * ia) >> 8));
                        if (na > 255) na = 255;
                        if (nr > na) nr = na; if (ng > na) ng = na; if (nb > na) nb = na;
                        row[px] = (na << 24) | (nr << 16) | (ng << 8) | nb;
                    }
                }
            }
            dirty.Add(new Rectangle(dx, dy, sw, sh));
        }

        public void ClearAll()
        {
            Ov.Clear();
            Ov.Present();
            dirty.Clear(); dirtyPrev.Clear();
        }

        public void Dispose() { Ov.Dispose(); }

        // ---- Selbsttest (--selftest): prüft Popup-Schieben, Schaufel und Roboter ohne echte Fenster

        public static string SelfTest()
        {
            var sb = new StringBuilder();
            Sprites.Build();
            var w = new World(new Rectangle(0, 0, 1920, 1080), new Rectangle(0, 0, 1920, 1032), 1f, 1);
            var g = w.ground;
            var none = new List<WinInfo>();
            var icons = new List<RECT>();
            var fresh = new HashSet<IntPtr>();

            // 1) Popup klappt über Boden-Schnee auf
            for (int j = 1000; j < 1400; j++) g.H[j] = 10f;
            g.Peak = 10f;
            w.UpdateSurfaces(none, fresh, icons, 0, 48);
            var pop = new WinInfo(); pop.H = new IntPtr(4242); pop.Surface = true;
            pop.R.Left = 1000; pop.R.Top = 880; pop.R.Right = 1400; pop.R.Bottom = 1080;
            var wl = new List<WinInfo> { pop };
            fresh.Add(pop.H);
            w.UpdateSurfaces(wl, fresh, icons, 0, 48);
            Surface ps;
            w.map.TryGetValue(4242, out ps);
            float onPopup = ps != null ? ps.Sum(0, ps.N) : -1, leftOnGround = g.Sum(1001, 1399);
            sb.AppendLine(string.Format("Popup: Schnee auf Popup = {0:F0} px², unter Popup am Boden = {1:F0} px²  -> {2}",
                onPopup, leftOnGround, onPopup > 3500 && leftOnGround < 1 ? "OK" : "FEHLER"));
            fresh.Clear();
            w.UpdateSurfaces(none, fresh, icons, 0, 48);
            sb.AppendLine(string.Format("Popup zu: {0} Brocken fallen herunter -> {1}", w.ndb, w.ndb > 100 ? "OK" : "FEHLER"));
            for (int k = 0; k < 120; k++) w.Step(1f / 30f, k / 30f);
            sb.AppendLine(string.Format("  nach 4 s: am Boden unter dem Popup wieder {0:F0} px² -> {1}", g.Sum(990, 1410), g.Sum(990, 1410) > 1500 ? "OK" : "FEHLER"));

            // 2) Schaufel schiebt Schnee nach rechts
            Array.Clear(g.H, 0, g.N); w.ndb = 0;
            for (int j = 300; j < 700; j++) g.H[j] = 6f;
            g.Peak = 6f;
            float load = 0, before = g.Sum(0, g.N);
            float y = g.Y - 1;
            for (int x = 290; x <= 720; x += 6) w.ShovelStep(x, y, x - 6, y, true, ref load);
            float cleared = g.Sum(320, 690);
            sb.AppendLine(string.Format("Schaufel: Rest auf der Strecke = {0:F0} px², vorne aufgehäuft = {1:F0} px², auf Schaufel = {2:F0} px² -> {3}",
                cleared, g.Sum(700, 900), load, cleared < 300 && g.Sum(700, 900) + load > 1500 ? "OK" : "FEHLER"));
            w.ShovelStep(720, 900, 720, y, true, ref load);
            float carried = load;
            w.DropLoad(720, 900, ref load, 200, -100);
            sb.AppendLine(string.Format("  hochgehoben {0:F0} px², geworfen als {1} Brocken -> {2}", carried, w.ndb, carried > 50 && w.ndb > 10 ? "OK" : "FEHLER"));

            // 3) Roboter räumt den Boden zu einem Haufen
            Array.Clear(g.H, 0, g.N); w.ndb = 0;
            var rnd = new Random(3);
            for (int j = 0; j < g.N; j++) g.H[j] = 3f + (float)rnd.NextDouble() * 2f;
            g.Peak = 5f;
            var r = new Robot();
            w.RobotPlace(r);
            r.X = 900;
            float total = g.Sum(0, g.N);
            w.Poke();
            int steps = 0;
            for (; steps < 30 * 120; steps++) { w.Step(1f / 30f, steps / 30f); if (steps > 30 && r.State == Robot.Idle) break; }
            float nearHeap = g.Sum(900 - 250, 900 + 250), all = g.Sum(0, g.N);
            sb.AppendLine(string.Format("Roboter: fertig nach {0:F1} s, {1:P0} des Schnees im Haufen um x=900 (max. Höhe {2:F0} px) -> {3}",
                steps / 30f, nearHeap / Math.Max(1f, all), g.Peak, nearHeap / Math.Max(1f, all) > 0.85f && r.State == Robot.Idle ? "OK" : "FEHLER"));
            sb.AppendLine(string.Format("  Schnee vorher {0:F0} px², nachher {1:F0} px² (Tauen/Brocken eingerechnet)", total, all));

            // 4) Roboter auf einem Fenster schiebt den Schnee über die Kante
            var win = new WinInfo(); win.H = new IntPtr(777); win.Surface = true;
            win.R.Left = 400; win.R.Top = 300; win.R.Right = 1000; win.R.Bottom = 700;
            w.UpdateSurfaces(new List<WinInfo> { win }, fresh, icons, 0, 48);
            Surface ws;
            w.map.TryGetValue(777, out ws);
            for (int j = 0; j < ws.N; j++) ws.H[j] = 4f;
            ws.Peak = 4f;
            r.Surf = ws; r.X = 700; r.Yb = ws.Y; r.State = Robot.Idle;
            w.Poke();
            for (steps = 0; steps < 30 * 60; steps++) { w.Step(1f / 30f, steps / 30f); if (steps > 30 && r.State == Robot.Idle) break; }
            sb.AppendLine(string.Format("Roboter auf Fenster: Rest {0:F0} px² von {1:F0} -> {2}", ws.Sum(0, ws.N), 4f * ws.N, ws.Sum(0, ws.N) < 0.1f * 4f * ws.N ? "OK" : "FEHLER"));

            w.Dispose();
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------ Desktop-Icons

    static class DesktopIcons
    {
        public static IntPtr FindListView()
        {
            IntPtr def = N.FindWindowEx(N.FindWindow("Progman", null), IntPtr.Zero, "SHELLDLL_DefView", null);
            if (def == IntPtr.Zero)
            {
                IntPtr w = IntPtr.Zero;
                while ((w = N.FindWindowEx(IntPtr.Zero, w, "WorkerW", null)) != IntPtr.Zero)
                {
                    def = N.FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (def != IntPtr.Zero) break;
                }
            }
            return def == IntPtr.Zero ? IntPtr.Zero : N.FindWindowEx(def, IntPtr.Zero, "SysListView32", null);
        }

        // Bildschirm-Rechtecke der Icon-Grafiken (LVIR_ICON) aus dem Explorer-Prozess lesen.
        public static List<RECT> Get(out IntPtr listView)
        {
            var res = new List<RECT>();
            IntPtr lv = FindListView();
            listView = lv;
            if (lv == IntPtr.Zero || !N.IsWindowVisible(lv)) return res;
            uint pid;
            N.GetWindowThreadProcessId(lv, out pid);
            IntPtr hp = N.OpenProcess(0x0008 | 0x0010 | 0x0020, false, pid);
            if (hp == IntPtr.Zero) return res;
            IntPtr mem = N.VirtualAllocEx(hp, IntPtr.Zero, new UIntPtr(64), 0x3000, 0x04);
            try
            {
                if (mem == IntPtr.Zero) return res;
                IntPtr r, io;
                if (N.SendMessageTimeout(lv, 0x1004, IntPtr.Zero, IntPtr.Zero, 2, 300, out r) == IntPtr.Zero) return res;
                int count = Math.Min(400, (int)r.ToInt64());
                for (int i = 0; i < count; i++)
                {
                    RECT rc = new RECT(); rc.Left = 1; // LVIR_ICON
                    N.WriteProcessMemory(hp, mem, ref rc, new IntPtr(16), out io);
                    if (N.SendMessageTimeout(lv, 0x100E, new IntPtr(i), mem, 2, 300, out r) == IntPtr.Zero || r == IntPtr.Zero) continue;
                    N.ReadProcessMemory(hp, mem, out rc, new IntPtr(16), out io);
                    N.MapWindowPoints(lv, IntPtr.Zero, ref rc, 2);
                    if (rc.Right > rc.Left && rc.Bottom > rc.Top) res.Add(rc);
                }
            }
            finally
            {
                if (mem != IntPtr.Zero) N.VirtualFreeEx(hp, mem, UIntPtr.Zero, 0x8000);
                N.CloseHandle(hp);
            }
            return res;
        }
    }

    // ------------------------------------------------------------ Maus-Hook

    struct HookEvt { public int Kind; public POINT Pt; }   // 1 Roboter angeklickt, 2 Ziel gewählt, 3 Werkzeug beenden

    // Low-Level-Maushook auf eigenem Thread, damit der Cursor nie auf die Render-Schleife wartet.
    sealed class MouseHook
    {
        public volatile int Tool;            // 0 keins, 1 Schaufel, 2 Roboter hinschicken
        public volatile bool LeftDown;
        public volatile bool RobotNeedsDesktop;
        public readonly ConcurrentQueue<HookEvt> Events = new ConcurrentQueue<HookEvt>();
        readonly object lk = new object();
        Rectangle robotRect;
        bool robotActive, swallowLUp, swallowRUp;
        Thread th;
        uint tid;
        N.LLProc proc;
        IntPtr hook;

        public bool Running { get { return th != null; } }

        public void SetRobot(Rectangle r, bool active)
        {
            lock (lk) { robotRect = r; robotActive = active; }
        }

        public void Start()
        {
            if (th != null) return;
            var ready = new ManualResetEvent(false);
            th = new Thread(delegate ()
            {
                tid = N.GetCurrentThreadId();
                proc = Proc;
                hook = N.SetWindowsHookEx(14, proc, N.GetModuleHandle(null), 0);
                ready.Set();
                MSG m;
                while (N.GetMessage(out m, IntPtr.Zero, 0, 0) > 0) { }
                if (hook != IntPtr.Zero) N.UnhookWindowsHookEx(hook);
                hook = IntPtr.Zero;
            });
            th.IsBackground = true;
            th.Name = "Snowglobe.MouseHook";
            th.Start();
            ready.WaitOne(1000);
        }

        public void Stop()
        {
            if (th == null) return;
            N.PostThreadMessage(tid, 0x0012, IntPtr.Zero, IntPtr.Zero); // WM_QUIT
            th.Join(1000);
            th = null;
            LeftDown = false;
        }

        IntPtr Proc(int code, IntPtr w, IntPtr l)
        {
            if (code >= 0)
            {
                int msg = w.ToInt32();
                if (msg != 0x0200) // Mausbewegung nie anfassen
                {
                    var info = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(l, typeof(MSLLHOOKSTRUCT));
                    int tool = Tool;
                    switch (msg)
                    {
                        case 0x0201: // LBUTTONDOWN
                            if (tool == 1) { LeftDown = true; return new IntPtr(1); }
                            if (tool == 2) { Enqueue(2, info.pt); swallowLUp = true; return new IntPtr(1); }
                            if (HitRobot(info.pt)) { Enqueue(1, info.pt); swallowLUp = true; return new IntPtr(1); }
                            break;
                        case 0x0202: // LBUTTONUP
                            if (tool == 1) { LeftDown = false; return new IntPtr(1); }
                            if (swallowLUp) { swallowLUp = false; return new IntPtr(1); }
                            break;
                        case 0x0204: // RBUTTONDOWN
                            if (tool != 0) { swallowRUp = true; return new IntPtr(1); }
                            break;
                        case 0x0205: // RBUTTONUP
                            if (swallowRUp) { swallowRUp = false; Enqueue(3, info.pt); return new IntPtr(1); }
                            break;
                    }
                }
            }
            return N.CallNextHookEx(IntPtr.Zero, code, w, l);
        }

        void Enqueue(int kind, POINT p)
        {
            var e = new HookEvt(); e.Kind = kind; e.Pt = p;
            Events.Enqueue(e);
        }

        bool HitRobot(POINT p)
        {
            lock (lk)
            {
                if (!robotActive || !robotRect.Contains(p.X, p.Y)) return false;
            }
            if (RobotNeedsDesktop)
            {
                string c = N.ClassOf(N.GetAncestor(N.WindowFromPoint(p), 2));
                if (c != "Progman" && c != "WorkerW") return false;
            }
            return true;
        }
    }

    sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        public Action<int> Pressed;

        public HotkeyWindow()
        {
            var cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE
            CreateHandle(cp);
        }

        public bool Register(int id, uint mods, uint vk) { return N.RegisterHotKey(Handle, id, mods | 0x4000, vk); }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && Pressed != null) Pressed(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            N.UnregisterHotKey(Handle, 1);
            N.UnregisterHotKey(Handle, 2);
            DestroyHandle();
        }
    }

    // ------------------------------------------------------------ App

    sealed class App : ApplicationContext
    {
        static readonly int[] DensityBase = { 110, 230, 450 };
        static readonly string[] DensityName = { "Leicht", "Mittel", "Stark" };
        const string RegKey = @"Software\Snowglobe";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        readonly List<World> worlds = new List<World>();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        readonly Stopwatch sw = Stopwatch.StartNew();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly HashSet<IntPtr> own = new HashSet<IntPtr>();
        readonly N.EnumProc enumProc;
        readonly MouseHook hook = new MouseHook();
        readonly HotkeyWindow hotkeys = new HotkeyWindow();
        readonly Robot robot = new Robot();
        List<WinInfo> wins = new List<WinInfo>(), scan = new List<WinInfo>();
        HashSet<IntPtr> fresh = new HashSet<IntPtr>();
        List<RECT> icons = new List<RECT>();
        long winSig, iconSig;
        float iconTopOffset, iconSizePx = 48f;
        double last;
        int frame, density, tool;
        bool paused, locked, excludeCapture, background, robotShown, needSurfaceUpdate = true;
        IntPtr deskHost;
        float shovelLoad;
        POINT prevCur;
        bool prevDown;
        World robotWorld;
        ToolStripMenuItem[] densityItems;
        ToolStripMenuItem pauseItem, captureItem, autostartItem, backgroundItem, shovelItem, sendItem, robotShowItem, robotAutoItem;
        Icon trayIcon;

        public App(bool forceCapture)
        {
            enumProc = EnumCallback;
            Sprites.Build();
            using (var k = Registry.CurrentUser.CreateSubKey(RegKey))
            {
                density = Math.Max(0, Math.Min(2, Convert.ToInt32(k.GetValue("Density", 1))));
                excludeCapture = Convert.ToInt32(k.GetValue("ExcludeFromCapture", 1)) != 0;
                background = Convert.ToInt32(k.GetValue("Background", 0)) != 0;
                robotShown = Convert.ToInt32(k.GetValue("Robot", 1)) != 0;
                World.RobotAuto = Convert.ToInt32(k.GetValue("RobotAuto", 0)) != 0;
            }
            if (forceCapture) excludeCapture = false;
            if (Program.ForceAuto) { World.RobotAuto = true; robotShown = true; }

            BuildWorlds();
            BuildTray();

            hotkeys.Pressed = OnHotkey;
            hotkeys.Register(1, 0x0002 | 0x0001, (uint)Keys.S); // Strg+Alt+S
            hotkeys.Register(2, 0x0002 | 0x0001, (uint)Keys.R); // Strg+Alt+R
            UpdateHook();

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            timer.Interval = 33;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        void BuildWorlds()
        {
            foreach (var w in worlds) w.Dispose();
            worlds.Clear(); own.Clear();
            robotWorld = null;
            int seed = Environment.TickCount;
            foreach (var sc in Screen.AllScreens)
            {
                float scale = 1f;
                var c = new POINT(); c.X = sc.Bounds.X + sc.Bounds.Width / 2; c.Y = sc.Bounds.Y + sc.Bounds.Height / 2;
                IntPtr mon = N.MonitorFromPoint(c, 2);
                uint dx, dy;
                try { if (N.GetDpiForMonitor(mon, 0, out dx, out dy) == 0) scale = dx / 96f; } catch { }
                var w = new World(sc.Bounds, sc.WorkingArea, scale, seed++);
                w.SetCount(FlakeCount(w));
                w.Ov.SetExcludeFromCapture(excludeCapture);
                if (!paused) w.Ov.SetShown(true);
                own.Add(w.Ov.Handle);
                worlds.Add(w);
                if (sc.Primary && robotShown) { w.RobotPlace(robot); robotWorld = w; }
            }
            winSig = 0; iconSig = 0; needSurfaceUpdate = true;
            ScanIcons();
            ApplyZOrder(true);
        }

        int FlakeCount(World w)
        {
            double area = (double)w.B.Width * w.B.Height / (w.S * w.S);
            return (int)(DensityBase[density] * area / (1920.0 * 1080.0));
        }

        World WorldAt(int x, int y)
        {
            foreach (var w in worlds) if (w.B.Contains(x, y)) return w;
            return null;
        }

        // ---- Fenster scannen (Z-Reihenfolge, oben zuerst)

        bool EnumCallback(IntPtr h, IntPtr l)
        {
            if (own.Contains(h) || !N.IsWindowVisible(h) || N.IsIconic(h)) return true;
            int cloaked;
            if (N.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
            int ex = N.GetWindowLong(h, -20), st = N.GetWindowLong(h, -16);
            if ((ex & 0x20) != 0 && (ex & 0x80000) != 0) return true;      // durchklickbare Overlays
            if ((ex & 0x80000) != 0)
            {
                uint key, fl; byte alpha;
                if (N.GetLayeredWindowAttributes(h, out key, out alpha, out fl) && (fl & 2) != 0 && alpha == 0) return true;
            }
            string cls = N.ClassOf(h);
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return true;
            RECT r;
            if (N.DwmGetWindowAttribute(h, 9, out r, 16) != 0) N.GetWindowRect(h, out r);
            int w = r.Right - r.Left, hh = r.Bottom - r.Top;
            if (w <= 0 || hh <= 0) return true;
            bool tool = (ex & 0x80) != 0;
            bool full = false;
            foreach (var wd in worlds)
            {
                if (r.Left <= wd.B.Left && r.Top <= wd.B.Top && r.Right >= wd.B.Right && r.Bottom >= wd.B.Bottom) full = true;
                if (tool && (long)w * hh > (long)wd.B.Width * wd.B.Height / 2) return true; // große unsichtbare Tool-Overlays
            }
            bool zoomed = N.IsZoomed(h);
            var wi = new WinInfo();
            wi.H = h; wi.R = r;
            wi.Big = zoomed || full;
            wi.Surface = !wi.Big && w >= 80 && hh >= 40 && (st & 0x40000000) == 0 && cls != "tooltips_class32";
            scan.Add(wi);
            return true;
        }

        void ScanWindows()
        {
            scan.Clear();
            N.EnumWindows(enumProc, IntPtr.Zero);
            long sig = 17;
            foreach (var w in scan)
            {
                sig = sig * 31 + w.H.ToInt64();
                sig = sig * 31 + w.R.Left; sig = sig * 31 + w.R.Top; sig = sig * 31 + w.R.Right; sig = sig * 31 + w.R.Bottom;
                sig = sig * 31 + (w.Surface ? 1 : 0);
            }
            if (sig == winSig) return;
            winSig = sig;
            // "frisch" = neu aufgetaucht oder bewegt (nicht bloß in den Vordergrund geholt)
            var prev = new Dictionary<IntPtr, RECT>();
            foreach (var w in wins) prev[w.H] = w.R;
            fresh.Clear();
            foreach (var w in scan)
            {
                RECT o;
                if (!prev.TryGetValue(w.H, out o) || o.Left != w.R.Left || o.Top != w.R.Top || o.Right != w.R.Right || o.Bottom != w.R.Bottom)
                    fresh.Add(w.H);
            }
            var t = wins; wins = scan; scan = t;
            needSurfaceUpdate = true;
        }

        void ScanIcons()
        {
            IntPtr lv;
            List<RECT> list;
            try { list = DesktopIcons.Get(out lv); }
            catch { list = new List<RECT>(); lv = IntPtr.Zero; }
            IntPtr host = lv != IntPtr.Zero ? N.GetAncestor(lv, 2) : N.FindWindow("Progman", null);
            if (host != deskHost) { deskHost = host; ApplyZOrder(true); }
            long sig = 7;
            foreach (var r in list) { sig = sig * 31 + r.Left; sig = sig * 31 + r.Top; sig = sig * 31 + r.Right; }
            if (sig == iconSig) return;
            iconSig = sig;
            icons = list;
            // Das Icon-Rechteck enthält oben etwas Rand; die Grafik selbst ist darin zentriert.
            float iconPx = 48f;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Bags\1\Desktop"))
                    if (k != null) iconPx = Convert.ToInt32(k.GetValue("IconSize", 48));
            }
            catch { }
            float dpi = lv != IntPtr.Zero ? N.GetDpiForWindow(lv) : 96f;
            if (dpi <= 0) dpi = 96f;
            iconPx *= dpi / 96f;
            iconSizePx = iconPx;
            iconTopOffset = 0f;
            if (list.Count > 0)
            {
                int hgt = list[0].Bottom - list[0].Top;
                iconTopOffset = Math.Max(0f, (hgt - iconPx) / 2f);
            }
            needSurfaceUpdate = true;
        }

        // ---- Z-Reihenfolge: ganz vorne oder direkt über dem Desktop

        bool WantTopmost { get { return !background || tool != 0 || deskHost == IntPtr.Zero; } }

        void ApplyZOrder(bool force)
        {
            if (WantTopmost)
            {
                foreach (var w in worlds) w.Ov.KeepOnTop();
                return;
            }
            if (!force)
            {
                // passt es noch? Alle Overlays müssen direkt über dem Desktop liegen.
                int n = 0;
                IntPtr h = N.GetWindow(deskHost, 3);
                while (h != IntPtr.Zero && own.Contains(h)) { n++; h = N.GetWindow(h, 3); }
                if (n == worlds.Count) return;
            }
            foreach (var w in worlds) w.Ov.PlaceAbove(deskHost);
        }

        // ---- Hauptschleife

        void Tick()
        {
            double now = sw.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, now - last);
            last = now;
            HandleHookEvents();
            if (paused || locked) return;
            frame++;

            if (frame % 3 == 0) ScanWindows();
            if (frame % 60 == 1) ScanIcons();
            if (needSurfaceUpdate)
            {
                needSurfaceUpdate = false;
                foreach (var w in worlds) w.UpdateSurfaces(wins, fresh, icons, iconTopOffset, iconSizePx);
                fresh.Clear();
                UpdateCovered();
            }
            if (frame % 15 == 0) CheckFullscreen();
            if (frame % 30 == 0) ApplyZOrder(false);

            UpdateTools(dt);

            foreach (var w in worlds)
            {
                if (w.Hidden || w.Covered) continue;
                w.Step(dt, (float)now);
                w.Render();
            }

            if (hook.Running)
            {
                bool act = robotWorld != null && !robotWorld.Hidden && !robotWorld.Covered && robotWorld.RobotVisible();
                hook.SetRobot(act ? robotWorld.RobotRect() : Rectangle.Empty, act);
                hook.RobotNeedsDesktop = !WantTopmost;
            }
        }

        // Im Hintergrund-Modus: komplett verdeckte Monitore nicht berechnen (spart CPU).
        void UpdateCovered()
        {
            foreach (var w in worlds)
            {
                bool cov = false;
                if (!WantTopmost)
                    foreach (var wi in wins)
                        if (wi.Big && wi.R.Left <= w.Work.Left && wi.R.Top <= w.Work.Top && wi.R.Right >= w.Work.Right && wi.R.Bottom >= w.Work.Bottom) { cov = true; break; }
                w.Covered = cov;
            }
        }

        void UpdateTools(float dt)
        {
            POINT cp;
            N.GetCursorPos(out cp);
            World wc = WorldAt(cp.X, cp.Y);
            foreach (var w in worlds) { w.ShovelOn = false; w.MarkerOn = false; }
            if (tool == 1 && wc != null)
            {
                bool down = hook.LeftDown;
                float lx = cp.X - wc.B.X, ly = cp.Y - wc.B.Y;
                if (prevDown && !down)
                {
                    float vx = (cp.X - prevCur.X) / Math.Max(dt, 0.01f), vy = (cp.Y - prevCur.Y) / Math.Max(dt, 0.01f);
                    wc.DropLoad(lx, ly, ref shovelLoad, vx * 0.6f, vy * 0.6f);
                    wc.ShovelStep(lx, ly, lx, ly, false, ref shovelLoad);
                }
                else wc.ShovelStep(lx, ly, prevCur.X - wc.B.X, prevCur.Y - wc.B.Y, down, ref shovelLoad);
                prevDown = down;
            }
            else if (tool == 2 && wc != null)
            {
                wc.SetMarker(cp.X - wc.B.X, cp.Y - wc.B.Y);
            }
            prevCur = cp;
        }

        void HandleHookEvents()
        {
            HookEvt e;
            while (hook.Events.TryDequeue(out e))
            {
                if (e.Kind == 1 && robotWorld != null) robotWorld.Poke();
                else if (e.Kind == 2) { SendRobot(e.Pt); SetTool(0); }
                else if (e.Kind == 3) SetTool(0);
            }
        }

        void SendRobot(POINT p)
        {
            var w = WorldAt(p.X, p.Y);
            if (w == null) return;
            if (!robotShown) { robotShown = true; SaveSetting("Robot", 1); UpdateHook(); }
            float lx = p.X - w.B.X, ly = p.Y - w.B.Y;
            if (robotWorld != w)
            {
                if (robotWorld != null) robotWorld.Bot = null;
                robotWorld = w;
                w.RobotEnter(robot, lx, true, lx);
            }
            else w.SendTo(lx, ly);
        }

        // Bei Vollbild-Apps (Video, Spiele, Präsentationen) auf dem Monitor ausblenden.
        void CheckFullscreen()
        {
            IntPtr fg = N.GetForegroundWindow();
            RECT r = new RECT();
            bool valid = fg != IntPtr.Zero && !own.Contains(fg) && N.GetWindowRect(fg, out r) && !N.IsZoomed(fg);
            if (valid)
            {
                string cls = N.ClassOf(fg);
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") valid = false;
            }
            foreach (var w in worlds)
            {
                bool full = valid && r.Left <= w.B.Left && r.Top <= w.B.Top && r.Right >= w.B.Right && r.Bottom >= w.B.Bottom;
                if (full == w.Hidden) continue;
                w.Hidden = full;
                if (full) { w.ClearAll(); w.Ov.SetShown(false); }
                else w.Ov.SetShown(true);
            }
        }

        // ---- Werkzeuge

        void SetTool(int t)
        {
            if (t == tool) return;
            if (tool == 1 && shovelLoad > 0)
            {
                POINT cp; N.GetCursorPos(out cp);
                var w = WorldAt(cp.X, cp.Y) ?? worlds[0];
                w.DropLoad(cp.X - w.B.X, cp.Y - w.B.Y, ref shovelLoad, 0, 0);
            }
            tool = t;
            hook.Tool = t;
            hook.LeftDown = false;
            prevDown = false;
            if (t != 0 && paused) TogglePause();
            UpdateHook();
            ApplyZOrder(true);
            UpdateCovered();
            if (t == 1) tray.ShowBalloonTip(3000, "Schaufel aktiv", "Mit gedrückter linker Maustaste Schnee schieben, hochheben und werfen. Rechtsklick oder Strg+Alt+S beendet.", ToolTipIcon.None);
            if (t == 2) tray.ShowBalloonTip(3000, "Schneeroboter", "Klick auf eine Stelle: Der Roboter räumt den Schnee dort zu einem Haufen. Rechtsklick bricht ab.", ToolTipIcon.None);
        }

        void OnHotkey(int id)
        {
            if (id == 1) SetTool(tool == 1 ? 0 : 1);
            if (id == 2) SetTool(tool == 2 ? 0 : 2);
        }

        void UpdateHook()
        {
            bool need = tool != 0 || robotShown;
            if (need && !hook.Running) hook.Start();
            else if (!need && hook.Running) hook.Stop();
        }

        void SetRobotShown(bool on)
        {
            robotShown = on;
            SaveSetting("Robot", on ? 1 : 0);
            if (on && robotWorld == null)
            {
                var w = worlds.Find(delegate (World x) { return x.B.Contains(Screen.PrimaryScreen.Bounds.Location); }) ?? worlds[0];
                robotWorld = w;
                w.RobotEnter(robot, w.B.Width * 0.18f, false, float.NaN);
            }
            else if (!on && robotWorld != null)
            {
                robotWorld.Bot = null;
                robotWorld = null;
            }
            UpdateHook();
        }

        // ---- Tray

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Snowglobe ❄") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            densityItems = new ToolStripMenuItem[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                densityItems[i] = new ToolStripMenuItem("Schnee: " + DensityName[i], null, delegate { SetDensity(idx); });
                menu.Items.Add(densityItems[i]);
            }
            menu.Items.Add(new ToolStripSeparator());
            shovelItem = new ToolStripMenuItem("Schaufel", null, delegate { SetTool(tool == 1 ? 0 : 1); });
            shovelItem.ShortcutKeyDisplayString = "Strg+Alt+S";
            menu.Items.Add(shovelItem);
            var robotMenu = new ToolStripMenuItem("Schneeroboter");
            sendItem = new ToolStripMenuItem("Hinschicken …", null, delegate { SetTool(tool == 2 ? 0 : 2); });
            sendItem.ShortcutKeyDisplayString = "Strg+Alt+R";
            robotShowItem = new ToolStripMenuItem("Anzeigen", null, delegate { SetRobotShown(!robotShown); });
            robotAutoItem = new ToolStripMenuItem("Automatik (räumt selbstständig)", null, delegate
            {
                World.RobotAuto = !World.RobotAuto;
                SaveSetting("RobotAuto", World.RobotAuto ? 1 : 0);
                if (World.RobotAuto && !robotShown) SetRobotShown(true);
            });
            var robotHint = new ToolStripMenuItem("Tipp: Roboter anklicken = jetzt räumen") { Enabled = false };
            robotMenu.DropDownItems.Add(sendItem);
            robotMenu.DropDownItems.Add(robotShowItem);
            robotMenu.DropDownItems.Add(robotAutoItem);
            robotMenu.DropDownItems.Add(robotHint);
            menu.Items.Add(robotMenu);
            menu.Items.Add(new ToolStripMenuItem("Schnee abschütteln", null, delegate { foreach (var w in worlds) w.ShakeOff(); }));
            menu.Items.Add(new ToolStripSeparator());
            backgroundItem = new ToolStripMenuItem("Im Hintergrund (hinter Fenstern)", null, delegate { ToggleBackground(); });
            menu.Items.Add(backgroundItem);
            pauseItem = new ToolStripMenuItem("Pausieren", null, delegate { TogglePause(); });
            menu.Items.Add(pauseItem);
            menu.Items.Add(new ToolStripSeparator());
            captureItem = new ToolStripMenuItem("In Screenshots/Bildschirmfreigabe ausblenden", null, delegate { ToggleCapture(); });
            menu.Items.Add(captureItem);
            autostartItem = new ToolStripMenuItem("Mit Windows starten", null, delegate { ToggleAutostart(); });
            menu.Items.Add(autostartItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Beenden", null, delegate { Quit(); }));
            menu.Opening += delegate { RefreshMenu(); };

            trayIcon = MakeIcon(32);
            tray.Icon = trayIcon;
            tray.Text = "Snowglobe";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { TogglePause(); };
            tray.Visible = true;
            RefreshMenu();
        }

        void RefreshMenu()
        {
            for (int i = 0; i < 3; i++) densityItems[i].Checked = i == density;
            pauseItem.Text = paused ? "Weiter schneien lassen" : "Pausieren";
            captureItem.Checked = excludeCapture;
            autostartItem.Checked = IsAutostart();
            backgroundItem.Checked = background;
            shovelItem.Checked = tool == 1;
            sendItem.Checked = tool == 2;
            robotShowItem.Checked = robotShown;
            robotAutoItem.Checked = World.RobotAuto;
        }

        void SetDensity(int d)
        {
            density = d;
            foreach (var w in worlds) w.SetCount(FlakeCount(w));
            SaveSetting("Density", d);
        }

        void ToggleBackground()
        {
            background = !background;
            SaveSetting("Background", background ? 1 : 0);
            ApplyZOrder(true);
            UpdateCovered();
        }

        void TogglePause()
        {
            paused = !paused;
            if (paused) SetTool(0);
            foreach (var w in worlds)
            {
                if (paused) { w.ClearAll(); w.Ov.SetShown(false); }
                else if (!w.Hidden) w.Ov.SetShown(true);
            }
            if (!paused) ApplyZOrder(true);
            last = sw.Elapsed.TotalSeconds;
        }

        void ToggleCapture()
        {
            excludeCapture = !excludeCapture;
            foreach (var w in worlds) w.Ov.SetExcludeFromCapture(excludeCapture);
            SaveSetting("ExcludeFromCapture", excludeCapture ? 1 : 0);
        }

        static bool IsAutostart()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("Snowglobe") != null;
        }

        static void ToggleAutostart()
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (k.GetValue("Snowglobe") != null) k.DeleteValue("Snowglobe", false);
                else k.SetValue("Snowglobe", "\"" + Application.ExecutablePath + "\"");
            }
        }

        static void SaveSetting(string name, int v)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RegKey)) k.SetValue(name, v, RegistryValueKind.DWord);
        }

        void OnDisplayChanged(object s, EventArgs e)
        {
            BuildWorlds();
        }

        void OnSessionSwitch(object s, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock) { locked = true; SetTool(0); }
            else if (e.Reason == SessionSwitchReason.SessionUnlock) { locked = false; last = sw.Elapsed.TotalSeconds; }
        }

        void Quit()
        {
            timer.Stop();
            hook.Stop();
            hotkeys.Dispose();
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            tray.Visible = false;
            tray.Dispose();
            foreach (var w in worlds) w.Dispose();
            worlds.Clear();
            ExitThread();
        }

        public static Icon MakeIcon(int size)
        {
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    float s = size;
                    using (var b = new SolidBrush(Color.FromArgb(255, 92, 62, 46)))
                        g.FillRectangle(b, s * 0.22f, s * 0.76f, s * 0.56f, s * 0.2f);
                    var globe = new RectangleF(s * 0.06f, s * 0.02f, s * 0.88f, s * 0.84f);
                    using (var b = new LinearGradientBrush(globe, Color.FromArgb(255, 140, 200, 255), Color.FromArgb(255, 28, 78, 170), 90f))
                        g.FillEllipse(b, globe);
                    using (var b = new SolidBrush(Color.FromArgb(235, 255, 255, 255)))
                        g.FillPie(b, globe.X, globe.Y, globe.Width, globe.Height, 20, 140);
                    float cx = s * 0.5f, cy = s * 0.38f, rr = s * 0.22f;
                    using (var p = new Pen(Color.White, Math.Max(1.4f, s / 13f)))
                    {
                        p.StartCap = p.EndCap = LineCap.Round;
                        for (int k = 0; k < 3; k++)
                        {
                            double a = k * Math.PI / 3 + Math.PI / 2;
                            float dx = (float)Math.Cos(a) * rr, dy = (float)Math.Sin(a) * rr;
                            g.DrawLine(p, cx - dx, cy - dy, cx + dx, cy + dy);
                        }
                    }
                }
                IntPtr h = bmp.GetHicon();
                var ic = (Icon)Icon.FromHandle(h).Clone();
                N.DestroyIcon(h);
                return ic;
            }
        }
    }

    static class Program
    {
        public static bool ForceAuto;

        [STAThread]
        static void Main(string[] args)
        {
            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }

            bool capture = false, dump = false;
            foreach (var a in args)
            {
                if (a == "--capture") capture = true;
                if (a == "--dump") dump = true;
                if (a == "--fast") World.DepositBoost = 8f;
                if (a == "--auto") ForceAuto = true;
                if (a == "--selftest")
                {
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "selftest.txt"), World.SelfTest());
                    return;
                }
            }

            if (dump) { Dump(); return; }

            bool created;
            using (var mutex = new Mutex(true, @"Local\Snowglobe.SingleInstance", out created))
            {
                if (!created) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new App(capture));
            }
        }

        // Diagnose: Icon-Rechtecke und Monitore in snowglobe-dump.txt schreiben.
        static void Dump()
        {
            var sb = new StringBuilder();
            foreach (var sc in Screen.AllScreens) sb.AppendLine("Screen " + sc.Bounds + " work " + sc.WorkingArea);
            IntPtr lv;
            var icons = DesktopIcons.Get(out lv);
            sb.AppendLine("ListView " + lv + " dpi " + (lv != IntPtr.Zero ? N.GetDpiForWindow(lv) : 0) + " icons " + icons.Count);
            foreach (var r in icons) sb.AppendLine(string.Format("  {0},{1} - {2},{3}  ({4}x{5})", r.Left, r.Top, r.Right, r.Bottom, r.Right - r.Left, r.Bottom - r.Top));
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "snowglobe-dump.txt"), sb.ToString());
        }
    }
}
