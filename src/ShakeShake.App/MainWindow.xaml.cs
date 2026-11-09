using System;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.UI;

namespace ShakeShake
{
    public sealed partial class MainWindow : Window
    {
        private const double BaseWindowWidth = 600;
        private const double BaseWindowHeight = 920;

        private readonly Settings _settings;
        private readonly Action<Settings> _onChanged;
        private readonly ObservableCollection<string> _exclude = new ObservableCollection<string>();
        private readonly DispatcherQueueTimer _saveTimer;
        private readonly DispatcherQueueTimer _captureTimer;

        private bool _updating;
        private bool _initialized;
        private bool _allowClose;
        private bool _capturing;
        private bool _sized;
        private int _captureLeft;

        public event Action LanguageChanged;

        public MainWindow(Settings settings, Action<Settings> onChanged)
        {
            _settings = settings;
            _onChanged = onChanged;

            InitializeComponent();

            Title = L.T("form.title");
            try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")); } catch { }
            ConfigureWindow();

            DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
            _saveTimer = dispatcher.CreateTimer();
            _saveTimer.Interval = TimeSpan.FromMilliseconds(400);
            _saveTimer.Tick += delegate(DispatcherQueueTimer t, object a) { t.Stop(); _settings.Save(); };

            _captureTimer = dispatcher.CreateTimer();
            _captureTimer.Interval = TimeSpan.FromSeconds(1);
            _captureTimer.Tick += CaptureTick;

            ExcludeList.ItemsSource = _exclude;
            RootGrid.Loaded += OnRootLoaded;
            AppWindow.Closing += OnAppWindowClosing;
            Closed += OnClosed;

            L.SetLanguage(_settings.Language);
            ApplyTexts();
            ApplyValues();
            UpdatePreview();

            if (Environment.GetEnvironmentVariable("SHAKESHAKE_UI_ADVANCED") == "1")
            {
                AdvancedExpander.IsExpanded = true;
            }

            _initialized = true;
        }

        // ------------------------------------------------------------ window setup

        private void ConfigureWindow()
        {
            OverlappedPresenter presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = false;
            }

            try
            {
                if (MicaController.IsSupported())
                {
                    SystemBackdrop = new MicaBackdrop();
                    RootGrid.Background = new SolidColorBrush(Colors.Transparent);
                }
            }
            catch (Exception ex)
            {
                Log.Write("mica unavailable: " + ex.Message);
            }
        }

        private void OnRootLoaded(object sender, RoutedEventArgs e)
        {
            if (_sized) return;
            _sized = true;
            ResizeToContent();
        }

        private void ResizeToContent()
        {
            try
            {
                double scale = 1.0;
                if (RootGrid.XamlRoot != null && RootGrid.XamlRoot.RasterizationScale > 0)
                {
                    scale = RootGrid.XamlRoot.RasterizationScale;
                }

                DisplayArea area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
                if (area == null) return;

                RectInt32 work = area.WorkArea;
                int width = (int)Math.Round(BaseWindowWidth * scale);
                int height = (int)Math.Round(BaseWindowHeight * scale);
                width = Math.Min(width, work.Width - (int)Math.Round(40 * scale));
                height = Math.Min(height, work.Height - (int)Math.Round(48 * scale));
                if (width < 320) width = 320;
                if (height < 320) height = 320;

                AppWindow.Resize(new SizeInt32(width, height));
                AppWindow.Move(new PointInt32(work.X + (work.Width - width) / 2,
                                             work.Y + (work.Height - height) / 2));
            }
            catch (Exception ex)
            {
                Log.Write("resize failed: " + ex.Message);
            }
        }

