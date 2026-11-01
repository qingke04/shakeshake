using System;
using System.Runtime.InteropServices;

namespace ShakeShake
{
    /// <summary>Global low level mouse hook (WH_MOUSE_LL).</summary>
    internal sealed class MouseHook : IDisposable
    {
        private readonly Native.LowLevelMouseProc _proc;
        private IntPtr _hook;
        private readonly Action<int, int> _onMove;

        public MouseHook(Action<int, int> onMove)
        {
            _onMove = onMove;
            _proc = Callback; // keep a reference: the GC must not collect the delegate
        }

        public bool IsRunning
        {
            get { return _hook != IntPtr.Zero; }
        }

        public bool Start()
        {
            if (_hook != IntPtr.Zero) return true;
            IntPtr module = Native.GetModuleHandle(null);
            _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _proc, module, 0);
            if (_hook == IntPtr.Zero)
            {
                Log.Write("SetWindowsHookEx failed: " + Marshal.GetLastWin32Error());
                return false;
            }
            return true;
        }

        private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)Native.WM_MOUSEMOVE)
            {
                try
                {
                    int x = Marshal.ReadInt32(lParam, 0);
                    int y = Marshal.ReadInt32(lParam, 4);
                    _onMove(x, y);
                }
                catch
                {
                    // never let an exception escape a hook callback
                }
            }
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_hook != IntPtr.Zero)
            {
                Native.UnhookWindowsHookEx(_hook);
                _hook = IntPtr.Zero;
            }
        }
    }
}
