using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using XWidgetReborn.Shared;
using XWidgetReborn.WidgetSdk;

namespace XWidgetReborn
{
    internal static class Stage4UiVerification
    {
        public static void Run()
        {
            using (var dashboard = new MainForm())
            {
                string[] buttons = Descendants(dashboard)
                    .OfType<Button>()
                    .Select(button => button.Text)
                    .ToArray();
                Require(buttons.Contains("Home"), "Home navigation is missing.");
                Require(buttons.Contains("Widgets"), "Widgets navigation is missing.");
                Require(buttons.Contains("Docks"), "Docks navigation is missing.");
                Require(buttons.Contains("Designer"), "Designer navigation is missing.");
                Require(buttons.Contains("Widget Templates"), "Widget Templates navigation is missing.");
                Require(buttons.Contains("Settings"), "Settings navigation is missing.");
                Require(buttons.Contains(
                    "Open Selected Widget in Designer"),
                    "The Designer widget selector launch action is missing.");
                ComboBox widgetSelector = Descendants(dashboard)
                    .OfType<ComboBox>()
                    .FirstOrDefault(combo =>
                        combo.Name == "DesignerWidgetCombo");
                Require(widgetSelector != null &&
                    widgetSelector.Items.Count >= 4,
                    "The Designer does not list every built-in widget type.");
                ComboBox themeSelector = Descendants(dashboard)
                    .OfType<ComboBox>()
                    .FirstOrDefault(combo =>
                        combo.Name == "DesignerThemeCombo");
                Require(themeSelector != null &&
                    themeSelector.Items.Count ==
                        EmilyDeskThemeCatalog.Names.Count(),
                    "The Designer theme selector is incomplete.");
                Require(buttons.Contains("Add Widget / Open Gallery"),
                    "The primary Gallery action is missing.");
                Require(buttons.Contains("Import Widget..."),
                    "The optional widget import action is missing.");
                foreach (string themeName in EmilyDeskThemeCatalog.Names)
                {
                    string launchText = "Launch " + themeName + " Dock";
                    Require(buttons.Contains(launchText),
                        "The " + themeName +
                        " Dock launch action is missing.");
                }
                Require(buttons.Contains("Dock Settings"),
                    "Dock settings are missing.");
                Require(!Descendants(dashboard).Any(control =>
                    control.Text.IndexOf("Coming soon", StringComparison.OrdinalIgnoreCase) >= 0),
                    "Obsolete coming-soon UI is visible.");
            }

            EmilyDeskDockSettings dockDefaults =
                EmilyDeskDockSettings.CreateDefaults();
            Require(dockDefaults.Theme == "Industrial",
                "The initial dock theme is not Industrial.");
            Require(dockDefaults.Edge == EmilyDeskDockEdge.Bottom.ToString(),
                "The dock does not default to the bottom edge.");
            Require(dockDefaults.StartWithEmilyDesk &&
                dockDefaults.StartupPreferenceInitialized,
                "The dock does not start with EmilyDesk by default.");
            Require(dockDefaults.Items != null &&
                dockDefaults.Items.Count >= 2,
                "The dock default launcher items are missing.");
            using (System.Drawing.Image webIcon = ShellIconLoader.Load(
                new EmilyDeskDockItem
                {
                    Label = "Website",
                    Target = "https://www.example.com",
                    Arguments = string.Empty,
                    CustomIconPath = string.Empty
                }))
            {
                Require(webIcon != null && webIcon.Width >= 128 &&
                    webIcon.Height >= 128,
                    "The high-resolution dock web icon is missing.");
            }
            using (System.Drawing.Image folderIcon = ShellIconLoader.Load(
                new EmilyDeskDockItem
                {
                    Label = "Folder",
                    Target = Environment.CurrentDirectory,
                    Arguments = string.Empty,
                    CustomIconPath = string.Empty
                }))
            {
                Require(folderIcon != null && folderIcon.Width >= 128 &&
                    folderIcon.Height >= 128,
                    "The high-resolution dock folder icon is missing.");
            }
            using (System.Drawing.Image steampunkWebIcon =
                ShellIconLoader.Load(
                    new EmilyDeskDockItem
                    {
                        Label = "Website",
                        Target = "https://www.example.com",
                        Arguments = string.Empty,
                        CustomIconPath = string.Empty
                    },
                    "Steampunk"))
            using (System.Drawing.Image steampunkFolderIcon =
                ShellIconLoader.Load(
                    new EmilyDeskDockItem
                    {
                        Label = "Folder",
                        Target = Environment.CurrentDirectory,
                        Arguments = string.Empty,
                        CustomIconPath = string.Empty
                    },
                    "Steampunk"))
            {
                Require(steampunkWebIcon != null &&
                    steampunkWebIcon.Width >= 128 &&
                    steampunkWebIcon.Height >= 128,
                    "The Steampunk dock web icon is missing.");
                Require(steampunkFolderIcon != null &&
                    steampunkFolderIcon.Width >= 128 &&
                    steampunkFolderIcon.Height >= 128,
                    "The Steampunk dock folder icon is missing.");
            }
            using (System.Drawing.Image steampunkExplorerIcon =
                ShellIconLoader.Load(
                    new EmilyDeskDockItem
                    {
                        Label = "File Explorer",
                        Target = System.IO.Path.Combine(
                            Environment.GetFolderPath(
                                Environment.SpecialFolder.Windows),
                            "explorer.exe"),
                        Arguments = string.Empty,
                        CustomIconPath = string.Empty
                    },
                    "Steampunk"))
            {
                Require(steampunkExplorerIcon != null &&
                    steampunkExplorerIcon.Width >= 128 &&
                    steampunkExplorerIcon.Height >= 128,
                    "The Steampunk File Explorer icon is missing.");
            }
            using (System.Drawing.Image industrialExplorerIcon =
                ShellIconLoader.Load(
                    new EmilyDeskDockItem
                    {
                        Label = "File Explorer",
                        Target = System.IO.Path.Combine(
                            Environment.GetFolderPath(
                                Environment.SpecialFolder.Windows),
                            "explorer.exe"),
                        Arguments = string.Empty,
                        CustomIconPath = string.Empty
                    },
                    "Industrial"))
            using (System.Drawing.Image artDecoExplorerIcon =
                ShellIconLoader.Load(
                    new EmilyDeskDockItem
                    {
                        Label = "File Explorer",
                        Target = System.IO.Path.Combine(
                            Environment.GetFolderPath(
                                Environment.SpecialFolder.Windows),
                            "explorer.exe"),
                        Arguments = string.Empty,
                        CustomIconPath = string.Empty
                    },
                    "Art Deco"))
            using (System.Drawing.Image artDecoWebIcon =
                ShellIconLoader.Load(
                    new EmilyDeskDockItem
                    {
                        Label = "Website",
                        Target = "https://www.example.com",
                        Arguments = string.Empty,
                        CustomIconPath = string.Empty
                    },
                    "Art Deco"))
            using (System.Drawing.Image artDecoFolderIcon =
                ShellIconLoader.Load(
                    new EmilyDeskDockItem
                    {
                        Label = "Folder",
                        Target = Environment.CurrentDirectory,
                        Arguments = string.Empty,
                        CustomIconPath = string.Empty
                    },
                    "Art Deco"))
            {
                Require(industrialExplorerIcon != null &&
                    industrialExplorerIcon.Width >= 128 &&
                    industrialExplorerIcon.Height >= 128,
                    "The Industrial File Explorer icon is missing.");
                Require(artDecoExplorerIcon != null &&
                    artDecoExplorerIcon.Width >= 128 &&
                    artDecoExplorerIcon.Height >= 128,
                    "The Art Deco File Explorer icon is missing.");
                Require(artDecoWebIcon != null &&
                    artDecoWebIcon.Width >= 128 &&
                    artDecoWebIcon.Height >= 128,
                    "The Art Deco web icon is missing.");
                Require(artDecoFolderIcon != null &&
                    artDecoFolderIcon.Width >= 128 &&
                    artDecoFolderIcon.Height >= 128,
                    "The Art Deco folder icon is missing.");
            }

            VerifyCollectionThemeIcons("Vintage");
            VerifyCollectionThemeIcons("Modern");
            VerifyCollectionThemeIcons("Botanical Nature");
            VerifyCollectionThemeIcons("Woodland Nature");

            var descriptors = new List<WidgetDescriptor>
            {
                Official("native.weather", "Weather"),
                Official("native.clock", "Clock"),
                Official("native.calendar", "Calendar"),
                Official("native.recyclebin", "Recycle Bin"),
                new WidgetDescriptor(
                    "utility.test", "Imported Test", "Imported widget",
                    "1.0.0", "Test Publisher", string.Empty,
                    string.Empty, "Utilities", false, false,
                    AppConstants.Version, new string[0])
            };
            using (var gallery = new VirtualWidgetManagerForm(descriptors))
            {
                Require(gallery.Text.StartsWith("EmilyDesk Gallery", StringComparison.Ordinal),
                    "Gallery window identity is incorrect.");
                Require(Descendants(gallery).OfType<Button>()
                    .Any(button => button.Text == "Clear Search"),
                    "Clear Search is missing.");
                var surface = Descendants(gallery)
                    .OfType<VirtualWidgetGallerySurface>()
                    .SingleOrDefault();
                Require(surface != null, "Owner-drawn virtual Gallery surface is missing.");
                int expectedCards = (descriptors.Count - 1) *
                    EmilyDeskThemeCatalog.Names.Count() + 1;
                Require(surface.TotalItemCount == expectedCards,
                    "The complete themed widget card collection was not created.");
                Require(surface.HasCardActions,
                    "Gallery cards do not expose a clear action.");
                Require(Descendants(gallery).OfType<Button>()
                    .Any(button => button.Text == "Import Widget..."),
                    "Gallery does not expose widget package import.");
            }
        }

