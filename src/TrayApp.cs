using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ShakeShake
{
    internal sealed class TrayApp : ApplicationContext, IMessageFilter
    {
        private readonly Settings _settings;
        private readonly AppController _controller;
        private readonly NotifyIcon _tray;
        private readonly ContextMenuStrip _menu;
        private readonly ToolStripMenuItem _miEnable;
        private readonly ToolStripMenuItem _miSettings;
        private readonly ToolStripMenuItem _miExit;
        private readonly SettingsForm _form;
        private readonly EventWaitHandle _showEvent;
        private readonly System.Windows.Forms.Timer _showPoll;

        private readonly Icon _iconOn;
        private readonly Icon _iconOff;
        private readonly IntPtr _iconOffHandle;

        private int _hotMods;
        private int _hotVk;
        private int _hotId;
        private bool _exiting;
        private volatile bool _pendingEngineNotice;

        private const string ShowEventName = @"Local\ShakeShake.Show";

        public TrayApp(bool openSettings)
        {
            bool firstRun = !File.Exists(Settings.FilePath);
            _settings = Settings.Load();
            L.SetLanguage(_settings.Language);
            if (firstRun) _settings.Save();

            _controller = new AppController();
            _controller.Notice += OnNotice;
            _controller.Apply(_settings);
            _controller.Start();

            _iconOn = LoadAppIcon();
            _iconOff = MakeGrayIcon(_iconOn, out _iconOffHandle);

            _menu = new ContextMenuStrip();
            _miEnable = new ToolStripMenuItem();
            _miEnable.Click += delegate { ToggleEnabled(true); };
            _miSettings = new ToolStripMenuItem();
            _miSettings.Click += delegate { ShowSettings(); };
            _miExit = new ToolStripMenuItem();
            _miExit.Click += delegate { ExitApp(); };

            _menu.Items.Add(_miEnable);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miSettings);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miExit);

            _tray = new NotifyIcon();
            _tray.Icon = _settings.Enabled ? _iconOn : _iconOff;
            _tray.Text = L.T("tray.tip");
            _tray.ContextMenuStrip = _menu;
            _tray.Visible = true;
            _tray.DoubleClick += delegate { ShowSettings(); };

            _form = new SettingsForm(_settings, OnSettingsChanged, OnLanguageChanged);

            RegisterHotkey();
            Application.AddMessageFilter(this);

            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
            _showPoll = new System.Windows.Forms.Timer();
            _showPoll.Interval = 300;
            _showPoll.Tick += delegate
            {
                try
                {
                    if (_showEvent.WaitOne(0)) ShowSettings();
                    if (_pendingEngineNotice)
                    {
                        _pendingEngineNotice = false;
                        _tray.ShowBalloonTip(4000, L.T("err.title"), L.T("err.engine"), ToolTipIcon.Warning);
                    }
                }
                catch { }
            };
            _showPoll.Start();

            UpdateMenu();
            Log.Write("started; language=" + L.Current + " enabled=" + _settings.Enabled);
            if (openSettings || firstRun)
            {
                _form.ShowSettings();
                if (firstRun)
                {
                    try
                    {
                        _tray.ShowBalloonTip(4000, L.T("app.name"), L.T("tray.run"), ToolTipIcon.Info);
                    }
                    catch { }
                }
            }
        }

        private static Icon LoadAppIcon()
        {
            try
            {
                Icon i = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (i != null) return i;
            }
            catch { }
            return SystemIcons.Application;
        }

        /// <summary>Greyed-out copy of the tray icon, used while paused.</summary>
        private static Icon MakeGrayIcon(Icon source, out IntPtr handle)
        {
            handle = IntPtr.Zero;
            try
            {
                using (Bitmap bmp = source.ToBitmap())
                {
                    for (int y = 0; y < bmp.Height; y++)
                    {
                        for (int x = 0; x < bmp.Width; x++)
                        {
                            Color c = bmp.GetPixel(x, y);
                            int g = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                            g = 120 + g / 4;
                            bmp.SetPixel(x, y, Color.FromArgb(c.A, g, g, g));
                        }
                    }
                    handle = bmp.GetHicon();
                    return Icon.FromHandle(handle);
                }
            }
            catch
            {
                return source;
            }
        }

        private void UpdateMenu()
        {
            _miEnable.Text = L.T("tray.enable");
            _miEnable.Checked = _settings.Enabled;
            _miSettings.Text = L.T("tray.settings");
            _miExit.Text = L.T("tray.exit");
            _tray.Text = L.T("tray.tip");
            _tray.Icon = _settings.Enabled ? _iconOn : _iconOff;
        }

        private void OnSettingsChanged(Settings s)
        {
            _controller.Apply(s);
            UpdateMenu();
        }

        private void OnLanguageChanged()
        {
            UpdateMenu();
            _tray.Visible = false;
            _tray.Visible = true;
        }

        private void ShowSettings()
        {
            _form.ShowSettings();
        }

        public void ToggleEnabled(bool announce)
        {
            _settings.Enabled = !_settings.Enabled;
            _settings.Save();
            _controller.Apply(_settings);
            _form.SyncFromSettings();
            UpdateMenu();
            if (announce)
            {
                try
                {
                    _tray.ShowBalloonTip(1800, L.T("app.name"),
                        L.T(_settings.Enabled ? "tray.enabled" : "tray.paused"), ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private void OnNotice(AppController.NoticeKind kind)
        {
            if (kind != AppController.NoticeKind.EngineFailed) return;
            _pendingEngineNotice = true; // shown from the UI timer
        }

        // ------------------------------------------------------------- hotkey

        private void RegisterHotkey()
        {
            int mods, vk;
            if (!Hotkey.TryParse(_settings.Hotkey, out mods, out vk))
            {
                mods = Native.MOD_CONTROL | Native.MOD_ALT;
                vk = (int)Keys.S;
            }
            _hotMods = mods;
            _hotVk = vk;
            _hotId = 1;
            if (!Native.RegisterHotKey(IntPtr.Zero, _hotId, mods, vk))
            {
                Log.Write("RegisterHotKey failed for " + _settings.Hotkey);
            }
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && m.WParam.ToInt32() == _hotId)
            {
                ToggleEnabled(true);
                return true;
            }
            return false;
        }

        // -------------------------------------------------------------- exit

        public void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;

            try { _showPoll.Stop(); _showPoll.Dispose(); } catch { }
            try { Application.RemoveMessageFilter(this); } catch { }
            try { if (_hotId != 0) Native.UnregisterHotKey(IntPtr.Zero, _hotId); } catch { }

            try
            {
                _controller.Dispose(); // restores the cursor
            }
            catch (Exception ex) { Log.Write("controller dispose: " + ex.Message); }

            try { _settings.Save(); } catch { }

            try
            {
                _form.AllowClose();
                _form.Dispose();
            }
            catch { }

            try
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
            catch { }

            if (_iconOffHandle != IntPtr.Zero) Native.DestroyIcon(_iconOffHandle);

            Log.Write("exit");
            ExitThread();
        }
    }
}



