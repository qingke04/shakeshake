using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ShakeShake
{
    /// <summary>
    /// Headless check for the size animation: injects one shake, samples the system arrow
    /// size every few ms and prints the size sequence (so we can see it ramps, not jumps).
    /// </summary>
    internal static class AnimTest
    {
        private const uint MOUSEEVENTF_ABSOLUTE = 0x8000;

        private sealed class Sample
        {
            public long T;
            public int Px;
        }

        public static int Run()
        {
            string logPath = Path.Combine(Path.GetTempPath(), "shakeshake-animtest.log");
            StringBuilder sb = new StringBuilder();
            List<Sample> samples = new List<Sample>();
            object gate = new object();
            bool finished = false;
            int baseline = Native.SystemArrowPx();

            Action<string> log = delegate(string msg)
            {
                sb.AppendLine(msg);
                Console.WriteLine(msg);
            };

            log("animtest start; baseline=" + baseline);

            int sw = Native.GetSystemMetrics(Native.SM_CXSCREEN);
            int sh = Native.GetSystemMetrics(Native.SM_CYSCREEN);
            int cx = sw / 2;
            int cy = sh / 2;

            Settings s = new Settings();
            s.Zoom = 3.0;
            s.Sensitivity = 10;
            s.RestoreMs = 100;
            s.Enabled = true;

            AppController c = new AppController();
            c.Apply(s);
            c.Start();

            Thread sampler = new Thread(delegate()
            {
                long deadline = Clock.NowMs + 3000;
                while (!finished && Clock.NowMs < deadline)
                {
                    int px = Native.SystemArrowPx();
                    Sample smp = new Sample();
                    smp.T = Clock.NowMs;
                    smp.Px = px;
                    lock (gate) samples.Add(smp);
                    Thread.Sleep(3);
                }
            });
            sampler.IsBackground = true;

            ApplicationContext ctx = new ApplicationContext();
            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 25;
            long start = 0;
            int injected = 0;

            t.Tick += delegate
            {
                long now = Clock.NowMs;
                if (start == 0)
                {
                    start = now;
                    Native.SetCursorPos(cx, cy);
                    sampler.Start();
                    return;
                }

                long elapsed = now - start;

                if (elapsed > 300 && elapsed <= 700)
                {
                    int x = (injected % 2 == 0) ? cx - 70 : cx + 70;
                    int nx = (int)((long)x * 65535 / Math.Max(1, sw - 1));
                    int ny = (int)((long)cy * 65535 / Math.Max(1, sh - 1));
                    Native.mouse_event(Native.MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE, nx, ny, 0, UIntPtr.Zero);
                    injected++;
                }

                if (elapsed > 2400)
                {
                    finished = true;
                    t.Stop();
                    ctx.ExitThread();
                }
            };

            t.Start();
            Application.Run(ctx);
            c.Dispose();
            finished = true;
            try { sampler.Join(500); } catch { }

            // compress consecutive equal samples into runs
            List<string> runs = new List<string>();
            int prev = -999;
            long runStart = 0;
            int runPx = 0;
            lock (gate)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    Sample smp = samples[i];
                    if (smp.Px != prev)
                    {
                        if (prev != -999) runs.Add(string.Format("{0}px x{1}ms", runPx, smp.T - runStart));
                        prev = smp.Px;
                        runPx = smp.Px;
                        runStart = smp.T;
                    }
                }
                if (prev != -999)
                {
                    long end = samples.Count > 0 ? samples[samples.Count - 1].T : runStart;
                    runs.Add(string.Format("{0}px x{1}ms", runPx, end - runStart));
                }
            }

            log("samples=" + samples.Count + " distinctRuns=" + runs.Count);
            log("sequence: " + string.Join(" -> ", runs.ToArray()));

            int distinct = 0;
            int last = -1;
            int max = 0;
            lock (gate)
            {
                for (int i = 0; i < samples.Count; i++)
                {
                    if (samples[i].Px != last) { distinct++; last = samples[i].Px; }
                    if (samples[i].Px > max) max = samples[i].Px;
                }
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
