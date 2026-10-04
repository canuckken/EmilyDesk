using System;
using System.IO;
using System.Xml;

namespace XWidgetReborn.Shared
{
    public sealed class LegacyWidgetMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public static class LegacyWidgetMetadataReader
    {
        public static LegacyWidgetMetadata Read(
            string widgetManifestPath,
            string fallbackName)
        {
            using (var stream = new FileStream(
                widgetManifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read))
                return Read(stream, fallbackName);
        }

        public static LegacyWidgetMetadata Read(
            Stream stream,
            string fallbackName)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            var document = new XmlDocument { XmlResolver = null };
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };
            using (XmlReader reader = XmlReader.Create(stream, settings))
                document.Load(reader);

            return new LegacyWidgetMetadata
            {
                Name = ReadText(
                    document,
                    "displayName",
                    ReadText(document, "name", fallbackName)),
                Description = ReadText(
                    document,
                    "description",
                    "Original XWidget skin"),
                Version = ReadText(document, "version", string.Empty),
                Author = ReadText(document, "authorName", string.Empty),
                Width = ReadInteger(document, "width", 300),
                Height = ReadInteger(document, "height", 180)
            };
        }

        private static string ReadText(
            XmlDocument document,
            string elementName,
            string fallback)
        {
            string normalizedName = elementName.ToLowerInvariant();
            XmlNode node = document.SelectSingleNode(
                "//*[translate(local-name()," +
                "'ABCDEFGHIJKLMNOPQRSTUVWXYZ'," +
                "'abcdefghijklmnopqrstuvwxyz')='" +
                normalizedName +
                "']");
            return node == null || string.IsNullOrWhiteSpace(node.InnerText)
                ? fallback
                : node.InnerText.Trim();
        }

        private static int ReadInteger(
            XmlDocument document,
            string elementName,
            int fallback)
        {
            int value;
            return int.TryParse(
                ReadText(document, elementName, null),
                out value)
                ? value
                : fallback;
        }
    }
}
