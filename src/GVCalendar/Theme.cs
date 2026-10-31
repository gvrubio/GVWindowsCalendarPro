using System;
using System.Drawing;
using Microsoft.Win32;

namespace GVCalendar
{
    internal sealed class Theme
    {
        public bool Dark { get; private set; }
        /// <summary>Tema de la barra de tareas (puede diferir del de las aplicaciones).</summary>
        public bool TaskbarDark { get; private set; }
        public Color Back { get; private set; }
        public Color Fore { get; private set; }
        public Color Muted { get; private set; }
        public Color Dim { get; private set; }
        public Color Hover { get; private set; }
        public Color Pressed { get; private set; }
        public Color WeekRow { get; private set; }
        public Color Separator { get; private set; }
        public Color Border { get; private set; }
        public Color Accent { get; private set; }
        public Color AccentText { get; private set; }

        public static Theme Load()
        {
            var t = new Theme();
            // La ventana emergente del calendario de Windows sigue el tema del sistema (barra de tareas).
            t.TaskbarDark = ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0) == 0;
            t.Dark = t.TaskbarDark;

            if (t.Dark)
            {
                t.Back = Color.FromArgb(0x24, 0x24, 0x24);
                t.Fore = Color.FromArgb(0xFF, 0xFF, 0xFF);
                t.Muted = Color.FromArgb(0xC5, 0xC5, 0xC5);
                t.Dim = Color.FromArgb(0x78, 0x78, 0x78);
                t.Hover = Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF);
                t.Pressed = Color.FromArgb(0x0B, 0xFF, 0xFF, 0xFF);
                t.WeekRow = Color.FromArgb(0x0C, 0xFF, 0xFF, 0xFF);
                t.Separator = Color.FromArgb(0x3A, 0x3A, 0x3A);
                t.Border = Color.FromArgb(0x45, 0x45, 0x45);
            }
            else
            {
                t.Back = Color.FromArgb(0xF3, 0xF3, 0xF3);
                t.Fore = Color.FromArgb(0x1A, 0x1A, 0x1A);
                t.Muted = Color.FromArgb(0x5C, 0x5C, 0x5C);
                t.Dim = Color.FromArgb(0xA0, 0xA0, 0xA0);
                t.Hover = Color.FromArgb(0x10, 0x00, 0x00, 0x00);
                t.Pressed = Color.FromArgb(0x08, 0x00, 0x00, 0x00);
                t.WeekRow = Color.FromArgb(0x09, 0x00, 0x00, 0x00);
                t.Separator = Color.FromArgb(0xDD, 0xDD, 0xDD);
                t.Border = Color.FromArgb(0xC8, 0xC8, 0xC8);
            }

            t.Accent = ReadAccent(t.Dark);
            t.AccentText = t.Dark ? Color.Black : Color.White;
            return t;
        }

        private static Color ReadAccent(bool dark)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    // AccentPalette: 8 colores RGBA. [1] = Light2 (modo oscuro), [4] = Dark1 (modo claro).
                    if (key?.GetValue("AccentPalette") is byte[] p && p.Length >= 32)
                    {
                        int i = (dark ? 1 : 4) * 4;
                        return Color.FromArgb(p[i], p[i + 1], p[i + 2]);
                    }
                }
                int abgr = ReadDword(@"Software\Microsoft\Windows\DWM", "AccentColor", -1);
                if (abgr != -1)
                    return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
            }
            catch { /* usar color por defecto */ }
            return dark ? Color.FromArgb(0x60, 0xCD, 0xFF) : Color.FromArgb(0x00, 0x5F, 0xB8);
        }

        private static int ReadDword(string path, string name, int fallback)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(path))
                    return key?.GetValue(name) is int v ? v : fallback;
            }
            catch { return fallback; }
        }
    }
}