        private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            if (_allowClose) return;
            args.Cancel = true;
            HideWindow();
        }

        private void OnClosed(object sender, WindowEventArgs args)
        {
            try { _saveTimer.Stop(); } catch { }
            try { _captureTimer.Stop(); } catch { }
        }

        public void ShowSettings()
        {
            if (_allowClose) return;
            try
            {
                AppWindow.Show();
                Activate();
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                Win32.SetForegroundWindow(hwnd);
            }
            catch (Exception ex)
            {
                Log.Write("show settings failed: " + ex.Message);
            }
        }

        public void HideWindow()
        {
            try { AppWindow.Hide(); } catch { }
        }

        public void AllowClose()
        {
            _allowClose = true;
        }

        // ---------------------------------------------------------------- text

        private void ApplyTexts()
        {
            _updating = true;
            try
            {
                Title = L.T("form.title");
                HeaderText.Text = L.T("app.name");
                HeaderSubText.Text = L.T("page.subtitle");

                LangLabel.Text = L.T("lang.label");
                int langIndex = _settings.Language == "zh-CN" ? 1 : (_settings.Language == "en-US" ? 2 : 0);
                LangBox.Items.Clear();
                LangBox.Items.Add(L.T("lang.auto"));
                LangBox.Items.Add(L.T("lang.zh"));
                LangBox.Items.Add(L.T("lang.en"));
                LangBox.SelectedIndex = langIndex;

                EnableTitle.Text = L.T("chk.enable");
                EnableHint.Text = L.T("chk.enable.hint");
                ZoomTitle.Text = L.T("grp.zoom");
                ZoomHint.Text = L.T("zoom.hint");
                ZoomValue.Text = string.Format("{0:0.0}\u00D7", _settings.Zoom);
                ExcludeTitle.Text = L.T("grp.exclude");
                ExcludeHint.Text = L.T("exclude.hint");
                BtnBrowse.Content = L.T("btn.browse");
                if (!_capturing) BtnAddCurrent.Content = L.T("btn.addcurrent");
                BtnRemove.Content = L.T("btn.remove");
                AutostartTitle.Text = L.T("chk.autostart");
                AdvancedHeader.Text = L.T("advanced.title");
                SensLabel.Text = L.T("adv.sensitivity");
                RestoreLabel.Text = L.T("adv.restore");
                AdvancedNote.Text = L.T("adv.note");
                SensValue.Text = _settings.Sensitivity.ToString();
                RestoreValue.Text = L.T("unit.ms", _settings.RestoreMs);
                BtnDefaults.Content = L.T("btn.defaults");
                BtnClose.Content = L.T("btn.close");
                AboutText.Text = L.T("about", L.Version);
                ToolTipService.SetToolTip(AboutText, L.T("about.tip", Settings.FilePath));
            }
            finally
            {
                _updating = false;
            }
        }

        private void ApplyValues()
        {
            _updating = true;
            try
            {
                EnableToggle.IsOn = _settings.Enabled;
                ZoomSlider.Value = _settings.Zoom * 10;
                SensSlider.Value = _settings.Sensitivity;
                RestoreSlider.Value = _settings.RestoreMs;
                AutostartToggle.IsOn = AutoStart.IsEnabled();
                _settings.Autostart = AutostartToggle.IsOn;

                _exclude.Clear();
                for (int i = 0; i < _settings.Exclude.Count; i++) _exclude.Add(_settings.Exclude[i]);
                ExcludeList.SelectedIndex = -1;
                BtnRemove.IsEnabled = false;
            }
            finally
            {
                _updating = false;
            }
        }

        public void SyncFromSettings()
        {
            _updating = true;
            try
            {
                EnableToggle.IsOn = _settings.Enabled;
                AutostartToggle.IsOn = AutoStart.IsEnabled();
            }
            finally
            {
                _updating = false;
            }
        }

        // ---------------------------------------------------------------- events

        private void OnEnableToggled(object sender, RoutedEventArgs e)
        {
            if (_updating || !_initialized) return;
            _settings.Enabled = EnableToggle.IsOn;
            NotifyChanged();
        }

        private void OnAutostartToggled(object sender, RoutedEventArgs e)
        {
            if (_updating || !_initialized) return;
            _settings.Autostart = AutostartToggle.IsOn;
            AutoStart.SetEnabled(_settings.Autostart);
            NotifyChanged();
        }

        private void OnZoomChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_updating || !_initialized) return;
            _settings.Zoom = ZoomSlider.Value / 10.0;
            ZoomValue.Text = string.Format("{0:0.0}\u00D7", _settings.Zoom);
            UpdatePreview();
            NotifyChanged();
        }

        private void OnSensChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_updating || !_initialized) return;
            _settings.Sensitivity = (int)Math.Round(SensSlider.Value);
            SensValue.Text = _settings.Sensitivity.ToString();
            NotifyChanged();
        }

        private void OnRestoreChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_updating || !_initialized) return;
            _settings.RestoreMs = (int)Math.Round(RestoreSlider.Value);
            RestoreValue.Text = L.T("unit.ms", _settings.RestoreMs);
            NotifyChanged();
        }

        private void OnLanguageSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updating || !_initialized || LangBox.SelectedIndex < 0) return;
            string tag = LangBox.SelectedIndex == 1 ? "zh-CN" : (LangBox.SelectedIndex == 2 ? "en-US" : "auto");
            if (tag == _settings.Language) return;
            _settings.Language = tag;
            L.SetLanguage(tag);
            ApplyTexts();
            if (LanguageChanged != null) LanguageChanged();
            NotifyChanged();
        }

        private void OnExcludeSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            BtnRemove.IsEnabled = ExcludeList.SelectedIndex >= 0;
        }

        private async void OnBrowseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                FileOpenPicker picker = new FileOpenPicker();
                picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                picker.FileTypeFilter.Add(".exe");
                picker.CommitButtonText = L.T("dlg.pickexe");
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                Windows.Storage.StorageFile file = await picker.PickSingleFileAsync();
                if (file != null) AddExclude(file.Name);
            }
            catch (Exception ex)
            {
                Log.Write("browse failed: " + ex.Message);
            }
        }

        private void OnAddCurrentClick(object sender, RoutedEventArgs e)
        {
            StartCapture();
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e)
        {
            int index = ExcludeList.SelectedIndex;
            if (index < 0) return;
            _settings.Exclude.RemoveAt(index);
            _exclude.RemoveAt(index);
            BtnRemove.IsEnabled = false;
            NotifyChanged();
        }

        private void OnDefaultsClick(object sender, RoutedEventArgs e)
        {
            _settings.Zoom = 3.0;
            _settings.Sensitivity = 5;
            _settings.RestoreMs = 100;
            ApplyValues();
            UpdatePreview();
            NotifyChanged();
            ShowInfo(L.T("defaults.done"));
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            HideWindow();
        }

        // ------------------------------------------------------------- capture

        private void StartCapture()
        {
            if (_capturing) return;
            _capturing = true;
            _captureLeft = 3;
            BtnAddCurrent.IsEnabled = false;
            BtnAddCurrent.Content = L.T("exclude.wait", _captureLeft);
            _captureTimer.Start();
        }

        private void CaptureTick(DispatcherQueueTimer timer, object args)
        {
            _captureLeft--;
            if (_captureLeft > 0)
            {
                BtnAddCurrent.Content = L.T("exclude.wait", _captureLeft);
                return;
            }

            timer.Stop();
            _capturing = false;
            BtnAddCurrent.IsEnabled = true;
            BtnAddCurrent.Content = L.T("btn.addcurrent");

            string name = ProcInfo.ForegroundName();
            if (string.IsNullOrEmpty(name) ||
                string.Equals(name, ProcInfo.OwnExeName, StringComparison.OrdinalIgnoreCase))
            {
                ShowInfo(L.T("exclude.fail"));
                return;
            }
            AddExclude(name);
        }

        private void AddExclude(string name)
        {
            for (int i = 0; i < _settings.Exclude.Count; i++)
            {
                if (string.Equals(_settings.Exclude[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    ShowInfo(L.T("exclude.dupe"));
                    return;
                }
            }
            _settings.Exclude.Add(name);
            _exclude.Add(name);
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            if (_onChanged != null) _onChanged(_settings);
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private async void ShowInfo(string text)
        {
            ContentDialog dialog = new ContentDialog();
            dialog.Title = L.T("err.title");
            dialog.Content = text;
            dialog.CloseButtonText = "OK";
            dialog.XamlRoot = RootGrid.XamlRoot;
            try { await dialog.ShowAsync(); }
            catch { }
        }

        // ------------------------------------------------------------- preview

        private void UpdatePreview()
        {
            const double baseSize = 15.0;
            double bigSize = Math.Min(baseSize * _settings.Zoom, 44.0);

            PreviewSmall.RenderTransform = new ScaleTransform { ScaleX = baseSize, ScaleY = baseSize };
            Canvas.SetLeft(PreviewSmall, 24);
            Canvas.SetTop(PreviewSmall, 46 - baseSize);

            PreviewBig.RenderTransform = new ScaleTransform { ScaleX = bigSize, ScaleY = bigSize };
            Canvas.SetLeft(PreviewBig, 66);
            Canvas.SetTop(PreviewBig, 46 - bigSize);
        }
    }
}
