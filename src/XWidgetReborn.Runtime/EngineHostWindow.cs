using System;
using System.Drawing;
using System.Windows.Forms;
using XWidgetReborn.Runtime.Core;

namespace XWidgetReborn.Runtime
{
    /// <summary>
    /// Invisible lifetime anchor for the Engine message loop.
    /// Widget windows may all close without ending the Engine process.
    /// </summary>
    internal sealed class EngineHostWindow : Form
    {
        private bool _allowClose;
        private readonly IRuntimeLoggingService _logging;

        public EngineHostWindow(IRuntimeLoggingService logging)
        {
            if (logging == null) throw new System.ArgumentNullException("logging");
            _logging = logging;
            Text = "EmilyDesk Engine Host";
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(1, 1);
            Opacity = 0d;
            _logging.Debug("EngineHostWindow constructor completed.");
        }

        public void PermitShutdown()
        {
            _allowClose = true;
            _logging.Debug("EngineHostWindow shutdown permission granted.");
        }

        protected override void SetVisibleCore(bool value)
        {
            // This form exists only to own the WinForms application lifetime.
            base.SetVisibleCore(false);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _logging.Debug("EngineHostWindow OnFormClosing. Reason=" + e.CloseReason + "; allowClose=" + _allowClose + ".");
            if (!_allowClose && e.CloseReason != CloseReason.WindowsShutDown)
            {
                e.Cancel = true;
                _logging.Debug("EngineHostWindow close cancelled.");
                return;
            }

            _logging.Debug("EngineHostWindow close accepted.");
            base.OnFormClosing(e);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _logging.Debug("EngineHostWindow OnFormClosed. Reason=" + e.CloseReason + ".");
            base.OnFormClosed(e);
        }
    }
}
