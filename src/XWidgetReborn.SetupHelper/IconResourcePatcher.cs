using System;
using System.Diagnostics;
using System.IO;

namespace XWidgetReborn
{
    internal static class IconResourcePatcher
    {
        private const string BackupFolderName =
            "XWidgetReborn-OriginalResources";

        private static readonly string[] RelativeFiles =
        {
            @"Res\Images\tray.ico",
            @"Res\Images\tray2.ico",
            @"Res\Images\WidgetDock\bg.png",
            @"Res\Images\WidgetDock\bt.png",
            @"Res\Images\WidgetDock\bt2.png",
            @"Res\Images\WidgetDock\btleft.png",
            @"Res\Images\WidgetDock\btleft2.png",
            @"Res\Images\WidgetDock\btright.png",
            @"Res\Images\WidgetDock\btright2.png",
            @"Res\Images\WidgetDock\manage.png",
            @"Res\Images\WidgetDock\Icon.png",
            @"Res\Images\WidgetDock\Icon_mini.png"
        };

        public static void Apply(string executable, string themeDirectory)
        {
            ValidateFile(executable, "XWidget executable");
            ValidateDirectory(themeDirectory, "EmilyDesk theme directory");

            string installDirectory =
                Path.GetDirectoryName(executable);
            string backupDirectory =
                Path.Combine(installDirectory, BackupFolderName);

            StopXWidget();

            foreach (string relative in RelativeFiles)
            {
                string target =
                    Path.Combine(installDirectory, relative);
                string source =
                    ThemeSource(themeDirectory, relative);
                string backup =
                    Path.Combine(backupDirectory, relative);

                ValidateFile(source, "Theme resource");

                if (File.Exists(target) && !File.Exists(backup))
                {
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(backup));
                    File.Copy(target, backup, false);
                }

                Directory.CreateDirectory(
                    Path.GetDirectoryName(target));
                File.Copy(source, target, true);
            }

            StartXWidget(executable);
        }

        public static void Restore(string executable)
        {
            ValidateFile(executable, "XWidget executable");

            string installDirectory =
                Path.GetDirectoryName(executable);
            string backupDirectory =
                Path.Combine(installDirectory, BackupFolderName);

            ValidateDirectory(
                backupDirectory,
                "Original XWidget resource backup");

            StopXWidget();

            foreach (string relative in RelativeFiles)
            {
                string backup =
                    Path.Combine(backupDirectory, relative);
                string target =
                    Path.Combine(installDirectory, relative);

                if (!File.Exists(backup))
                    continue;

                Directory.CreateDirectory(
                    Path.GetDirectoryName(target));
                File.Copy(backup, target, true);
            }

            StartXWidget(executable);
        }

        private static string ThemeSource(
            string themeDirectory,
            string relative)
        {
            if (relative.EndsWith(
                @"Res\Images\tray.ico",
                StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(themeDirectory, "tray.ico");
            }

            if (relative.EndsWith(
                @"Res\Images\tray2.ico",
                StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(themeDirectory, "tray2.ico");
            }

            return Path.Combine(
                themeDirectory,
                "WidgetDock",
                Path.GetFileName(relative));
        }

        private static void ValidateFile(
            string path,
            string description)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
            {
                throw new FileNotFoundException(
                    description + " was not found.",
                    path);
            }
        }

        private static void ValidateDirectory(
            string path,
            string description)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !Directory.Exists(path))
            {
                throw new DirectoryNotFoundException(
                    description + " was not found: " + path);
            }
        }

        private static void StopXWidget()
        {
            foreach (Process process in
                Process.GetProcessesByName("xwidget"))
            {
                try
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(2500))
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch
                {
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private static void StartXWidget(string executable)
        {
            Process.Start(executable);
        }
    }
}
