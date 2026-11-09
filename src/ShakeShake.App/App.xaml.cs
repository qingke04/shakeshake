using System;
using System.IO;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ShakeShake
{
    public partial class App : Application
    {
        public static App Instance { get; private set; }

        public Settings Settings { get; private set; }
        public AppController Controller { get; private set; }
        public MainWindow MainWindow { get; private set; }
        public TrayIcon Tray { get; private set; }

        private Mutex _mutex;
        private EventWaitHandle _showEvent;
        private DispatcherQueueTimer _poll;
        private volatile bool _engineNoticePending;
        private bool _exiting;

        public App()
        {
            Instance = this;
            string forcedTheme = Environment.GetEnvironmentVariable("SHAKESHAKE_UI_THEME");
            if (string.Equals(forcedTheme, "dark", StringComparison.OrdinalIgnoreCase)) RequestedTheme = ApplicationTheme.Dark;
            else if (string.Equals(forcedTheme, "light", StringComparison.OrdinalIgnoreCase)) RequestedTheme = ApplicationTheme.Light;
            UnhandledException += delegate(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
            {
                Log.Write("Xaml unhandled exception: " + e.Message + " / " + e.Exception);
            };
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Log.Write("App InitializeComponent failed: " + ex);
                throw;
            }
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                OnLaunchedCore(args);
            }
            catch (Exception ex)
            {
                Log.Write("OnLaunched failed: " + ex);
                throw;
            }
        }

        private void OnLaunchedCore(LaunchActivatedEventArgs args)
        {
            Settings = Settings.Load();
            L.SetLanguage(Settings.Language);

            bool firstRun = !File.Exists(Settings.FilePath);
            if (firstRun) Settings.Save();

            bool createdNew;
            _mutex = new Mutex(true, @"Local\ShakeShake.SingleInstance", out createdNew);
            try { _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\ShakeShake.Show"); }
            catch { }
            if (!createdNew)
            {
                try { if (_showEvent != null) _showEvent.Set(); } catch { }
                Exit();
                return;
            }

            Controller = new AppController();
            Controller.Notice += OnControllerNotice;
            Controller.Apply(Settings);
            Controller.Start();

            MainWindow = new MainWindow(Settings, OnSettingsChanged);
            MainWindow.LanguageChanged += OnLanguageChanged;

            Tray = new TrayIcon(Settings);
            Tray.ShowSettingsRequested += delegate { ShowSettings(); };
            Tray.ToggleRequested += delegate { ToggleEnabled(true); };
            Tray.ExitRequested += delegate { ExitApp(); };
            Tray.Refresh(Settings);

            _poll = DispatcherQueue.GetForCurrentThread().CreateTimer();
            _poll.Interval = TimeSpan.FromMilliseconds(300);
            _poll.Tick += OnPoll;
            _poll.Start();

            Log.Write("started (winui3); language=" + L.Current + " enabled=" + Settings.Enabled);

            if (Environment.GetEnvironmentVariable("SHAKESHAKE_UI_MENU") == "1")
            {
                DispatcherQueueTimer menuTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
                menuTimer.Interval = TimeSpan.FromMilliseconds(900);
                menuTimer.Tick += delegate(DispatcherQueueTimer t, object a2)
                {
                    t.Stop();
                    try
                    {
                        Windows.Graphics.PointInt32 pos = MainWindow.AppWindow.Position;
                        Windows.Graphics.SizeInt32 size = MainWindow.AppWindow.Size;
                        Tray.ShowMenuForTest(pos.X + size.Width / 2, pos.Y + size.Height / 2);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("test menu failed: " + ex.Message);
                    }
                };
                menuTimer.Start();
            }

            bool openSettings = HasArg("--settings");
            if (firstRun || openSettings) ShowSettings();
            if (firstRun)
            {
                Tray.ShowBalloon(L.T("app.name"), L.T("tray.run"), false);
            }
        }

        private static bool HasArg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private void OnPoll(object sender, object args)
        {
            try
            {
                if (_showEvent != null && _showEvent.WaitOne(0)) ShowSettings();
            }
            catch { }

            if (_engineNoticePending)
            {
                _engineNoticePending = false;
                Tray.ShowBalloon(L.T("err.title"), L.T("err.engine"), true);
            }
        }

        private void OnControllerNotice(AppController.NoticeKind kind)
        {
            if (kind != AppController.NoticeKind.EngineFailed) return;
            _engineNoticePending = true;
        }

        public void ShowSettings()
        {
            if (_exiting) return;
            MainWindow.ShowSettings();
        }

        public void OnSettingsChanged(Settings s)
        {
            Controller.Apply(s);
            Tray.Refresh(s);
        }

        public void OnLanguageChanged()
        {
            Tray.Refresh(Settings);
        }

        public void ToggleEnabled(bool announce)
        {
            Settings.Enabled = !Settings.Enabled;
            Settings.Save();
            Controller.Apply(Settings);
            MainWindow.SyncFromSettings();
            Tray.Refresh(Settings);
            if (announce)
            {
                Tray.ShowBalloon(L.T("app.name"),
                    L.T(Settings.Enabled ? "tray.enabled" : "tray.paused"), false);
            }
        }

        public void ExitApp()
        {
            if (_exiting) return;
            _exiting = true;

            try { if (_poll != null) { _poll.Stop(); _poll = null; } } catch { }
            try { Controller.Dispose(); } catch (Exception ex) { Log.Write("controller dispose: " + ex.Message); }
            try { Settings.Save(); } catch { }
            try { Tray.Dispose(); } catch { }
            try
            {
                MainWindow.AllowClose();
                MainWindow.Close();
            }
            catch { }

            try { _mutex.ReleaseMutex(); } catch { }
            try { _mutex.Dispose(); } catch { }

            Log.Write("exit");
            Exit();
        }
    }
}
