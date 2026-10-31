using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace GVCalendar
{
    /// <summary>
    /// Ventana emergente al estilo del calendario de la barra de tareas de Windows,
    /// con una columna adicional de números de semana.
    /// </summary>
    internal sealed class CalendarFlyout : Form
    {
        private enum ViewMode { Month, Year, Decade }

        private sealed class HitRegion
        {
            public RectangleF Rect;
            public string Id;
            public Action Click;
        }

        // Medidas en píxeles a 96 ppp.
        private const float Pad = 16, WeekCol = 38, Cell = 42, HeaderH = 96, NavY = 104, NavH = 36;
        private const float DowY = 148, DowH = 30, GridY = 178, FooterY = 440, TotalH = 488;
        private const float TotalW = Pad + WeekCol + Cell * 7 + Pad;

        public static readonly int ShowMessage = Native.RegisterWindowMessage("GVWindowsCalendarPro_Show");

        private readonly CultureInfo culture = CultureInfo.CurrentCulture;
        private readonly List<HitRegion> hits = new List<HitRegion>();
        private readonly Timer clockTimer = new Timer { Interval = 250 };
        private readonly Timer animTimer = new Timer { Interval = 15 };

        private Theme theme = Theme.Load();
        private float scale = 1f;
        private Font timeFont, linkFont, titleFont, dowFont, dayFont, dayBoldFont, weekFont, weekBoldFont, cellFont, footerFont;

        private ViewMode view = ViewMode.Month;
        private DateTime displayMonth; // primer día del mes mostrado
        private DateTime selected;
        private DateTime? selectedWeek; // inicio de la semana seleccionada al pulsar su número
        private string hoverId, pressedId;
        private string lastClock;

        private DateTime animStart;
        private int animFromY, animTargetY;

        public DateTime LastHidden { get; private set; }
        public event EventHandler ShowRequested;

        public CalendarFlyout()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            KeyPreview = true;
            Text = L.AppName;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            clockTimer.Tick += (s, e) => TickClock();
            animTimer.Tick += (s, e) => TickAnimation();
            GoToToday();
            BuildFonts();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
                cp.ClassStyle |= Native.CS_DROPSHADOW;
                return cp;
            }
        }

        // ---------------------------------------------------------------- mostrar / ocultar

        public void ShowFlyout()
        {
            theme = Theme.Load();
            GoToToday();

            Point cursor = Cursor.Position;
            Screen screen = Screen.FromPoint(cursor);
            float newScale = Native.GetDpiForPoint(new Point(screen.WorkingArea.Left + 1, screen.WorkingArea.Top + 1)) / 96f;
            if (Math.Abs(newScale - scale) > 0.01f) { scale = newScale; BuildFonts(); }

            var size = new Size((int)Math.Ceiling(TotalW * scale), (int)Math.Ceiling(TotalH * scale));
            Point pos = ComputePosition(screen, size, out bool fromTop);

            BackColor = theme.Back;
            Opacity = 0;
            // La animación entra desde el borde de la barra de tareas.
            int offset = (int)(14 * scale);
            animTargetY = pos.Y;
            animFromY = fromTop ? pos.Y - offset : pos.Y + offset;
            Bounds = new Rectangle(pos.X, animFromY, size.Width, size.Height);

            Show();
            ApplyWindowAttributes();
            Activate();
            Native.SetForegroundWindow(Handle);
            Focus();

            animStart = DateTime.Now;
            animTimer.Start();
            clockTimer.Start();
            Invalidate();
        }

        public void HideFlyout()
        {
            if (!Visible) return;
            animTimer.Stop();
            clockTimer.Stop();
            Hide();
            LastHidden = DateTime.Now;
        }

        private Point ComputePosition(Screen screen, Size size, out bool fromTop)
        {
            Rectangle wa = screen.WorkingArea, b = screen.Bounds;
            int m = (int)(12 * scale);
            int x = wa.Right - size.Width - m;
            int y = wa.Bottom - size.Height - m;
            fromTop = false;

            if (wa.Top > b.Top) { y = wa.Top + m; fromTop = true; }          // barra arriba
            else if (wa.Left > b.Left) { x = wa.Left + m; }                  // barra a la izquierda
            // barra a la derecha o abajo: esquina inferior derecha (por defecto)
            return new Point(x, y);
        }

        private void ApplyWindowAttributes()
        {
            Native.TrySetAttribute(Handle, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, theme.Dark ? 1 : 0);
            if (Native.IsWindows11)
            {
                Native.TrySetAttribute(Handle, Native.DWMWA_WINDOW_CORNER_PREFERENCE, Native.DWMWCP_ROUND);
                Color c = theme.Border;
                Native.TrySetAttribute(Handle, Native.DWMWA_BORDER_COLOR, c.R | (c.G << 8) | (c.B << 16));
            }
        }

        private void TickAnimation()
        {
            const double duration = 170;
            double t = Math.Min(1, (DateTime.Now - animStart).TotalMilliseconds / duration);
            double ease = 1 - Math.Pow(1 - t, 3);
            Opacity = ease;
            Top = (int)Math.Round(animFromY + (animTargetY - animFromY) * ease);
            if (t >= 1)
            {
                Top = animTargetY;
                Opacity = 1;
                animTimer.Stop();
            }
        }

        private void TickClock()
        {
            if (DateTime.Today != Today) { Today = DateTime.Today; Invalidate(); }
            string now = FormatTime(DateTime.Now);
            if (now != lastClock) Invalidate(Rectangle.Ceiling(Sc(new RectangleF(0, 0, TotalW, HeaderH))));
        }

        private DateTime Today { get; set; } = DateTime.Today;

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            HideFlyout();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == ShowMessage && ShowMessage != 0)
            {
                ShowRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (m.Msg == Native.WM_DPICHANGED)
            {
                // Gestionamos la escala nosotros mismos para que el dibujo sea nítido.
                float newScale = (m.WParam.ToInt64() & 0xFFFF) / 96f;
                if (newScale > 0 && Math.Abs(newScale - scale) > 0.01f)
                {
                    scale = newScale;
                    BuildFonts();
                    var size = new Size((int)Math.Ceiling(TotalW * scale), (int)Math.Ceiling(TotalH * scale));
                    var pos = ComputePosition(Screen.FromHandle(Handle), size, out _);
                    animFromY = animTargetY = pos.Y;
                    Bounds = new Rectangle(pos, size);
                    Invalidate();
                }
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        public void RefreshTheme()
        {
            theme = Theme.Load();
            BackColor = theme.Back;
            if (IsHandleCreated) ApplyWindowAttributes();
            Invalidate();
        }

        public void RefreshSettings()
        {
            Today = DateTime.Today;
            selectedWeek = null;
            Invalidate();
        }

        // ---------------------------------------------------------------- navegación

        private void GoToToday()
        {
            Today = DateTime.Today;
            selected = Today;
            selectedWeek = null;
            displayMonth = new DateTime(Today.Year, Today.Month, 1);
            view = ViewMode.Month;
            Invalidate();
        }

        private void Navigate(int delta)
        {
            int months = view == ViewMode.Month ? delta : view == ViewMode.Year ? 12 * delta : 120 * delta;
            SetDisplayMonth(displayMonth.Year, displayMonth.Month + months);
            selectedWeek = null;
            Invalidate();
        }

        /// <summary>Cambia el mes mostrado sin salir del rango que DateTime puede dibujar.</summary>
        private void SetDisplayMonth(int year, int month)
        {
            long index = (long)year * 12 + (month - 1);
            index = Math.Max(1L * 12 + 1, Math.Min(9999L * 12 + 10, index)); // feb. 0001 – nov. 9999
            displayMonth = new DateTime((int)(index / 12), (int)(index % 12) + 1, 1);
        }

        private void ZoomOut()
        {
            if (view == ViewMode.Month) view = ViewMode.Year;
            else if (view == ViewMode.Year) view = ViewMode.Decade;
            Invalidate();
        }

        private void SelectDay(DateTime d)
        {
            selected = d;
            selectedWeek = null;
            if (d.Month != displayMonth.Month || d.Year != displayMonth.Year)
                SetDisplayMonth(d.Year, d.Month);
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Escape: HideFlyout(); break;
                case Keys.Home: GoToToday(); break;
                case Keys.PageUp: Navigate(-1); break;
                case Keys.PageDown: Navigate(1); break;
                case Keys.Left: MoveSelection(-1); break;
                case Keys.Right: MoveSelection(1); break;
                case Keys.Up: MoveSelection(-7); break;
                case Keys.Down: MoveSelection(7); break;
                default: return;
            }
            e.Handled = true;
        }

        private void MoveSelection(int days)
        {
            if (view != ViewMode.Month) { Navigate(days > 0 ? 1 : -1); return; }
            try { SelectDay(selected.AddDays(days)); } catch (ArgumentOutOfRangeException) { }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Navigate(e.Delta > 0 ? -1 : 1);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            string id = HitTest(e.Location)?.Id;
            if (id != hoverId)
            {
                hoverId = id;
                Cursor = id != null ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverId != null) { hoverId = null; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressedId = HitTest(e.Location)?.Id;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left) return;
            HitRegion hit = HitTest(e.Location);
            bool same = hit != null && hit.Id == pressedId;
            pressedId = null;
            if (same) hit.Click?.Invoke();
            Invalidate();
        }

        private HitRegion HitTest(Point p)
        {
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].Rect.Contains(p)) return hits[i];
            return null;
        }

        // ---------------------------------------------------------------- dibujo

        private void BuildFonts()
        {
            foreach (var f in new[] { timeFont, linkFont, titleFont, dowFont, dayFont, dayBoldFont, weekFont, weekBoldFont, cellFont, footerFont })
                f?.Dispose();

            string text = PickFont("Segoe UI Variable Text", "Segoe UI");
            string display = PickFont("Segoe UI Variable Display", "Segoe UI Light", "Segoe UI");
            string semibold = PickFont("Segoe UI Variable Text Semibold", "Segoe UI Semibold", "Segoe UI");

            timeFont = MakeFont(display, 38, FontStyle.Regular);
            linkFont = MakeFont(text, 14, FontStyle.Regular);
            titleFont = MakeFont(semibold, 15, semibold == "Segoe UI" ? FontStyle.Bold : FontStyle.Regular);
            dowFont = MakeFont(text, 13, FontStyle.Regular);
            dayFont = MakeFont(text, 14, FontStyle.Regular);
            dayBoldFont = MakeFont(semibold, 14, semibold == "Segoe UI" ? FontStyle.Bold : FontStyle.Regular);
            weekFont = MakeFont(text, 12, FontStyle.Regular);
            weekBoldFont = MakeFont(semibold, 12, semibold == "Segoe UI" ? FontStyle.Bold : FontStyle.Regular);
            cellFont = MakeFont(text, 14, FontStyle.Regular);
            footerFont = MakeFont(text, 13, FontStyle.Regular);
        }

        private Font MakeFont(string family, float px, FontStyle style) => new Font(family, px * scale, style, GraphicsUnit.Pixel);

        private static HashSet<string> installedFonts;

        private static string PickFont(params string[] names)
        {
            if (installedFonts == null)
            {
                installedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in FontFamily.Families) installedFonts.Add(f.Name);
            }
            foreach (var n in names)
                if (installedFonts.Contains(n)) return n;
            return SystemFonts.MessageBoxFont.FontFamily.Name;
        }

        private float Sc(float v) => v * scale;
        private RectangleF Sc(RectangleF r) => new RectangleF(r.X * scale, r.Y * scale, r.Width * scale, r.Height * scale);

        private void AddHit(string id, RectangleF dipRect, Action click)
        {
            hits.Add(new HitRegion { Id = id, Rect = Sc(dipRect), Click = click });
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(theme.Back);
            hits.Clear();

            DrawHeader(g);
            DrawSeparator(g, HeaderH);
            DrawNavigation(g);

            switch (view)
            {
                case ViewMode.Month: DrawMonth(g); break;
                case ViewMode.Year: DrawYear(g); break;
                case ViewMode.Decade: DrawDecade(g); break;
            }

            DrawSeparator(g, FooterY);
            DrawFooter(g);

            if (!Native.IsWindows11)
            {
                using (var pen = new Pen(theme.Border))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private string FormatTime(DateTime t)
        {
            var dtf = culture.DateTimeFormat;
            return t.ToString(Settings.ShowSeconds ? dtf.LongTimePattern : dtf.ShortTimePattern, culture);
        }

        private string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0], culture) + s.Substring(1);

        private void DrawHeader(Graphics g)
        {
            lastClock = FormatTime(DateTime.Now);
            DrawText(g, lastClock, timeFont, theme.Fore, new RectangleF(Pad - 2, 8, TotalW - Pad * 2, 50), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            string date = Capitalize(Today.ToString(culture.DateTimeFormat.LongDatePattern, culture));
            Size sz = TextRenderer.MeasureText(date, linkFont, Size.Empty, TextFormatFlags.NoPadding);
            var rect = new RectangleF(Pad, 60, sz.Width / scale + 2, 22);
            AddHit("today", rect, GoToToday);
            Color c = theme.Accent;
            if (hoverId == "today") c = theme.Dark ? ControlPaint.Light(theme.Accent, 0.4f) : ControlPaint.Dark(theme.Accent, 0.1f);
            DrawText(g, date, linkFont, c, rect, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private void DrawSeparator(Graphics g, float y)
        {
            using (var pen = new Pen(theme.Separator, Math.Max(1, scale)))
                g.DrawLine(pen, 0, Sc(y), Width, Sc(y));
        }

        private void DrawNavigation(Graphics g)
        {
            string title;
            switch (view)
            {
                case ViewMode.Month:
                    title = Capitalize(displayMonth.ToString(culture.DateTimeFormat.YearMonthPattern, culture));
                    break;
                case ViewMode.Year:
                    title = displayMonth.Year.ToString(culture);
                    break;
                default:
                    int start = displayMonth.Year / 10 * 10;
                    title = start + " - " + (start + 9);
                    break;
            }

            Size sz = TextRenderer.MeasureText(title, titleFont);
            var titleRect = new RectangleF(Pad - 6, NavY, sz.Width / scale + 12, NavH);
            if (view != ViewMode.Decade)
            {
                AddHit("zoom", titleRect, ZoomOut);
                DrawHover(g, "zoom", titleRect, 4);
            }
            DrawText(g, title, titleFont, theme.Fore, titleRect, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            var up = new RectangleF(TotalW - Pad - 72, NavY + 2, 32, 32);
            var down = new RectangleF(TotalW - Pad - 34, NavY + 2, 32, 32);
            AddHit("prev", up, () => Navigate(-1));
            AddHit("next", down, () => Navigate(1));
            DrawHover(g, "prev", up, 4);
            DrawHover(g, "next", down, 4);
            DrawChevron(g, up, true);
            DrawChevron(g, down, false);
        }

        private void DrawChevron(Graphics g, RectangleF dip, bool upward)
        {
            RectangleF r = Sc(dip);
            float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, w = Sc(5), h = Sc(2.6f);
            float dir = upward ? -1 : 1;
            using (var pen = new Pen(theme.Muted, Sc(1.4f)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(cx - w, cy - h * dir), new PointF(cx, cy + h * dir), new PointF(cx + w, cy - h * dir) });
        }

        private void DrawHover(Graphics g, string id, RectangleF dip, float radius)
        {
            Color? c = id == pressedId && id == hoverId ? theme.Pressed : id == hoverId ? theme.Hover : (Color?)null;
            if (c == null) return;
            using (var path = TrayIconRenderer.RoundRect(Sc(dip), Sc(radius)))
            using (var b = new SolidBrush(c.Value))
                g.FillPath(b, path);
        }

        private void DrawMonth(Graphics g)
        {
            DayOfWeek first = Weeks.FirstDay;
            string[] names = culture.DateTimeFormat.AbbreviatedDayNames;
            float x0 = Pad + WeekCol;

            DrawText(g, L.WeekShort, weekFont, theme.Dim, new RectangleF(Pad, DowY, WeekCol, DowH), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            for (int i = 0; i < 7; i++)
            {
                // Dos letras, como el calendario de Windows 11 ("lu", "ma", "mi"...).
                string n = names[((int)first + i) % 7].TrimEnd('.');
                n = Capitalize(n.Length > 2 ? n.Substring(0, 2) : n);
                DrawText(g, n, dowFont, theme.Fore, new RectangleF(x0 + i * Cell, DowY, Cell, DowH), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            DateTime start = Weeks.StartOfWeek(displayMonth);

            for (int row = 0; row < 6; row++)
            {
                DateTime weekStart = start.AddDays(row * 7);
                float y = GridY + row * Cell;
                bool currentWeek = Today >= weekStart && Today < weekStart.AddDays(7);
                bool selWeek = selectedWeek == weekStart;

                if (currentWeek || selWeek)
                {
                    var rowRect = new RectangleF(Pad + 2, y + 3, WeekCol + Cell * 7 - 4, Cell - 6);
                    using (var path = TrayIconRenderer.RoundRect(Sc(rowRect), Sc(6)))
                    using (var b = new SolidBrush(selWeek ? Color.FromArgb(theme.Dark ? 0x30 : 0x24, theme.Accent) : theme.WeekRow))
                        g.FillPath(b, path);
                }

                int wk = Weeks.WeekOfYear(weekStart);
                var wkRect = new RectangleF(Pad + 4, y + 7, WeekCol - 8, Cell - 14);
                string wkId = "w" + row;
                DateTime ws = weekStart;
                AddHit(wkId, wkRect, () => { selectedWeek = selectedWeek == ws ? (DateTime?)null : ws; Invalidate(); });
                DrawHover(g, wkId, wkRect, 4);
                DrawText(g, wk.ToString(culture), currentWeek ? weekBoldFont : weekFont, currentWeek ? theme.Accent : theme.Muted, wkRect, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                for (int col = 0; col < 7; col++)
                {
                    DateTime d = weekStart.AddDays(col);
                    var cell = new RectangleF(x0 + col * Cell, y, Cell, Cell);
                    var circle = new RectangleF(cell.X + 3, cell.Y + 3, Cell - 6, Cell - 6);
                    string id = "d" + row + "_" + col;
                    AddHit(id, circle, () => SelectDay(d));

                    bool isToday = d == Today;
                    bool inMonth = d.Month == displayMonth.Month;
                    Color fore = inMonth ? theme.Fore : theme.Dim;

                    if (isToday)
                    {
                        Color fill = theme.Accent;
                        if (hoverId == id) fill = theme.Dark ? ControlPaint.Light(fill, 0.25f) : ControlPaint.Dark(fill, 0.08f);
                        using (var b = new SolidBrush(fill)) g.FillEllipse(b, Sc(circle));
                        if (d == selected && selectedWeek == null)
                        {
                            var inner = new RectangleF(circle.X + 2.5f, circle.Y + 2.5f, circle.Width - 5, circle.Height - 5);
                            using (var pen = new Pen(theme.Back, Sc(1.5f))) g.DrawEllipse(pen, Sc(inner));
                        }
                        fore = theme.AccentText;
                    }
                    else
                    {
                        if (hoverId == id)
                            using (var b = new SolidBrush(id == pressedId ? theme.Pressed : theme.Hover)) g.FillEllipse(b, Sc(circle));
                        if (d == selected && selectedWeek == null)
                            using (var pen = new Pen(theme.Accent, Sc(2))) g.DrawEllipse(pen, Sc(circle));
                    }

                    DrawText(g, d.Day.ToString(culture), isToday ? dayBoldFont : dayFont, fore, cell, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }
        }

        private void DrawYear(Graphics g)
        {
            var names = culture.DateTimeFormat.AbbreviatedMonthNames;
            int year = displayMonth.Year;
            DrawTileGrid(g, i =>
            {
                int y = year + i / 12, m = i % 12 + 1;
                if (y > 9999) return null;
                string name = Capitalize(names[m - 1].TrimEnd('.'));
                bool current = y == Today.Year && m == Today.Month;
                return Tuple.Create(name, i < 12, current, (Action)(() =>
                {
                    SetDisplayMonth(y, m);
                    view = ViewMode.Month;
                    Invalidate();
                }));
            });
        }

        private void DrawDecade(Graphics g)
        {
            int start = displayMonth.Year / 10 * 10;
            DrawTileGrid(g, i =>
            {
                int y = start + i;
                if (y < 1 || y > 9999) return null;
                return Tuple.Create(y.ToString(culture), i < 10, y == Today.Year, (Action)(() =>
                {
                    SetDisplayMonth(y, displayMonth.Month);
                    view = ViewMode.Year;
                    Invalidate();
                }));
            });
        }

        /// <summary>Rejilla 4x4 de las vistas de año y década. item(i) = (texto, activo, actual, acción).</summary>
        private void DrawTileGrid(Graphics g, Func<int, Tuple<string, bool, bool, Action>> item)
        {
            float w = (TotalW - Pad * 2) / 4, h = (FooterY - DowY - 10) / 4;
            for (int i = 0; i < 16; i++)
            {
                var it = item(i);
                if (it == null) continue;
                var cell = new RectangleF(Pad + (i % 4) * w, DowY + (i / 4) * h, w, h);
                float d = Math.Min(w, h) - 8;
                var circle = new RectangleF(cell.X + (w - d) / 2, cell.Y + (h - d) / 2, d, d);
                string id = "t" + i;
                AddHit(id, circle, it.Item4);

                Color fore = it.Item2 ? theme.Fore : theme.Dim;
                if (it.Item3)
                {
                    using (var b = new SolidBrush(theme.Accent)) g.FillEllipse(b, Sc(circle));
                    fore = theme.AccentText;
                }
                else if (hoverId == id)
                {
                    using (var b = new SolidBrush(theme.Hover)) g.FillEllipse(b, Sc(circle));
                }
                DrawText(g, it.Item1, cellFont, fore, cell, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawFooter(Graphics g)
        {
            string text;
            if (selectedWeek is DateTime ws)
            {
                DateTime we = ws.AddDays(6);
                int wk = Weeks.WeekOfYear(ws);
                string fmt = "d MMM";
                text = L.Week + " " + wk + "  ·  " + ws.ToString(fmt, culture).TrimEnd('.') + " – " + we.ToString(fmt, culture).TrimEnd('.') + " " + we.Year;
            }
            else
            {
                DateTime d = selected;
                int wk = Weeks.WeekOfYear(d);
                int wkYear = Weeks.WeekYear(d);
                int days = DateTime.IsLeapYear(d.Year) ? 366 : 365;
                text = string.Format(L.WeekOf, wk, Weeks.WeeksInYear(wkYear)) + "  ·  " + string.Format(L.DayOf, d.DayOfYear, days);
                int diff = (int)(d - Today).TotalDays;
                string rel = diff == 0 ? L.Today : diff == 1 ? L.Tomorrow : diff == -1 ? L.Yesterday
                    : diff > 0 ? string.Format(L.InDays, diff) : string.Format(L.DaysAgo, -diff);
                text += "  ·  " + rel;
            }
            DrawText(g, text, footerFont, theme.Muted, new RectangleF(Pad, FooterY + 4, TotalW - Pad * 2, TotalH - FooterY - 8), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawText(Graphics g, string text, Font font, Color color, RectangleF dip, TextFormatFlags flags)
        {
            TextRenderer.DrawText(g, text, font, Rectangle.Round(Sc(dip)), color, flags | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                clockTimer.Dispose();
                animTimer.Dispose();
                foreach (var f in new[] { timeFont, linkFont, titleFont, dowFont, dayFont, dayBoldFont, weekFont, weekBoldFont, cellFont, footerFont })
                    f?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
