using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ShakeShake
{
    /// <summary>
    /// Headless end-to-end check used during development:
    /// installs the hook, injects a real mouse shake, verifies the cursor got bigger,
    /// stops, verifies it went back.
    /// </summary>
    internal static class SelfTest
    {
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        public static int Run()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "shakeshake-selftest.log");
            StringBuilder sb = new StringBuilder();
            int injected = 0;
            int maxSeen = 0;
            long firstBigMs = -1;
            int baseline = 0;
            int restored = 0;
            bool started = false;
            bool stopped = false;
            bool pass = false;

            Action<string> log = delegate(string msg)
            {
                sb.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg);
                Console.WriteLine(msg);
            };

            try { File.Delete(logPath); } catch { }
            log("selftest start; os=" + Environment.OSVersion.VersionString + " 64bit=" + Environment.Is64BitProcess);

            int sw = Native.GetSystemMetrics(Native.SM_CXSCREEN);
            int sh = Native.GetSystemMetrics(Native.SM_CYSCREEN);
            baseline = Native.SystemArrowPx();
            log("screen=" + sw + "x" + sh + " baselineArrowPx=" + baseline);

            Settings s = new Settings();
            s.Zoom = 3.0;
            s.Sensitivity = 5;
            s.RestoreMs = 100;
            s.Enabled = true;
            s.Language = "en-US";

            AppController c = new AppController();
            c.Apply(s);
            c.Start();

            int cx = sw / 2;
            int cy = sh / 2;
            Native.SetCursorPos(cx, cy);

            ApplicationContext ctx = new ApplicationContext();
            Timer t = new Timer();
            t.Interval = 25;
            long phaseStart = 0;

            t.Tick += delegate
            {
                long now = Clock.NowMs;
                if (phaseStart == 0) phaseStart = now;

                long elapsed = now - phaseStart;

                if (elapsed > 300 && !started)
                {
                    started = true;
                    log("injecting shake at " + cx + "," + cy);
                }

                if (started && !stopped)
                {
                    // Alternate between two screen points: a clean horizontal shake.
                    int x = (injected % 2 == 0) ? cx - 70 : cx + 70;
                    int nx = (int)((long)x * 65535 / Math.Max(1, sw - 1));
                    int ny = (int)((long)cy * 65535 / Math.Max(1, sh - 1));
                    Native.mouse_event(Native.MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, nx, ny, 0, UIntPtr.Zero);
                    injected++;

                    int px = Native.SystemArrowPx();
                    if (px > maxSeen) maxSeen = px;
                    if (firstBigMs < 0 && px >= baseline * 2) firstBigMs = elapsed;

                    if (elapsed > 1600)
                    {
                        stopped = true;
                        log("stopped injecting after " + injected + " moves; maxArrowPx=" + maxSeen + " firstBigAt=" + firstBigMs + "ms");
                    }
                }

                if (stopped && elapsed > 2600)
                {
                    restored = Native.SystemArrowPx();
                    log("after 1s idle: arrowPx=" + restored);
                    pass = maxSeen >= baseline * 3 - 6 && restored <= baseline + 8;
                    log(pass ? "RESULT: PASS" : "RESULT: FAIL");
                    t.Stop();
                    ctx.ExitThread();
                }
            };

            t.Start();
            Application.Run(ctx);

            try
            {
                c.Dispose();
                log("controller disposed; final arrowPx=" + Native.SystemArrowPx());
            }
            catch (Exception ex)
            {
                log("dispose failed: " + ex.Message);
            }

            t.Dispose();
            sb.AppendLine(pass ? "PASS" : "FAIL");
            try { File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false)); } catch { }
            return pass ? 0 : 1;
        }
    }
}


