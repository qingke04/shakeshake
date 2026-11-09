using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ShakeShake
{
    /// <summary>
    /// Shell_NotifyIcon wrapper: tray icon, balloon notifications, the global hotkey and
    /// a message-only window that receives the tray / hotkey callbacks.
    /// </summary>
    public sealed class TrayIcon : IDisposable
    {
        private readonly Win32.WndProc _wndProc;
        private readonly string _className;
        private readonly IntPtr _hInstance;
        private readonly IntPtr _iconOn;
        private readonly IntPtr _iconOff;
        private readonly uint _taskbarCreated;

        private IntPtr _hwnd;
        private TrayMenuWindow _menu;
        private bool _iconAdded;
        private bool _enabled = true;
        private bool _disposed;
        private bool _hotkeyRegistered;
        private string _tip = "ShakeShake";

        public event Action ShowSettingsRequested;
        public event Action ToggleRequested;
        public event Action ExitRequested;

        public TrayIcon(Settings settings)
        {
            _wndProc = WndProcImpl;
            _hInstance = Native.GetModuleHandle(null);
            _className = "ShakeShakeTrayWnd_" + Guid.NewGuid().ToString("N");

            CreateMessageWindow();
            _iconOn = LoadIcon("app.ico");
            _iconOff = LoadIcon("tray_off.ico");
            _taskbarCreated = Win32.RegisterWindowMessage("TaskbarCreated");
            AddIcon();
            RegisterHotkey(settings != null ? settings.Hotkey : "Ctrl+Alt+S");
        }

        // ------------------------------------------------------------ icon

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max);
        }

        private static IntPtr LoadIcon(string fileName)
        {
            try
            {
                string dir = Path.Combine(AppContext.BaseDirectory, "Assets");
                string path = Path.Combine(dir, fileName);
                if (!File.Exists(path)) path = Path.Combine(dir, "app.ico");
                if (!File.Exists(path)) return IntPtr.Zero;
                int size = Native.GetSystemMetrics(Win32.SM_CXSMICON);
                if (size <= 0) size = 16;
                return Native.LoadImage(IntPtr.Zero, path, Win32.IMAGE_ICON, size, size, Win32.LR_LOADFROMFILE);
            }
            catch (Exception ex)
            {
                Log.Write("tray icon load failed: " + ex.Message);
                return IntPtr.Zero;
            }
        }

        private Win32.NOTIFYICONDATAW MakeData()
        {
            Win32.NOTIFYICONDATAW data = new Win32.NOTIFYICONDATAW();
            data.cbSize = (uint)Marshal.SizeOf(typeof(Win32.NOTIFYICONDATAW));
            data.hWnd = _hwnd;
            data.uID = Win32.TrayId;
            data.uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP;
            data.uCallbackMessage = (uint)Win32.TrayMessage;
            data.hIcon = _enabled ? _iconOn : _iconOff;
            data.szTip = Trim(_tip, 127);
            return data;
        }

        private void AddIcon()
        {
            if (_hwnd == IntPtr.Zero) return;
            try
            {
                Win32.NOTIFYICONDATAW data = MakeData();
                _iconAdded = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data);
                Log.Write("tray icon added=" + _iconAdded + " hwnd=" + _hwnd);
            }
            catch (Exception ex)
            {
                Log.Write("tray add failed: " + ex.Message);
            }
        }

        private void ModifyIcon()
        {
            if (!_iconAdded) return;
            try
            {
                Win32.NOTIFYICONDATAW data = MakeData();
                Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref data);
            }
            catch (Exception ex)
            {
                Log.Write("tray modify failed: " + ex.Message);
            }
        }

        public void Refresh(Settings settings)
        {
            if (settings != null) _enabled = settings.Enabled;
            _tip = L.T("tray.tip");
            ModifyIcon();
        }

        public void ShowBalloon(string title, string text, bool warning)
        {
            if (!_iconAdded) return;
            try
            {
                Win32.NOTIFYICONDATAW data = MakeData();
                data.uFlags = Win32.NIF_INFO;
                data.szInfoTitle = Trim(title, 63);
                data.szInfo = Trim(text, 255);
                data.dwInfoFlags = warning ? Win32.NIIF_WARNING : Win32.NIIF_INFO;
                data.uTimeoutOrVersion = 4000;
                Win32.Shell_NotifyIcon(Win32.NIM_MODIFY, ref data);
            }
            catch (Exception ex)
            {
                Log.Write("tray balloon failed: " + ex.Message);
            }
        }

        // ------------------------------------------------------------ menu

        private void ShowMenu()
        {
            Native.POINT pt;
            if (!Native.GetCursorPos(out pt)) return;
            ShowMenuAt(pt.X, pt.Y);
        }

        /// <summary>Shows the tray menu at an explicit point (also used by UI smoke tests).</summary>
        public void ShowMenuForTest(int x, int y)
        {
            ShowMenuAt(x, y);
        }

        private void ShowMenuAt(int x, int y)
        {
            try
            {
                if (_menu == null) _menu = new TrayMenuWindow();
                _menu.ShowAt(x, y, _enabled,
                    delegate { Raise(ToggleRequested); },
                    delegate { Raise(ShowSettingsRequested); },
                    delegate { Raise(ExitRequested); });
            }
            catch (Exception ex)
            {
                Log.Write("tray menu failed: " + ex.Message);
            }
        }

        private static void Raise(Action handler)
        {
            if (handler == null) return;
            try { handler(); }
            catch (Exception ex) { Log.Write("tray handler failed: " + ex); }
        }

        // ------------------------------------------------------------ hotkey

        private void RegisterHotkey(string text)
        {
            int mods, vk;
            if (!Hotkey.TryParse(text, out mods, out vk))
            {
                mods = Native.MOD_CONTROL | Native.MOD_ALT;
                vk = 0x53; // S
            }
            if (_hwnd == IntPtr.Zero) return;
            _hotkeyRegistered = Native.RegisterHotKey(_hwnd, Win32.HotkeyId, mods, vk);
            if (!_hotkeyRegistered) Log.Write("RegisterHotKey failed for " + text);
        }

        // ------------------------------------------------------------ window

        private void CreateMessageWindow()
        {
            Win32.WNDCLASSEX wc = new Win32.WNDCLASSEX();
            wc.cbSize = (uint)Marshal.SizeOf(typeof(Win32.WNDCLASSEX));
            wc.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc);
            wc.hInstance = _hInstance;
            wc.lpszClassName = _className;
            Win32.RegisterClassEx(ref wc);

            _hwnd = Win32.CreateWindowEx(0, _className, "", 0, 0, 0, 0, 0,
                new IntPtr(Win32.HWND_MESSAGE), IntPtr.Zero, _hInstance, IntPtr.Zero);
            if (_hwnd == IntPtr.Zero) Log.Write("tray message window failed: " + Marshal.GetLastWin32Error());
        }

        private IntPtr WndProcImpl(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (_taskbarCreated != 0 && msg == _taskbarCreated)
                {
                    _iconAdded = false;
                    AddIcon();
                    return IntPtr.Zero;
                }

                if (msg == (uint)Win32.TrayMessage)
                {
                    int notification = (int)(lParam.ToInt64() & 0xFFFF);
                    if (notification == Win32.WM_LBUTTONDBLCLK)
                    {
                        Raise(ShowSettingsRequested);
                    }
                    else if (notification == Win32.WM_RBUTTONUP || notification == Win32.WM_CONTEXTMENU)
                    {
                        ShowMenu();
                    }
                    return IntPtr.Zero;
                }

                if (msg == Win32.WM_HOTKEY)
                {
                    Raise(ToggleRequested);
                    return IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                Log.Write("tray wndproc failed: " + ex.Message);
            }

            return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (_iconAdded)
                {
                    Win32.NOTIFYICONDATAW data = MakeData();
                    data.uFlags = 0;
                    Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
                    _iconAdded = false;
                }
            }
            catch { }

            try { if (_hotkeyRegistered) Native.UnregisterHotKey(_hwnd, Win32.HotkeyId); } catch { }
            try { if (_menu != null) { _menu.Close(); _menu = null; } } catch { }
            try { if (_hwnd != IntPtr.Zero) { Win32.DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; } } catch { }
            try
            {
                if (_iconOn != IntPtr.Zero) Native.DestroyIcon(_iconOn);
                if (_iconOff != IntPtr.Zero) Native.DestroyIcon(_iconOff);
            }
            catch { }
        }
    }
}
