using System;
using System.Globalization;

namespace GVCalendar
{
    internal static class Weeks
    {
        public static DayOfWeek FirstDay => Settings.WeekMode == WeekMode.Iso ? DayOfWeek.Monday : DayOfWeek.Sunday;

        /// <summary>Número de la semana que contiene <paramref name="d"/>.</summary>
        public static int WeekOfYear(DateTime d)
        {
            if (Settings.WeekMode == WeekMode.Iso)
                return (Thursday(d).DayOfYear - 1) / 7 + 1;
            // EE. UU.: la semana que contiene el 1 de enero es la semana 1, aunque empiece en diciembre.
            return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(EndOfWeek(d), CalendarWeekRule.FirstDay, DayOfWeek.Sunday);
        }

        /// <summary>Año al que pertenece la semana que contiene <paramref name="d"/>.</summary>
        public static int WeekYear(DateTime d) => Settings.WeekMode == WeekMode.Iso ? Thursday(d).Year : EndOfWeek(d).Year;

        public static int WeeksInYear(int year)
        {
            if (Settings.WeekMode == WeekMode.Iso)
                return WeekOfYear(new DateTime(year, 12, 28));
            var d = new DateTime(year, 12, 31);
            while (d.DayOfWeek != DayOfWeek.Saturday) d = d.AddDays(-1);
            return WeekOfYear(d);
        }

        public static DateTime StartOfWeek(DateTime d)
        {
            int diff = ((int)d.DayOfWeek - (int)FirstDay + 7) % 7;
            return d.Date.AddDays(-diff);
        }

        private static DateTime EndOfWeek(DateTime d)
        {
            int diff = 6 - ((int)d.DayOfWeek - (int)FirstDay + 7) % 7;
            return d.Date.AddDays(diff);
        }

        private static DateTime Thursday(DateTime d)
        {
            int dow = ((int)d.DayOfWeek + 6) % 7; // lunes = 0
            return d.Date.AddDays(3 - dow);
        }
    }
}
