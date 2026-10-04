using System;
using System.IO;
using System.Windows.Forms;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal static class WidgetPackageUi
    {
        public static WidgetPackageInstallResult Import(IWin32Window owner)
        {
            using (var dialog = new OpenFileDialog
            {
                Title = "Import EmilyDesk Widget",
                Filter =
                    "EmilyDesk widgets (*.emilywidget)|*.emilywidget|" +
                    "Earlier EmilyDesk widget packages (*.xwrwidget;*.xrwwidget)|*.xwrwidget;*.xrwwidget",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = EnsureImportDirectory(),
                RestoreDirectory = true
            })
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK)
                    return null;

                WidgetPackageInspection inspection = null;
                bool engineStopped = false;
                try
                {
                    inspection = WidgetPackageInstaller.Inspect(
                        dialog.FileName);
                    bool replace = false;
                    if (inspection.IsInstalled)
                    {
                        string installedName =
                            string.IsNullOrWhiteSpace(
                                inspection.InstalledName)
                                ? inspection.Name
                                : inspection.InstalledName;
                        if (inspection.VersionComparison == 0)
                        {
                            MessageBox.Show(
                                owner,
                                installedName + " version " +
                                inspection.InstalledVersion +
                                " is already installed.",
                                "Widget Already Installed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            return null;
                        }
                        if (inspection.VersionComparison < 0)
                        {
                            MessageBox.Show(
                                owner,
                                installedName + " version " +
                                inspection.InstalledVersion +
                                " is newer than the selected package. " +
                                "No changes were made.",
                                "Newer Widget Already Installed",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                            return null;
                        }
                        if (MessageBox.Show(
                            owner,
                            "Replace " + installedName + " " +
                            inspection.InstalledVersion + " with version " +
                            inspection.Version + "?",
                            "Update Widget",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) != DialogResult.Yes)
                            return null;
                        replace = true;
                    }

                    if (MessageBox.Show(
                        owner,
                        inspection.Name + " " + inspection.Version +
                        "\r\nPublisher: " + inspection.Author +
                        "\r\n\r\nImported widgets contain executable program code. " +
                        "Only continue if you trust the person or site that supplied this file.",
                        "Trust This Widget?",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes)
                        return null;

                    if (replace)
                    {
                        RebornApplicationContext.
                            StopEngineForWidgetPackageMaintenance();
                        engineStopped = true;
                    }

                    WidgetPackageInstallResult result = replace
                        ? WidgetPackageInstaller.Replace(dialog.FileName)
                        : WidgetPackageInstaller.Install(dialog.FileName);

                    bool ready;
                    if (engineStopped)
                    {
                        ready = RebornApplicationContext.
                            StartEngineAfterWidgetPackageMaintenance();
                        engineStopped = false;
                    }
                    else
                    {
                        ready = EngineClient.RefreshWidgets();
                    }
                    if (!ready)
                        throw new InvalidOperationException(
                            "The widget files were imported, but the EmilyDesk Engine could not refresh them.");

                    MessageBox.Show(
                        owner,
                        result.Name + " is now available in the Widget Gallery.\r\n\r\n" +
                        "It was installed separately from EmilyDesk and can be removed without reinstalling the program.",
                        replace ? "Widget Updated" : "Widget Imported",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return result;
                }
                catch (Exception error)
                {
                    MessageBox.Show(
                        owner,
                        "EmilyDesk could not import this widget.\r\n\r\n" +
                        FriendlyError(error),
                        "Widget Import",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return null;
                }
                finally
                {
                    if (engineStopped)
                        RebornApplicationContext.
                            StartEngineAfterWidgetPackageMaintenance();
                }
            }
        }

        private static string EnsureImportDirectory()
        {
            string directory = WidgetPackagePaths.ImportedWidgetsRoot;
            try
            {
                Directory.CreateDirectory(directory);
                return directory;
            }
            catch
            {
                return Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments);
            }
        }

        public static bool Uninstall(
            IWin32Window owner,
            string widgetId,
            string widgetName)
        {
            if (!WidgetPackageInstaller.CanUninstall(widgetId))
                return false;
            if (MessageBox.Show(
                owner,
                "Remove " + widgetName + " from EmilyDesk?\r\n\r\n" +
                "The main EmilyDesk installation and your other widgets will not be changed.",
                "Remove Imported Widget",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
                return false;

            bool engineStopped = false;
            try
            {
                RebornApplicationContext.
                    StopEngineForWidgetPackageMaintenance();
                engineStopped = true;
                WidgetPackageInstallResult result =
                    WidgetPackageInstaller.Uninstall(widgetId);
                if (!RebornApplicationContext.
                    StartEngineAfterWidgetPackageMaintenance())
                    throw new InvalidOperationException(
                        "The widget was removed, but the EmilyDesk Engine did not restart.");
                engineStopped = false;
                MessageBox.Show(
                    owner,
                    result.Name + " was removed. Your EmilyDesk installation was not changed.",
                    "Widget Removed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return true;
            }
            catch (Exception error)
            {
                MessageBox.Show(
                    owner,
                    "EmilyDesk could not remove this widget.\r\n\r\n" +
                    FriendlyError(error),
                    "Widget Removal",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
            finally
            {
                if (engineStopped)
                    RebornApplicationContext.
                        StartEngineAfterWidgetPackageMaintenance();
            }
        }

        private static string FriendlyError(Exception error)
        {
            if (error is UnauthorizedAccessException)
                return "Windows denied access to the personal widget folder.";
            if (error is InvalidDataException)
                return error.Message;
            return error.Message;
        }
    }
}
