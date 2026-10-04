using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal static class DashboardProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ProcessDpiAwareness.Enable();
            if (args.Length == 1 && string.Equals(
                args[0],
                "--verify-stage4-ui",
                StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    Stage4UiVerification.Run();
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.WriteAllText(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "stage4-ui-error.txt"),
                            ex.ToString());
                    }
                    catch { }
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args.Length == 1 && string.Equals(
                args[0],
                "--verify-stage5-themes",
                StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Stage5ThemeVerification.Run();
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.WriteAllText(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "stage5-theme-error.txt"),
                            ex.ToString());
                    }
                    catch { }
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args.Length == 2 && string.Equals(
                args[0],
                "--generate-gallery-previews",
                StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Stage5ThemeVerification.GenerateGalleryPreviews(args[1]);
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.WriteAllText(
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                                "gallery-preview-error.txt"),
                            ex.ToString());
                    }
                    catch { }
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args.Length == 4 && string.Equals(args[0],
                "--render-optional-widget-preview",
                StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    OptionalWidgetPreviewPublisher.Publish(
                        args[1], args[2], args[3]);
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "optional-preview-error.txt"), ex.ToString());
                    }
                    catch { }
                    Environment.ExitCode = 1;
                }
                return;
            }
            if (args.Length == 3 &&
                string.Equals(
                    args[0],
                    "--verify-widget-package-install",
                    StringComparison.OrdinalIgnoreCase))
            {
                VerifyWidgetPackageInstall(args[1], args[2]);
                return;
            }
            bool createdNew;
            using (var mutex = new Mutex(
                true,
                AppConstants.DashboardMutex,
                out createdNew))
            {
                if (!createdNew)
                {
                    SignalExistingCompanion();
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(
                    UnhandledExceptionMode.CatchException);
                // EmilyDesk starts quietly with the user's widgets.  The
                // Dashboard is opened explicitly from the tray menu.
                Application.Run(new RebornApplicationContext(false));
            }
        }

        private static void VerifyWidgetPackageInstall(
            string packagePath,
            string widgetsRoot)
        {
            try
            {
                WidgetPackageInstaller.VerifyBuildScenarios(
                    packagePath,
                    widgetsRoot);
                Environment.ExitCode = 0;
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(
                        Path.Combine(
                            Path.GetFullPath(widgetsRoot),
                            "install-verification-error.txt"),
                        ex.ToString());
                }
                catch { }
                Environment.ExitCode = 1;
            }
        }

        private static void SignalExistingCompanion()
        {
            try
            {
                using (var showEvent = EventWaitHandle.OpenExisting(
                    AppConstants.DashboardShowEvent))
                {
                    showEvent.Set();
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                MessageBox.Show(
                    "EmilyDesk is starting. Please try opening the " +
                    "dashboard again in a moment.",
                    AppConstants.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }
    }
}
