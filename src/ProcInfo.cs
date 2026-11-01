using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ShakeShake
{
    /// <summary>Resolves the executable name of a window / the foreground window.</summary>
    internal static class ProcInfo
    {
        public static string OwnExeName
        {
            get
            {
                try { return Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName); }
                catch { return "ShakeShake.exe"; }
            }
        }

        public static string ForegroundName()
        {
            return NameOfWindow(Native.GetForegroundWindow());
        }

        public static string NameOfWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return null;
            uint pid;
            Native.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return null;

            IntPtr hp = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hp == IntPtr.Zero) return null;
            try
            {
                StringBuilder sb = new StringBuilder(1024);
                uint size = 1024;
                if (!Native.QueryFullProcessImageName(hp, 0, sb, ref size)) return null;
                return Path.GetFileName(sb.ToString());
            }
            finally
            {
                Native.CloseHandle(hp);
            }
        }
    }
}
