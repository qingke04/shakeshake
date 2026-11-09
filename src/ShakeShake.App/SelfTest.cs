using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ShakeShake
{
    /// <summary>
    /// Headless end-to-end check used during development: installs the hook, injects a real
    /// mouse shake, verifies the cursor got bigger, stops, verifies it went back.
    /// </summary>
    internal static class SelfTest
    {
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint PM_REMOVE = 0x0001;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public Native.POINT pt;
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage(ref MSG lpMsg);

        private static void Pump()
        {
            MSG msg;
            while (PeekMessage(out msg, IntPtr.Zero, 0, 0, PM_REMOVE))
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }

        public static int Run()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "shakeshake-selftest.log");
            StringBuilder sb = new StringBuilder();
            Action<string> log = delegate(string msg)
            {
                sb.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg);
                Console.WriteLine(msg);
            };

            try { File.Delete(logPath); } catch { }
            log("selftest start; os=" + Environment.OSVersion.VersionString + " 64bit=" + Environment.Is64BitProcess);

            int screenW = Native.GetSystemMetrics(Native.SM_CXSCREEN);
            int screenH = Native.GetSystemMetrics(Native.SM_CYSCREEN);
            int baseline = Native.SystemArrowPx();
            log("screen=" + screenW + "x" + screenH + " baselineArrowPx=" + baseline);

            Settings settings = new Settings();
            settings.Zoom = 3.0;
            settings.Sensitivity = 5;
            settings.RestoreMs = 100;
            settings.Enabled = true;
            settings.Language = "en-US";

            AppController controller = new AppController();
            controller.Apply(settings);
            controller.Start();

            int cx = screenW / 2;
            int cy = screenH / 2;
            Native.SetCursorPos(cx, cy);

            Stopwatch watch = Stopwatch.StartNew();
            int injected = 0;
            int maxSeen = 0;
            long firstBigMs = -1;
            bool started = false;
            bool stopped = false;
            bool pass = false;
            int restored = 0;
            long nextInjectMs = 300;

            while (true)
            {
                Pump();
                Thread.Sleep(2);

                long elapsed = watch.ElapsedMilliseconds;
                if (!started && elapsed > 300)
                {
                    started = true;
                    log("injecting shake at " + cx + "," + cy);
                }

                if (started && !stopped && elapsed >= nextInjectMs && elapsed <= 1600)
                {
                    int x = (injected % 2 == 0) ? cx - 70 : cx + 70;
                    int nx = (int)((long)x * 65535 / Math.Max(1, screenW - 1));
                    int ny = (int)((long)cy * 65535 / Math.Max(1, screenH - 1));
                    Native.mouse_event(Native.MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, nx, ny, 0, UIntPtr.Zero);
                    injected++;
                    nextInjectMs += 25;

                    int px = Native.SystemArrowPx();
                    if (px > maxSeen) maxSeen = px;
                    if (firstBigMs < 0 && px >= baseline * 2) firstBigMs = elapsed;
                }

                if (started && !stopped && elapsed > 1600)
                {
                    stopped = true;
                    log("stopped injecting after " + injected + " moves; maxArrowPx=" + maxSeen +
                        " firstBigAt=" + firstBigMs + "ms");
                }

                if (stopped && elapsed > 2600)
                {
                    restored = Native.SystemArrowPx();
                    log("after 1s idle: arrowPx=" + restored);
                    pass = maxSeen >= baseline * 3 - 6 && restored <= baseline + 8;
                    log(pass ? "RESULT: PASS" : "RESULT: FAIL");
                    break;
                }
            }

            try
            {
                controller.Dispose();
                log("controller disposed; final arrowPx=" + Native.SystemArrowPx());
            }
            catch (Exception ex)
            {
                log("dispose failed: " + ex.Message);
            }

            sb.AppendLine(pass ? "PASS" : "FAIL");
            try { File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false)); } catch { }
            return pass ? 0 : 1;
        }
    }
}
