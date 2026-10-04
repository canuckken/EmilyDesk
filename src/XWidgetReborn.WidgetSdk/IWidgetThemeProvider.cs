using System.Collections.Generic;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>
    /// Optional contract for official widgets whose visual themes can be
    /// selected directly from the shared host context menu.
    /// </summary>
    public interface IWidgetThemeProvider
    {
        IEnumerable<string> Themes { get; }
        string Theme { get; set; }
    }
}
