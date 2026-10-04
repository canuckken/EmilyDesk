using System;
using System.IO;
using XWidgetReborn.Shared;

namespace XWidgetReborn.Runtime.Compatibility
{
    internal static class LegacyXWidgetAdapter
    {
        public static LegacyWidgetManifest TryLoad(string path)
        {
            string folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            string xml = Directory.Exists(path) ? Path.Combine(path, "widget.xml") : path;
            var result = new LegacyWidgetManifest { Name = Path.GetFileName(folder), Description = "Original XWidget skin", Version = string.Empty, Author = string.Empty, SourceKind = "Legacy XWidget compatibility adapter", Width = 300, Height = 180, Left = 100, Top = 100 };
            if (!File.Exists(xml)) return result;
            try
            {
                LegacyWidgetMetadata metadata =
                    LegacyWidgetMetadataReader.Read(xml, result.Name);
                result.Name = metadata.Name;
                result.Description = metadata.Description;
                result.Version = metadata.Version;
                result.Author = metadata.Author;
                result.Width = metadata.Width;
                result.Height = metadata.Height;
            }
            catch { }
            return result;
        }
    }
}
