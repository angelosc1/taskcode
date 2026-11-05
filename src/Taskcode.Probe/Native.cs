using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Taskcode.Probe
{
    internal static class Native
    {
        internal const int GwlStyle = -16;
        internal const long Child = 0x40000000L, Popup = 0x80000000L;
        internal static readonly IntPtr Top = IntPtr.Zero, TopMost = new IntPtr(-1);
        internal const uint NoActivate = 0x10, FrameChanged = 0x20;
        [StructLayout(LayoutKind.Sequential)] internal struct Rect
        {
            internal int Left, Top, Right, Bottom;
            internal Rectangle Bounds { get { return Rectangle.FromLTRB(Left, Top, Right, Bottom); } }
        }
        [StructLayout(LayoutKind.Sequential)] internal struct Point { internal int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct WindowSize { internal int Width, Height; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct Blend { internal byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destination, IntPtr location, ref WindowSize size, IntPtr source, ref Point sourcePoint, uint key, ref Blend blend, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetParent(IntPtr hwnd, IntPtr parent);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetStyle(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)] internal static extern IntPtr SetStyle(IntPtr hwnd, int index, IntPtr value);
        [DllImport("user32.dll")] internal static extern bool ScreenToClient(IntPtr hwnd, ref Point point);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
