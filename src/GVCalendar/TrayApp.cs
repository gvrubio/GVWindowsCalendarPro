using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GVCalendar
{
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly NotifyIcon notifyIcon = new NotifyIcon();
        private readonly CalendarFlyout flyout = new CalendarFlyout();
        private readonly Timer dateTimer = new Timer { Interval = 5000 };
        private readonly ContextMenuStrip menu = new ContextMenuStrip();
        private ToolStripMenuItem autoStartItem, isoItem, usItem, iconDayItem, iconWeekItem, secondsItem;
        private Theme theme = Theme.Load();
        private DateTime iconDate;
        private Icon currentIcon;

        public TrayApp(bool openOnStart)
        {
            // Crear el identificador para recibir el mensaje de "mostrar" de una segunda instancia.
            _ = flyout.Handle;
            flyout.ShowRequested += (s, e) => ShowFlyout();

            BuildMenu();
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.MouseClick += OnTrayClick;
            UpdateIcon(force: true);
            notifyIcon.Visible = true;

            dateTimer.Tick += (s, e) => UpdateIcon(force: false);
            dateTimer.Start();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemEvents.TimeChanged += OnTimeChanged;

            if (openOnStart) ShowFlyout();
        }

        private void OnTrayClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            if (flyout.Visible) { flyout.HideFlyout(); return; }
            // Al pulsar el icono con la ventana abierta, ésta se cierra al perder el foco
            // justo antes del clic: no la volvemos a abrir.
            if ((DateTime.Now - flyout.LastHidden).TotalMilliseconds < 300) return;
            ShowFlyout();
        }

        private void ShowFlyout()
        {
            if (flyout.Visible) { flyout.Activate(); return; }
            flyout.ShowFlyout();
        }

        private void UpdateIcon(bool force)
        {
            DateTime today = DateTime.Today;
            if (!force && today == iconDate) return;
            iconDate = today;

            Icon old = currentIcon;
            currentIcon = TrayIconRenderer.Render(today, theme);
            notifyIcon.Icon = currentIcon;
            old?.Dispose();

            string tip = char.ToUpper(today.ToString("D")[0]) + today.ToString("D").Substring(1)
                         + "\n" + L.Week + " " + Weeks.WeekOfYear(today);
            notifyIcon.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
            if (!force) flyout.RefreshSettings();
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color
                && e.Category != UserPreferenceCategory.VisualStyle && e.Category != UserPreferenceCategory.Locale)
                return;
            theme = Theme.Load();
            ApplyMenuTheme();
            flyout.RefreshTheme();
            UpdateIcon(force: true);
        }

        private void OnTimeChanged(object sender, EventArgs e) => UpdateIcon(force: true);

        // ---------------------------------------------------------------- menú

        private void BuildMenu()
        {
            var open = new ToolStripMenuItem(L.Open, null, (s, e) => ShowFlyout()) { Font = new Font(menu.Font, FontStyle.Bold) };
            autoStartItem = new ToolStripMenuItem(L.StartWithWindows, null, (s, e) => ToggleAutoStart());

            isoItem = new ToolStripMenuItem(L.WeekIso, null, (s, e) => SetWeekMode(WeekMode.Iso));
            usItem = new ToolStripMenuItem(L.WeekUs, null, (s, e) => SetWeekMode(WeekMode.Us));
            var weeks = new ToolStripMenuItem(L.WeekNumbering);
            weeks.DropDownItems.AddRange(new ToolStripItem[] { isoItem, usItem });

            iconDayItem = new ToolStripMenuItem(L.IconDay, null, (s, e) => SetIconMode(IconMode.Day));
            iconWeekItem = new ToolStripMenuItem(L.IconWeek, null, (s, e) => SetIconMode(IconMode.Week));
            var icon = new ToolStripMenuItem(L.TrayIcon);
            icon.DropDownItems.AddRange(new ToolStripItem[] { iconDayItem, iconWeekItem });

            secondsItem = new ToolStripMenuItem(L.ShowSeconds, null, (s, e) =>
            {
                Settings.ShowSeconds = !Settings.ShowSeconds;
                Settings.Save();
                flyout.RefreshSettings();
            });

            menu.Items.AddRange(new ToolStripItem[]
            {
                open,
                new ToolStripSeparator(),
                autoStartItem,
                weeks,
                icon,
                secondsItem,
                new ToolStripSeparator(),
                new ToolStripMenuItem(L.About, null, (s, e) => ShowAbout()),
                new ToolStripMenuItem(L.Exit, null, (s, e) => ExitThread()),
            });
            menu.Opening += (s, e) => SyncMenu();
            ApplyMenuTheme();
        }

        private void SyncMenu()
        {
            autoStartItem.Checked = AutoStart.IsEnabled;
            isoItem.Checked = Settings.WeekMode == WeekMode.Iso;
            usItem.Checked = Settings.WeekMode == WeekMode.Us;
            iconDayItem.Checked = Settings.IconMode == IconMode.Day;
            iconWeekItem.Checked = Settings.IconMode == IconMode.Week;
            secondsItem.Checked = Settings.ShowSeconds;
        }

        private void ApplyMenuTheme()
        {
            menu.Renderer = new ToolStripProfessionalRenderer(new MenuColors(theme)) { RoundedEdges = false };
            Color fore = theme.Dark ? Color.White : Color.Black;
            foreach (ToolStripItem item in menu.Items) SetForeColor(item, fore);
        }

        private static void SetForeColor(ToolStripItem item, Color c)
        {
            item.ForeColor = c;
            if (item is ToolStripMenuItem mi)
                foreach (ToolStripItem child in mi.DropDownItems) SetForeColor(child, c);
        }

        private void ToggleAutoStart()
        {
            try { AutoStart.Set(!AutoStart.IsEnabled); }
            catch (Exception ex)
            {
                MessageBox.Show(L.AutoStartError + "\n" + ex.Message, L.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetWeekMode(WeekMode mode)
        {
            Settings.WeekMode = mode;
            Settings.Save();
            flyout.RefreshSettings();
            UpdateIcon(force: true);
        }

        private void SetIconMode(IconMode mode)
        {
            Settings.IconMode = mode;
            Settings.Save();
            UpdateIcon(force: true);
        }

        private void ShowAbout()
        {
            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            MessageBox.Show(L.AppName + " " + v.ToString(3) + "\n\n" + L.AboutText, L.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void ExitThreadCore()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.TimeChanged -= OnTimeChanged;
            dateTimer.Stop();
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            currentIcon?.Dispose();
            flyout.Dispose();
            menu.Dispose();
            base.ExitThreadCore();
        }

        private sealed class MenuColors : ProfessionalColorTable
        {
            private readonly Theme t;
            private readonly Color back, hover, border;

            public MenuColors(Theme theme)
            {
                t = theme;
                back = t.Dark ? Color.FromArgb(0x2B, 0x2B, 0x2B) : Color.FromArgb(0xF9, 0xF9, 0xF9);
                hover = t.Dark ? Color.FromArgb(0x3D, 0x3D, 0x3D) : Color.FromArgb(0xE5, 0xE5, 0xE5);
                border = t.Dark ? Color.FromArgb(0x45, 0x45, 0x45) : Color.FromArgb(0xD0, 0xD0, 0xD0);
                UseSystemColors = false;
            }

            public override Color ToolStripDropDownBackground => back;
            public override Color ImageMarginGradientBegin => back;
            public override Color ImageMarginGradientMiddle => back;
            public override Color ImageMarginGradientEnd => back;
            public override Color MenuBorder => border;
            public override Color MenuItemBorder => hover;
            public override Color MenuItemSelected => hover;
            public override Color MenuItemSelectedGradientBegin => hover;
            public override Color MenuItemSelectedGradientEnd => hover;
            public override Color SeparatorDark => border;
            public override Color SeparatorLight => back;
            public override Color CheckBackground => hover;
            public override Color CheckSelectedBackground => hover;
            public override Color CheckPressedBackground => hover;
        }
    }
}
