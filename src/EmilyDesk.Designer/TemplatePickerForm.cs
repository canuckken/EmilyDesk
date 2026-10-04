using System;
using System.Drawing;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;

namespace EmilyDesk.Designer
{
    internal sealed class TemplatePickerForm : Form
    {
        private readonly ComboBox _theme = new ComboBox();
        private readonly ListBox _kind = new ListBox();
        public string WidgetKind { get { return new[] { "clock", "calendar", "weather", "recyclebin" }[_kind.SelectedIndex]; } }
        public string ThemeName { get { return Convert.ToString(_theme.SelectedItem); } }
        public string OpenProjectPath { get; private set; }

        public TemplatePickerForm()
        {
            Text = "EmilyDesk - Widget Templates";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(570, 390);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            Font = new Font("Segoe UI", 10F);
            Controls.Add(new Label { Text = "Start with a working widget", AutoSize = true,
                Font = new Font(Font.FontFamily, 17F, FontStyle.Bold), Location = new Point(22, 20) });
            Controls.Add(new Label { Text = "Choose a template, then drop in your PNG artwork.\r\nYour existing widgets and saved edits are not changed.",
                Location = new Point(24, 64), Size = new Size(520, 50) });
            _kind.SetBounds(24, 123, 520, 91);
            _kind.Items.AddRange(new object[] { "Clock - dial, hour, minute and second hands",
                "Calendar - dates and working month navigation", "Weather - live fields, forecasts and details panel",
                "Recycle Bin - separate empty and full artwork" });
            _kind.SelectedIndex = 0;
            Controls.Add(_kind);
            Controls.Add(new Label { Text = "Starting theme", Location = new Point(24, 234), AutoSize = true });
            _theme.SetBounds(156, 229, 388, 30);
            _theme.DropDownStyle = ComboBoxStyle.DropDownList;
            foreach (string name in EmilyDeskThemeCatalog.Names) _theme.Items.Add(name);
            _theme.SelectedItem = "Industrial";
            Controls.Add(_theme);
            Controls.Add(new Label { Text = "Save as a project. Apply to the selected theme only when ready.",
                Location = new Point(24, 278), Size = new Size(520, 32) });
            var open = new Button { Text = "Open project...", Bounds = new Rectangle(24, 332, 150, 34) };
            open.Click += delegate {
                using (var dialog = new OpenFileDialog { Filter = "EmilyDesk project (*.emilyproject.json)|*.emilyproject.json" })
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    { OpenProjectPath = dialog.FileName; DialogResult = DialogResult.OK; }
            };
            var create = new Button { Text = "Create from template", Bounds = new Rectangle(194, 332, 210, 34), DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Bounds = new Rectangle(420, 332, 124, 34), DialogResult = DialogResult.Cancel };
            Controls.Add(open); Controls.Add(create); Controls.Add(cancel);
            AcceptButton = create; CancelButton = cancel;
        }
    }
}
