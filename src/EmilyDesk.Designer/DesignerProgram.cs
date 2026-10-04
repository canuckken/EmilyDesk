using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using XWidgetReborn.Widgets.Clock;

namespace EmilyDesk.Designer
{
    internal static class DesignerProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length == 2 && string.Equals(args[0],
                "--export-clock-package-artwork",
                StringComparison.OrdinalIgnoreCase))
            {
                ExportClockPackageArtwork(args[1]);
                return;
            }
            if (args.Length == 4 && string.Equals(args[0],
                "--optional-widget-assembly",
                StringComparison.OrdinalIgnoreCase) &&
                string.Equals(args[2], "--type",
                    StringComparison.OrdinalIgnoreCase))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    Application.Run(DesignerForm.OpenOptionalWidget(
                        args[1], args[3]));
                }
                catch (Exception error)
                {
                    MessageBox.Show(error.Message, "EmilyDesk Designer",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 1 && args[0] == "--templates")
            {
                using (var picker = new TemplatePickerForm())
                {
                    if (picker.ShowDialog() != DialogResult.OK) return;
                    try
                    {
                        using (DesignerForm form = picker.OpenProjectPath == null
                            ? DesignerForm.CreateTemplateProject(picker.WidgetKind, picker.ThemeName)
                            : DesignerForm.OpenTemplateProject(picker.OpenProjectPath))
                            Application.Run(form);
                    }
                    catch (Exception error)
                    {
                        MessageBox.Show(error.Message, "Widget Templates",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                return;
            }
            if (args.Length == 1 && args[0] == "--verify-designer-saves")
            {
                try { DesignerSaveVerification.Run(); }
                catch (Exception error)
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "designer-save-error.txt"), error.ToString());
                    Environment.ExitCode = 1;
                }
                return;
            }
            string widget = "weather";
            string theme = "Industrial";
            for (int index = 0; index + 1 < args.Length; index++)
            {
                if (string.Equals(args[index], "--widget",
                    StringComparison.OrdinalIgnoreCase))
                    widget = args[index + 1];
                else if (string.Equals(args[index], "--theme",
                    StringComparison.OrdinalIgnoreCase))
                    theme = args[index + 1];
            }
            Application.Run(new DesignerForm(widget, theme));
        }

        private static void ExportClockPackageArtwork(string root)
        {
            string folder = Path.Combine(root, "Widgets", "Clock");
            var clock = new NativeClockWidget();
            clock.Theme = "Woodland Nature";
            using (var preview = new Bitmap(512, 512,
                PixelFormat.Format32bppArgb))
            using (var graphics = Graphics.FromImage(preview))
            {
                graphics.Clear(Color.Transparent);
                graphics.ScaleTransform(512F / 360F, 512F / 360F);
                clock.RenderPackagePreview(graphics,
                    new Rectangle(0, 0, 360, 360));
                preview.Save(Path.Combine(folder, "preview.png"),
                    ImageFormat.Png);
                using (var icon = new Bitmap(256, 256,
                    PixelFormat.Format32bppArgb))
                using (var iconGraphics = Graphics.FromImage(icon))
                {
                    iconGraphics.Clear(Color.Transparent);
                    iconGraphics.DrawImage(preview,
                        new Rectangle(0, 0, 256, 256));
                    icon.Save(Path.Combine(folder, "icon.png"),
                        ImageFormat.Png);
                }
            }
        }
    }
}
