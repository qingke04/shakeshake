using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace ShakeShake
{
    /// <summary>
    /// Enlarges the real Windows cursors by loading the user's own cursor files at a bigger
    /// size and swapping them in with SetSystemCursor(); restores with SPI_SETCURSORS.
    /// </summary>
    internal sealed class CursorEngine
    {
        private sealed class Entry
        {
            public uint Id;
            public string SchemeValue;
            public Entry(uint id, string value) { Id = id; SchemeValue = value; }
        }

        // Cursor ids that have a file in a normal Windows scheme.
        private static readonly Entry[] Entries = new Entry[]
        {
            new Entry(32512, "Arrow"),     // OCR_NORMAL
            new Entry(32649, "Hand"),      // OCR_HAND
            new Entry(32651, "Help"),      // OCR_HELP
            new Entry(32648, "No"),        // OCR_NO
            new Entry(32646, "SizeAll"),   // OCR_SIZEALL
            new Entry(32645, "SizeNS"),    // OCR_SIZENS
            new Entry(32644, "SizeWE"),    // OCR_SIZEWE
            new Entry(32642, "SizeNWSE"),  // OCR_SIZENWSE
            new Entry(32643, "SizeNESW"),  // OCR_SIZENESW
            new Entry(32516, "UpArrow"),   // OCR_UP
            new Entry(32671, "Pin"),
            new Entry(32672, "Person"),
        };

        private readonly object _gate = new object();
        private readonly Dictionary<uint, string> _scheme = new Dictionary<uint, string>();

        public bool IsEnlarged { get; private set; }
        public string LastError { get; private set; }

        private string MarkerPath
        {
            get { return Path.Combine(Settings.Dir, "enlarged.marker"); }
        }

        public CursorEngine()
        {
            LoadScheme();
        }

        private void LoadScheme()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Cursors", false))
                {
                    if (k == null) return;
                    for (int i = 0; i < Entries.Length; i++)
                    {
                        object v = k.GetValue(Entries[i].SchemeValue);
                        string path = v as string;
                        if (string.IsNullOrEmpty(path)) continue;
                        path = Environment.ExpandEnvironmentVariables(path.Trim());
                        if (!File.Exists(path)) continue;
                        if (path.EndsWith(".ani", StringComparison.OrdinalIgnoreCase)) continue; // animated: skip
                        _scheme[Entries[i].Id] = path;
                    }
                }

                // If the scheme has no arrow file, fall back to the stock Aero cursor.
                if (!_scheme.ContainsKey(32512))
                {
                    string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Cursors", "aero_arrow.cur");
                    if (File.Exists(fallback)) _scheme[32512] = fallback;
                }
            }
            catch (Exception ex)
            {
                Log.Write("scheme load failed: " + ex.Message);
            }
        }

        public int MeasureBaselinePx()
        {
            int px = Native.SystemArrowPx();
            return px > 0 ? px : 32;
        }

        /// <summary>Swap in bigger cursors. Returns false if nothing could be changed.</summary>
        public bool ApplyFrame(int px)
        {
            lock (_gate)
            {
                LastError = null;
                if (px < 16) px = 16;

                if (_scheme.Count == 0) LoadScheme();
                if (_scheme.Count == 0)
                {
                    LastError = "no cursor files found";
                    return false;
                }

                int applied = 0;
                foreach (Entry e in Entries)
                {
                    string path;
                    if (!_scheme.TryGetValue(e.Id, out path)) continue;
                    IntPtr h = Native.LoadImage(IntPtr.Zero, path, Native.IMAGE_CURSOR, px, px, Native.LR_LOADFROMFILE);
                    if (h == IntPtr.Zero) continue;
                    if (Native.SetSystemCursor(h, e.Id)) applied++;
                    else Native.DestroyCursor(h);
                }

                if (applied == 0)
                {
                    LastError = "SetSystemCursor failed for all cursors";
                    return false;
                }

                if (!IsEnlarged)
                {
                    IsEnlarged = true;
                    WriteMarker();
                }
                NudgeCursor();
                return true;
            }
        }

        /// <summary>Reload the user's normal cursor scheme.</summary>
        public void Restore()
        {
            lock (_gate)
            {
                if (!IsEnlarged) return;
                Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, Native.SPIF_SENDCHANGE);
                IsEnlarged = false;
                DeleteMarker();
                NudgeCursor();
            }
        }

        /// <summary>Force the window under the pointer to re-apply its cursor right now.</summary>
        private static void NudgeCursor()
        {
            try
            {
                Native.POINT p;
                if (!Native.GetCursorPos(out p)) return;
                IntPtr hwnd = Native.WindowFromPoint(p);
                if (hwnd == IntPtr.Zero) return;
                IntPtr unused;
                IntPtr lp = (IntPtr)((Native.WM_MOUSEMOVE << 16) | Native.HTCLIENT);
                Native.SendMessageTimeout(hwnd, (uint)Native.WM_SETCURSOR, hwnd, lp, Native.SMTO_ABORTIFHUNG, 40, out unused);
            }
            catch
            {
                // cosmetic only
            }
        }

        private void WriteMarker()
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                File.WriteAllText(MarkerPath,
                    "enlarged at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { }
        }

        private void DeleteMarker()
        {
            try { if (File.Exists(MarkerPath)) File.Delete(MarkerPath); }
            catch { }
        }

        /// <summary>
        /// If a previous run died while enlarged, put the normal cursors back on start-up.
        /// </summary>
        public static void RestoreLeftovers()
        {
            try
            {
                string marker = Path.Combine(Settings.Dir, "enlarged.marker");
                if (!File.Exists(marker)) return;
                Native.SystemParametersInfo(Native.SPI_SETCURSORS, 0, IntPtr.Zero, Native.SPIF_SENDCHANGE);
                File.Delete(marker);
                Log.Write("restored leftover enlarged cursor from a previous run");
            }
            catch (Exception ex)
            {
                Log.Write("RestoreLeftovers failed: " + ex.Message);
            }
        }
    }
}
