using System;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Graphics;
using Windows.System;

namespace ShakeShake
{
    /// <summary>
    /// A small borderless Fluent window showing the tray context menu.
    /// WinUI 3 has no built-in tray icon, so the menu is hosted here instead of
    /// using a classic Win32 HMENU popup.
    /// </summary>
    public sealed partial class TrayMenuWindow : Window
    {
        private const double LogicalWidth = 240;
        private const double ItemHeight = 36;
        private const double SeparatorHeight = 9;
        private const double VerticalPadding = 8;

        public TrayMenuWindow()
        {
            InitializeComponent();
            ConfigureWindow();
            Activated += OnActivated;

            KeyboardAccelerator escape = new KeyboardAccelerator();
            escape.Key = VirtualKey.Escape;
            escape.Invoked += delegate(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
            {
                args.Handled = true;
                HideMenu();
            };
            MenuRoot.KeyboardAccelerators.Add(escape);

            AppWindow.Closing += delegate(AppWindow sender, AppWindowClosingEventArgs args)
            {
                args.Cancel = true;
                HideMenu();
            };
        }

        private void ConfigureWindow()
        {
            OverlappedPresenter presenter = AppWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                presenter.SetBorderAndTitleBar(false, false);
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
                presenter.IsMinimizable = false;
                presenter.IsAlwaysOnTop = true;
            }
            AppWindow.IsShownInSwitchers = false;
        }

        private void HideMenu()
        {
            try { AppWindow.Hide(); }
            catch { }
        }

        private void OnActivated(object sender, WindowActivatedEventArgs args)
        {
            if (args.WindowActivationState == WindowActivationState.Deactivated) HideMenu();
        }

        /// <summary>Build the menu and show it near the given screen point (physical pixels).</summary>
        public void ShowAt(int x, int y, bool enabled, Action toggle, Action settings, Action exit)
        {
            Build(enabled, toggle, settings, exit);

            double scale = Win32.ScaleForPoint(x, y);
            Win32.RECT work = Win32.WorkAreaForPoint(x, y);
            int width = (int)Math.Round(LogicalWidth * scale);
            int height = (int)Math.Round((ItemHeight * 3 + SeparatorHeight + VerticalPadding) * scale);

            if (x + width > work.Right) x = work.Right - width - (int)Math.Round(8 * scale);
            if (y + height > work.Bottom) y = work.Bottom - height - (int)Math.Round(8 * scale);
            if (x < work.Left) x = work.Left;
            if (y < work.Top) y = work.Top;

            AppWindow.Resize(new SizeInt32(width, height));
            AppWindow.Move(new PointInt32(x, y));
            AppWindow.Show();
            Activate();
        }

        private void Build(bool enabled, Action toggle, Action settings, Action exit)
        {
            ItemsHost.Children.Clear();

            AddItem(enabled ? "\uE73E" : "", L.T("tray.enable"), delegate
            {
                HideMenu();
                toggle();
            });
            AddSeparator();
            AddItem("\uE713", L.T("tray.settings"), delegate
            {
                HideMenu();
                settings();
            });
            AddItem("\uE7E8", L.T("tray.exit"), delegate
            {
                HideMenu();
                exit();
            });
        }

        private void AddItem(string glyph, string text, Action action)
        {
            Application app = Application.Current;
            Button button = new Button();
            Style style = app.Resources["MenuItemButtonStyle"] as Style;
            if (style != null) button.Style = style;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            FontIcon icon = new FontIcon();
            icon.FontSize = 14;
            icon.Glyph = glyph ?? "";
            icon.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(icon);

            TextBlock label = new TextBlock();
            label.Text = text;
            label.FontSize = 14;
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);

            button.Content = grid;
            button.Click += delegate { action(); };
            ItemsHost.Children.Add(button);
        }

        private void AddSeparator()
        {
            Brush brush = Application.Current.Resources["DividerStrokeColorDefaultBrush"] as Brush;
            Rectangle line = new Rectangle();
            line.Height = 1;
            line.Margin = new Thickness(4, 4, 4, 4);
            if (brush != null) line.Fill = brush;
            ItemsHost.Children.Add(line);
        }
    }
}
