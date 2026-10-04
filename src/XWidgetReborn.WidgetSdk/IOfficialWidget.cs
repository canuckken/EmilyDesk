using System;
using System.Collections.Generic;
using System.Drawing;

namespace XWidgetReborn.WidgetSdk
{
    /// <summary>
    /// Optional contract for official widgets that expose persistent settings.
    /// Existing IWidget implementations remain fully compatible.
    /// </summary>
    public interface IOfficialWidget
    {
        void AttachHost(IWidgetHostContext host);
        void ShowSettings();
        void ResetSettings();
        IEnumerable<WidgetMenuCommand> GetMenuCommands();
    }

    public interface IWidgetHostContext
    {
        string GetSetting(string key, string fallback);
        void SetSetting(string key, string value);
        double Opacity { get; set; }
        float Scale { get; set; }
        void SetPreferredSize(Size size);
        void SetPreferredSizeAnchoredRight(Size size);
        void SetFixedCompositionSurface(Size size, Rectangle parentBounds);
        void SetWindowShape(WidgetWindowShape shape);
        void Invalidate();
        void ReportDiagnostic(string message);
    }

    // Optional stable snap anchor for widgets whose movable foreground
    // artwork can extend beyond the main frame. Coordinates are in the
    // rendered surface passed by the host.
    public interface IWidgetSnapBoundsProvider
    {
        Rectangle GetSnapBounds(Size renderedSurface);
    }

    public enum WidgetWindowShape
    {
        Rectangle,
        AlphaRectangle,
        Ellipse,
        RoundedRectangle
    }

    public sealed class WidgetMenuCommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _isChecked;

        public WidgetMenuCommand(
            string text,
            bool isChecked,
            Action execute)
        {
            Text = text ?? string.Empty;
            _isChecked = delegate { return isChecked; };
            _execute = execute;
        }

        public WidgetMenuCommand(
            string text,
            Func<bool> isChecked,
            Action execute)
        {
            Text = text ?? string.Empty;
            _isChecked = isChecked;
            _execute = execute;
        }

        public string Text { get; private set; }
        public bool IsChecked
        {
            get { return _isChecked != null && _isChecked(); }
        }

        public void Execute()
        {
            if (_execute != null) _execute();
        }
    }
}
