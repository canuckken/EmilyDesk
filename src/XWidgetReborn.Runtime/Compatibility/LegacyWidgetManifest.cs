namespace XWidgetReborn.Runtime.Compatibility
{
    internal sealed class LegacyWidgetManifest
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string SourceKind { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
        public string Author { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool IsNative { get; set; }

        public static LegacyWidgetManifest CreateWelcome()
        {
            return new LegacyWidgetManifest
            {
                Id = "native.clock",
                Name = "EmilyDesk Clock",
                SourceKind = "Native widget",
                Width = 330,
                Height = 190,
                Left = 80,
                Top = 80,
                AlwaysOnTop = false,
                IsNative = true
            };
        }
    }
}
