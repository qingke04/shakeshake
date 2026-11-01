using System;

namespace ShakeShake
{
    /// <summary>
    /// Detects a quick back-and-forth "shake" of the mouse.
    /// Pure logic: feed every mouse move, get true while a shake is in progress.
    /// </summary>
    internal sealed class ShakeDetector
    {
        public int MinReversals = 4;
        public int WindowMs = 400;
        public int MinStrokePx = 12;
        public int MaxStrokeMs = 260;

        private struct Stroke
        {
            public double X;
            public double Y;
            public double Len;
            public long StartMs;
        }


        private Stroke _stroke;
        private readonly long[] _reversals = new long[64];
        private int _count;
        private int _head;
        private int _lastX;
        private int _lastY;
        private bool _hasLast;
        private long _lastMoveMs = long.MinValue / 2;

        /// <summary>Builds a detector from a 1..10 sensitivity setting.</summary>
        public static ShakeDetector FromSensitivity(int sensitivity)
        {
            if (sensitivity < 1) sensitivity = 1;
            if (sensitivity > 10) sensitivity = 10;

            ShakeDetector d = new ShakeDetector();
            d.MinReversals = sensitivity <= 3 ? 5 : (sensitivity <= 6 ? 4 : 3);
            d.WindowMs = 520 - (sensitivity - 1) * 20;              // 520 .. 340
            d.MinStrokePx = (int)Math.Round(22 - (sensitivity - 1) * 1.5); // 22 .. 9
            d.MaxStrokeMs = 300 - (sensitivity - 1) * 8;            // 300 .. 228
            return d;
        }

        public void Reset()
        {
            _stroke = new Stroke();
            _count = 0;
            _head = 0;
            _hasLast = false;
            _lastMoveMs = long.MinValue / 2;
        }

        /// <summary>Feed one mouse position. Returns true while a shake is happening.</summary>
        public bool Feed(int x, int y, long nowMs)
        {
            if (!_hasLast)
            {
                _lastX = x;
                _lastY = y;
                _hasLast = true;
                return false;
            }

            int dx = x - _lastX;
            int dy = y - _lastY;
            _lastX = x;
            _lastY = y;

            double len = Math.Sqrt((double)dx * dx + (double)dy * dy);
            if (len < 0.6) return false;

            // A long pause starts a brand new gesture.
            if (nowMs - _lastMoveMs > WindowMs) _stroke = new Stroke();
            _lastMoveMs = nowMs;

            if (_stroke.Len < 0.001)
            {
                StartStroke(dx, dy, nowMs);
                return false;
            }

            double dot = (dx * _stroke.X + dy * _stroke.Y) / (len * _stroke.Len);
            if (dot < -0.5)
            {
                // Direction reversed by more than ~120 degrees.
                bool valid = _stroke.Len >= MinStrokePx && (nowMs - _stroke.StartMs) <= MaxStrokeMs;
                if (valid) AddReversal(nowMs);
                StartStroke(dx, dy, nowMs);
                return CountWithinWindow(nowMs) >= MinReversals;
            }

            _stroke.X += dx;
            _stroke.Y += dy;
            _stroke.Len = Math.Sqrt(_stroke.X * _stroke.X + _stroke.Y * _stroke.Y);

            if (nowMs - _stroke.StartMs > MaxStrokeMs) StartStroke(dx, dy, nowMs);
            return CountWithinWindow(nowMs) >= MinReversals;
        }

        private void StartStroke(int dx, int dy, long nowMs)
        {
            _stroke.X = dx;
            _stroke.Y = dy;
            _stroke.Len = Math.Sqrt((double)dx * dx + (double)dy * dy);
            _stroke.StartMs = nowMs;
        }

        private void AddReversal(long nowMs)
        {
            _reversals[_head] = nowMs;
            _head = (_head + 1) % _reversals.Length;
            if (_count < _reversals.Length) _count++;
        }

        private int CountWithinWindow(long nowMs)
        {
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - 1 - i + _reversals.Length * 2) % _reversals.Length;
                if (nowMs - _reversals[idx] <= WindowMs) n++;
                else break; // stored in increasing time order
            }
            return n;
        }
    }
}

