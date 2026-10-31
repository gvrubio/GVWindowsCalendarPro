using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace GVCalendar
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            using (var mutex = new Mutex(true, "GVWindowsCalendarPro_Mutex", out bool firstInstance))
            {
                if (!firstInstance)
                {
                    // Ya se está ejecutando: pedir a la instancia existente que muestre el calendario.
                    Native.PostMessage(Native.HWND_BROADCAST, CalendarFlyout.ShowMessage, IntPtr.Zero, IntPtr.Zero);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Settings.Load();

                bool open = args.Any(a => a.Equals("/open", StringComparison.OrdinalIgnoreCase));
                Application.Run(new TrayApp(open));
                GC.KeepAlive(mutex);
            }
        }
    }
}
