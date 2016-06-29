using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace OverParse
{
    public static class WindowsServices
    {
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int GWL_EXSTYLE = (-20);
 
        public static string GetActiveWindowTitle()
        {
            int chars = 256;
            StringBuilder buff = new StringBuilder(chars);
            IntPtr handle = NativeMethods.GetForegroundWindow();
            if (NativeMethods.GetWindowTextW(handle, buff, chars) > 0)
                return buff.ToString();
            return null;
        }

        public static void SetWindowExTransparent(IntPtr hwnd)
        {
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
        }

        public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            if (IntPtr.Size == 8)
                return NativeMethods.GetWindowLongPtrW(hWnd, nIndex);
            else
                return new IntPtr(NativeMethods.GetWindowLongW(hWnd, nIndex));
        }

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong)
        {
            return SetWindowLongPtr(hWnd, nIndex, new IntPtr(dwNewLong));
        }

        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8)
                return NativeMethods.SetWindowLongPtrW(hWnd, nIndex, dwNewLong);
            else
                return new IntPtr(NativeMethods.SetWindowLongW(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        internal static IntPtr GetForegroundWindow()
        {
            return NativeMethods.GetForegroundWindow();
        }

        internal static void SetForegroundWindow(IntPtr wasActive)
        {
            NativeMethods.SetForegroundWindow(wasActive);
        }

        internal static int GetWindowLong(IntPtr hwndcontainer, int gWL_EXSTYLE)
        {
            return GetWindowLong(hwndcontainer, gWL_EXSTYLE);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowLongW(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr GetWindowLongPtrW(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetWindowLongPtrW(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.DLL", CharSet = CharSet.Unicode, SetLastError = false)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}