using System;
using System.IO;

namespace XWidgetReborn.Shared
{
    public static class WidgetPackagePaths
    {
        public const string DisabledMarkerFileName = ".disabled";

        public static string ImportedWidgetsRoot
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments),
                    AppConstants.ProductName,
                    "Widgets");
            }
        }

        public static bool IsImportedWidgetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string root = Path.GetFullPath(ImportedWidgetsRoot)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            return candidate.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