        private static WidgetDescriptor Official(string id, string name)
        {
            return new WidgetDescriptor(
                id, name, name + " widget", "1.0", "EmilyDesk",
                string.Empty, string.Empty, "Official", true, true,
                string.Empty, new string[0]);
        }

        private static void VerifyCollectionThemeIcons(string theme)
        {
            var webItem = new EmilyDeskDockItem
            {
                Label = "Website",
                Target = "https://www.example.com",
                Arguments = string.Empty,
                CustomIconPath = string.Empty
            };
            var folderItem = new EmilyDeskDockItem
            {
                Label = "Folder",
                Target = Environment.CurrentDirectory,
                Arguments = string.Empty,
                CustomIconPath = string.Empty
            };
            var explorerItem = new EmilyDeskDockItem
            {
                Label = "File Explorer",
                Target = System.IO.Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.Windows),
                    "explorer.exe"),
                Arguments = string.Empty,
                CustomIconPath = string.Empty
            };
            using (System.Drawing.Image web = ShellIconLoader.Load(
                webItem, theme))
            using (System.Drawing.Image folder = ShellIconLoader.Load(
                folderItem, theme))
            using (System.Drawing.Image explorer = ShellIconLoader.Load(
                explorerItem, theme))
            {
                Require(web != null && web.Width >= 128 && web.Height >= 128,
                    "The " + theme + " web icon is missing.");
                Require(folder != null && folder.Width >= 128 &&
                    folder.Height >= 128,
                    "The " + theme + " folder icon is missing.");
                Require(explorer != null && explorer.Width >= 128 &&
                    explorer.Height >= 128,
                    "The " + theme + " File Explorer icon is missing.");
            }
        }

        private static IEnumerable<Control> Descendants(Control root)
        {
            foreach (Control child in root.Controls)
            {
                yield return child;
                foreach (Control nested in Descendants(child))
                    yield return nested;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
