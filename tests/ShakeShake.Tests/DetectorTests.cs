using System;
using System.Collections.Generic;
using ShakeShake;

namespace ShakeShake.Tests
{
    /// <summary>Console tests for the pure shake detector.</summary>
    internal static class DetectorTests
    {
        private static int _failed;
        private static int _passed;

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) { _passed++; Console.WriteLine("PASS  " + name); }
            else { _failed++; Console.WriteLine("FAIL  " + name + "  " + detail); }
        }

        private static bool RunShake(ShakeDetector d, int amplitude, int stepMs, int strokes, long startMs)
        {
            bool fired = false;
            int x = 0;
            long t = startMs;
            for (int i = 0; i < strokes; i++)
            {
                x = (i % 2 == 0) ? amplitude : -amplitude;
                t += stepMs;
                if (d.Feed(x, 0, t)) fired = true;
            }
            return fired;
        }

        public static int Main()
        {
            // 1. a real shake triggers
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(5);
                bool fired = RunShake(d, 40, 40, 12, 1000);
                Check("shake 40px/40ms triggers", fired, "never fired");
            }

            // 2. slow drift does not
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(5);
                bool fired = false;
                for (int i = 0; i < 120; i++) fired |= d.Feed(i * 2, 0, 1000 + i * 50);
                Check("slow drift does not trigger", !fired, "fired on a 40px/s drift");
            }

            // 3. one sweep does not
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(5);
                bool fired = false;
                for (int i = 0; i < 20; i++) fired |= d.Feed(i * 20, 0, 1000 + i * 10);
                Check("single sweep does not trigger", !fired, "fired on one sweep");
            }

            // 4. tiny jitter does not
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(5);
                bool fired = RunShake(d, 4, 16, 40, 1000);
                Check("4px jitter does not trigger", !fired, "fired on tiny jitter");
            }

            // 5. after a pause the gesture restarts
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(5);
                RunShake(d, 40, 40, 12, 1000);
                bool fired = d.Feed(-40, 0, 4000) | d.Feed(40, 0, 4040) | d.Feed(-40, 0, 4080);
                Check("pause resets the gesture", !fired, "fired after a 2s pause");
            }

            // 6. a real shake still triggers after a pause
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(5);
                RunShake(d, 40, 40, 12, 1000);
                bool fired = RunShake(d, 40, 40, 12, 5000);
                Check("shake after pause triggers", fired, "did not fire");
            }

            // 7. higher sensitivity needs fewer strokes
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(10);
                bool fired = RunShake(d, 30, 30, 5, 1000);
                Check("sensitivity 10 triggers on 5 strokes", fired, "did not fire");
            }

            // 8. sensitivity mapping
            {
                ShakeDetector a = ShakeDetector.FromSensitivity(1);
                ShakeDetector b = ShakeDetector.FromSensitivity(10);
                Check("sensitivity mapping", a.MinReversals == 5 && b.MinReversals == 3 && a.MinStrokePx > b.MinStrokePx,
                    "a=" + a.MinReversals + "/" + a.MinStrokePx + " b=" + b.MinReversals + "/" + b.MinStrokePx);
            }

            // 9. vertical shake triggers too
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(5);
                bool fired = false;
                long t = 1000;
                for (int i = 0; i < 12; i++)
                {
                    t += 40;
                    fired |= d.Feed(0, (i % 2 == 0) ? 35 : -35, t);
                }
                Check("vertical shake triggers", fired, "did not fire");
            }

            // 10. diagonal shake triggers
            {
                ShakeDetector d = ShakeDetector.FromSensitivity(5);
                bool fired = false;
                long t = 1000;
                for (int i = 0; i < 12; i++)
                {
                    t += 40;
                    int s = (i % 2 == 0) ? 30 : -30;
                    fired |= d.Feed(s, s, t);
                }
                Check("diagonal shake triggers", fired, "did not fire");
            }

            Console.WriteLine();
            Console.WriteLine("passed=" + _passed + " failed=" + _failed);
            return _failed == 0 ? 0 : 1;
        }
    }
}
