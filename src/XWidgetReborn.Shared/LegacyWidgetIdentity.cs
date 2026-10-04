using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace XWidgetReborn.Shared
{
    public static class LegacyWidgetIdentity
    {
        public static string LibraryPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.MyDocuments),
                    "XWidget",
                    "Widgets");
            }
        }

        public static string CreateId(string widgetDirectory)
        {
            string name = Path.GetFileName(
                Path.GetFullPath(widgetDirectory)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar));
            byte[] bytes = Encoding.UTF8.GetBytes(
                name.Trim().ToLowerInvariant());
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
                hash = algorithm.ComputeHash(bytes);

            var text = new StringBuilder("legacy.");
            for (int i = 0; i < 8; i++)
                text.Append(hash[i].ToString("x2"));
            return text.ToString();
        }
    }
}
