using System;
using System.Threading;
using System.Windows.Forms;
using XWidgetReborn.Runtime.Core;
using XWidgetReborn.Shared;

namespace XWidgetReborn.Runtime
{
    internal static class RuntimeProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ProcessDpiAwareness.Enable();
            var logging = new RuntimeLoggingService();
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                logging.Error("UI exception.", e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                logging.Error(
                    "Unhandled exception.",
                    e.ExceptionObject as Exception);
            };

            logging.Debug("============================================================");
            logging.Information("Runtime process starting. " + AppConstants.BuildDisplay +
                "; Executable: " + Application.ExecutablePath +
                "; Base directory: " + AppDomain.CurrentDomain.BaseDirectory +
                "; OS shutdown: false");
            AppDomain.CurrentDomain.ProcessExit += delegate
            {
                logging.Debug("AppDomain ProcessExit event raised.");
            };

            string verificationTheme = GetArgument(
                args, "--verify-theme-weather-launch");
            if (!string.IsNullOrWhiteSpace(verificationTheme))
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    ArtDecoWeatherLaunchVerification.RunTheme(
                        logging, verificationTheme);
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    logging.Error(
                        verificationTheme + " Weather launch verification failed.", ex);
                    try
                    {
                        System.IO.File.WriteAllText(
                            System.IO.Path.Combine(
                                AppDomain.CurrentDomain.BaseDirectory,
                                "theme-weather-launch-error.txt"),
                            ex.ToString());
                    }
                    catch { }
                    Environment.ExitCode = 1;
                }
                return;
            }

            if (HasArgument(args, "--verify-artdeco-weather-launch"))
            {
                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    ArtDecoWeatherLaunchVerification.Run(logging);
                    Environment.ExitCode = 0;
                }
                catch (Exception ex)
                {
                    logging.Error("Art Deco Weather launch verification failed.", ex);
                    Environment.ExitCode = 1;
                }
                return;
            }

            bool createdNew;
            using (var mutex = new Mutex(true, AppConstants.RuntimeMutex, out createdNew))
            {
                if (!createdNew)
                {
                    logging.Information("Another runtime instance already owns the mutex; exiting.");
                    return;
                }

                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    string widgetPath = GetArgument(args, "--widget");
                    logging.Information("Runtime initialized. Widget argument: " + (widgetPath ?? "<welcome>"));
                    Application.Run(
                        new RuntimeApplicationContext(widgetPath, logging));
                    logging.Information("Runtime message loop ended normally.");
                    logging.Debug("Runtime Main method is returning.");
                }
                catch (Exception ex)
                {
                    logging.Error("Fatal startup exception.", ex);
                    MessageBox.Show(
                        "The EmilyDesk Runtime could not start.\r\n\r\n" + ex.Message +
                        "\r\n\r\nLog: " + RuntimeLog.LogPath,
                        "EmilyDesk Runtime",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }

            logging.Debug("Runtime mutex scope ended.");
        }

        private static string GetArgument(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static bool HasArgument(string[] args, string name)
        {
            foreach (string argument in args)
                if (string.Equals(argument, name,
                    StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
