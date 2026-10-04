using System;
using XWidgetReborn.Shared;

namespace XWidgetReborn
{
    internal sealed class GalleryWidgetItem
    {
        public GalleryWidgetItem(WidgetDescriptor descriptor)
            : this(descriptor, string.Empty)
        {
        }

        public GalleryWidgetItem(
            WidgetDescriptor descriptor,
            string theme)
        {
            if (descriptor == null)
                throw new ArgumentNullException("descriptor");
            Descriptor = descriptor;
            Theme = theme ?? string.Empty;
        }

        public WidgetDescriptor Descriptor { get; private set; }
        public string Theme { get; private set; }
        public string GalleryKey
        {
            get { return Descriptor.Id + "|" + Theme; }
        }
        public string DisplayName
        {
            get
            {
                return string.IsNullOrWhiteSpace(Theme)
                    ? Descriptor.Name
                    : Theme + " " + Descriptor.Name;
            }
        }
        public bool IsLegacy
        {
            get
            {
                return Descriptor.Id.StartsWith(
                    "legacy.",
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        public bool IsImported
        {
            get { return !Descriptor.IsBundled && !IsLegacy; }
        }
        public string TypeName
        {
            get
            {
                return IsLegacy
                    ? "Legacy"
                    : IsImported
                        ? "Imported"
                        : "EmilyDesk";
            }
        }
        public bool IsRunning { get; set; }

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            string value = query.Trim();
            return Contains(DisplayName, value) ||
                Contains(Theme, value) ||
                Contains(Descriptor.Description, value) ||
                Contains(Descriptor.Author, value) ||
                Contains(Descriptor.Version, value) ||
                Contains(Descriptor.Category, value) ||
                (Descriptor.IsOfficial && Contains("Official", value)) ||
                Contains(TypeName, value);
        }

        private static bool Contains(string source, string value)
        {
            return !string.IsNullOrWhiteSpace(source) &&
                source.IndexOf(
                    value,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
