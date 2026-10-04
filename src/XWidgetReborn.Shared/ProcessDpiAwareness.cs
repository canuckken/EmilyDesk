using System;
using System.Runtime.InteropServices;

namespace XWidgetReborn.Shared
{
    public static class ProcessDpiAwareness
    {
        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(int awareness);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDPIAware();

        public static void Enable()
        {
            try
            {
                // PROCESS_PER_MONITOR_DPI_AWARE. WidgetWindow handles
                // WM_DPICHANGED and redraws its layered bitmap at native pixels.
                SetProcessDpiAwareness(2);
                return;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            catch { return; }

            try { SetProcessDPIAware(); }
            catch { }
        }
    }
}
