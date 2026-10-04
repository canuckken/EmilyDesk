using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace XWidgetReborn
{
    internal static class WidgetConverter
    {
        [STAThread]
        public static void Run(string[] args)
        {
            string folder = args.Length > 0 ? args[0] : null;
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select an XWidget weather-widget folder";
                    if (dialog.ShowDialog() != DialogResult.OK)
                        return;
                    folder = dialog.SelectedPath;
                }
            }

            try
            {
                ConversionResult result = ConvertWidget(folder);
                MessageBox.Show(
                    result.Message,
                    "EmilyDesk Widget Converter",
                    MessageBoxButtons.OK,
                    result.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "The widget could not be converted.\r\n\r\n" + ex.Message,
                    "EmilyDesk Widget Converter",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static ConversionResult ConvertWidget(string folder)
        {
            string[] supportedExtensions =
            {
                ".xul", ".xwl", ".xml", ".ini", ".json", ".js", ".txt"
            };

            var files = Directory.GetFiles(folder, "*", SearchOption.AllDirectories)
                .Where(f => supportedExtensions.Contains(
                    Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (files.Count == 0)
                return ConversionResult.Fail("No editable XWidget files were found in this folder.");

            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string file in files)
            {
                string text;
                try { text = File.ReadAllText(file, DetectEncoding(file)); }
                catch { continue; }

                foreach (Match m in Regex.Matches(
                    text,
                    @"\bacc(?:u)?weathercore\d+\b",
                    RegexOptions.IgnoreCase))
                {
                    matches.Add(m.Value);
                }
            }

            if (matches.Count == 0)
            {
                return ConversionResult.Fail(
                    "No AccuWeather core instance was found.\r\n\r\n" +
                    "The compiled core type in XWidget Designer will still be named AccuWeatherCore2. " +
                    "This converter only renames widget instance references.");
            }

            string oldName = matches
                .OrderByDescending(x => x.IndexOf("accweathercore", StringComparison.OrdinalIgnoreCase) >= 0)
                .First();

            string newName = "openmeteobridge1";
            string backup = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

            CopyDirectory(folder, backup);

            int changedFiles = 0;
            int replacements = 0;

            foreach (string file in files)
            {
                Encoding encoding = DetectEncoding(file);
                string text;
                try { text = File.ReadAllText(file, encoding); }
                catch { continue; }

                int count = Regex.Matches(
                    text,
                    Regex.Escape(oldName),
                    RegexOptions.IgnoreCase).Count;

                if (count == 0) continue;

                string updated = Regex.Replace(
                    text,
                    Regex.Escape(oldName),
                    newName,
                    RegexOptions.IgnoreCase);

                File.WriteAllText(file, updated, encoding);
                changedFiles++;
                replacements += count;
            }

            string report = Path.Combine(folder, "OpenMeteoBridgeConversion.txt");
            File.WriteAllText(
                report,
                "EmilyDesk Widget Converter\r\n" +
                "Converted: " + DateTime.Now.ToString("u") + "\r\n" +
                "Original core instance: " + oldName + "\r\n" +
                "New core instance: " + newName + "\r\n" +
                "Changed files: " + changedFiles + "\r\n" +
                "Updated references: " + replacements + "\r\n" +
                "Backup folder: " + backup + "\r\n" +
                "\r\n" +
                "Note: XWidget Designer will still show the compiled core type as " +
                "AccuWeatherCore2. The data provider is Open-Meteo through the compatibility bridge.\r\n",
                new UTF8Encoding(false));

            return ConversionResult.Ok(
                "Widget conversion completed.\r\n\r\n" +
                "Core instance:\r\n" + oldName + "  →  " + newName + "\r\n\r\n" +
                "Changed files: " + changedFiles + "\r\n" +
                "Updated references: " + replacements + "\r\n\r\n" +
                "Backup created at:\r\n" + backup + "\r\n\r\n" +
                "The Designer's compiled core type will still display AccuWeatherCore2. " +
                "That label is part of XWidget itself and is intentionally left unchanged.");
        }

        private static Encoding DetectEncoding(string file)
        {
            byte[] bom = new byte[3];
            using (var stream = File.OpenRead(file))
            {
                int read = stream.Read(bom, 0, bom.Length);
                if (read >= 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    return new UTF8Encoding(true);
                if (read >= 2 && bom[0] == 0xFF && bom[1] == 0xFE)
                    return Encoding.Unicode;
                if (read >= 2 && bom[0] == 0xFE && bom[1] == 0xFF)
                    return Encoding.BigEndianUnicode;
            }
            return new UTF8Encoding(false);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);

            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }

            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(
                    directory,
                    Path.Combine(destination, Path.GetFileName(directory)));
            }
        }

        private sealed class ConversionResult
        {
            public bool Success { get; private set; }
            public string Message { get; private set; }

            public static ConversionResult Ok(string message)
            {
                return new ConversionResult { Success = true, Message = message };
            }

            public static ConversionResult Fail(string message)
            {
                return new ConversionResult { Success = false, Message = message };
            }
        }
    }
}
