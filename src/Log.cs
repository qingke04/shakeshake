using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ShakeShake
{
    internal static class Log
    {
        private static readonly object Gate = new object();
        private static string _path;

        public static string Path
        {
            get
            {
                if (_path == null) _path = System.IO.Path.Combine(Settings.Dir, "shakeshake.log");
                return _path;
            }
        }

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Settings.Dir);
                    FileInfo fi = new FileInfo(Path);
                    if (fi.Exists && fi.Length > 256 * 1024)
                    {
                        try { File.Delete(Path); } catch { }
                    }
                    File.AppendAllText(Path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // logging must never break the app
            }
        }
    }

    internal static class Clock
    {
        private static readonly double MsPerTick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;

        public static long NowMs
        {
            get { return (long)(System.Diagnostics.Stopwatch.GetTimestamp() * MsPerTick); }
        }
    }
}
