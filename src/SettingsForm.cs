using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace ShakeShake
{
    internal sealed class SettingsForm : Form
    {
        private const int ContentWidth = 480;

        private readonly Settings _settings;
        private readonly Action<Settings> _onChanged;
        private readonly Action _onLanguageChanged;

        private TableLayoutPanel _root;
        private TableLayoutPanel _langRow;
        private TableLayoutPanel _bottom;

        private Label _langLabel;
        private ComboBox _lang;
        private CheckBox _chkEnable;
        private Label _hintEnable;
        private GroupBox _grpZoom;
        private TrackBar _zoom;
        private Label _zoomValue;
        private PictureBox _preview;
        private GroupBox _grpExclude;
        private ListBox _excludeList;
        private Label _excludeHint;
        private Button _btnBrowse;
        private Button _btnAddCurrent;
        private Button _btnRemove;
        private CheckBox _chkAutostart;
        private LinkLabel _lnkAdvanced;
        private Panel _advanced;
        private Label _lblSens;
        private Label _lblRestore;
        private Label _lblNote;
        private Label _sensValue;
        private Label _restoreValue;
        private TrackBar _sens;
        private TrackBar _restore;
        private Button _btnDefaults;
        private Button _btnClose;
        private Label _about;
        private readonly ToolTip _tips = new ToolTip();

        private readonly Timer _saveTimer;
        private readonly Timer _captureTimer;
        private bool _updating;
        private bool _advancedOpen;
        private bool _allowClose;
        private bool _capturing;
        private int _captureLeft;

        private static readonly Color Accent = Color.FromArgb(59, 111, 245);
        private static readonly Color TextColor = Color.FromArgb(31, 35, 41);
        private static readonly Color MutedColor = Color.FromArgb(110, 118, 130);
        private static readonly Color PageColor = Color.FromArgb(247, 248, 250);

        public SettingsForm(Settings settings, Action<Settings> onChanged, Action onLanguageChanged)
        {
            _settings = settings;
            _onChanged = onChanged;
            _onLanguageChanged = onLanguageChanged;

            SuspendLayout();
            BuildUi();
            ResumeLayout(false);

            _saveTimer = new Timer();
            _saveTimer.Interval = 400;
            _saveTimer.Tick += delegate { _saveTimer.Stop(); _settings.Save(); };

            _captureTimer = new Timer();
            _captureTimer.Interval = 1000;
            _captureTimer.Tick += CaptureTick;

            L.SetLanguage(_settings.Language);
            ApplyTexts();
            ApplyValues();
            AdjustSize();

            if (Environment.GetEnvironmentVariable("SHAKESHAKE_UI_ADVANCED") == "1") ToggleAdvanced();
        }

        // ---------------------------------------------------------------- layout

        private void BuildUi()
        {
            Text = "ShakeShake";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = PageColor;
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            Padding = new Padding(22, 16, 22, 18);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { }

            _root = new TableLayoutPanel();
            _root.AutoSize = true;
            _root.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _root.ColumnCount = 1;
            _root.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _root.RowCount = 9;
            for (int i = 0; i < 9; i++) _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _root.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            _root.Location = new Point(Padding.Left, Padding.Top);
            _root.Margin = Padding.Empty;
            Controls.Add(_root);

            // --- row 0: language
            _langRow = new TableLayoutPanel();
            _langRow.AutoSize = true;
            _langRow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _langRow.ColumnCount = 2;
            _langRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _langRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 236));
            _langRow.RowCount = 1;
            _langRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _langRow.Margin = new Padding(0, 0, 0, 8);
            _root.Controls.Add(_langRow, 0, 0);

            _langLabel = new Label();
            _langLabel.AutoSize = true;
            _langLabel.BackColor = Color.Transparent;
            _langLabel.Margin = new Padding(0, 7, 12, 0);
            _langRow.Controls.Add(_langLabel, 0, 0);

            _lang = new ComboBox();
            _lang.DropDownStyle = ComboBoxStyle.DropDownList;
            _lang.Dock = DockStyle.Fill;
            _lang.Margin = new Padding(0);
            _lang.SelectedIndexChanged += delegate { OnLanguageChanged(); };
            _langRow.Controls.Add(_lang, 1, 0);

            // --- row 1: enable
            _chkEnable = new CheckBox();
            _chkEnable.AutoSize = true;
            _chkEnable.Font = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold);
            _chkEnable.Margin = new Padding(0, 2, 0, 2);
            _chkEnable.CheckedChanged += delegate
            {
                if (_updating) return;
                _settings.Enabled = _chkEnable.Checked;
                NotifyChanged();
            };
            _root.Controls.Add(_chkEnable, 0, 1);

            // --- row 2: hint
            _hintEnable = new Label();
            _hintEnable.AutoSize = false;
            _hintEnable.Size = new Size(ContentWidth - 18, 18);
            _hintEnable.ForeColor = MutedColor;
            _hintEnable.BackColor = Color.Transparent;
            _hintEnable.Font = new Font("Microsoft YaHei UI", 8.25f);
            _hintEnable.Margin = new Padding(18, 0, 0, 10);
            _root.Controls.Add(_hintEnable, 0, 2);

            // --- row 3: zoom
            _grpZoom = new GroupBox();
            _grpZoom.Size = new Size(ContentWidth, 140);
            _grpZoom.Margin = new Padding(0, 0, 0, 12);
            _root.Controls.Add(_grpZoom, 0, 3);

            _zoom = new TrackBar();
            _zoom.SetBounds(24, 30, 300, 45);
            _zoom.Minimum = 20;
            _zoom.Maximum = 60;
            _zoom.SmallChange = 5;
            _zoom.LargeChange = 10;
            _zoom.TickStyle = TickStyle.None;
            _zoom.ValueChanged += delegate
            {
                if (_updating) return;
                _settings.Zoom = _zoom.Value / 10.0;
                UpdateZoomLabel();
                _preview.Invalidate();
                NotifyChanged();
            };
            _grpZoom.Controls.Add(_zoom);

            _zoomValue = new Label();
            _zoomValue.AutoSize = false;
            _zoomValue.SetBounds(336, 34, 120, 30);
            _zoomValue.Font = new Font("Microsoft YaHei UI", 13f, FontStyle.Bold);
            _zoomValue.ForeColor = Accent;
            _grpZoom.Controls.Add(_zoomValue);

            _preview = new PictureBox();
            _preview.SetBounds(24, 82, 130, 48);
            _preview.BackColor = Color.White;
            _preview.Paint += PreviewPaint;
            _grpZoom.Controls.Add(_preview);

            Label zoomHint = new Label();
            zoomHint.AutoSize = false;
            zoomHint.SetBounds(170, 92, 290, 32);
            zoomHint.ForeColor = MutedColor;
            zoomHint.BackColor = Color.Transparent;
            zoomHint.Font = new Font("Microsoft YaHei UI", 8.25f);
            zoomHint.Tag = "zoom.hint";
            _grpZoom.Controls.Add(zoomHint);

            // --- row 4: exclude list
            _grpExclude = new GroupBox();
            _grpExclude.Size = new Size(ContentWidth, 168);
            _grpExclude.Margin = new Padding(0, 0, 0, 12);
            _root.Controls.Add(_grpExclude, 0, 4);

            _excludeList = new ListBox();
            _excludeList.SetBounds(24, 24, 432, 88);
            _excludeList.IntegralHeight = false;
            _excludeList.SelectedIndexChanged += delegate
            {
                _btnRemove.Enabled = _excludeList.SelectedIndex >= 0;
            };
            _grpExclude.Controls.Add(_excludeList);

            _excludeHint = new Label();
            _excludeHint.AutoSize = false;
            _excludeHint.SetBounds(24, 116, 432, 18);
            _excludeHint.ForeColor = MutedColor;
            _excludeHint.BackColor = Color.Transparent;
            _excludeHint.Font = new Font("Microsoft YaHei UI", 8.25f);
            _grpExclude.Controls.Add(_excludeHint);

            _btnBrowse = MakeButton();
            _btnBrowse.SetBounds(24, 136, 104, 28);
            _btnBrowse.Click += delegate { BrowseForExe(); };
            _grpExclude.Controls.Add(_btnBrowse);

            _btnAddCurrent = MakeButton();
            _btnAddCurrent.SetBounds(136, 136, 128, 28);
            _btnAddCurrent.Click += delegate { StartCapture(); };
            _grpExclude.Controls.Add(_btnAddCurrent);

            _btnRemove = MakeButton();
            _btnRemove.SetBounds(272, 136, 84, 28);
            _btnRemove.Enabled = false;
            _btnRemove.Click += delegate { RemoveSelected(); };
            _grpExclude.Controls.Add(_btnRemove);

            // --- row 5: autostart
            _chkAutostart = new CheckBox();
            _chkAutostart.AutoSize = true;
            _chkAutostart.Margin = new Padding(0, 2, 0, 6);
            _chkAutostart.CheckedChanged += delegate
            {
                if (_updating) return;
                _settings.Autostart = _chkAutostart.Checked;
                AutoStart.SetEnabled(_settings.Autostart);
                NotifyChanged();
            };
            _root.Controls.Add(_chkAutostart, 0, 5);

            // --- row 6: advanced link
            _lnkAdvanced = new LinkLabel();
            _lnkAdvanced.AutoSize = true;
            _lnkAdvanced.LinkColor = Accent;
            _lnkAdvanced.ActiveLinkColor = Color.FromArgb(30, 80, 200);
            _lnkAdvanced.LinkBehavior = LinkBehavior.HoverUnderline;
            _lnkAdvanced.Margin = new Padding(0, 0, 0, 8);
            _lnkAdvanced.LinkClicked += delegate { ToggleAdvanced(); };
            _root.Controls.Add(_lnkAdvanced, 0, 6);

            // --- row 7: advanced panel
            _advanced = new Panel();
            _advanced.Size = new Size(ContentWidth, 118);
            _advanced.Margin = new Padding(0, 0, 0, 10);
            _advanced.Visible = false;
            _root.Controls.Add(_advanced, 0, 7);

            _lblSens = new Label();
            _lblSens.AutoSize = true;
            _lblSens.SetBounds(8, 12, 96, 20);
            _advanced.Controls.Add(_lblSens);

            _sens = new TrackBar();
            _sens.SetBounds(104, 4, 240, 45);
            _sens.Minimum = 1;
            _sens.Maximum = 10;
            _sens.TickStyle = TickStyle.None;
            _sens.ValueChanged += delegate
            {
                if (_updating) return;
                _settings.Sensitivity = _sens.Value;
                _sensValue.Text = _sens.Value.ToString();
                NotifyChanged();
            };
            _advanced.Controls.Add(_sens);

            _sensValue = new Label();
            _sensValue.AutoSize = false;
            _sensValue.SetBounds(352, 14, 60, 20);
            _sensValue.ForeColor = TextColor;
            _advanced.Controls.Add(_sensValue);

            _lblRestore = new Label();
            _lblRestore.AutoSize = true;
            _lblRestore.SetBounds(8, 56, 96, 20);
            _advanced.Controls.Add(_lblRestore);

            _restore = new TrackBar();
            _restore.SetBounds(104, 48, 240, 45);
            _restore.Minimum = 50;
            _restore.Maximum = 500;
            _restore.SmallChange = 10;
            _restore.LargeChange = 50;
            _restore.TickStyle = TickStyle.None;
            _restore.ValueChanged += delegate
            {
                if (_updating) return;
                _settings.RestoreMs = _restore.Value;
                UpdateRestoreLabel();
                NotifyChanged();
            };
            _advanced.Controls.Add(_restore);

            _restoreValue = new Label();
            _restoreValue.AutoSize = false;
            _restoreValue.SetBounds(352, 58, 90, 20);
            _restoreValue.ForeColor = TextColor;
            _advanced.Controls.Add(_restoreValue);

            _lblNote = new Label();
            _lblNote.AutoSize = false;
            _lblNote.SetBounds(8, 96, 450, 18);
            _lblNote.ForeColor = MutedColor;
            _lblNote.BackColor = Color.Transparent;
            _lblNote.Font = new Font("Microsoft YaHei UI", 8.25f);
            _advanced.Controls.Add(_lblNote);

            // --- row 8: bottom bar
            _bottom = new TableLayoutPanel();
            _bottom.AutoSize = true;
            _bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _bottom.ColumnCount = 3;
            _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _bottom.RowCount = 1;
            _bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _bottom.Dock = DockStyle.Fill;
            _bottom.Margin = new Padding(0, 6, 0, 0);
            _root.Controls.Add(_bottom, 0, 8);

            _about = new Label();
            _about.AutoSize = false;
            _about.Dock = DockStyle.Fill;
            _about.TextAlign = ContentAlignment.MiddleLeft;
            _about.ForeColor = MutedColor;
            _about.BackColor = Color.Transparent;
            _about.Font = new Font("Microsoft YaHei UI", 7.5f);
            _about.Margin = Padding.Empty;
            _tips.SetToolTip(_about, Settings.FilePath);
            _bottom.Controls.Add(_about, 0, 0);

            _btnDefaults = MakeButton();
            _btnDefaults.AutoSize = true;
            _btnDefaults.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _btnDefaults.MinimumSize = new Size(96, 32);
            _btnDefaults.Padding = new Padding(12, 2, 12, 2);
            _btnDefaults.Margin = new Padding(8, 0, 0, 0);
            _btnDefaults.Click += delegate { RestoreDefaults(); };
            _bottom.Controls.Add(_btnDefaults, 1, 0);

            _btnClose = MakeButton();
            _btnClose.AutoSize = true;
            _btnClose.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _btnClose.MinimumSize = new Size(96, 32);
            _btnClose.Padding = new Padding(12, 2, 12, 2);
            _btnClose.Margin = new Padding(8, 0, 0, 0);
            _btnClose.BackColor = Accent;
            _btnClose.ForeColor = Color.White;
            _btnClose.FlatAppearance.BorderColor = Accent;
            _btnClose.Click += delegate { Hide(); };
            _bottom.Controls.Add(_btnClose, 2, 0);
        }

        /// <summary>Fit the window to the current content (called after DPI scaling and on collapse).</summary>
        private void AdjustSize()
        {
            if (_root == null) return;
            _root.PerformLayout();
            Size pref = _root.PreferredSize;
            int w = pref.Width + Padding.Horizontal;
            int h = pref.Height + Padding.Vertical;

            // On a small screen (or very high scaling) never grow past the work area: scroll instead.
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int maxH = Math.Max(320, wa.Height - 60);
            bool needScroll = h > maxH;
            if (needScroll) h = maxH;
            AutoScroll = needScroll;

            ClientSize = new Size(w, h);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            AdjustSize();
        }

        private static Button MakeButton()
        {
            Button b = new Button();
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Color.FromArgb(205, 210, 220);
            b.BackColor = Color.White;
            b.ForeColor = TextColor;
            b.UseVisualStyleBackColor = false;
            b.Font = new Font("Microsoft YaHei UI", 9f);
            return b;
        }

        // ---------------------------------------------------------------- text

        private void ApplyTexts()
        {
            _updating = true;
            Text = L.T("form.title");

            _langLabel.Text = L.T("lang.label");
            int langIndex = _settings.Language == "zh-CN" ? 1 : (_settings.Language == "en-US" ? 2 : 0);
            _lang.Items.Clear();
            _lang.Items.Add(L.T("lang.auto"));
            _lang.Items.Add(L.T("lang.zh"));
            _lang.Items.Add(L.T("lang.en"));
            _lang.SelectedIndex = langIndex;

            _chkEnable.Text = L.T("chk.enable");
            _hintEnable.Text = L.T("chk.enable.hint");
            _grpZoom.Text = L.T("grp.zoom");
            _grpExclude.Text = L.T("grp.exclude");
            _excludeHint.Text = L.T("exclude.hint");
            _btnBrowse.Text = L.T("btn.browse");
            if (!_capturing) _btnAddCurrent.Text = L.T("btn.addcurrent");
            _btnRemove.Text = L.T("btn.remove");
            _chkAutostart.Text = L.T("chk.autostart");
            _lnkAdvanced.Text = _advancedOpen ? L.T("lnk.advanced.open") : L.T("lnk.advanced");
            _lblSens.Text = L.T("adv.sensitivity");
            _lblRestore.Text = L.T("adv.restore");
            _lblNote.Text = L.T("adv.note");
            _btnDefaults.Text = L.T("btn.defaults");
            _btnClose.Text = L.T("btn.close");
            _about.Text = L.T("about", L.Version);

            Label zoomHint = FindZoomHint();
            if (zoomHint != null) zoomHint.Text = L.T("zoom.hint");

            UpdateZoomLabel();
            UpdateRestoreLabel();
            _sensValue.Text = _settings.Sensitivity.ToString();
            _updating = false;

            AdjustSize();
        }

        private Label FindZoomHint()
        {
            foreach (Control c in _grpZoom.Controls)
            {
                Label l = c as Label;
                if (l != null && (string)l.Tag == "zoom.hint") return l;
            }
            return null;
        }

        private void ApplyValues()
        {
            _updating = true;
            _chkEnable.Checked = _settings.Enabled;
            _zoom.Value = (int)Math.Round(_settings.Zoom * 10);
            _sens.Value = _settings.Sensitivity;
            _restore.Value = _settings.RestoreMs;
            _chkAutostart.Checked = AutoStart.IsEnabled();
            _settings.Autostart = _chkAutostart.Checked;
            _excludeList.Items.Clear();
            for (int i = 0; i < _settings.Exclude.Count; i++) _excludeList.Items.Add(_settings.Exclude[i]);
            _updating = false;
            UpdateZoomLabel();
            UpdateRestoreLabel();
            _sensValue.Text = _settings.Sensitivity.ToString();
            _preview.Invalidate();
        }

        public void SyncFromSettings()
        {
            _updating = true;
            _chkEnable.Checked = _settings.Enabled;
            _updating = false;
        }

        private void UpdateZoomLabel()
        {
            _zoomValue.Text = string.Format("{0:0.0}×", _settings.Zoom);
        }

        private void UpdateRestoreLabel()
        {
            _restoreValue.Text = L.T("unit.ms", _settings.RestoreMs);
        }

        private void PreviewPaint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.White);
            using (Pen p = new Pen(Color.FromArgb(228, 231, 237)))
            {
                g.DrawRectangle(p, 0, 0, _preview.Width - 1, _preview.Height - 1);
            }
            float baseSize = 15f;
            float bigSize = (float)Math.Min(baseSize * _settings.Zoom, 44f);
            DrawArrow(g, new RectangleF(22, 44 - baseSize, baseSize, baseSize), Color.FromArgb(214, 219, 228), Color.FromArgb(180, 186, 196));
            DrawArrow(g, new RectangleF(62, 44 - bigSize, bigSize, bigSize), Color.White, Color.FromArgb(40, 44, 52));
        }

        private static readonly PointF[] ArrowShape = new PointF[]
        {
            new PointF(0.10f, 0.02f), new PointF(0.10f, 0.80f), new PointF(0.30f, 0.60f),
            new PointF(0.44f, 0.98f), new PointF(0.58f, 0.90f), new PointF(0.44f, 0.54f),
            new PointF(0.68f, 0.52f)
        };

        private static void DrawArrow(Graphics g, RectangleF box, Color fill, Color outline)
        {
            PointF[] pts = new PointF[ArrowShape.Length];
            for (int i = 0; i < pts.Length; i++)
            {
                pts[i] = new PointF(box.X + ArrowShape[i].X * box.Width, box.Y + ArrowShape[i].Y * box.Height);
            }
            using (SolidBrush b = new SolidBrush(fill))
            using (Pen p = new Pen(outline, Math.Max(1f, box.Width / 22f)))
            {
                p.LineJoin = LineJoin.Round;
                g.FillPolygon(b, pts);
                g.DrawPolygon(p, pts);
            }
        }

        // ---------------------------------------------------------------- events

        private void OnLanguageChanged()
        {
            if (_updating || _lang.SelectedIndex < 0) return;
            string tag = _lang.SelectedIndex == 1 ? "zh-CN" : (_lang.SelectedIndex == 2 ? "en-US" : "auto");
            if (tag == _settings.Language) return;
            _settings.Language = tag;
            L.SetLanguage(tag);
            ApplyTexts();
            if (_onLanguageChanged != null) _onLanguageChanged();
            NotifyChanged();
        }

        private void ToggleAdvanced()
        {
            _advancedOpen = !_advancedOpen;
            _advanced.Visible = _advancedOpen;
            _lnkAdvanced.Text = _advancedOpen ? L.T("lnk.advanced.open") : L.T("lnk.advanced");
            AdjustSize();
        }

        private void NotifyChanged()
        {
            if (_onChanged != null) _onChanged(_settings);
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private void RestoreDefaults()
        {
            _settings.Zoom = 3.0;
            _settings.Sensitivity = 5;
            _settings.RestoreMs = 100;

            ApplyValues();
            NotifyChanged();
            MessageBox.Show(this, L.T("defaults.done"), L.T("err.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BrowseForExe()
        {
            using (OpenFileDialog dlg = new OpenFileDialog())
            {
                dlg.Title = L.T("dlg.pickexe");
                dlg.Filter = L.T("dlg.filter");
                dlg.CheckFileExists = true;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                AddExclude(Path.GetFileName(dlg.FileName));
            }
        }

        private void StartCapture()
        {
            if (_capturing) return;
            _capturing = true;
            _captureLeft = 3;
            _btnAddCurrent.Enabled = false;
            _btnAddCurrent.Text = L.T("exclude.wait", _captureLeft);
            AdjustSize();
            _captureTimer.Start();
        }

        private void CaptureTick(object sender, EventArgs e)
        {
            _captureLeft--;
            if (_captureLeft > 0)
            {
                _btnAddCurrent.Text = L.T("exclude.wait", _captureLeft);
                AdjustSize();
                return;
            }

            _captureTimer.Stop();
            _capturing = false;
            _btnAddCurrent.Enabled = true;
            _btnAddCurrent.Text = L.T("btn.addcurrent");
            AdjustSize();

            string name = ProcInfo.ForegroundName();
            if (string.IsNullOrEmpty(name) ||
                string.Equals(name, ProcInfo.OwnExeName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, L.T("exclude.fail"), L.T("err.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    MessageBox.Show(this, L.T("exclude.dupe"), L.T("err.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }
            _settings.Exclude.Add(name);
            _excludeList.Items.Add(name);
            NotifyChanged();
        }

        private void RemoveSelected()
        {
            int i = _excludeList.SelectedIndex;
            if (i < 0) return;
            _settings.Exclude.RemoveAt(i);
            _excludeList.Items.RemoveAt(i);
            _btnRemove.Enabled = false;
            NotifyChanged();
        }

        // ---------------------------------------------------------------- window

        public void ShowSettings()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        public void AllowClose()
        {
            _allowClose = true;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
