using System;
using Microsoft.Win32;
using System.Globalization;
using System.Windows.Forms;

namespace GVCalendar
{
    internal enum WeekMode { Iso, Us }
    internal enum IconMode { Day, Week }

    internal static class Settings
    {
        private const string KeyPath = @"Software\GVWindowsCalendarPro";

        public static WeekMode WeekMode { get; set; }
        public static IconMode IconMode { get; set; } = IconMode.Day;
        public static bool ShowSeconds { get; set; } = true;

        public static void Load()
        {
            WeekMode = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek == DayOfWeek.Sunday ? WeekMode.Us : WeekMode.Iso;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(KeyPath))
                {
                    if (key == null) return;
                    if (key.GetValue("WeekMode") is int wm && Enum.IsDefined(typeof(WeekMode), wm)) WeekMode = (WeekMode)wm;
                    if (key.GetValue("IconMode") is int im && Enum.IsDefined(typeof(IconMode), im)) IconMode = (IconMode)im;
                    if (key.GetValue("ShowSeconds") is int ss) ShowSeconds = ss != 0;
                }
            }
            catch { /* usar valores por defecto */ }
        }

        public static void Save()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    key.SetValue("WeekMode", (int)WeekMode, RegistryValueKind.DWord);
                    key.SetValue("IconMode", (int)IconMode, RegistryValueKind.DWord);
                    key.SetValue("ShowSeconds", ShowSeconds ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { /* sin permisos: ignorar */ }
        }
    }

    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        // Debe coincidir con el valor que escribe el instalador (installer/GVWindowsCalendarPro.iss).
        private const string ValueName = "GVWindowsCalendarPro";

        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                        return key?.GetValue(ValueName) != null;
                }
                catch { return false; }
            }
        }

        public static void Set(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else key.DeleteValue(ValueName, false);
            }
        }
    }
}
