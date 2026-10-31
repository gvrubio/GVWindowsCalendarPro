using System.Globalization;

namespace GVCalendar
{
    /// <summary>Textos de la interfaz (español con alternativa en inglés).</summary>
    internal static class L
    {
        private static readonly bool Es = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es";

        public static string AppName => "GV Windows Calendar Pro";
        public static string Open => Es ? "Abrir calendario" : "Open calendar";
        public static string StartWithWindows => Es ? "Iniciar con Windows" : "Start with Windows";
        public static string WeekNumbering => Es ? "Numeración de semanas" : "Week numbering";
        public static string WeekIso => Es ? "ISO 8601 (empieza en lunes)" : "ISO 8601 (starts on Monday)";
        public static string WeekUs => Es ? "EE. UU. (empieza en domingo)" : "US (starts on Sunday)";
        public static string TrayIcon => Es ? "Icono de la bandeja" : "Tray icon";
        public static string IconDay => Es ? "Mostrar día del mes" : "Show day of month";
        public static string IconWeek => Es ? "Mostrar número de semana" : "Show week number";
        public static string ShowSeconds => Es ? "Mostrar segundos" : "Show seconds";
        public static string About => Es ? "Acerca de..." : "About...";
        public static string Exit => Es ? "Salir" : "Exit";
        public static string WeekShort => Es ? "Sem" : "Wk";
        public static string Week => Es ? "Semana" : "Week";
        public static string DayOf => Es ? "Día {0} de {1}" : "Day {0} of {1}";
        public static string WeekOf => Es ? "Semana {0} de {1}" : "Week {0} of {1}";
        public static string Today => Es ? "Hoy" : "Today";
        public static string Tomorrow => Es ? "Mañana" : "Tomorrow";
        public static string Yesterday => Es ? "Ayer" : "Yesterday";
        public static string InDays => Es ? "En {0} días" : "In {0} days";
        public static string DaysAgo => Es ? "Hace {0} días" : "{0} days ago";
        public static string AutoStartError => Es ? "No se pudo cambiar el inicio automático:" : "Could not change auto start:";
        public static string AboutText => Es
            ? "Calendario para la bandeja del sistema con números de semana.\n\nClic izquierdo en el icono: abrir/cerrar el calendario.\nRueda del ratón o flechas: cambiar de mes.\nClic en el título: vista de año / década.\nClic en un número de semana: ver su rango de fechas.\nInicio: volver a hoy."
            : "System tray calendar with week numbers.\n\nLeft click the icon: open/close the calendar.\nMouse wheel or arrows: change month.\nClick the title: year / decade view.\nClick a week number: show its date range.\nHome: back to today.";
    }
}
