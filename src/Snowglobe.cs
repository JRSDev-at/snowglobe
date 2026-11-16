// Snowglobe – leichter Schneefall für den Windows-Desktop.
// Ein transparentes, durchklickbares Overlay pro Monitor. Schnee bleibt auf
// Fensteroberkanten, Desktop-Icons und am Bildschirmboden liegen.
// Build: build.cmd (nutzt den in Windows enthaltenen C#-Compiler, kein SDK nötig)

using System;
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
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

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
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
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

        public void KeepOnTop()
        {
            N.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // NOSIZE|NOMOVE|NOACTIVATE
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

    // ------------------------------------------------------------ Simulation

    struct Flake { public float X, Y, Vy, R, Phase, Freq, Amp, Alpha, Drift; public int Cls; }
    struct Debris { public float X, Y, Vx, Vy, R, Alpha; public int Cls; }
    struct WinInfo { public IntPtr H; public RECT R; public bool Surface; }

    // Eine horizontale Kante, auf der Schnee liegen bleibt (Fenster, Icon, Boden).
    sealed class Surface
    {
        public readonly long Key;
        public readonly int Kind, X0, X1, Y, N;
        public readonly float MaxH;
        public readonly float[] H, Cap, ClipY;
        public readonly bool[] Vis;
        public int Z;
        public bool Seen;
        public float Peak;

        public Surface(long key, int kind, int x0, int x1, int y, float maxH, bool taper)
        {
            Key = key; Kind = kind; X0 = x0; X1 = x1; Y = y; MaxH = maxH; N = x1 - x0;
            H = new float[N]; Cap = new float[N]; ClipY = new float[N]; Vis = new bool[N];
            for (int j = 0; j < N; j++)
            {
                Cap[j] = taper ? Math.Min(maxH, (Math.Min(j, N - 1 - j) + 1) * 0.45f) : maxH;
                Vis[j] = true;
                ClipY[j] = -1e9f;
            }
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
                if (j < 0 || j >= N) continue;
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
            if (!flip)
            {
                for (int j = 0; j < N - 1; j++) Relax(j);
            }
            else
            {
                for (int j = N - 2; j >= 0; j--) Relax(j);
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

        void Relax(int j)
        {
            const float lim = 0.35f;
            float d = H[j] - H[j + 1];
            if (d > lim) { float m = (d - lim) * 0.45f; H[j] -= m; H[j + 1] = Math.Min(Cap[j + 1], H[j + 1] + m); }
            else if (d < -lim) { float m = (-d - lim) * 0.45f; H[j + 1] -= m; H[j] = Math.Min(Cap[j], H[j] + m); }
        }
    }

    // Ein Monitor: Overlay, Flocken, Schneehaufen.
    unsafe sealed class World : IDisposable
    {
        public readonly Rectangle B, Work;
        public readonly float S;
        public readonly Overlay Ov;
        public bool Hidden;
        public static float DepositBoost = 1f;
        readonly int W, Hh;
        Flake[] fl = new Flake[0];
        int nfl;
        readonly Debris[] db = new Debris[6000];
        int ndb;
        readonly List<Surface> surfs = new List<Surface>();
        readonly Dictionary<long, Surface> map = new Dictionary<long, Surface>();
        readonly Surface ground;
        readonly Random rnd;
        List<Rectangle> dirty = new List<Rectangle>(2048), dirtyPrev = new List<Rectangle>(2048);
        bool groundFade, flip;

        public World(Rectangle bounds, Rectangle work, float scale, int seed)
        {
            B = bounds; Work = work; S = scale;
            W = B.Width; Hh = B.Height;
            rnd = new Random(seed);
            Ov = new Overlay(B);
            int gy = Math.Min(Hh, Work.Bottom - B.Top);
            ground = new Surface(-1, 0, 0, W, gy, 26f * S, false);
            surfs.Add(ground);
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

            flip = !flip;
            float melt = 0.006f * S;
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                s.Step(dt, s == ground && groundFade ? 25f * S : melt, flip);
            }
            if (groundFade && ground.Peak <= 0f) groundFade = false;
        }

        // ---- Oberflächen aus Fenster-/Icon-Liste aktualisieren

        public void UpdateSurfaces(List<WinInfo> wins, List<RECT> icons, float iconTop, float iconPx)
        {
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
                Upsert(w.H.ToInt64(), 1, x0, x1, y, 14f * S, i);
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
                Upsert(key, 2, x0, x1, y, 7f * S, int.MaxValue);
            }

            for (int k = surfs.Count - 1; k >= 0; k--)
            {
                var s = surfs[k];
                if (s.Seen) continue;
                Release(s);
                surfs.RemoveAt(k);
                map.Remove(s.Key);
            }

            for (int k = 0; k < surfs.Count; k++)
                if (surfs[k] != ground) ComputeVis(surfs[k], wins);
        }

        void Upsert(long key, int kind, int x0, int x1, int y, float maxH, int z)
        {
            Surface s;
            if (map.TryGetValue(key, out s))
            {
                if (s.X0 == x0 && s.X1 == x1 && s.Y == y) { s.Seen = true; s.Z = z; return; }
                Release(s);
                surfs.Remove(s);
                map.Remove(key);
            }
            s = new Surface(key, kind, x0, x1, y, maxH, true);
            s.Z = z; s.Seen = true;
            surfs.Add(s);
            map[key] = s;
        }

        // Verdeckung durch Fenster, die in der Z-Reihenfolge darüber liegen.
        void ComputeVis(Surface s, List<WinInfo> wins)
        {
            for (int j = 0; j < s.N; j++) { s.Vis[j] = true; s.ClipY[j] = -1e9f; }
            int lim = Math.Min(s.Z, wins.Count);
            int line = s.Y - 1;
            for (int i = 0; i < lim; i++)
            {
                RECT r = wins[i].R;
                int ox0 = r.Left - B.Left, ox1 = r.Right - B.Left, oy0 = r.Top - B.Top, oy1 = r.Bottom - B.Top;
                int a = Math.Max(ox0, s.X0), b = Math.Min(ox1, s.X1);
                if (a >= b) continue;
                if (oy0 <= line && oy1 > line)
                {
                    for (int x = a; x < b; x++) s.Vis[x - s.X0] = false;
                }
                else if (oy1 <= line && oy1 > s.Y - s.MaxH - 2)
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

            Ov.Present();
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
                // leichte Textur pro Spalte
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
                    int a = (int)(cov * 245f);
                    bits[(long)y * W + x] = ((uint)a << 24) | ((uint)(cr * a / 255) << 16) | ((uint)(cg * a / 255) << 8) | (uint)(cb * a / 255);
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
            int S = Sprites.Size[cls], off = Sprites.Off[cls];
            uint[] spr = Sprites.Data[cls * 16 + sy * 4 + sx];
            int dx = ix - off, dy = iy - off;
            if (dx >= W || dy >= Hh || dx + S <= 0 || dy + S <= 0) return;
            int al = (int)(alpha * 256f);
            int px0 = Math.Max(0, -dx), py0 = Math.Max(0, -dy);
            int px1 = Math.Min(S, W - dx), py1 = Math.Min(S, Hh - dy);
            for (int py = py0; py < py1; py++)
            {
                uint* row = bits + (long)(dy + py) * W + dx;
                int so = py * S;
                for (int px = px0; px < px1; px++)
                {
                    uint s = spr[so + px];
                    int a = (int)(s >> 24) * al >> 8;
                    if (a == 0) continue;
                    int sr = (int)((s >> 16) & 255) * al >> 8, sg = (int)((s >> 8) & 255) * al >> 8, sb = (int)(s & 255) * al >> 8;
                    uint d = row[px];
                    if (d == 0)
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
            dirty.Add(new Rectangle(dx, dy, S, S));
        }

        public void ClearAll()
        {
            Ov.Clear();
            Ov.Present();
            dirty.Clear(); dirtyPrev.Clear();
        }

        public void Dispose() { Ov.Dispose(); }
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
        List<WinInfo> wins = new List<WinInfo>(), scan = new List<WinInfo>();
        List<RECT> icons = new List<RECT>();
        long winSig, iconSig;
        float iconTopOffset, iconSizePx = 48f;
        double last;
        int frame, density;
        bool paused, locked, excludeCapture, needSurfaceUpdate = true;
        ToolStripMenuItem[] densityItems;
        ToolStripMenuItem pauseItem, captureItem, autostartItem;
        Icon trayIcon;

        public App(bool forceCapture)
        {
            enumProc = EnumCallback;
            Sprites.Build();
            using (var k = Registry.CurrentUser.CreateSubKey(RegKey))
            {
                density = Math.Max(0, Math.Min(2, Convert.ToInt32(k.GetValue("Density", 1))));
                excludeCapture = Convert.ToInt32(k.GetValue("ExcludeFromCapture", 1)) != 0;
            }
            if (forceCapture) excludeCapture = false;

            BuildWorlds();
            BuildTray();

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
            }
            winSig = 0; iconSig = 0; needSurfaceUpdate = true;
            ScanIcons();
        }

        int FlakeCount(World w)
        {
            double area = (double)w.B.Width * w.B.Height / (w.S * w.S);
            return (int)(DensityBase[density] * area / (1920.0 * 1080.0));
        }

        // ---- Fenster scannen (Z-Reihenfolge, oben zuerst)

        bool EnumCallback(IntPtr h, IntPtr l)
        {
            if (own.Contains(h) || !N.IsWindowVisible(h) || N.IsIconic(h)) return true;
            int cloaked;
            if (N.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) return true;
            int ex = N.GetWindowLong(h, -20), st = N.GetWindowLong(h, -16);
            if ((ex & 0x20) != 0 && (ex & 0x80000) != 0) return true;      // durchklickbare Overlays
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
            var wi = new WinInfo();
            wi.H = h; wi.R = r;
            wi.Surface = !tool && !full && !N.IsZoomed(h) && w >= 120 && hh >= 50 && (st & 0x40000000) == 0;
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
            if (sig != winSig)
            {
                winSig = sig;
                var t = wins; wins = scan; scan = t;
                needSurfaceUpdate = true;
            }
        }

        void ScanIcons()
        {
            IntPtr lv;
            List<RECT> list;
            try { list = DesktopIcons.Get(out lv); }
            catch { list = new List<RECT>(); lv = IntPtr.Zero; }
            long sig = 7;
            foreach (var r in list) { sig = sig * 31 + r.Left; sig = sig * 31 + r.Top; sig = sig * 31 + r.Right; }
            if (sig == iconSig) return;
            iconSig = sig;
            icons = list;
            // Das Icon-Rechteck enthält oben etwas Rand; die Grafik selbst liegt unten bündig.
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

        // ---- Hauptschleife

        void Tick()
        {
            double now = sw.Elapsed.TotalSeconds;
            float dt = (float)Math.Min(0.1, now - last);
            last = now;
            if (paused || locked) return;
            frame++;

            if (frame % 3 == 0) ScanWindows();
            if (frame % 60 == 1) ScanIcons();
            if (needSurfaceUpdate)
            {
                needSurfaceUpdate = false;
                foreach (var w in worlds) w.UpdateSurfaces(wins, icons, iconTopOffset, iconSizePx);
            }
            if (frame % 15 == 0) CheckFullscreen();
            if (frame % 90 == 0) foreach (var w in worlds) if (!w.Hidden) w.Ov.KeepOnTop();

            foreach (var w in worlds)
            {
                if (w.Hidden) continue;
                w.Step(dt, (float)now);
                w.Render();
            }
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

        // ---- Tray

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            var head = new ToolStripMenuItem("Snowglobe ❄") { Enabled = false };
            menu.Items.Add(head);
            menu.Items.Add(new ToolStripSeparator());
            densityItems = new ToolStripMenuItem[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                densityItems[i] = new ToolStripMenuItem("Schnee: " + DensityName[i], null, delegate { SetDensity(idx); });
                menu.Items.Add(densityItems[i]);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Schnee abschütteln", null, delegate { foreach (var w in worlds) w.ShakeOff(); }));
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
        }

        void SetDensity(int d)
        {
            density = d;
            foreach (var w in worlds) w.SetCount(FlakeCount(w));
            SaveSetting("Density", d);
        }

        void TogglePause()
        {
            paused = !paused;
            foreach (var w in worlds)
            {
                if (paused) { w.ClearAll(); w.Ov.SetShown(false); }
                else if (!w.Hidden) w.Ov.SetShown(true);
            }
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
            if (e.Reason == SessionSwitchReason.SessionLock) locked = true;
            else if (e.Reason == SessionSwitchReason.SessionUnlock) { locked = false; last = sw.Elapsed.TotalSeconds; }
        }

        void Quit()
        {
            timer.Stop();
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
