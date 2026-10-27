using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace SunPet;
public static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    [DllImport("kernel32.dll")] public static extern bool AttachConsole(uint process);
    public static IntPtr Handle(Window w) => new WindowInteropHelper(w).Handle;
    public static void NonActivating(Window w)
    {
        var h = Handle(w);
        var style = GetWindowLongPtr(h, -20).ToInt64() | 0x08000000;
        style = Program.UiCheck ? style & ~0x80L : style | 0x80;
        SetWindowLongPtr(h, -20, new IntPtr(style));
    }
    public static void Move(Window w, int x, int y) => SetWindowPos(Handle(w), IntPtr.Zero, x, y, 0, 0, 0x0015);
    public static void EnsureVisible(Window w, bool reset = false)
    {
        GetWindowRect(Handle(w), out var r);
        int width = r.Right - r.Left, height = r.Bottom - r.Top;
        var screen = reset ? Forms.Screen.PrimaryScreen! : Forms.Screen.FromRectangle(new System.Drawing.Rectangle(r.Left, r.Top, width, height));
        var work = screen.WorkingArea;
        var x = reset ? work.Right - width - 32 : Math.Clamp(r.Left, work.Left, Math.Max(work.Left, work.Right - width));
        var y = reset ? work.Bottom - height - 24 : Math.Clamp(r.Top, work.Top, Math.Max(work.Top, work.Bottom - height));
        Move(w, x, y);
    }
}
