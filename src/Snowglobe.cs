// Snowglobe – leichter Schneefall für den Windows-Desktop.
// Ein transparentes, durchklickbares Overlay pro Monitor. Schnee bleibt auf
// Fensteroberkanten, Desktop-Icons und am Bildschirmboden liegen. Dazu eine
// Schaufel, ein Schneeroboter und automatische Updates über GitHub-Releases.
// Build: build.cmd (nutzt den in Windows enthaltenen C#-Compiler, kein SDK nötig)

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Snowglobe")]
[assembly: AssemblyProduct("Snowglobe")]
[assembly: AssemblyVersion("1.3.0.0")]

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
        [DllImport("gdi32.dll")] public static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, uint rop);
        [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref POINT pt, ref SIZE sz, IntPtr src, ref POINT ptSrc, int key, ref BLENDFUNCTION bf, int flags);
        [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint key, byte alpha, uint flags);
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

    // ------------------------------------------------------------ GPU-Ausgabe (Direct3D 11 + DirectComposition)

    [StructLayout(LayoutKind.Sequential)]
    struct DXGI_SWAP_CHAIN_DESC1
    {
        public uint Width, Height; public int Format, Stereo; public uint SampleCount, SampleQuality, BufferUsage, BufferCount;
        public int Scaling, SwapEffect, AlphaMode; public uint Flags;
    }
    [StructLayout(LayoutKind.Sequential)] struct D3D11_BOX { public uint Left, Top, Front, Right, Bottom, Back; }
    [StructLayout(LayoutKind.Sequential)]
    struct D3D11_TEXTURE2D_DESC
    {
        public uint Width, Height, MipLevels, ArraySize; public int Format; public uint SampleCount, SampleQuality;
        public int Usage; public uint BindFlags, CPUAccessFlags, MiscFlags;
    }
    [StructLayout(LayoutKind.Sequential)] struct D3D11_MAPPED_SUBRESOURCE { public IntPtr pData; public uint RowPitch, DepthPitch; }

    // Gemeinsame D3D11-/DComp-Geräte; Aufrufe direkt über die COM-vtables.
    static class Gpu
    {
        [DllImport("d3d11.dll")] static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr levels, uint numLevels, uint sdk, out IntPtr device, out int level, out IntPtr context);
        [DllImport("dcomp.dll")] static extern int DCompositionCreateDevice(IntPtr dxgiDevice, ref Guid iid, out IntPtr dev);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetAdapterFn(IntPtr self, out IntPtr adapter);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetParentFn(IntPtr self, ref Guid iid, out IntPtr parent);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateSwapChainFn(IntPtr self, IntPtr device, ref DXGI_SWAP_CHAIN_DESC1 desc, IntPtr restrict, out IntPtr sc);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int PresentFn(IntPtr self, uint sync, uint flags);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int GetBufferFn(IntPtr self, uint idx, ref Guid iid, out IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void UpdateSubresourceFn(IntPtr self, IntPtr res, uint sub, ref D3D11_BOX box, IntPtr src, uint rowPitch, uint depthPitch);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateTexture2DFn(IntPtr self, ref D3D11_TEXTURE2D_DESC desc, IntPtr init, out IntPtr tex);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int MapFn(IntPtr self, IntPtr res, uint sub, int type, uint flags, out D3D11_MAPPED_SUBRESOURCE m);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void UnmapFn(IntPtr self, IntPtr res, uint sub);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate void CopyResourceFn(IntPtr self, IntPtr dst, IntPtr src);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CommitFn(IntPtr self);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateTargetFn(IntPtr self, IntPtr hwnd, int topmost, out IntPtr target);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int CreateVisualFn(IntPtr self, out IntPtr visual);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] public delegate int SetPtrFn(IntPtr self, IntPtr p);

        public static IntPtr Device, Context, DComp, Factory;
        public static UpdateSubresourceFn UpdateSubresource;
        public static CreateTexture2DFn CreateTexture2D;
        public static MapFn Map;
        public static UnmapFn Unmap;
        public static CopyResourceFn CopyResource;
        public static PresentFn Present;
        public static GetBufferFn GetBuffer;
        public static CommitFn Commit;
        static bool tried;
        public static bool Ok;

        public static Guid IID_Texture2D = new Guid("6f15aaf2-d208-4e89-9ab4-489535d34f9c");

        public static T Fn<T>(IntPtr obj, int slot) where T : class
        {
            IntPtr vtbl = Marshal.ReadIntPtr(obj);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vtbl, slot * IntPtr.Size), typeof(T));
        }

        public static bool Init()
        {
            if (tried) return Ok;
            tried = true;
            if (Program.NoGpu) return false;
            try
            {
                IntPtr dev, ctx; int lvl;
                if (D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out dev, out lvl, out ctx) < 0) return false;
                Device = dev; Context = ctx;
                Guid iidDxgiDevice = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
                IntPtr dxgiDev;
                if (Marshal.QueryInterface(dev, ref iidDxgiDevice, out dxgiDev) < 0) return false;
                IntPtr adapter;
                if (Fn<GetAdapterFn>(dxgiDev, 7)(dxgiDev, out adapter) < 0) return false;
                Guid iidFactory2 = new Guid("50c83a1c-e072-4c48-87b0-3630fa36a6d0");
                IntPtr factory;
                int hr = Fn<GetParentFn>(adapter, 6)(adapter, ref iidFactory2, out factory);
                Marshal.Release(adapter);
                if (hr < 0) return false;
                Factory = factory;
                Guid iidDComp = new Guid("C37EA93A-E7AA-450D-B16F-9746CB0407F3");
                IntPtr dcomp;
                hr = DCompositionCreateDevice(dxgiDev, ref iidDComp, out dcomp);
                Marshal.Release(dxgiDev);
                if (hr < 0) return false;
                DComp = dcomp;
                UpdateSubresource = Fn<UpdateSubresourceFn>(ctx, 48);
                CreateTexture2D = Fn<CreateTexture2DFn>(dev, 5);
                Map = Fn<MapFn>(ctx, 14);
                Unmap = Fn<UnmapFn>(ctx, 15);
                CopyResource = Fn<CopyResourceFn>(ctx, 47);
                Commit = Fn<CommitFn>(dcomp, 3);
                Ok = true;
            }
            catch { Ok = false; }
            return Ok;
        }
    }

    // ------------------------------------------------------------ Overlay

    // Transparentes, durchklickbares Fenster mit eigenem 32-bit-Puffer (premultiplied ARGB).
    // Ausgabe bevorzugt über die GPU (nur geänderte Kacheln werden hochgeladen), sonst per UpdateLayeredWindow.
    unsafe sealed class Overlay : NativeWindow, IDisposable
    {
        public readonly int X, Y, W, H;
        public readonly uint* Bits;
        public readonly bool OnGpu;
        IntPtr memDC, dib, oldObj, mem;
        IntPtr swap, target, visual;
        int fullFrames = 2;
        public int LastTiles;
        public int TotalTiles { get { return W * H; } }

        public static Overlay Create(Rectangle b)
        {
            if (Gpu.Init())
            {
                Overlay o = null;
                try { o = new Overlay(b, true); if (o.OnGpu) return o; }
                catch { }
                if (o != null) o.Dispose();
            }
            return new Overlay(b, false);
        }

        Overlay(Rectangle b, bool gpu)
        {
            X = b.X; Y = b.Y; W = b.Width; H = b.Height;
            var cp = new CreateParams();
            cp.Caption = "Snowglobe";
            cp.X = X; cp.Y = Y; cp.Width = W; cp.Height = H;
            cp.Style = unchecked((int)0x80000000);                 // WS_POPUP
            cp.ExStyle = 0x00080000 | 0x00000020 | 0x00000080      // LAYERED | TRANSPARENT | TOOLWINDOW
                       | 0x00000008 | 0x08000000;                  // TOPMOST | NOACTIVATE
            if (gpu) cp.ExStyle |= 0x00200000;                     // NOREDIRECTIONBITMAP
            CreateHandle(cp);

            if (gpu)
            {
                N.SetLayeredWindowAttributes(Handle, 0, 255, 2);
                mem = Marshal.AllocHGlobal(new IntPtr((long)W * H * 4));
                Bits = (uint*)mem;
                Clear();
                OnGpu = InitSwapChain();
                return;
            }
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

        bool InitSwapChain()
        {
            var d = new DXGI_SWAP_CHAIN_DESC1();
            d.Width = (uint)W; d.Height = (uint)H; d.Format = 87;     // B8G8R8A8_UNORM
            d.SampleCount = 1; d.BufferUsage = 0x20; d.BufferCount = 2;
            d.Scaling = 0; d.SwapEffect = 3; d.AlphaMode = 1;          // STRETCH, FLIP_SEQUENTIAL, PREMULTIPLIED
            if (Gpu.Fn<Gpu.CreateSwapChainFn>(Gpu.Factory, 24)(Gpu.Factory, Gpu.Device, ref d, IntPtr.Zero, out swap) < 0) return false;
            if (Gpu.Present == null) { Gpu.Present = Gpu.Fn<Gpu.PresentFn>(swap, 8); Gpu.GetBuffer = Gpu.Fn<Gpu.GetBufferFn>(swap, 9); }
            if (Gpu.Fn<Gpu.CreateTargetFn>(Gpu.DComp, 6)(Gpu.DComp, Handle, 1, out target) < 0) return false;
            if (Gpu.Fn<Gpu.CreateVisualFn>(Gpu.DComp, 7)(Gpu.DComp, out visual) < 0) return false;
            if (Gpu.Fn<Gpu.SetPtrFn>(visual, 15)(visual, swap) < 0) return false;    // SetContent
            if (Gpu.Fn<Gpu.SetPtrFn>(target, 3)(target, visual) < 0) return false;   // SetRoot
            if (!InitStaging()) return false;
            return Gpu.Commit(Gpu.DComp) >= 0;
        }

        public void SetExcludeFromCapture(bool on)
        {
            N.SetWindowDisplayAffinity(Handle, on ? 0x11u : 0u);
        }

        // Ring aus CPU-beschreibbaren Staging-Texturen: pro Frame werden nur die geänderten Rechtecke
        // hineinkopiert (memcpy), die GPU kopiert die Textur dann selbst in den Ausgabepuffer.
        const int Ring = 3;
        readonly IntPtr[] staging = new IntPtr[Ring];
        readonly int[] stagingFull = new int[Ring];
        readonly List<Rectangle>[] hist = new List<Rectangle>[Ring + 1];
        int ringPos;

        bool InitStaging()
        {
            var d = new D3D11_TEXTURE2D_DESC();
            d.Width = (uint)W; d.Height = (uint)H; d.MipLevels = 1; d.ArraySize = 1; d.Format = 87;
            d.SampleCount = 1; d.Usage = 3; d.CPUAccessFlags = 0x10000;   // STAGING, CPU_ACCESS_WRITE
            for (int i = 0; i < Ring; i++)
            {
                if (Gpu.CreateTexture2D(Gpu.Device, ref d, IntPtr.Zero, out staging[i]) < 0) return false;
                stagingFull[i] = 1;
            }
            for (int i = 0; i < hist.Length; i++) hist[i] = new List<Rectangle>(1024);
            return true;
        }

        // drawn = in diesem Frame gezeichnete Rechtecke (alte Positionen wurden in diesem Frame gelöscht
        // und stehen im vorigen Eintrag). Eine Staging-Textur ist Ring Frames alt.
        public void Present(List<Rectangle> drawn)
        {
            if (!OnGpu)
            {
                POINT dst = new POINT(); dst.X = X; dst.Y = Y;
                SIZE sz = new SIZE(); sz.cx = W; sz.cy = H;
                POINT src = new POINT();
                BLENDFUNCTION bf = new BLENDFUNCTION(); bf.SourceConstantAlpha = 255; bf.AlphaFormat = 1;
                N.UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref sz, memDC, ref src, 0, ref bf, 2);
                return;
            }
            if (fullFrames > 0) { fullFrames--; for (int i = 0; i < Ring; i++) stagingFull[i] = 1; }
            // Verlauf schieben: hist[0] = dieser Frame, hist[k] = vor k Frames
            var last = hist[hist.Length - 1];
            for (int i = hist.Length - 1; i > 0; i--) hist[i] = hist[i - 1];
            hist[0] = last; last.Clear(); last.AddRange(drawn);

            long q0 = Stopwatch.GetTimestamp();
            IntPtr st = staging[ringPos];
            D3D11_MAPPED_SUBRESOURCE m;
            if (Gpu.Map(Gpu.Context, st, 0, 2, 0, out m) < 0) return;   // D3D11_MAP_WRITE
            long q1 = Stopwatch.GetTimestamp();
            byte* dstBase = (byte*)m.pData;
            int pitch = (int)m.RowPitch, tiles = 0;
            if (stagingFull[ringPos] != 0)
            {
                stagingFull[ringPos] = 0;
                for (int y = 0; y < H; y++) CopyRow(dstBase + (long)y * pitch, Bits + (long)y * W, W);
                tiles = W * H;
            }
            else
            {
                for (int k = 0; k < hist.Length; k++)
                {
                    var list = hist[k];
                    for (int i = 0; i < list.Count; i++)
                    {
                        Rectangle r = list[i];
                        int x0 = Math.Max(0, r.X), y0 = Math.Max(0, r.Y), x1 = Math.Min(W, r.Right), y1 = Math.Min(H, r.Bottom);
                        if (x1 <= x0 || y1 <= y0) continue;
                        int n = x1 - x0;
                        for (int y = y0; y < y1; y++) CopyRow(dstBase + (long)y * pitch + x0 * 4, Bits + (long)y * W + x0, n);
                        tiles += n * (y1 - y0);
                    }
                }
            }
            Gpu.Unmap(Gpu.Context, st, 0);
            LastTiles = tiles;
            IntPtr tex;
            if (Gpu.GetBuffer(swap, 0, ref Gpu.IID_Texture2D, out tex) >= 0)
            {
                Gpu.CopyResource(Gpu.Context, tex, st);
                Marshal.Release(tex);
            }
            ringPos = (ringPos + 1) % Ring;
            long q2 = Stopwatch.GetTimestamp();
            Gpu.Present(swap, 0, 0);
            long q3 = Stopwatch.GetTimestamp();
            TGet += q1 - q0; TUpload += q2 - q1; TPresent += q3 - q2;
        }

        static void CopyRow(byte* dst, uint* src, int n)
        {
            ulong* d = (ulong*)dst, s = (ulong*)src;
            int pairs = n >> 1;
            for (int i = 0; i < pairs; i++) d[i] = s[i];
            if ((n & 1) != 0) ((uint*)dst)[n - 1] = src[n - 1];
        }

        public static long TGet, TUpload, TPresent;

        public void Clear()
        {
            long n = (long)W * H;
            for (long i = 0; i < n; i++) Bits[i] = 0;
            fullFrames = 2;
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
            if (visual != IntPtr.Zero) { Marshal.Release(visual); visual = IntPtr.Zero; }
            if (target != IntPtr.Zero) { Marshal.Release(target); target = IntPtr.Zero; }
            if (swap != IntPtr.Zero) { Marshal.Release(swap); swap = IntPtr.Zero; }
            for (int i = 0; i < Ring; i++) if (staging[i] != IntPtr.Zero) { Marshal.Release(staging[i]); staging[i] = IntPtr.Zero; }
            if (Gpu.Ok) Gpu.Commit(Gpu.DComp);
            DestroyHandle();
            if (mem != IntPtr.Zero) { Marshal.FreeHGlobal(mem); mem = IntPtr.Zero; }
        }
    }

    // ------------------------------------------------------------ Bildschirm-Probe

    // Liest einen schmalen Streifen vom Bildschirm (ohne Layered-Fenster, also ohne den eigenen Schnee),
    // um die tatsächlich sichtbare Kante von Popups mit transparentem Rand zu finden.
    unsafe static class ScreenProbe
    {
        static int[] Grab(int x, int y, int w, int h)
        {
            var res = new int[w * h];
            IntPtr screen = N.GetDC(IntPtr.Zero);
            IntPtr dc = N.CreateCompatibleDC(screen);
            var bi = new BITMAPINFOHEADER();
            bi.biSize = 40; bi.biWidth = w; bi.biHeight = -h; bi.biPlanes = 1; bi.biBitCount = 32;
            IntPtr bits;
            IntPtr dib = N.CreateDIBSection(screen, ref bi, 0, out bits, IntPtr.Zero, 0);
            IntPtr old = N.SelectObject(dc, dib);
            N.BitBlt(dc, 0, 0, w, h, screen, x, y, 0x00CC0020); // SRCCOPY ohne CAPTUREBLT
            Marshal.Copy(bits, res, 0, w * h);
            N.SelectObject(dc, old);
            N.DeleteObject(dib);
            N.DeleteDC(dc);
            N.ReleaseDC(IntPtr.Zero, screen);
            return res;
        }

        static int Diff(int a, int b)
        {
            return Math.Abs(((a >> 16) & 255) - ((b >> 16) & 255)) + Math.Abs(((a >> 8) & 255) - ((b >> 8) & 255)) + Math.Abs((a & 255) - (b & 255));
        }

        // erste deutliche, danach stabile Farbkante ab Index 'pre' (= Rechteckkante); Ergebnis relativ dazu
        static int Edge(int[] c, int pre)
        {
            for (int i = 1; i < c.Length - 4; i++)
            {
                if (Diff(c[i], c[i - 1]) < 30) continue;
                if (Diff(c[i], c[i + 1]) > 20 || Diff(c[i], c[i + 2]) > 24 || Diff(c[i], c[i + 3]) > 28) continue;
                return Math.Max(0, i - pre);
            }
            return 0;
        }

        // Einrückung (links, oben, rechts) der sichtbaren Fläche gegenüber dem Fensterrechteck.
        public static int[] Inset(RECT r)
        {
            var res = new int[3];
            int w = r.Right - r.Left, h = r.Bottom - r.Top;
            if (w < 40 || h < 30) return res;
            const int pre = 3, len = 36;
            try
            {
                var tops = new List<int>();
                for (int k = 1; k <= 3; k++)
                {
                    int x = r.Left + w * k / 4;
                    var c = Grab(x, r.Top - pre, 1, len + pre);
                    tops.Add(Edge(c, pre));
                }
                tops.Sort();
                res[1] = tops[1];
                int row = r.Top + res[1] + Math.Min(12, h / 3);
                var l = Grab(r.Left - pre, row, len + pre, 1);
                res[0] = Edge(l, pre);
                var rr = Grab(r.Right - len, row, len + pre, 1);
                Array.Reverse(rr);
                res[2] = Edge(rr, pre);
            }
            catch { }
            return res;
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
            if (Data != null) return;
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

    // ------------------------------------------------------------ Art (Roboter, Schaufel, Röhre)

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

        // Schaufel in ShovelAngles Neigungen (ShovelStep0 + k * ShovelStepDeg Grad):
        // [k] schiebt nach rechts, [ShovelAngles + k] nach links. Anker = Mitte der Schaufelkante,
        // um ihn wird gedreht (negativ = Blatt vorne hoch).
        public const int ShovelAngles = 17;
        public const float ShovelStep0 = -40f, ShovelStepDeg = 5f;

        public static Art[] Shovel(float u)
        {
            int w = (int)Math.Ceiling(74 * u), h = (int)Math.Ceiling(66 * u);
            int ax = (int)(48 * u), ay = (int)(53 * u);
            var res = new Art[2 * ShovelAngles];
            for (int dir = 0; dir < 2; dir++)
            for (int k = 0; k < ShovelAngles; k++)
            {
                float hb = 46 * u - 0.5f;
                float ang = ShovelStep0 + k * ShovelStepDeg;
                res[dir * ShovelAngles + k] = Build(w, h, ax, ay, dir == 1, delegate (Graphics g)
                {
                    g.TranslateTransform(ax, ay);
                    g.RotateTransform(ang);
                    g.TranslateTransform(-22 * u, -46 * u);
                    using (var pen = new Pen(Color.FromArgb(255, 150, 98, 52), 2.8f * u))
                    {
                        pen.StartCap = pen.EndCap = LineCap.Round;
                        g.DrawLine(pen, 18 * u, hb - 4 * u, 6 * u, hb - 36 * u);
                    }
                    using (var pen = new Pen(Color.FromArgb(255, 196, 140, 84), 1f * u))
                        g.DrawLine(pen, 17.4f * u, hb - 5 * u, 5.6f * u, hb - 35 * u);
                    using (var pen = new Pen(Color.FromArgb(255, 40, 40, 46), 1.8f * u))
                    {
                        g.DrawLine(pen, 2 * u, hb - 37 * u, 10 * u, hb - 37 * u);
                        g.DrawLine(pen, 2 * u, hb - 37 * u, 4 * u, hb - 42 * u);
                        g.DrawLine(pen, 10 * u, hb - 37 * u, 8 * u, hb - 42 * u);
                        g.DrawLine(pen, 4 * u, hb - 42 * u, 8 * u, hb - 42 * u);
                    }
                    var pan = new PointF[] { new PointF(11 * u, hb - 2.4f * u), new PointF(32 * u, hb - 2.4f * u), new PointF(33 * u, hb), new PointF(12 * u, hb) };
                    var wall = new PointF[] { new PointF(29.5f * u, hb - 13 * u), new PointF(33 * u, hb - 14.5f * u), new PointF(34 * u, hb), new PointF(31 * u, hb) };
                    using (var b = new SolidBrush(Color.FromArgb(255, 70, 132, 210))) { g.FillPolygon(b, pan); g.FillPolygon(b, wall); }
                    using (var pen = new Pen(Color.FromArgb(255, 30, 70, 130), 0.8f * u)) { g.DrawPolygon(pen, pan); g.DrawPolygon(pen, wall); }
                    using (var pen = new Pen(Color.FromArgb(255, 200, 206, 216), 1.2f * u)) g.DrawLine(pen, 31.2f * u, hb - 0.4f * u, 33.6f * u, hb - 0.4f * u);
                });
            }
            return res;
        }

        // Geparkte Schaufel: steckt aufrecht im Schnee. Anker = Blattunterkante.
        public static Art ShovelStand(float u)
        {
            int w = (int)Math.Ceiling(20 * u), h = (int)Math.Ceiling(50 * u);
            return Build(w, h, w / 2, h, false, delegate (Graphics g)
            {
                float c = w / 2f, hb = h - 0.5f;
                using (var pen = new Pen(Color.FromArgb(255, 150, 98, 52), 2.8f * u))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, c, hb - 14 * u, c + 1.5f * u, hb - 42 * u);
                }
                using (var pen = new Pen(Color.FromArgb(255, 196, 140, 84), 1f * u))
                    g.DrawLine(pen, c - 0.6f * u, hb - 15 * u, c + 0.9f * u, hb - 41 * u);
                using (var pen = new Pen(Color.FromArgb(255, 40, 40, 46), 1.8f * u))
                {
                    g.DrawLine(pen, c - 2.5f * u, hb - 43 * u, c + 5.5f * u, hb - 43 * u);
                    g.DrawLine(pen, c - 2.5f * u, hb - 43 * u, c - 1 * u, hb - 48 * u);
                    g.DrawLine(pen, c + 5.5f * u, hb - 43 * u, c + 4 * u, hb - 48 * u);
                    g.DrawLine(pen, c - 1 * u, hb - 48 * u, c + 4 * u, hb - 48 * u);
                }
                var blade = new PointF[] { new PointF(c - 7 * u, hb - 15 * u), new PointF(c + 7 * u, hb - 15 * u), new PointF(c + 8 * u, hb - 1 * u), new PointF(c, hb), new PointF(c - 8 * u, hb - 1 * u) };
                using (var b = new LinearGradientBrush(new RectangleF(c - 8 * u, hb - 15 * u, 16 * u, 15 * u), Color.FromArgb(255, 96, 158, 232), Color.FromArgb(255, 52, 108, 190), 0f))
                    g.FillPolygon(b, blade);
                using (var pen = new Pen(Color.FromArgb(255, 30, 70, 130), 0.8f * u)) g.DrawPolygon(pen, blade);
                using (var b = new SolidBrush(Color.FromArgb(255, 250, 252, 255))) g.FillEllipse(b, c - 9 * u, hb - 4 * u, 18 * u, 6 * u);
            });
        }

        // Rohrstück, aus dem Boden ragend: [0] Öffnung links, [1] Öffnung rechts. Anker = Mitte der Öffnung, Boden.
        public static Art[] Pipe(float u)
        {
            int w = (int)Math.Ceiling(60 * u), h = (int)Math.Ceiling(46 * u);
            int ax = (int)(7 * u);
            var res = new Art[2];
            for (int dir = 0; dir < 2; dir++)
            {
                res[dir] = Build(w, h, ax, h, dir == 1, delegate (Graphics g)
                {
                    float hb = h - 0.5f, top = hb - 42 * u;
                    var tube = new RectangleF(6 * u, top + 1 * u, 44 * u, 41 * u);
                    // Zylinder-Schattierung
                    using (var b = new LinearGradientBrush(tube, Color.Black, Color.Black, 90f))
                    {
                        var cb = new ColorBlend();
                        cb.Colors = new Color[] { Color.FromArgb(255, 120, 130, 146), Color.FromArgb(255, 205, 214, 226), Color.FromArgb(255, 150, 160, 176), Color.FromArgb(255, 80, 88, 102) };
                        cb.Positions = new float[] { 0f, 0.28f, 0.6f, 1f };
                        b.InterpolationColors = cb;
                        g.FillRectangle(b, tube);
                    }
                    // Bogen nach unten in den Boden
                    using (var b = new LinearGradientBrush(new RectangleF(44 * u, top + 1 * u, 16 * u, 41 * u), Color.FromArgb(255, 170, 180, 196), Color.FromArgb(255, 84, 92, 106), 0f))
                        g.FillPie(b, 30 * u, top + 1 * u, 28 * u, 82 * u, 270, 90);
                    // Ringe mit Nieten
                    for (int k = 0; k < 2; k++)
                    {
                        float x = (22 + k * 16) * u;
                        using (var b = new SolidBrush(Color.FromArgb(255, 92, 100, 116))) g.FillRectangle(b, x, top + 1 * u, 3 * u, 41 * u);
                        using (var b = new SolidBrush(Color.FromArgb(255, 200, 206, 216)))
                            for (int n = 0; n < 4; n++) g.FillEllipse(b, x + 0.6f * u, top + (5 + n * 10) * u, 1.8f * u, 1.8f * u);
                    }
                    // Flansch mit Warnstreifen
                    var flange = new RectangleF(0, top, 14 * u, 42 * u);
                    using (var b = new SolidBrush(Color.FromArgb(255, 245, 170, 40))) g.FillEllipse(b, flange);
                    using (var pen = new Pen(Color.FromArgb(255, 40, 40, 46), 2.2f * u))
                    {
                        var st = g.Save();
                        using (var clip = new GraphicsPath()) { clip.AddEllipse(flange); g.SetClip(clip); }
                        for (int k = -4; k < 8; k++) g.DrawLine(pen, k * 5 * u, top + 42 * u, k * 5 * u + 14 * u, top);
                        g.Restore(st);
                    }
                    using (var pen = new Pen(Color.FromArgb(255, 60, 50, 30), 0.8f * u)) g.DrawEllipse(pen, flange);
                    // dunkle Öffnung
                    using (var b = new SolidBrush(Color.FromArgb(255, 16, 18, 24))) g.FillEllipse(b, 2.5f * u, top + 3 * u, 9 * u, 36 * u);
                    // Schnee auf dem Rohr
                    using (var b = new SolidBrush(Color.FromArgb(255, 250, 252, 255)))
                        g.FillEllipse(b, 10 * u, top - 1.5f * u, 36 * u, 4 * u);
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
    struct Debris { public float X, Y, Vx, Vy, R, Alpha, G, Delay, Ph; public int Cls; }
    struct WinInfo { public IntPtr H; public RECT R; public bool Surface, Big; }

    // Eine horizontale Kante, auf der Schnee liegen bleibt (Fenster, Icon, Boden).
    sealed class Surface
    {
        public readonly long Key;
        public readonly int Kind, X0, X1, Y, N;     // Kind: 0 Boden, 1 Fenster, 2 Icon
        public readonly float MaxH;
        public readonly float[] H, Cap, Cap2, ClipY, Off;
        public readonly bool[] Vis;
        public readonly int[] Cover;
        public int Z;
        public bool Seen;
        public float Peak;

        public Surface(long key, int kind, int x0, int x1, int y, float maxH, bool taper, float pushFactor, float cornerR)
        {
            Key = key; Kind = kind; X0 = x0; X1 = x1; Y = y; MaxH = maxH; N = x1 - x0;
            H = new float[N]; Cap = new float[N]; Cap2 = new float[N]; ClipY = new float[N]; Off = new float[N];
            Vis = new bool[N]; Cover = new int[N];
            float max2 = maxH * pushFactor;
            for (int j = 0; j < N; j++)
            {
                float edge = Math.Min(j, N - 1 - j) + 1;
                Cap[j] = taper ? Math.Min(maxH, edge * 0.45f) : maxH;
                Cap2[j] = taper ? Math.Min(max2, edge * 0.6f) : max2;   // Grenze für geschobenen Schnee
                Vis[j] = true;
                ClipY[j] = -1e9f;
                Cover[j] = -1;
                // abgerundete Fensterecken (Windows 11): Kante fällt zum Rand hin ab
                if (cornerR > 0 && edge - 0.5f < cornerR)
                {
                    float d = cornerR - (edge - 0.5f);
                    Off[j] = cornerR - (float)Math.Sqrt(Math.Max(0f, cornerR * cornerR - d * d));
                }
            }
        }

        public float Base(int j) { return Y + Off[j]; }

        public float Top(float x)
        {
            int j = (int)x - X0;
            if (j < 0) j = 0; else if (j >= N) j = N - 1;
            return Y + Off[j] - H[j];
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
        public const int Idle = 0, Travel = 1, Push = 2, Hop = 3, Fall = 4, GoPipe = 5, Exit = 6, Gone = 7;
        public Surface Surf;
        public float X, Yb, Load, Vy, IdleT, Anim, Blink;
        public int Dir = 1, State = Fall;
        public bool Blade;
        public float TargetX;
        public bool PushAfter, Spill;
        public Run Cur;
        public readonly List<Run> Runs = new List<Run>();
        public float H0x, H0y, H1x, H1y, HT, HDur, HArc;
        public Surface HopSurf;
        public bool CleanAfter, AfterHopPipe;
        // Monitorwechsel durch die Röhre
        public World Target;
        public bool NoJob, PendingAuto, PendingSend;
        public float PendingX, PendingY;
        public bool Busy { get { return State != Idle || Runs.Count > 0; } }
    }

    struct Run { public float Start, End; public int Dir; }

    // Ein Monitor: Overlay, Flocken, Schneehaufen, ggf. Roboter, Röhre und Schaufel.
    unsafe sealed class World : IDisposable
    {
        public static float DepositBoost = 1f;
        public static bool RobotAuto;

        public readonly Rectangle B, Work;
        public readonly float S;
        public readonly Overlay Ov;
        public bool Hidden, Covered;
        public Robot Bot;
        public bool ShovelOn, MarkerOn, ShovelParked;
        public bool RobotGone;
        float shX, shY, shLoad, mkX, mkY;
        float shVx, shVy, shAng = 10f, shAngV, shBob;   // Animation der gehaltenen Schaufel
        bool shDown;
        int shDir = 1;
        // Abgestellte Schaufel: steckt bei parkX im Schnee, fällt bei Bedarf auf die nächste Fläche darunter.
        bool parkHome = true, parkInit;
        float parkX, parkY, parkVy, parkHint;

        readonly int W, Hh;
        Flake[] fl = new Flake[0];
        int nfl;
        Debris[] db = new Debris[8000];
        int ndb;
        readonly List<Surface> surfs = new List<Surface>();
        readonly Dictionary<long, Surface> map = new Dictionary<long, Surface>();
        readonly Surface ground;
        readonly Random rnd;
        List<Rectangle> dirty = new List<Rectangle>(2048), dirtyPrev = new List<Rectangle>(2048);
        bool groundFade, flip;
        float time, wind;

        // Röhre
        int pipeState;          // 0 weg, 1 auftauchen/da, 2 verschwinden
        float pipeX, pipeAnim, pipeCloseT;
        bool pipeMouthRight;

        readonly Art[] robotArt, shovelArt, pipeArt;
        readonly Art markerArt, lampRed, lampAmber, shovelStand;
        readonly float RF, RB, EdgeHeap, HeapMax;

        public World(Rectangle bounds, Rectangle work, float scale, int seed)
        {
            B = bounds; Work = work; S = scale;
            W = B.Width; Hh = B.Height;
            rnd = new Random(seed);
            Ov = Overlay.Create(B);
            int gy = Math.Min(Hh, Work.Bottom - B.Top);
            ground = new Surface(-1, 0, 0, W, gy, 26f * S, false, 4f, 0f);
            surfs.Add(ground);
            robotArt = Art.Robot(S, out RF, out RB);
            shovelArt = Art.Shovel(S);
            shovelStand = Art.ShovelStand(S);
            pipeArt = Art.Pipe(S);
            markerArt = Art.Marker(S);
            lampRed = Art.Lamp(S, Color.FromArgb(255, 255, 60, 50));
            lampAmber = Art.Lamp(S, Color.FromArgb(255, 255, 180, 30));
            EdgeHeap = 26f * S;
            HeapMax = 28f * S;     // Randhügel des Roboters: höchstens so hoch, der Rest fliegt aus dem Bild
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
                float top = s.Y + s.Off[i] - s.H[i];
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
            wind = S * (float)(11 * Math.Sin(t * 0.07) + 6 * Math.Sin(t * 0.19 + 1.3) + 3 * Math.Sin(t * 0.43 + 2.1));

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

            StepDebris(dt);
            if (Bot != null) StepRobot(dt);
            StepPipe(dt);
            if (ShovelParked) StepPark(dt);

            flip = !flip;
            float melt = 0.006f * S;
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                s.Step(dt, s == ground && groundFade ? 25f * S : melt, flip);
            }
            if (groundFade && ground.Peak <= 0f) groundFade = false;
        }

        // Herabfallender Schnee: bröckelt von oben nach unten ab, Klumpen fallen schneller als Pulver.
        void StepDebris(float dt)
        {
            float g = 520f * S;
            for (int i = 0; i < ndb; )
            {
                Debris d = db[i];
                if (d.Delay > 0f)
                {
                    d.Delay -= dt;
                    db[i] = d; i++;
                    continue;
                }
                float yPrev = d.Y;
                float term = (150f + 170f * d.G) * S;
                d.Vy = Math.Min(term, d.Vy + g * d.G * dt);
                d.Vx += (wind * (1.3f - d.G) - d.Vx) * Math.Min(1f, 1.4f * dt);
                d.X += (d.Vx + (float)Math.Sin(time * 4f + d.Ph) * 9f * S * (1.2f - d.G)) * dt;
                d.Y += d.Vy * dt;
                bool dead = d.Y > Hh + 10 || d.X < -20 || d.X > W + 20
                         || TryLand(d.X, yPrev, d.Y, 2.2f * d.R * d.R);
                if (dead) { db[i] = db[--ndb]; continue; }
                db[i] = d;
                i++;
            }
        }

        void AddDebris(float x, float y, float vx, float vy, float delay, bool clump)
        {
            if (ndb >= db.Length) return;
            Debris d;
            float r = clump ? (1.7f + Rnd() * 1.3f) : (0.8f + Rnd() * 0.8f);
            d.R = r * S;
            d.G = clump ? 0.9f + Rnd() * 0.2f : 0.55f + Rnd() * 0.3f;
            d.X = x; d.Y = y;
            d.Vx = vx; d.Vy = vy;
            d.Alpha = clump ? 0.95f : 0.75f + 0.2f * Rnd();
            d.Delay = delay;
            d.Ph = Rnd() * 6.28f;
            d.Cls = Sprites.ClassFor(d.R);
            db[ndb++] = d;
        }

        // ---- Schnee bewegen

        // Schnee oberhalb der Höhe h0 im Spaltenbereich [wx0, wx1) aufnehmen; was über
        // "keep" hinausgeht, wird vor dem Schild in Richtung dir abgelegt (Schneepflug).
        void Plow(Surface s, float wx0, float wx1, float h0, int dir, ref float load, float keep, bool spillOver, float limit = float.MaxValue)
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
                if (j < 0 || j >= s.N) { if (spillOver) spill += part; else left += part; continue; }
                float room = Math.Max(0f, Math.Min(limit, s.Cap2[j]) - s.H[j]);
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
        void Fill(Surface s, float wx, ref float load, float limit = float.MaxValue)
        {
            int c = Math.Max(0, Math.Min(s.N - 1, (int)wx - s.X0));
            for (int r = 0; r < s.N && load > 0.01f; r++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    if (r == 0 && side > 0) continue;
                    int j = c + side * r;
                    if (j < 0 || j >= s.N) continue;
                    float add = Math.Min(load, Math.Max(0f, Math.Min(limit, s.Cap2[j]) - s.H[j]) * 0.5f);
                    s.H[j] += add; load -= add;
                    if (s.H[j] > s.Peak) s.Peak = s.H[j];
                }
            }
        }

        // Überschüssigen Schnee über den Bildschirmrand hinausschieben (verschwindet aus dem Bild).
        void PushOut(int dir, float y, float amount)
        {
            float edge = dir > 0 ? W : 0;
            int n = Math.Min(400, (int)(amount / (8f * S * S)) + 1);
            for (int i = 0; i < n; i++)
                AddDebris(edge - dir * Rnd() * 10f * S, y - Rnd() * 8f * S,
                          dir * (90f + Rnd() * 120f) * S, -(20f + Rnd() * 60f) * S, Rnd() * 0.5f, Rnd() < 0.4f);
        }

        // Schnee als herabfallende Brocken freisetzen.
        void Spill(float x, float y, float amount, float vx, float vy)
        {
            int n = Math.Min(900, (int)(amount / (5f * S * S)) + 1);
            for (int i = 0; i < n; i++)
                AddDebris(x + (Rnd() - 0.5f) * 8f * S, y - Rnd() * 6f * S,
                          vx + (Rnd() - 0.5f) * 40f * S, vy + (Rnd() - 0.6f) * 40f * S, Rnd() * 0.08f, Rnd() < 0.35f);
        }

        // ---- Schaufel

        public void ShovelStep(float x, float y, float px, float py, bool down, ref float load)
        {
            ShovelOn = true; shX = x; shY = y;
            shVx = x - px; shVy = y - py; shDown = false;
            if (x - px > 0.5f) shDir = 1; else if (x - px < -0.5f) shDir = -1;
            if (down)
            {
                // Berührt das Blatt gerade Schnee bzw. eine Fläche? (für die Animation)
                for (int k = 0; k < surfs.Count && !shDown; k++)
                {
                    var s = surfs[k];
                    int col = (int)x - s.X0;
                    if (col < 0 || col >= s.N || !s.Vis[col]) continue;
                    float bse = s.Base(col);
                    if (y >= bse - s.H[col] - 1f && y <= bse + 8f * S) shDown = true;
                }
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
                        float bse = s.Base(col), top = bse - s.H[col];
                        if (qy < top - 1f || qy > bse + 8f * S) continue;
                        float h0 = bse - qy;
                        if (h0 < 3f * S) h0 = 0f;   // Blatt am Boden: sauber bis unten räumen
                        Plow(s, qx - bw, qx + bw, h0, sdx, ref load, cap, true);
                    }
                }
            }
            shLoad = load;
        }

        public void DropLoad(float x, float y, ref float load, float vx, float vy)
        {
            if (load > 0.5f)
            {
                Spill(x, y - 3f * S, load, vx, vy);
                shAngV -= 520f;   // Wurf: Blatt schnellt nach oben
            }
            load = 0; shLoad = 0;
        }

        // Neigung der gehaltenen Schaufel: gedämpfte Feder auf einen Zielwinkel je nach Tätigkeit.
        public void ShovelAnimate(float dt)
        {
            if (dt <= 0f) return;
            float inv = 1f / Math.Max(dt, 0.005f);
            float vx = shVx * inv / S, vy = shVy * inv / S;   // Pixel pro Sekunde (DPI-bereinigt)
            float target;
            if (shDown)
            {
                if (vy < -160f) target = -24f;                // hochziehen: Blatt kippt nach hinten, Ladung bleibt drauf
                else target = 2f + Math.Min(4f, Math.Abs(vx) * 0.004f);   // flach am Boden schieben
                shBob += Math.Abs(shVx) * 0.45f / S;
            }
            else
            {
                target = shLoad > 1f ? -16f : 12f;            // mit Ladung waagrecht halten, leer locker hängen lassen
                target -= Math.Max(-16f, Math.Min(16f, vx * shDir * 0.012f));   // hinterherschwingen
                target += Math.Max(-12f, Math.Min(12f, vy * 0.01f));
                shBob *= 0.9f;
            }
            float acc = (target - shAng) * 210f - shAngV * 15f;
            shAngV += acc * dt;
            shAng += shAngV * dt;
            float lo = Art.ShovelStep0, hi = Art.ShovelStep0 + (Art.ShovelAngles - 1) * Art.ShovelStepDeg;
            if (shAng < lo) { shAng = lo; shAngV = 0; } else if (shAng > hi) { shAng = hi; shAngV = 0; }
        }

        public void SetMarker(float x, float y) { MarkerOn = true; mkX = x; mkY = y; }

        float ParkX { get { if (parkHome) parkX = W - 120f * S; return parkX; } }

        // Oberste Schneeoberfläche bei x, die nicht über y liegt.
        float TopBelow(float x, float y)
        {
            float best = ground.Top(x);
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                if (s == ground) continue;
                int col = (int)x - s.X0;
                if (col < 0 || col >= s.N || !s.Vis[col]) continue;
                float t = s.Base(col) - s.H[col];
                if (t >= y && t < best) best = t;
            }
            return best;
        }

        float ParkTarget()
        {
            float x = ParkX;
            return (parkHome ? ground.Top(x) : TopBelow(x, parkHint)) + 4f * S;
        }

        void StepPark(float dt)
        {
            float tgt = ParkTarget();
            if (!parkInit) { parkInit = true; parkY = tgt; parkVy = 0; }
            if (parkY < tgt - 0.5f)
            {
                // fällt (oder sticht beim Abstellen ein)
                parkVy += 2600f * S * dt;
                parkY += parkVy * dt;
                if (parkY >= tgt)
                {
                    parkY = tgt;
                    if (parkVy > 250f * S) Puff(ParkX, tgt - 4f * S, Math.Min(18, (int)(parkVy / (40f * S))));
                    parkVy = 0;
                }
            }
            else { parkY = tgt; parkVy = 0; }
            if (!parkHome) parkHint = parkY - 6f * S;
        }

        // Kleine Schneewolke beim Einstechen und Herausziehen.
        void Puff(float x, float y, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float side = Rnd() < 0.5f ? -1f : 1f;
                AddDebris(x + side * (3f + Rnd() * 6f) * S, y - Rnd() * 3f * S,
                          side * (30f + Rnd() * 70f) * S, -(60f + Rnd() * 110f) * S, Rnd() * 0.05f, Rnd() < 0.25f);
            }
        }

        // Schaufel an der Mausposition in den Schnee stecken.
        public void PlantShovel(float x, float y)
        {
            parkHome = false;
            parkX = Math.Max(10f * S, Math.Min(W - 10f * S, x));
            parkHint = y - 2f * S;
            float tgt = TopBelow(parkX, parkHint) + 4f * S;
            parkY = Math.Min(tgt, y + 30f * S) - 16f * S;   // aus der Hand heraus einstechen
            parkVy = 380f * S;
            parkInit = true;
        }

        public void ShovelHome()
        {
            parkHome = true;
            parkInit = false;
        }

        public void ShovelPickedUp()
        {
            Puff(ParkX, parkY - 4f * S, 10);
            shAng = -30f; shAngV = 0;   // wird mit Schwung aus dem Schnee gezogen
        }

        // Großzügige Klickfläche um die abgestellte Schaufel.
        public Rectangle ShovelParkRect()
        {
            var a = shovelStand;
            float y = parkInit ? parkY : ParkTarget();
            int bw = (int)Math.Max(a.W + 24 * S, 46 * S);
            int top = (int)(y - a.H - 10 * S), bottom = (int)(y + 8 * S);
            return new Rectangle(B.X + (int)ParkX - bw / 2, B.Y + top, bw, bottom - top);
        }

        // ---- Roboter

        bool RobotSurface(Surface s)
        {
            return s == ground || (s.Kind == 1 && s.N >= (RF + RB) * 3);
        }

        public void RobotPlace(Robot r)
        {
            Bot = r;
            r.Surf = ground; r.State = Robot.Idle;
            r.X = W * 0.18f; r.Yb = ground.Y;
        }

        public void RobotDrop(Robot r, float x)
        {
            Bot = r;
            r.Surf = null; r.State = Robot.Fall; r.Vy = 0;
            r.X = Math.Max(RB + 2, Math.Min(W - RF - 2, x));
            r.Yb = -2f;
            r.Runs.Clear(); r.Load = 0; r.Blade = false;
        }

        public bool RobotVisible()
        {
            var b = Bot;
            if (b == null || b.State == Robot.Gone) return false;
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

        public bool RobotFree { get { var b = Bot; return b != null && b.Surf != null && (b.State == Robot.Idle || b.State == Robot.Travel || b.State == Robot.Push); } }

        // Anklicken: aktuelle Fläche zum Rand hin räumen.
        public void Poke()
        {
            var b = Bot;
            if (!RobotFree) return;
            HopTo(b.Surf, b.X, true);
        }

        // Roboter zu einer angeklickten Stelle schicken; dort räumt er die Fläche.
        public void SendTo(float x, float y)
        {
            var b = Bot;
            if (!RobotFree) { b.PendingSend = true; b.PendingX = x; b.PendingY = y; return; }
            Surface best = null;
            float bd = float.MaxValue;
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                if (!RobotSurface(s)) continue;
                int col = (int)x - s.X0;
                if (col < 0 || col >= s.N || !s.Vis[col]) continue;
                float top = s.Base(col) - s.H[col];
                if (y < top - 50f * S || y > s.Y + 30f * S) continue;
                float d = Math.Abs(y - top);
                if (d < bd) { bd = d; best = s; }
            }
            if (best == null) best = ground;
            HopTo(best, x, true);
        }

        void HopTo(Surface s, float x, bool clean)
        {
            var b = Bot;
            if (b.Load > 0.5f && b.Surf != null) Fill(b.Surf, b.X + b.Dir * RF, ref b.Load);
            b.Load = 0;
            b.H0x = b.X; b.H0y = b.Yb;
            b.H1x = Math.Max(s.X0 + RF + 1, Math.Min(s.X1 - RF - 1, x));
            b.H1y = s.Top(b.H1x);
            float dist = Math.Abs(b.H1x - b.H0x) + Math.Abs(b.H1y - b.H0y);
            b.HDur = 0.45f + dist / (1500f * S);
            b.HArc = 40f * S + Math.Max(0f, b.H0y - b.H1y) * 0.6f + dist * 0.05f;
            b.HT = 0;
            b.HopSurf = s;
            b.CleanAfter = clean;
            if (Math.Abs(b.H1x - b.H0x) > 1) b.Dir = b.H1x > b.H0x ? 1 : -1;
            b.State = Robot.Hop; b.Blade = false;
            b.Runs.Clear();
        }

        // Schnee einer Fläche zu den Rändern schieben: linke Hälfte nach links, rechte nach rechts.
        // Am Boden entstehen große Haufen am Bildschirmrand, von Fenstern fällt der Schnee über die Kante.
        void StartClean(Surface s)
        {
            var b = Bot;
            b.Surf = s;
            b.Runs.Clear();
            b.Spill = s != ground;
            float gap = s == ground ? EdgeHeap : 0f;
            float minX = s.X0 + RF + 1, maxX = s.X1 - RF - 1;     // Schild darf bis an den Rand
            if (maxX > minX)
            {
                int mid = s.N / 2;
                int lastL = -1, firstR = -1;
                for (int j = 0; j < mid; j++) if (s.H[j] > 0.6f) lastL = j;
                for (int j = s.N - 1; j >= mid; j--) if (s.H[j] > 0.6f) firstR = j;
                int lastLeftCol = -1;
                for (int j = mid - 1; j >= 0; j--) if (s.H[j] > 0.6f) { lastLeftCol = j; break; }
                int firstRightCol = -1;
                for (int j = mid; j < s.N; j++) if (s.H[j] > 0.6f) { firstRightCol = j; break; }
                // linke Hälfte: von innen nach links schieben
                if (lastLeftCol >= 0)
                {
                    var r = new Run();
                    r.Dir = -1;
                    r.Start = Math.Max(minX, Math.Min(maxX, s.X0 + lastLeftCol + RF + 2));
                    r.End = Math.Max(minX, Math.Min(maxX, s.X0 + gap + RF));
                    if (r.Start > r.End + 3) b.Runs.Add(r);
                }
                // rechte Hälfte: von innen nach rechts schieben
                if (firstRightCol >= 0)
                {
                    var r = new Run();
                    r.Dir = 1;
                    r.Start = Math.Max(minX, Math.Min(maxX, s.X0 + firstRightCol - RF - 2));
                    r.End = Math.Max(minX, Math.Min(maxX, s.X1 - gap - RF));
                    if (r.End > r.Start + 3) b.Runs.Add(r);
                }
                if (b.Runs.Count == 2 && Math.Abs(b.Runs[1].Start - b.X) < Math.Abs(b.Runs[0].Start - b.X)) b.Runs.Reverse();
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
            else
            {
                b.State = Robot.Idle; b.IdleT = 0; b.Blade = false;
                if (b.PendingSend) { b.PendingSend = false; SendTo(b.PendingX, b.PendingY); }
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
                    if (b.PendingSend) { b.PendingSend = false; SendTo(b.PendingX, b.PendingY); }
                    else if (RobotAuto && b.IdleT > 5f) { b.IdleT = 0; if (!AutoJob()) b.NoJob = true; }
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
                    Plow(s, Math.Min(front, back), Math.Max(front, back), 0f, b.Dir, ref b.Load, 0f, b.Spill, b.Spill ? float.MaxValue : HeapMax);
                    b.Yb += (s.Base(Math.Max(0, Math.Min(s.N - 1, (int)b.X - s.X0))) - b.Yb) * follow;
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
                        else
                        {
                            // Am Bildschirmrand nur ein kleiner Hügel, der Rest wird hinausgeschoben
                            float amt = b.Load;
                            int c0 = b.Dir > 0 ? (int)front - s.X0 : 0, c1 = b.Dir > 0 ? s.N : (int)front - s.X0 + 1;
                            for (int j = Math.Max(0, c0); j < Math.Min(s.N, c1); j++)
                                if (s.H[j] > HeapMax) { amt += s.H[j] - HeapMax; s.H[j] = HeapMax; }
                            b.Load = 0;
                            if (amt > 0.5f) PushOut(b.Dir, s.Y - HeapMax, amt);
                        }
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
                        if (b.AfterHopPipe) { b.AfterHopPipe = false; b.State = Robot.GoPipe; b.Dir = pipeMouthRight ? -1 : 1; }
                        else if (b.CleanAfter) StartClean(b.Surf);
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
                        float top = s.Base(xi - s.X0) - s.H[xi - s.X0];
                        if (yPrev <= top + 1 && b.Yb >= top && top < bt) { bt = top; landed = s; }
                    }
                    if (landed == null && b.Yb > Hh + 40) { landed = ground; bt = ground.Top(b.X); }
                    if (landed != null)
                    {
                        b.Surf = landed;
                        b.Yb = bt;
                        b.X = Math.Max(landed.X0 + RF + 1, Math.Min(landed.X1 - RF - 1, b.X));
                        b.State = Robot.Idle; b.IdleT = 0;
                        if (b.AfterHopPipe)
                        {
                            if (landed == ground) { b.AfterHopPipe = false; b.State = Robot.GoPipe; b.Dir = pipeMouthRight ? -1 : 1; }
                            else { HopTo(ground, pipeX + (pipeMouthRight ? RF + 30f * S : -RF - 30f * S), false); b.AfterHopPipe = true; }
                        }
                    }
                    break;
                }

                case Robot.GoPipe:
                {
                    // zur Röhre fahren und hinein
                    float step = 115f * S * dt;
                    b.Dir = pipeMouthRight ? -1 : 1;
                    b.X += b.Dir * step; b.Anim += step;
                    b.Yb += (ground.Top(b.X) - b.Yb) * follow;
                    bool inside = pipeMouthRight ? b.X + RB < pipeX - 2f * S : b.X - RB > pipeX + 2f * S;
                    if (inside && pipeAnim >= 1f) { b.State = Robot.Gone; RobotGone = true; }
                    break;
                }

                case Robot.Exit:
                {
                    if (pipeAnim < 1f) break;
                    float step = 90f * S * dt;
                    b.X += b.Dir * step; b.Anim += step;
                    b.Yb += (ground.Top(b.X) - b.Yb) * follow;
                    bool outside = b.Dir > 0 ? b.X - RB > pipeX + 10f * S : b.X + RB < pipeX - 10f * S;
                    if (outside)
                    {
                        b.State = Robot.Idle; b.IdleT = 0;
                        pipeCloseT = 0.8f;
                        if (b.PendingSend) { b.PendingSend = false; SendTo(b.PendingX, b.PendingY); }
                        else if (b.PendingAuto) { b.PendingAuto = false; if (!AutoJob()) b.NoJob = true; }
                    }
                    break;
                }
            }
        }

        // Automatik: die Fläche mit dem meisten Schnee räumen.
        bool AutoJob()
        {
            Surface best = null;
            float bestAmt = 0;
            for (int k = 0; k < surfs.Count; k++)
            {
                var s = surfs[k];
                float amt = JobAmount(s);
                if (amt > bestAmt) { bestAmt = amt; best = s; }
            }
            if (best == null) return false;
            if (best == Bot.Surf) StartClean(best);
            else HopTo(best, (best.X0 + best.X1) / 2f, true);
            return true;
        }

        float JobAmount(Surface s)
        {
            if (!RobotSurface(s) || !s.Vis[s.N / 2]) return 0;
            float amt;
            if (s == ground)
            {
                int r = (int)(EdgeHeap + 220f * S);   // Randhaufen nicht mitzählen
                amt = s.Sum(r, s.N - r);
                return amt >= 2.2f * S * (s.N - 2 * r) ? amt : 0;
            }
            amt = s.Sum(0, s.N);
            return amt >= 2.2f * S * s.N ? amt : 0;
        }

        // für die App: lohnt sich ein Besuch auf diesem Monitor?
        public float JobScore()
        {
            float best = 0;
            for (int k = 0; k < surfs.Count; k++) best = Math.Max(best, JobAmount(surfs[k]));
            return best;
        }

        // ---- Röhre (Monitorwechsel)

        void OpenPipe(float x, bool mouthRight)
        {
            pipeX = Math.Max(140f * S, Math.Min(W - 140f * S, x));
            pipeMouthRight = mouthRight;
            if (pipeState == 0) pipeAnim = 0;
            pipeState = 1;
            pipeCloseT = 0;
        }

        void StepPipe(float dt)
        {
            if (pipeState == 1)
            {
                pipeAnim = Math.Min(1f, pipeAnim + dt * 2.2f);
                if (pipeCloseT > 0) { pipeCloseT -= dt; if (pipeCloseT <= 0) pipeState = 2; }
            }
            else if (pipeState == 2)
            {
                pipeAnim -= dt * 2.2f;
                if (pipeAnim <= 0) { pipeAnim = 0; pipeState = 0; }
            }
        }

        // Roboter verlässt diesen Monitor in Richtung 'side' (+1 rechts, -1 links).
        public void SendAway(int side)
        {
            var b = Bot;
            if (b == null) return;
            b.Runs.Clear(); b.Blade = false;
            float px = b.X + side * 140f * S;
            if (px > W - 140f * S || px < 140f * S) px = b.X - side * 140f * S;
            // Öffnung zeigt zum Roboter
            OpenPipe(px, px < b.X);
            if (b.State == Robot.Fall || b.State == Robot.Hop) { b.AfterHopPipe = true; return; }
            if (b.Surf != ground)
            {
                HopTo(ground, pipeX + (pipeMouthRight ? RF + 30f * S : -RF - 30f * S), false);
                b.AfterHopPipe = true;
                return;
            }
            if (b.Load > 0.5f) Fill(ground, b.X, ref b.Load);
            b.State = Robot.GoPipe;
        }

        // Roboter kommt aus einer Röhre auf diesem Monitor heraus.
        public void Receive(Robot r, int side, float nearX)
        {
            Bot = r;
            RobotGone = false;
            r.Surf = ground; r.Runs.Clear(); r.Blade = false; r.Load = 0;
            // fährt zur Bildmitte hin heraus
            bool mouthRight = side < 0;
            OpenPipe(float.IsNaN(nearX) ? (side < 0 ? 200f * S : W - 200f * S) : nearX, mouthRight);
            r.Dir = mouthRight ? 1 : -1;
            r.X = mouthRight ? pipeX - RF - 2f * S : pipeX + RF + 2f * S;
            r.Yb = ground.Top(r.X);
            r.State = Robot.Exit;
        }

        public void ClosePipe() { if (pipeState == 1) pipeCloseT = 0.6f; }

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
                int x0 = Math.Max(0, w.R.Left - B.Left);
                int x1 = Math.Min(W, w.R.Right - B.Left);
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
                    if (h - add > 0.2f) Spill(x, t.Base(tc) - t.H[tc] - 1, h - add, 0, -20 * S);
                }
            }
        }

        Surface NewSurface(long key, int kind, int x0, int x1, int y, float maxH)
        {
            return new Surface(key, kind, x0, x1, y, maxH, true, 2.5f, kind == 1 ? 8f * S : 0f);
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
                    var n = NewSurface(key, kind, x0, x1, y, maxH);
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
            s = NewSurface(key, kind, x0, x1, y, maxH);
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
                else if (oy1 <= line && oy1 > s.Y - s.MaxH * 4f - 2)
                {
                    for (int x = a; x < b; x++)
                        if (s.ClipY[x - s.X0] < oy1) s.ClipY[x - s.X0] = oy1;
                }
            }
        }

        // Schnee eines verschwundenen/bewegten Fensters fällt herunter: die obere Schicht
        // bröckelt zuerst ab, unten liegende Klumpen folgen kurz danach.
        void Release(Surface s)
        {
            if (s.Peak <= 0.3f) return;
            float vx0 = wind * 0.5f;
            for (int i = 0; i < s.N; i += 2)
            {
                if (!s.Vis[i]) continue;
                float h = s.H[i];
                int k = (int)(h / (3.2f * S) + Rnd());
                float bse = s.Base(i);
                for (int j = 0; j < k; j++)
                {
                    float depth = Rnd();                          // 0 = Oberfläche, 1 = ganz unten
                    bool clump = Rnd() < 0.3f + 0.4f * depth;
                    AddDebris(s.X0 + i + Rnd() * 2f, bse - h + depth * h,
                              vx0 + (Rnd() - 0.5f) * 36f * S, (Rnd() * 30f - 8f) * S,
                              depth * 0.14f + Rnd() * 0.1f, clump);
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
            for (int k = 0; k < dirty.Count; k++)
            {
                Rectangle r = dirty[k];
                int x0 = Math.Max(0, r.X), x1 = Math.Min(W, r.Right), y0 = Math.Max(0, r.Y), y1 = Math.Min(Hh, r.Bottom);
                if (x1 <= x0) continue;
                for (int y = y0; y < y1; y++)
                {
                    uint* p = bits + (long)y * W + x0;
                    uint* e = p + (x1 - x0);
                    while (p < e) *p++ = 0;
                }
            }
            var tmp = dirtyPrev; dirtyPrev = dirty; dirty = tmp; dirty.Clear();

            for (int k = 0; k < surfs.Count; k++) DrawPile(bits, surfs[k]);
            for (int i = 0; i < ndb; i++) Blit(bits, db[i].X, db[i].Y, db[i].Cls, db[i].Alpha);
            for (int i = 0; i < nfl; i++) Blit(bits, fl[i].X, fl[i].Y, fl[i].Cls, fl[i].Alpha);
            if (ShovelParked) DrawArt(bits, shovelStand, (int)ParkX, (int)(parkInit ? parkY : ParkTarget()), int.MaxValue);
            if (Bot != null && RobotVisible()) DrawRobot(bits);
            if (pipeState != 0) DrawPipe(bits);
            if (ShovelOn) DrawShovel(bits);
            if (MarkerOn) DrawArt(bits, markerArt, (int)mkX, (int)mkY, int.MaxValue);
        }

        void DrawRobot(uint* bits)
        {
            var b = Bot;
            int frame = ((int)(b.Anim / (3f * S))) & 1;
            var a = robotArt[(b.Dir > 0 ? 0 : 4) + (b.Blade ? 2 : 0) + frame];
            int x = (int)Math.Round(b.X), y = (int)Math.Round(b.Yb);
            DrawArt(bits, a, x, y, int.MaxValue);
            float period = b.Busy ? 0.5f : 1.6f;
            if ((b.Blink % period) < period * 0.35f)
                DrawArt(bits, b.Busy ? lampAmber : lampRed, x - a.AX + (int)a.LX, y - a.AY + (int)a.LY, int.MaxValue);
        }

        void DrawPipe(uint* bits)
        {
            var a = pipeArt[pipeMouthRight ? 1 : 0];
            float e = pipeAnim * pipeAnim * (3f - 2f * pipeAnim);
            int gy = ground.Y;
            int y = gy + (int)((1f - e) * a.H);
            DrawArt(bits, a, (int)pipeX, y, gy);   // wächst aus dem Boden
        }

        void DrawShovel(uint* bits)
        {
            int k = (int)Math.Round((shAng - Art.ShovelStep0) / Art.ShovelStepDeg);
            k = Math.Max(0, Math.Min(Art.ShovelAngles - 1, k));
            var a = shovelArt[(shDir > 0 ? 0 : Art.ShovelAngles) + k];
            int x = (int)Math.Round(shX);
            int y = (int)Math.Round(shY - (shDown ? Math.Abs(Math.Sin(shBob)) * 1.4f * S : 0f));   // Schaben über den Boden
            DrawArt(bits, a, x, y, int.MaxValue);
            if (shLoad > 1f)
            {
                // Schnee auf dem Schaufelblatt
                float mh = Math.Min(11f * S, (float)Math.Sqrt(shLoad) * 0.7f);
                float hw = 9f * S;
                int cx = x + (int)(shDir * 0.5f * S), by = y - (int)(2.4f * S);
                float tilt = (float)Math.Sin((Art.ShovelStep0 + k * Art.ShovelStepDeg) * Math.PI / 180.0) * shDir;
                int x0 = (int)(cx - hw), x1 = (int)(cx + hw);
                for (int px = x0; px <= x1; px++)
                {
                    if (px < 0 || px >= W) continue;
                    float t = (px - cx) / hw;
                    float h = mh * (float)Math.Sqrt(Math.Max(0f, 1f - t * t));
                    float byc = by + (px - cx) * tilt;   // Blatt ist geneigt
                    float top = byc - h;
                    for (int py = (int)Math.Floor(top); py < byc; py++)
                    {
                        if (py < 0 || py >= Hh) continue;
                        float cov = Math.Min(1f, py + 1 - top);
                        if (cov <= 0) continue;
                        int al = (int)(cov * 250);
                        int c = 250 - (int)(20f * (py - top) / Math.Max(1f, h));
                        bits[(long)py * W + px] = ((uint)al << 24) | ((uint)(c * al / 255) << 16) | ((uint)((c + 3) * al / 255) << 8) | (uint)(255 * al / 255);
                    }
                }
                int ty = (int)(hw * Math.Abs(tilt)) + 1;
                dirty.Add(new Rectangle(x0, (int)(by - mh) - 2 - ty, x1 - x0 + 1, (int)mh + 3 + 2 * ty));
            }
        }

        void DrawArt(uint* bits, Art a, int x, int y, int clipBottom)
        {
            Blend(bits, a.Px, a.W, a.H, x - a.AX, y - a.AY, 256, clipBottom);
        }

        void DrawPile(uint* bits, Surface s)
        {
            if (s.Peak < 0.15f) return;
            for (int i = 0; i < s.N; i++)
            {
                if (!s.Vis[i]) continue;
                float h = s.H[i];
                if (h < 0.15f) continue;
                int x = s.X0 + i;
                float bse = s.Y + (int)(s.Off[i] + 0.5f);
                int maxY = Math.Min(Hh, (int)bse);
                float top = bse - h;
                int y0 = (int)Math.Floor(top);
                int yc = (int)Math.Ceiling(s.ClipY[i]);
                if (y0 < yc) y0 = yc;
                if (y0 < 0) y0 = 0;
                uint hsh = (uint)x * 2654435761u;
                int tex = (int)((hsh >> 27) & 7) - 3;
                float invH = 1f / Math.Max(h, 1f);
                // weicher Schatten über der Oberkante: Kontur auf hellem Hintergrund
                int ys = (int)Math.Floor(top) - 1;
                if (ys >= 0 && ys >= yc && ys < maxY)
                {
                    float sa = Math.Min(1f, h) * 38f * (1f - Math.Max(0f, Math.Min(1f, ys + 1 - top)));
                    int a = (int)sa;
                    if (a > 0 && bits[(long)ys * W + x] == 0)
                        bits[(long)ys * W + x] = ((uint)a << 24) | ((uint)(70 * a / 255) << 16) | ((uint)(80 * a / 255) << 8) | (uint)(100 * a / 255);
                }
                uint* p = bits + (long)y0 * W + x;
                for (int y = y0; y < maxY; y++, p += W)
                {
                    float cov = y + 1 - top;
                    if (cov <= 0f) continue;
                    if (cov > 1f) cov = 1f;
                    float d = (y + 0.5f - top) * invH;
                    if (d < 0f) d = 0f; else if (d > 1f) d = 1f;
                    float sh = d * d;
                    int cr = 255 - (int)(44 * sh) + tex, cg = 255 - (int)(32 * sh) + tex, cb = 255 - (int)(12 * sh);
                    if (y == maxY - 1 && h > 1.5f) { cr -= 18; cg -= 14; cb -= 6; }
                    if (cr > 255) cr = 255; if (cg > 255) cg = 255; if (cb > 255) cb = 255;
                    int al = (int)(cov * 245f);
                    *p = ((uint)al << 24) | ((uint)(cr * al / 255) << 16) | ((uint)(cg * al / 255) << 8) | (uint)(cb * al / 255);
                }
            }
            int ry = (int)(s.Y - s.Peak) - 2;
            dirty.Add(new Rectangle(s.X0, ry, s.N, s.Y + 10 - ry));
        }

        void Blit(uint* bits, float fx, float fy, int cls, float alpha)
        {
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            int sx = (int)((fx - ix) * 4f), sy = (int)((fy - iy) * 4f);
            if (sx > 3) sx = 3; if (sy > 3) sy = 3;
            int S2 = Sprites.Size[cls], off = Sprites.Off[cls];
            Blend(bits, Sprites.Data[cls * 16 + sy * 4 + sx], S2, S2, ix - off, iy - off, (int)(alpha * 256f), int.MaxValue);
        }

        // premultiplied "over"-Blending eines Sprites in den Puffer
        void Blend(uint* bits, uint[] spr, int sw, int sh, int dx, int dy, int al, int clipBottom)
        {
            int maxY = Math.Min(Hh, clipBottom);
            if (dx >= W || dy >= maxY || dx + sw <= 0 || dy + sh <= 0) return;
            int px0 = Math.Max(0, -dx), py0 = Math.Max(0, -dy);
            int px1 = Math.Min(sw, W - dx), py1 = Math.Min(sh, maxY - dy);
            fixed (uint* sp = spr)
            {
                for (int py = py0; py < py1; py++)
                {
                    uint* row = bits + (long)(dy + py) * W + dx;
                    uint* src = sp + py * sw;
                    for (int px = px0; px < px1; px++)
                    {
                        uint s = src[px];
                        if (s == 0) continue;
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
            }
            dirty.Add(new Rectangle(dx, dy, sw, sh));
        }

        // Rechtecke, die im letzten Render() gezeichnet wurden (für die Teil-Aktualisierung)
        public List<Rectangle> Drawn { get { return dirty; } }

        public void ClearAll()
        {
            Ov.Clear();
            dirty.Clear(); dirtyPrev.Clear();
            Ov.Present(dirty);
        }

        public void Dispose() { Ov.Dispose(); }

        // ---- Selbsttest (--selftest): prüft Popup-Schieben, Schaufel, Roboter und Röhre ohne echte Fenster

        static string Ok(bool b) { return b ? "OK" : "FEHLER"; }

        void StepN(int frames) { for (int k = 0; k < frames; k++) Step(1f / 30f, k / 30f); }

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
            fresh.Add(pop.H);
            w.UpdateSurfaces(new List<WinInfo> { pop }, fresh, icons, 0, 48);
            Surface ps;
            w.map.TryGetValue(4242, out ps);
            float onPopup = ps != null ? ps.Sum(0, ps.N) : -1, leftOnGround = g.Sum(1001, 1399);
            sb.AppendLine(string.Format("Popup: Schnee auf Popup = {0:F0} px², unter Popup am Boden = {1:F0} px² -> {2}", onPopup, leftOnGround, Ok(onPopup > 3500 && leftOnGround < 1)));
            sb.AppendLine(string.Format("  Ecke: Kante bei Spalte 0 um {0:F1} px abgesenkt, Spalte 20 um {1:F1} px -> {2}", ps.Off[0], ps.Off[20], Ok(ps.Off[0] > 4 && ps.Off[20] == 0)));
            fresh.Clear();
            w.UpdateSurfaces(none, fresh, icons, 0, 48);
            int n0 = w.ndb;
            w.StepN(3);
            int still = 0;
            for (int i = 0; i < w.ndb; i++) if (w.db[i].Delay > 0) still++;
            sb.AppendLine(string.Format("Popup zu: {0} Brocken, nach 0,1 s noch {1} gestaffelt in Wartestellung -> {2}", n0, still, Ok(n0 > 100 && still > 0 && still < n0)));
            w.StepN(120);
            sb.AppendLine(string.Format("  nach 4 s: am Boden unter dem Popup wieder {0:F0} px² -> {1}", g.Sum(990, 1410), Ok(g.Sum(990, 1410) > 1500)));

            // 2) Schaufel schiebt Schnee nach rechts
            Array.Clear(g.H, 0, g.N); w.ndb = 0;
            for (int j = 300; j < 700; j++) g.H[j] = 6f;
            g.Peak = 6f;
            float load = 0;
            float y = g.Y - 1;
            for (int x = 290; x <= 720; x += 6) w.ShovelStep(x, y, x - 6, y, true, ref load);
            float cleared = g.Sum(320, 690);
            sb.AppendLine(string.Format("Schaufel: Rest auf der Strecke = {0:F0} px², vorne + auf Schaufel = {1:F0} px² -> {2}",
                cleared, g.Sum(700, 900) + load, Ok(cleared < 50 && g.Sum(700, 900) + load > 2000)));
            w.ShovelStep(720, 900, 720, y, true, ref load);
            float carried = load;
            w.DropLoad(720, 900, ref load, 200, -100);
            sb.AppendLine(string.Format("  hochgehoben {0:F0} px², geworfen als {1} Brocken -> {2}", carried, w.ndb, Ok(carried > 50 && w.ndb > 10)));

            // 2b) Schaufel auf einem Fenster abstellen; Fenster weg -> sie fällt auf den Boden
            {
                w.ndb = 0;
                var pw = new WinInfo(); pw.H = new IntPtr(5151); pw.Surface = true;
                pw.R.Left = 400; pw.R.Top = 500; pw.R.Right = 900; pw.R.Bottom = 800;
                w.UpdateSurfaces(new List<WinInfo> { pw }, fresh, icons, 0, 48);
                w.ShovelParked = true;
                w.PlantShovel(600, 480);
                w.StepN(15);
                float onWin = w.parkY;
                Rectangle hit = w.ShovelParkRect();
                w.UpdateSurfaces(none, fresh, icons, 0, 48);
                w.StepN(30);
                float fell = w.parkY;
                sb.AppendLine(string.Format("Schaufel abgestellt: auf Fenster y={0:F0}, nach Schließen y={1:F0} (Boden {2}), Klickfläche {3}×{4} -> {5}",
                    onWin, fell, g.Y, hit.Width, hit.Height,
                    Ok(Math.Abs(onWin - 504) < 3 && Math.Abs(fell - (g.Top(600) + 4)) < 3 && hit.Width >= 44 && hit.Height >= 60 && hit.Contains(600, 470))));
                w.ShovelParked = false;
                w.ShovelHome();
            }

            // 3) Roboter räumt den Boden zu Haufen an beiden Rändern
            Array.Clear(g.H, 0, g.N); w.ndb = 0;
            var rnd = new Random(3);
            for (int j = 0; j < g.N; j++) g.H[j] = 3f + (float)rnd.NextDouble() * 2f;
            for (int j = g.N - 40; j < g.N; j++) g.H[j] = 90f;   // alter, zu großer Randhaufen
            g.Peak = 90f;
            var r = new Robot();
            w.RobotPlace(r);
            r.X = 700;
            float total = g.Sum(0, g.N);
            w.Poke();
            int steps = 0;
            for (; steps < 30 * 180; steps++) { w.Step(1f / 30f, steps / 30f); if (steps > 30 && r.State == Robot.Idle) break; }
            float leftHeap = g.Sum(0, 300), rightHeap = g.Sum(g.N - 300, g.N), all = g.Sum(0, g.N);
            { var dbg = new StringBuilder("  Verteilung:"); for (int q = 0; q < g.N; q += 120) dbg.Append(" " + g.Sum(q, q + 120).ToString("F0")); sb.AppendLine(dbg.ToString()); }
            float maxH = 0;
            for (int j = 0; j < g.N; j++) maxH = Math.Max(maxH, g.H[j]);
            sb.AppendLine(string.Format("Roboter: fertig nach {0:F1} s, links {1:P0} / rechts {2:P0} des Schnees am Rand, Hügelhöhe max. {3:F0} px -> {4}",
                steps / 30f, leftHeap / all, rightHeap / all, maxH, Ok((leftHeap + rightHeap) / all > 0.9f && leftHeap > 0.3f * all && rightHeap > 0.3f * all && maxH <= w.HeapMax + 1f)));
            sb.AppendLine(string.Format("  Schnee vorher {0:F0} px², nachher {1:F0} px² (Rest aus dem Bild geschoben) -> {2}", total, all, Ok(all < total * 0.85f)));

            // 4) Roboter auf einem Fenster schiebt den Schnee über beide Kanten
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
            sb.AppendLine(string.Format("Roboter auf Fenster: Rest {0:F0} px² von {1:F0} -> {2}", ws.Sum(0, ws.N), 4f * ws.N, Ok(ws.Sum(0, ws.N) < 0.1f * 4f * ws.N)));

            // 5) Röhre: Roboter wechselt auf einen zweiten Monitor
            w.UpdateSurfaces(none, fresh, icons, 0, 48);
            r.Surf = g; r.X = 800; r.Yb = g.Y; r.State = Robot.Idle;
            var w2 = new World(new Rectangle(1920, 0, 1920, 1080), new Rectangle(1920, 0, 1920, 1080), 1f, 2);
            w.SendAway(1);
            for (steps = 0; steps < 30 * 20 && !w.RobotGone; steps++) w.Step(1f / 30f, steps / 30f);
            bool gone = w.RobotGone;
            w.Bot = null; w.ClosePipe();
            w2.Receive(r, -1, float.NaN);
            for (steps = 0; steps < 30 * 20 && r.State != Robot.Idle; steps++) w2.Step(1f / 30f, steps / 30f);
            sb.AppendLine(string.Format("Röhre: hineingefahren = {0}, auf Monitor 2 herausgekommen = {1} (x={2:F0}) -> {3}",
                gone, r.State == Robot.Idle, r.X, Ok(gone && r.State == Robot.Idle && r.Surf == w2.ground)));

            w.Dispose(); w2.Dispose();
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

    // 1 Roboter angeklickt, 2 Ziel gewählt, 3 Werkzeug beenden, 4 Schaufel aufgenommen
    struct HookEvt { public int Kind; public POINT Pt; }

    // Low-Level-Maushook auf eigenem Thread, damit der Cursor nie auf die Render-Schleife wartet.
    sealed class MouseHook
    {
        public volatile int Tool;            // 0 keins, 1 Schaufel, 2 Roboter hinschicken
        public volatile bool LeftDown;
        public volatile bool NeedsDesktop;
        public readonly ConcurrentQueue<HookEvt> Events = new ConcurrentQueue<HookEvt>();
        readonly object lk = new object();
        Rectangle robotRect, shovelRect;
        bool robotActive, shovelActive, swallowLUp, swallowRUp, pickPress;
        POINT downPt;
        uint downTime;
        Thread th;
        uint tid;
        N.LLProc proc;
        IntPtr hook;

        public bool Running { get { return th != null; } }

        public void SetTargets(Rectangle robot, bool robotOn, Rectangle shovel, bool shovelOn)
        {
            lock (lk) { robotRect = robot; robotActive = robotOn; shovelRect = shovel; shovelActive = shovelOn; }
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
                            if (tool == 1) { LeftDown = true; pickPress = false; downPt = info.pt; downTime = info.time; return new IntPtr(1); }
                            if (tool == 2) { Enqueue(2, info.pt); swallowLUp = true; return new IntPtr(1); }
                            if (Hit(info.pt, true)) { Tool = 1; LeftDown = true; pickPress = true; Enqueue(4, info.pt); return new IntPtr(1); }
                            if (Hit(info.pt, false)) { Enqueue(1, info.pt); swallowLUp = true; return new IntPtr(1); }
                            break;
                        case 0x0202: // LBUTTONUP
                            if (tool == 1)
                            {
                                LeftDown = false;
                                // kurzer Klick ohne Ziehen = Schaufel hier abstellen (nicht beim Aufnehmen-Klick selbst)
                                if (pickPress) pickPress = false;
                                else if (Math.Abs(info.pt.X - downPt.X) <= 6 && Math.Abs(info.pt.Y - downPt.Y) <= 6 && info.time - downTime < 450)
                                    Enqueue(5, info.pt);
                                return new IntPtr(1);
                            }
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

        bool Hit(POINT p, bool shovel)
        {
            lock (lk)
            {
                if (shovel ? (!shovelActive || !shovelRect.Contains(p.X, p.Y)) : (!robotActive || !robotRect.Contains(p.X, p.Y))) return false;
            }
            if (NeedsDesktop)
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
            N.UnregisterHotKey(Handle, 2);
            DestroyHandle();
        }
    }

    // ------------------------------------------------------------ Updates

    // Prüft GitHub-Releases. Ein Release braucht die Assets "Snowglobe.exe" und "Snowglobe.exe.sha256".
    static class Updater
    {
        public const string Repo = "JRSDev-at/snowglobe";

        public static Version Current
        {
            get { var v = Assembly.GetExecutingAssembly().GetName().Version; return new Version(v.Major, v.Minor, v.Build); }
        }

        public static string ExePath { get { return Application.ExecutablePath; } }

        // Läuft im Hintergrund. Gibt bei Erfolg den Pfad der heruntergeladenen, geprüften neuen Version zurück.
        public static string Check(out string message, out Version found)
        {
            found = null;
            message = null;
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            var json = Get("https://api.github.com/repos/" + Repo + "/releases/latest");
            if (json == null) { message = "Update-Server nicht erreichbar (ist das Repository öffentlich?)"; return null; }
            var rel = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            string tag = Convert.ToString(rel["tag_name"]).TrimStart('v', 'V');
            Version v;
            if (!Version.TryParse(tag, out v)) { message = "Unbekanntes Versionsformat: " + tag; return null; }
            v = new Version(v.Major, v.Minor, Math.Max(0, v.Build));
            found = v;
            if (v <= Current) { message = "Snowglobe " + Current + " ist aktuell."; return null; }

            string exeUrl = null, shaUrl = null;
            var assets = rel["assets"] as System.Collections.ArrayList;
            if (assets != null)
                foreach (Dictionary<string, object> a in assets)
                {
                    string name = Convert.ToString(a["name"]);
                    if (name == "Snowglobe.exe") exeUrl = Convert.ToString(a["browser_download_url"]);
                    if (name == "Snowglobe.exe.sha256") shaUrl = Convert.ToString(a["browser_download_url"]);
                }
            if (exeUrl == null || shaUrl == null) { message = "Release " + v + " hat keine Snowglobe.exe + Prüfsumme."; return null; }

            string target = Path.Combine(Path.GetDirectoryName(ExePath), "Snowglobe.new.exe");
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "Snowglobe-Updater";
                string sha = wc.DownloadString(shaUrl).Trim().Split(' ', '\t', '\r', '\n')[0].ToLowerInvariant();
                wc.DownloadFile(exeUrl, target);
                string actual;
                using (var f = File.OpenRead(target))
                using (var h = SHA256.Create())
                    actual = BitConverter.ToString(h.ComputeHash(f)).Replace("-", "").ToLowerInvariant();
                if (actual != sha) { File.Delete(target); message = "Prüfsumme des Updates stimmt nicht – verworfen."; return null; }
            }
            var nv = AssemblyName.GetAssemblyName(target).Version;
            if (nv.Major != v.Major || nv.Minor != v.Minor || nv.Build != v.Build) { File.Delete(target); message = "Versionsnummer im Update passt nicht – verworfen."; return null; }
            message = "Snowglobe " + v + " wird installiert …";
            return target;
        }

        static string Get(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Snowglobe-Updater";
                req.Accept = "application/vnd.github+json";
                req.Timeout = 15000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch { return null; }
        }

        // Laufende EXE umbenennen (unter Windows erlaubt), neue an ihre Stelle, neu starten.
        public static bool Install(string newExe)
        {
            string exe = ExePath, old = exe + ".old";
            try
            {
                if (File.Exists(old)) File.Delete(old);
                File.Move(exe, old);
                File.Move(newExe, exe);
                Process.Start(exe, "--updated");
                return true;
            }
            catch
            {
                try { if (!File.Exists(exe) && File.Exists(old)) File.Move(old, exe); } catch { }
                return false;
            }
        }

        public static void Cleanup()
        {
            try { string old = ExePath + ".old"; if (File.Exists(old)) File.Delete(old); } catch { }
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
        readonly SynchronizationContext ui;
        readonly Dictionary<IntPtr, string> classCache = new Dictionary<IntPtr, string>();
        readonly Dictionary<IntPtr, int[]> insets = new Dictionary<IntPtr, int[]>();
        readonly Dictionary<IntPtr, double> insetTime = new Dictionary<IntPtr, double>();
        List<WinInfo> wins = new List<WinInfo>(), scan = new List<WinInfo>();
        HashSet<IntPtr> fresh = new HashSet<IntPtr>();
        List<RECT> icons = new List<RECT>();
        long winSig, iconSig;
        float iconTopOffset, iconSizePx = 48f;
        double last, nextUpdateCheck;
        int frame, density, tool;
        bool paused, locked, excludeCapture, background, robotShown, shovelShown, autoUpdate, updating, needSurfaceUpdate = true, shovelTipShown;
        IntPtr deskHost;
        float shovelLoad, shVelX, shVelY;
        POINT prevCur;
        bool prevDown;
        World robotWorld, shovelWorld;
        ToolStripMenuItem[] densityItems;
        ToolStripMenuItem pauseItem, captureItem, autostartItem, backgroundItem, shovelItem, sendItem, robotShowItem, robotAutoItem, updateItem;
        Icon trayIcon;
        // Performance-Messung (--perf)
        long tSim, tDraw, tPresent, tScan; int perfFrames;

        public App(bool forceCapture, bool justUpdated)
        {
            ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            enumProc = EnumCallback;
            Sprites.Build();
            using (var k = Registry.CurrentUser.CreateSubKey(RegKey))
            {
                density = Math.Max(0, Math.Min(2, Convert.ToInt32(k.GetValue("Density", 1))));
                excludeCapture = Convert.ToInt32(k.GetValue("ExcludeFromCapture", 1)) != 0;
                background = Convert.ToInt32(k.GetValue("Background", 0)) != 0;
                robotShown = Convert.ToInt32(k.GetValue("Robot", 1)) != 0;
                shovelShown = Convert.ToInt32(k.GetValue("Shovel", 0)) != 0;
                autoUpdate = Convert.ToInt32(k.GetValue("AutoUpdate", 1)) != 0;
                World.RobotAuto = Convert.ToInt32(k.GetValue("RobotAuto", 0)) != 0;
            }
            if (forceCapture) excludeCapture = false;
            if (Program.ForceAuto) { World.RobotAuto = true; robotShown = true; }

            BuildWorlds();
            BuildTray();

            hotkeys.Pressed = OnHotkey;
            hotkeys.Register(2, 0x0002 | 0x0001, (uint)Keys.R); // Strg+Alt+R: Roboter hinschicken
            UpdateHook();

            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            SystemEvents.SessionSwitch += OnSessionSwitch;

            Updater.Cleanup();
            if (justUpdated) tray.ShowBalloonTip(4000, "Snowglobe aktualisiert", "Jetzt auf Version " + Updater.Current + ".", ToolTipIcon.None);
            nextUpdateCheck = 60;   // erste Prüfung eine Minute nach dem Start

            timer.Interval = 33;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        void BuildWorlds()
        {
            foreach (var w in worlds) w.Dispose();
            worlds.Clear(); own.Clear();
            robotWorld = null; shovelWorld = null;
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
                if (sc.Primary)
                {
                    shovelWorld = w;
                    if (robotShown) { w.RobotPlace(robot); robotWorld = w; }
                }
            }
            if (shovelWorld == null) shovelWorld = worlds[0];
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

        string CachedClass(IntPtr h)
        {
            string c;
            if (!classCache.TryGetValue(h, out c)) { c = N.ClassOf(h); classCache[h] = c; }
            return c;
        }

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
            string cls = CachedClass(h);
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
            wi.H = h;
            wi.Big = zoomed || full;
            wi.Surface = !wi.Big && w >= 80 && hh >= 40 && (st & 0x40000000) == 0 && cls != "tooltips_class32";
            // Popups ohne Titelleiste haben oft einen unsichtbaren Rand: echte Kante am Bildschirm messen
            if (wi.Surface && (st & 0x00C00000) != 0x00C00000)
            {
                int[] ins;
                double now = sw.Elapsed.TotalSeconds, t0;
                if (!insets.TryGetValue(h, out ins) || (insetTime.TryGetValue(h, out t0) && t0 > 0 && now - t0 > 0.4))
                {
                    ins = ScreenProbe.Inset(r);
                    bool first = !insets.ContainsKey(h);
                    insets[h] = ins;
                    insetTime[h] = first ? now : -1;   // einmal nach der Öffnungs-Animation nachmessen
                }
                r.Left += ins[0]; r.Top += ins[1]; r.Right -= ins[2];
            }
            wi.R = r;
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
            var alive = new HashSet<IntPtr>();
            foreach (var w in scan)
            {
                alive.Add(w.H);
                RECT o;
                if (!prev.TryGetValue(w.H, out o) || o.Left != w.R.Left || o.Top != w.R.Top || o.Right != w.R.Right || o.Bottom != w.R.Bottom)
                    fresh.Add(w.H);
            }
            // Caches für geschlossene Fenster aufräumen
            if (insets.Count > 0)
            {
                var dead = new List<IntPtr>();
                foreach (var h in insets.Keys) if (!alive.Contains(h)) dead.Add(h);
                foreach (var h in dead) { insets.Remove(h); insetTime.Remove(h); }
            }
            if (classCache.Count > 2000) classCache.Clear();
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
            if (autoUpdate && !updating && now > nextUpdateCheck) { nextUpdateCheck = now + 6 * 3600; CheckForUpdate(false); }
            if (paused || locked) return;
            frame++;

            long t0 = Stopwatch.GetTimestamp();
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
            long t1 = Stopwatch.GetTimestamp();

            UpdateTools(dt);
            UpdateRobotTravel();
            foreach (var w in worlds) w.ShovelParked = shovelShown && tool != 1 && w == shovelWorld;

            long tSimLocal = 0, tDrawLocal = 0, tPresLocal = 0;
            foreach (var w in worlds)
            {
                if (w.Hidden || w.Covered) continue;
                long a = Stopwatch.GetTimestamp();
                w.Step(dt, (float)now);
                long b = Stopwatch.GetTimestamp();
                w.Render();
                long c = Stopwatch.GetTimestamp();
                w.Ov.Present(w.Drawn);
                long d = Stopwatch.GetTimestamp();
                tSimLocal += b - a; tDrawLocal += c - b; tPresLocal += d - c;
            }

            if (hook.Running)
            {
                bool rAct = robotWorld != null && !robotWorld.Hidden && !robotWorld.Covered && robotWorld.RobotVisible();
                bool sAct = shovelShown && tool == 0 && shovelWorld != null && !shovelWorld.Hidden && !shovelWorld.Covered;
                hook.SetTargets(rAct ? robotWorld.RobotRect() : Rectangle.Empty, rAct, sAct ? shovelWorld.ShovelParkRect() : Rectangle.Empty, sAct);
                hook.NeedsDesktop = !WantTopmost;
            }

            if (Program.Perf)
            {
                tScan += t1 - t0; tSim += tSimLocal; tDraw += tDrawLocal; tPresent += tPresLocal; perfFrames++;
                if (perfFrames == 150)
                {
                    double f = 1000.0 / Stopwatch.Frequency / perfFrames;
                    File.AppendAllText(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "perf.txt"),
                        string.Format("{0:HH:mm:ss} ms/Frame: Scan {1:F2}  Simulation {2:F2}  Zeichnen {3:F2}  Present {4:F2}  GPU={5} Kacheln {6}/{7}\r\n", DateTime.Now, tScan * f, tSim * f, tDraw * f, tPresent * f,
                            worlds[0].Ov.OnGpu, worlds[0].Ov.LastTiles, worlds[0].Ov.TotalTiles)
                        + string.Format("    Map {0:F2}  Kopieren {1:F2}  DXGI-Present {2:F2}\r\n", Overlay.TGet * f, Overlay.TUpload * f, Overlay.TPresent * f));
                    Overlay.TGet = Overlay.TUpload = Overlay.TPresent = 0;
                    tScan = tSim = tDraw = tPresent = 0; perfFrames = 0;
                }
            }
        }

        // Roboter zwischen Monitoren: Röhre hinein, auf dem Ziel-Monitor wieder heraus.
        void UpdateRobotTravel()
        {
            if (robotWorld == null) return;
            if (robotWorld.RobotGone)
            {
                var src = robotWorld;
                var tgt = robot.Target;
                if (tgt == null || !worlds.Contains(tgt)) tgt = src;
                src.RobotGone = false;
                src.Bot = null;
                src.ClosePipe();
                int side = Math.Sign((tgt.B.X + tgt.B.Width / 2) - (src.B.X + src.B.Width / 2));
                if (side == 0) side = 1;
                tgt.Receive(robot, -side, robot.PendingSend ? robot.PendingX : float.NaN);
                robot.Target = null;
                robotWorld = tgt;
                return;
            }
            if (robot.NoJob)
            {
                robot.NoJob = false;
                if (!World.RobotAuto) return;
                World best = null;
                float bestScore = 0;
                foreach (var w in worlds)
                {
                    if (w == robotWorld || w.Hidden || w.Covered) continue;
                    float s = w.JobScore();
                    if (s > bestScore) { bestScore = s; best = w; }
                }
                if (best != null) SendRobotTo(best, true, float.NaN, float.NaN);
            }
        }

        void SendRobotTo(World tgt, bool auto, float lx, float ly)
        {
            var src = robotWorld;
            robot.Target = tgt;
            robot.PendingAuto = auto;
            robot.PendingSend = !auto;
            robot.PendingX = lx; robot.PendingY = ly;
            int side = Math.Sign((tgt.B.X + tgt.B.Width / 2) - (src.B.X + src.B.Width / 2));
            src.SendAway(side == 0 ? 1 : side);
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
                float vx = (cp.X - prevCur.X) / Math.Max(dt, 0.01f), vy = (cp.Y - prevCur.Y) / Math.Max(dt, 0.01f);
                shVelX = shVelX * 0.55f + vx * 0.45f; shVelY = shVelY * 0.55f + vy * 0.45f;
                float speed = (float)Math.Sqrt(shVelX * shVelX + shVelY * shVelY) / wc.S;
                if (prevDown && !down)
                {
                    // Taste losgelassen nach Ziehen: Ladung abwerfen
                    wc.DropLoad(lx, ly, ref shovelLoad, vx * 0.6f, vy * 0.6f);
                    wc.ShovelStep(lx, ly, lx, ly, true, ref shovelLoad);
                }
                else if (!down && shovelLoad > 40f * wc.S * wc.S && speed > 1500f)
                {
                    // Schwungvolle Bewegung mit voller Schaufel: Schnee fliegt in Bewegungsrichtung
                    wc.DropLoad(lx, ly, ref shovelLoad, shVelX * 0.55f, shVelY * 0.55f);
                    wc.ShovelStep(lx, ly, lx, ly, true, ref shovelLoad);
                }
                // In der Hand ist die Schaufel immer aktiv, keine Taste nötig
                else wc.ShovelStep(lx, ly, prevCur.X - wc.B.X, prevCur.Y - wc.B.Y, true, ref shovelLoad);
                wc.ShovelAnimate(dt);
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
                else if (e.Kind == 3)
                {
                    // Rechtsklick mit Schaufel: zurück an ihren Platz unten rechts am Hauptmonitor
                    if (tool == 1) { shovelWorld = PrimaryWorld(); shovelWorld.ShovelHome(); }
                    SetTool(0);
                }
                else if (e.Kind == 4)
                {
                    prevCur = e.Pt;
                    shVelX = shVelY = 0;
                    if (shovelWorld != null) shovelWorld.ShovelPickedUp();
                    SetTool(1, true);
                    if (!shovelTipShown)
                    {
                        shovelTipShown = true;
                        tray.ShowBalloonTip(3000, "Schaufel in der Hand", "Einfach durch den Schnee fahren, keine Taste nötig. Mit Schwung wirft sie die Ladung. Linksklick stellt die Schaufel ab, Rechtsklick bringt sie zurück an ihren Platz.", ToolTipIcon.None);
                    }
                }
                else if (e.Kind == 5 && tool == 1)
                {
                    var w = WorldAt(e.Pt.X, e.Pt.Y);
                    if (w != null)
                    {
                        SetTool(0);
                        shovelWorld = w;
                        w.PlantShovel(e.Pt.X - w.B.X, e.Pt.Y - w.B.Y);
                    }
                }
            }
        }

        void SendRobot(POINT p)
        {
            var w = WorldAt(p.X, p.Y);
            if (w == null) return;
            if (!robotShown) SetRobotShown(true);
            float lx = p.X - w.B.X, ly = p.Y - w.B.Y;
            if (robotWorld != w) SendRobotTo(w, false, lx, ly);
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
                string cls = CachedClass(fg);
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

        void SetTool(int t, bool keepDown = false)
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
            if (!keepDown) hook.LeftDown = false;
            prevDown = false;
            if (t != 0 && paused) TogglePause();
            UpdateHook();
            ApplyZOrder(true);
            UpdateCovered();
            if (t == 2) tray.ShowBalloonTip(3000, "Schneeroboter", "Klick auf eine Stelle: Der Roboter fährt hin (auch auf andere Monitore) und schiebt den Schnee dort an den Rand. Rechtsklick bricht ab.", ToolTipIcon.None);
        }

        void OnHotkey(int id)
        {
            if (id == 2) SetTool(tool == 2 ? 0 : 2);
        }

        void UpdateHook()
        {
            bool need = tool != 0 || robotShown || shovelShown;
            if (need && !hook.Running) hook.Start();
            else if (!need && hook.Running) hook.Stop();
        }

        void SetRobotShown(bool on)
        {
            robotShown = on;
            SaveSetting("Robot", on ? 1 : 0);
            if (on && robotWorld == null)
            {
                var w = shovelWorld ?? worlds[0];
                robotWorld = w;
                w.RobotDrop(robot, w.B.Width * 0.18f);
            }
            else if (!on && robotWorld != null)
            {
                robotWorld.Bot = null;
                robotWorld.ClosePipe();
                robotWorld = null;
            }
            UpdateHook();
        }

        World PrimaryWorld()
        {
            var p = Screen.PrimaryScreen.Bounds;
            foreach (var w in worlds) if (w.B == p) return w;
            return worlds[0];
        }

        void SetShovelShown(bool on)
        {
            shovelShown = on;
            if (on && tool != 1) { shovelWorld = PrimaryWorld(); shovelWorld.ShovelHome(); }
            SaveSetting("Shovel", on ? 1 : 0);
            if (!on && tool == 1) SetTool(0);
            UpdateHook();
            if (on) tray.ShowBalloonTip(3000, "Schaufel bereit", "Die Schaufel steht unten rechts am Hauptmonitor. Linksklick nimmt sie in die Hand, ein weiterer Linksklick stellt sie dort ab.", ToolTipIcon.None);
        }

        // ---- Updates

        void CheckForUpdate(bool manual)
        {
            if (updating) return;
            updating = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                string msg = null, path = null;
                Version found;
                try { path = Updater.Check(out msg, out found); }
                catch (Exception ex) { msg = "Update fehlgeschlagen: " + ex.Message; }
                ui.Post(delegate
                {
                    updating = false;
                    if (path != null)
                    {
                        tray.ShowBalloonTip(3000, "Snowglobe-Update", msg, ToolTipIcon.None);
                        if (Updater.Install(path)) Quit();
                        else tray.ShowBalloonTip(4000, "Snowglobe-Update", "Konnte die Datei nicht ersetzen (Schreibrechte?).", ToolTipIcon.Warning);
                    }
                    else if (manual && msg != null) tray.ShowBalloonTip(3000, "Snowglobe-Update", msg, ToolTipIcon.None);
                }, null);
            });
        }

        // ---- Tray

        void BuildTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("Snowglobe " + Updater.Current + " ❄") { Enabled = false });
            menu.Items.Add(new ToolStripSeparator());
            densityItems = new ToolStripMenuItem[3];
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                densityItems[i] = new ToolStripMenuItem("Schnee: " + DensityName[i], null, delegate { SetDensity(idx); });
                menu.Items.Add(densityItems[i]);
            }
            menu.Items.Add(new ToolStripSeparator());
            shovelItem = new ToolStripMenuItem("Schaufel bereitstellen", null, delegate { SetShovelShown(!shovelShown); });
            menu.Items.Add(shovelItem);
            var robotMenu = new ToolStripMenuItem("Schneeroboter");
            sendItem = new ToolStripMenuItem("Hinschicken …", null, delegate { SetTool(tool == 2 ? 0 : 2); });
            sendItem.ShortcutKeyDisplayString = "Strg+Alt+R";
            robotShowItem = new ToolStripMenuItem("Anzeigen", null, delegate { SetRobotShown(!robotShown); });
            robotAutoItem = new ToolStripMenuItem("Automatik (räumt alle Monitore)", null, delegate
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
            var upd = new ToolStripMenuItem("Updates");
            updateItem = new ToolStripMenuItem("Automatisch aktualisieren", null, delegate { autoUpdate = !autoUpdate; SaveSetting("AutoUpdate", autoUpdate ? 1 : 0); });
            upd.DropDownItems.Add(updateItem);
            upd.DropDownItems.Add(new ToolStripMenuItem("Jetzt nach Updates suchen", null, delegate { CheckForUpdate(true); }));
            menu.Items.Add(upd);
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
            shovelItem.Checked = shovelShown;
            sendItem.Checked = tool == 2;
            robotShowItem.Checked = robotShown;
            robotAutoItem.Checked = World.RobotAuto;
            updateItem.Checked = autoUpdate;
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
        public static bool ForceAuto, Perf, NoGpu;

        [STAThread]
        static void Main(string[] args)
        {
            try { N.SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }

            bool capture = false, dump = false, updated = false;
            foreach (var a in args)
            {
                if (a == "--capture") capture = true;
                if (a == "--dump") dump = true;
                if (a == "--fast") World.DepositBoost = 8f;
                if (a == "--auto") ForceAuto = true;
                if (a == "--perf") Perf = true;
                if (a == "--nogpu") NoGpu = true;
                if (a == "--updated") updated = true;
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
                if (!created)
                {
                    // nach einem Update kurz warten, bis die alte Version beendet ist
                    if (!updated) return;
                    try { if (!mutex.WaitOne(15000)) return; }
                    catch (AbandonedMutexException) { }
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new App(capture, updated));
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
