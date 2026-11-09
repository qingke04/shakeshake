using System;
using System.Threading;

namespace ShakeShake
{
    /// <summary>
    /// Wires the mouse hook to the shake detector and animates the cursor size.
    /// The hook callback only does detection; the timer does the (slower) cursor work.
    /// </summary>
    public sealed class AppController : IDisposable
    {
        public enum NoticeKind { EngineFailed }

        private const int FrameMs = 15;          // ~66 fps
        private const double Ease = 0.38;        // fraction of the remaining distance per frame
        private const int MaxStepPx = 16;        // never jump more than this in one frame

        private readonly object _engineLock = new object();
        private readonly CursorEngine _engine = new CursorEngine();
        private readonly Timer _timer;
        private MouseHook _hook;
        private volatile ShakeDetector _detector;
        private volatile bool _enabled = true;
        private volatile int _restoreMs = 100;
        private double _zoom = 3.0;
        private volatile bool _disposed;
        private long _lastShakeMs;
        private long _suppressUntilMs;
        private int _busy;
        private string[] _exclude = new string[0];
        private string _fgName;
        private long _fgStampMs;
        private bool _engineNoticeShown;

        // animation state (owned by the timer thread)
        private int _basePx;
        private int _displayPx;
        private int _targetPx;
        private int _episodePx;

        public event Action<NoticeKind> Notice;

        public AppController()
        {
            _detector = ShakeDetector.FromSensitivity(5);
            _timer = new Timer(Tick, null, FrameMs, FrameMs);
        }

        public bool IsEnlarged
        {
            get { return _basePx > 0 && _displayPx > _basePx; }
        }

        public bool Enabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public void Start()
        {
            _hook = new MouseHook(OnMouseMove);
            if (!_hook.Start()) Log.Write("mouse hook could not be installed");
        }

        public void Apply(Settings s)
        {
            _detector = ShakeDetector.FromSensitivity(s.Sensitivity);
            _restoreMs = s.RestoreMs;
            _zoom = s.Zoom;
            _enabled = s.Enabled;
            _exclude = s.Exclude.ToArray();
            if (!s.Enabled) RestoreNow();
        }

        private void OnMouseMove(int x, int y)
        {
            if (_disposed) return;
            ShakeDetector d = _detector;
            long now = Clock.NowMs;
            if (d.Feed(x, y, now)) Interlocked.Exchange(ref _lastShakeMs, now);
        }

        private void Tick(object state)
        {
            if (_disposed) return;
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
            try
            {
                long now = Clock.NowMs;
                EnsureBaseline();

                long last = Interlocked.Read(ref _lastShakeMs);
                bool shaking = _enabled && (now - last) <= _restoreMs;

                if (!shaking)
                {
                    _targetPx = _basePx;
                }
                else if (_episodePx > 0)
                {
                    _targetPx = _episodePx;
                }
                else if (now < Interlocked.Read(ref _suppressUntilMs))
                {
                    _targetPx = _basePx;
                }
                else if (IsExcludedForeground(now))
                {
                    Interlocked.Exchange(ref _suppressUntilMs, now + 500);
                    _targetPx = _basePx;
                }
                else
                {
                    // New episode: re-measure in case the user changed the Windows cursor size.
                    _basePx = MeasureBaseline();
                    _displayPx = _basePx;
                    int target = (int)Math.Round(_basePx * _zoom);
                    if (target < _basePx + 6) target = _basePx + 6;
                    if (target > 256) target = 256;
                    _episodePx = target;
                    _targetPx = target;
                }

                if (_targetPx != _displayPx) StepToward(now);
            }
            catch (Exception ex)
            {
                Log.Write("tick failed: " + ex);
                Interlocked.Exchange(ref _suppressUntilMs, Clock.NowMs + 2000);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private void StepToward(long now)
        {
            int diff = _targetPx - _displayPx;
            int step = (int)Math.Round(diff * Ease);
            if (step == 0) step = diff > 0 ? 1 : -1;
            if (step > MaxStepPx) step = MaxStepPx;
            if (step < -MaxStepPx) step = -MaxStepPx;

            int next = _displayPx + step;
            if (Math.Abs(_targetPx - next) <= 2) next = _targetPx;

            // Back at the normal size -> reload the user's real scheme instead of a 32px swap.
            if (_targetPx == _basePx && Math.Abs(next - _basePx) <= 2)
            {
                lock (_engineLock) _engine.Restore();
                _displayPx = _basePx;
                _episodePx = 0;
                return;
            }

            if (next <= _basePx) next = _basePx + 1;

            bool ok;
            lock (_engineLock) ok = _engine.ApplyFrame(next);
            if (!ok)
            {
                Interlocked.Exchange(ref _suppressUntilMs, now + 2000);
                Log.Write("cursor frame failed: " + _engine.LastError);
                lock (_engineLock) _engine.Restore();
                _episodePx = 0;
                _targetPx = _basePx;
                _displayPx = _basePx;
                if (!_engineNoticeShown)
                {
                    _engineNoticeShown = true;
                    RaiseNotice(NoticeKind.EngineFailed);
                }
                return;
            }

            _engineNoticeShown = false;
            _displayPx = next;
        }

        private void EnsureBaseline()
        {
            if (_basePx > 0) return;
            _basePx = MeasureBaseline();
            _displayPx = _basePx;
            _targetPx = _basePx;
        }

        private int MeasureBaseline()
        {
            lock (_engineLock)
            {
                if (_engine.IsEnlarged) _engine.Restore();
            }
            int px = _engine.MeasureBaselinePx();
            return px > 0 ? px : 32;
        }

        private void RaiseNotice(NoticeKind kind)
        {
            Action<NoticeKind> n = Notice;
            if (n == null) return;
            try { n(kind); }
            catch { }
        }

        public void RestoreNow()
        {
            try
            {
                lock (_engineLock)
                {
                    if (_engine.IsEnlarged) _engine.Restore();
                }
                _displayPx = _basePx;
                _targetPx = _basePx;
                _episodePx = 0;
            }
            catch (Exception ex)
            {
                Log.Write("restore failed: " + ex);
            }
        }

        private bool IsExcludedForeground(long now)
        {
            if (_exclude.Length == 0) return false;
            string name = ForegroundProcessName(now);
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < _exclude.Length; i++)
            {
                if (string.Equals(_exclude[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private string ForegroundProcessName(long now)
        {
            if (_fgName != null && now - _fgStampMs < 400) return _fgName;
            string result = ProcInfo.NameOfWindow(Native.GetForegroundWindow());
            _fgName = result;
            _fgStampMs = now;
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { if (_timer != null) _timer.Dispose(); } catch { }
            try { if (_hook != null) _hook.Dispose(); } catch { }
            RestoreNow();
        }
    }
}
