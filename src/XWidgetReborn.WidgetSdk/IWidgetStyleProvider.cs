using System.Collections.Generic;
using System.Drawing;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>
    /// Optional contract for official widgets that provide structurally
    /// different built-in renderers. Styles affect layout and drawing; themes
    /// remain an independent color selection within the active style.
    /// </summary>
    public interface IWidgetStyleProvider
    {
        IEnumerable<WidgetStyleMetadata> Styles { get; }
        string Style { get; set; }
        void ManageStyles();
    }

    public sealed class WidgetStyleMetadata
    {
        public WidgetStyleMetadata(
            string id,
            string displayName,
            string description,
            string compatibleWidgetType,
            Size preferredSize,
            Size minimumSize,
            string previewImage,
            IEnumerable<string> supportedThemes)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            CompatibleWidgetType =
                compatibleWidgetType ?? string.Empty;
            PreferredSize = preferredSize;
            MinimumSize = minimumSize;
            PreviewImage = previewImage ?? string.Empty;
            SupportedThemes =
                supportedThemes ?? new string[0];
        }

        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public string Description { get; private set; }
        public string CompatibleWidgetType { get; private set; }
        public Size PreferredSize { get; private set; }
        public Size MinimumSize { get; private set; }
        public string PreviewImage { get; private set; }
        public IEnumerable<string> SupportedThemes
        {
            get;
            private set;
        }
    }
}
