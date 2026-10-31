using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace GVCalendar
{
    /// <summary>Dibuja el icono de la bandeja con el día del mes o el número de semana.</summary>
    internal static class TrayIconRenderer
    {
        public static Icon Render(DateTime date, Theme theme)
        {
            int size = Math.Max(16, SystemInformation.SmallIconSize.Width);
            float s = size / 16f;
            Color fg = theme.TaskbarDark ? Color.White : Color.Black;

            using (var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);

                    bool week = Settings.IconMode == IconMode.Week;
                    string text = (week ? Weeks.WeekOfYear(date) : date.Day).ToString();
                    var body = new RectangleF(0.5f * s, 1.5f * s, 15f * s, 14f * s);

                    if (week)
                    {
                        // Rectángulo relleno con el número "recortado": se distingue del modo día.
                        using (var path = RoundRect(body, 2.5f * s))
                        using (var b = new SolidBrush(fg))
                            g.FillPath(b, path);
                    }
                    else
                    {
                        using (var path = RoundRect(body, 2.5f * s))
                        using (var pen = new Pen(fg, Math.Max(1f, s)))
                            g.DrawPath(pen, path);
                        using (var b = new SolidBrush(fg))
                            g.FillRectangle(b, body.X, body.Y, body.Width, 3.2f * s);
                    }

                    float top = week ? body.Y : body.Y + 3f * s;
                    var textRect = new RectangleF(body.X - 2 * s, top, body.Width + 4 * s, body.Bottom - top);
                    float px = (text.Length > 1 ? 9.5f : 10.5f) * s;
                    using (var font = new Font(FontName(), px, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var b = new SolidBrush(fg))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        if (week)
                        {
                            // El número se "recorta" del fondo.
                            g.CompositingMode = CompositingMode.SourceCopy;
                            using (var clear = new SolidBrush(Color.FromArgb(0, 0, 0, 0)))
                                g.DrawString(text, font, clear, textRect, sf);
                            g.CompositingMode = CompositingMode.SourceOver;
                        }
                        else
                        {
                            g.DrawString(text, font, b, textRect, sf);
                        }
                    }
                }

                IntPtr h = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(h).Clone(); }
                finally { Native.DestroyIcon(h); }
            }
        }

        private static string FontName()
        {
            foreach (var f in FontFamily.Families)
                if (f.Name == "Segoe UI") return "Segoe UI";
            return SystemFonts.DefaultFont.FontFamily.Name;
        }

        internal static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
