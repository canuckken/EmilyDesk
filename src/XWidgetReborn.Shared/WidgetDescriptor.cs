using System;

namespace XWidgetReborn.Shared
{
    public sealed class WidgetDescriptor
    {
        public WidgetDescriptor(string id, string name, string description)
            : this(id, name, description, string.Empty, string.Empty, string.Empty, string.Empty)
        {
        }

        public WidgetDescriptor(string id, string name, string description, string version, string author, string iconPath)
            : this(id, name, description, version, author, iconPath, string.Empty)
        {
        }

        public WidgetDescriptor(string id, string name, string description, string version, string author, string iconPath, string previewPath)
            : this(id, name, description, version, author, iconPath, previewPath,
                string.Empty, false, false, string.Empty, new string[0])
        {
        }

        public WidgetDescriptor(string id, string name, string description, string version, string author, string iconPath, string previewPath,
            string category, bool isOfficial, bool isBundled, string minimumEngineVersion, string[] capabilities)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Widget ID is required.", "id");
            Id = id;
            Name = string.IsNullOrWhiteSpace(name) ? id : name;
            Description = description ?? string.Empty;
            Version = version ?? string.Empty;
            Author = author ?? string.Empty;
            IconPath = iconPath ?? string.Empty;
            PreviewPath = previewPath ?? string.Empty;
            Category = category ?? string.Empty;
            IsOfficial = isOfficial;
            IsBundled = isBundled;
            MinimumEngineVersion = minimumEngineVersion ?? string.Empty;
            Capabilities = capabilities ?? new string[0];
        }

        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public string Version { get; private set; }
        public string Author { get; private set; }
        public string IconPath { get; private set; }
        public string PreviewPath { get; private set; }
        public string Category { get; private set; }
        public bool IsOfficial { get; private set; }
        public bool IsBundled { get; private set; }
        public string MinimumEngineVersion { get; private set; }
        public string[] Capabilities { get; private set; }
    }
}
