using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace ShakeShake
{
    /// <summary>
    /// Headless check for the size animation: injects one shake, samples the system arrow size
    /// while it happens and prints the size sequence (so we can see it ramps, not jumps).
    /// </summary>
    internal static class AnimTest
    {
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint PM_REMOVE = 0x0001;

        private sealed class Sample
        {
            public long T;
            public int Px;
        }

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
            string logPath = Path.Combine(Path.GetTempPath(), "shakeshake-animtest.log");
            StringBuilder sb = new StringBuilder();
            List<Sample> samples = new List<Sample>();
            int baseline = Native.SystemArrowPx();

            Action<string> log = delegate(string msg)
            {
                sb.AppendLine(msg);
                Console.WriteLine(msg);
            };

            log("animtest start; baseline=" + baseline);

            int screenW = Native.GetSystemMetrics(Native.SM_CXSCREEN);
            int screenH = Native.GetSystemMetrics(Native.SM_CYSCREEN);
            int cx = screenW / 2;
            int cy = screenH / 2;

            Settings settings = new Settings();
            settings.Zoom = 3.0;
            settings.Sensitivity = 10;
            settings.RestoreMs = 100;
            settings.Enabled = true;

            AppController controller = new AppController();
            controller.Apply(settings);
            controller.Start();
            Native.SetCursorPos(cx, cy);

            Stopwatch watch = Stopwatch.StartNew();
            long nextInjectMs = 300;
            int injected = 0;
            int lastPx = -1;
            long lastSampleMs = -1;

            while (true)
            {
                Pump();
                Thread.Sleep(1);

                long elapsed = watch.ElapsedMilliseconds;
                int px = Native.SystemArrowPx();
                if (px != lastPx)
                {
                    Sample sample = new Sample();
                    sample.T = elapsed;
                    sample.Px = px;
                    samples.Add(sample);
                    lastPx = px;
                    lastSampleMs = elapsed;
                }

                if (elapsed >= nextInjectMs && elapsed <= 700)
                {
                    int x = (injected % 2 == 0) ? cx - 70 : cx + 70;
                    int nx = (int)((long)x * 65535 / Math.Max(1, screenW - 1));
                    int ny = (int)((long)cy * 65535 / Math.Max(1, screenH - 1));
                    Native.mouse_event(Native.MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, nx, ny, 0, UIntPtr.Zero);
                    injected++;
                    nextInjectMs += 25;
                }

                if (elapsed > 2400) break;
            }

            controller.Dispose();

            // compress consecutive equal samples into runs
            List<string> runs = new List<string>();
            for (int i = 0; i < samples.Count; i++)
            {
                long end = i + 1 < samples.Count ? samples[i + 1].T : lastSampleMs;
                runs.Add(string.Format("{0}px x{1}ms", samples[i].Px, end - samples[i].T));
            }

            log("samples=" + samples.Count + " distinctRuns=" + runs.Count);
            log("sequence: " + string.Join(" -> ", runs.ToArray()));

            int distinct = samples.Count;
            int max = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                if (samples[i].Px > max) max = samples[i].Px;
            }
            int final = Native.SystemArrowPx();
            log("distinctSizes=" + distinct + " max=" + max + " final=" + final);

            bool pass = max >= baseline * 3 - 4 && distinct >= 5 && final <= baseline + 4;
            log(pass ? "RESULT: PASS" : "RESULT: FAIL");

            try { File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false)); } catch { }
            return pass ? 0 : 1;
        }
    }
}
