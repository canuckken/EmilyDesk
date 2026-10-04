using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;

namespace EmilyDesk.Designer
{
    public sealed class TemplateProjectDocument
    {
        public int Version { get; set; }
        public string WidgetKind { get; set; }
        public string Theme { get; set; }
        public DesignerLayout Layout { get; set; }
    }

    internal sealed partial class DesignerForm
    {
        private bool _templateProject;
        private string _templateProjectPath;
        private string _projectSavedState;
        private ToolStripLabel _projectNotice;
        private ToolStripMenuItem _applyProjectItem;

        internal static DesignerForm CreateTemplateProject(string kind, string theme)
        {
            ValidateProjectRoute(kind, theme);
            // A unique, nonexistent directory bypasses both personal and bundled
            // layouts. No copy, reset, live-engine command or user write occurs.
            DesignerForm form;
            using (DesignerLayoutFiles.UseIsolatedDirectory(Path.Combine(
                Path.GetTempPath(), "EmilyDesk-Template-" + Guid.NewGuid().ToString("N"))))
                form = new DesignerForm(kind, theme);
            form.EnableProjectMode(null);
            return form;
        }

        internal static DesignerForm OpenTemplateProject(string path)
        {
            var serializer = ProjectSerializer();
            TemplateProjectDocument document = serializer.Deserialize<TemplateProjectDocument>(File.ReadAllText(path));
            if (document == null || document.Version != 1 || document.Layout == null ||
                document.Layout.CanvasWidth <= 0 || document.Layout.CanvasHeight <= 0 || document.Layout.Elements == null)
                throw new InvalidDataException("This is not a supported EmilyDesk template project.");
            ValidateProjectRoute(document.WidgetKind, document.Theme);
            DesignerForm form = CreateTemplateProject(document.WidgetKind, document.Theme);
            try
            {
                ResolveProjectAssets(document.Layout, Path.GetDirectoryName(Path.GetFullPath(path)));
                form.SetLayout(document.Layout);
                form.EnableProjectMode(Path.GetFullPath(path));
                return form;
            }
            catch { form.Dispose(); throw; }
        }

        private static void ValidateProjectRoute(string kind, string theme)
        {
            if (kind != "clock" && kind != "calendar" && kind != "weather" && kind != "recyclebin")
                throw new InvalidDataException("Choose a Clock, Calendar, Weather or Recycle Bin template.");
            bool found = false;
            foreach (string name in EmilyDeskThemeCatalog.Names) if (name == theme) found = true;
            if (!found) throw new InvalidDataException("Unknown template theme.");
        }

        private static JavaScriptSerializer ProjectSerializer()
        { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024 }; }

        private void EnableProjectMode(string path)
        {
            if (!_templateProject) FormClosing += ProjectFormClosing;
            _templateProject = true;
            _templateProjectPath = path;
            _projectSavedState = ProjectSerializer().Serialize(_layout);
            _projectNotice.Visible = true;
            _applyProjectItem.Enabled = true;
            UpdateProjectTitle();
        }

        private void UpdateProjectTitle()
        {
            Text = "EmilyDesk Designer - " + (_templateProjectPath == null ? "New " + _widgetKind + " project" :
                Path.GetFileName(_templateProjectPath)) + " - " + _weatherTheme + " template (not live)";
        }

        private void AddProjectTools(ToolStrip strip)
        {
            var project = new ToolStripDropDownButton("Project");
            project.DropDownItems.Add("New from Widget Templates...", null, delegate { ShowTemplatePicker(); });
            project.DropDownItems.Add("Open template project...", null, delegate { OpenProjectFromMenu(); });
            _applyProjectItem = new ToolStripMenuItem("Apply project to widget...") { Enabled = false };
            _applyProjectItem.Click += delegate { ApplyTemplateProject(); };
            project.DropDownItems.Add(_applyProjectItem);
            project.DropDownItems.Add("Drag-and-drop help", null, delegate {
                MessageBox.Show(this,
                    "Drop PNG files from File Explorer onto the canvas.\r\n\r\n" +
                    "Choose Background, add image layers, or connect a clock hand directly.\r\n" +
                    "Right-click any layer and choose Connect as to give it a role.\r\n" +
                    "Clock hands should point upward; they share the centre pivot.\r\n" +
                    "Background replacement keeps the existing canvas and layer positions.\r\n\r\n" +
                    "Save stores a separate project and its artwork. Apply project changes the selected theme only, after confirmation.",
                    "Widget Templates", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
            strip.Items.Add(project);
            _projectNotice = new ToolStripLabel("Separate project") { Visible = false, ForeColor = Color.DarkGreen };
            strip.Items.Add(_projectNotice);
        }

        private void ShowTemplatePicker()
        {
            using (var picker = new TemplatePickerForm())
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    DesignerForm form = picker.OpenProjectPath == null
                        ? CreateTemplateProject(picker.WidgetKind, picker.ThemeName)
                        : OpenTemplateProject(picker.OpenProjectPath);
                    form.Show(this);
                }
                catch (Exception error) { ShowProjectError(error); }
            }
        }

        private void OpenProjectFromMenu()
        {
            using (var dialog = new OpenFileDialog { Filter = "EmilyDesk project (*.emilyproject.json)|*.emilyproject.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try { OpenTemplateProject(dialog.FileName).Show(this); }
                catch (Exception error) { ShowProjectError(error); }
            }
        }

        private bool SaveTemplateProject(bool choosePath)
        {
            string path = _templateProjectPath;
            if (choosePath || string.IsNullOrEmpty(path))
            {
                using (var dialog = new SaveFileDialog { Filter = "EmilyDesk project (*.emilyproject.json)|*.emilyproject.json",
                    DefaultExt = "emilyproject.json", AddExtension = true,
                    FileName = path == null ? "My-" + _widgetKind + ".emilyproject.json" : Path.GetFileName(path) })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return false;
                    path = dialog.FileName;
                }
            }
            try
            {
                CaptureWoodlandChanges();
                SaveProjectFile(path);
                _templateProjectPath = Path.GetFullPath(path);
                _projectSavedState = ProjectSerializer().Serialize(_layout);
                UpdateProjectTitle();
                return true;
            }
            catch (Exception error) { ShowProjectError(error); return false; }
        }

        internal void SaveProjectFile(string path)
        {
            path = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string assetsName = Path.GetFileNameWithoutExtension(path) + ".assets";
            string assets = Path.Combine(directory, assetsName);
            DesignerLayout export = _layout.Clone();
            var copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Func<string, string> copyImage = delegate(string value)
            {
                if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value)) return value;
                string previous;
                if (copies.TryGetValue(value, out previous)) return previous;
                ValidateDroppedPng(value);
                Directory.CreateDirectory(assets);
                string name = Guid.NewGuid().ToString("N") + ".png";
                File.Copy(value, Path.Combine(assets, name));
                string relative = assetsName + "/" + name;
                copies[value] = relative;
                return relative;
            };
            export.BackgroundImage = copyImage(export.BackgroundImage);
            export.PackageIconImage = copyImage(export.PackageIconImage);
            export.PackagePreviewImage = copyImage(export.PackagePreviewImage);
            foreach (DesignerElement element in export.Elements)
            {
                element.ImagePath = copyImage(element.ImagePath);
                if (!string.IsNullOrWhiteSpace(element.FontFile) &&
                    Path.IsPathRooted(element.FontFile))
                {
                    string extension = Path.GetExtension(element.FontFile);
                    if (!string.Equals(extension, ".ttf",
                            StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(extension, ".otf",
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException(
                            "Widget fonts must be TTF or OTF files.");
                    Directory.CreateDirectory(assets);
                    string name = Guid.NewGuid().ToString("N") + extension;
                    File.Copy(element.FontFile, Path.Combine(assets, name));
                    element.FontFile = assetsName + "/" + name;
                }
            }
            if (!string.IsNullOrWhiteSpace(export.WeatherIconPack) && Path.IsPathRooted(export.WeatherIconPack))
            {
                string packName = "weather-" + Guid.NewGuid().ToString("N");
                string pack = Path.Combine(assets, packName);
                Directory.CreateDirectory(pack);
                for (int icon = 1; icon <= 44; icon++)
                {
                    string source = WeatherIconFile(export.WeatherIconPack, icon);
                    if (string.IsNullOrEmpty(source)) throw new InvalidDataException("Weather icon pack is incomplete.");
                    ValidateDroppedPng(source);
                    File.Copy(source, Path.Combine(pack, icon + ".png"));
                }
                export.WeatherIconPack = assetsName + "/" + packName;
            }
            var document = new TemplateProjectDocument { Version = 1, WidgetKind = _widgetKind,
                Theme = _weatherTheme, Layout = export };
            string temporary = path + ".saving-" + Guid.NewGuid().ToString("N");
            File.WriteAllText(temporary, ProjectSerializer().Serialize(document));
            if (File.Exists(path)) File.Replace(temporary, path, path + ".previous", true);
            else File.Move(temporary, path);
        }

        private static void ResolveProjectAssets(DesignerLayout layout, string directory)
        {
            Func<string, string> resolve = delegate(string value) {
                if (string.IsNullOrWhiteSpace(value) || Path.IsPathRooted(value)) return value;
                string candidate = Path.GetFullPath(Path.Combine(directory, value.Replace('/', Path.DirectorySeparatorChar)));
                return File.Exists(candidate) || Directory.Exists(candidate) ? candidate : value;
            };
            layout.BackgroundImage = resolve(layout.BackgroundImage);
            layout.PackageIconImage = resolve(layout.PackageIconImage);
            layout.PackagePreviewImage = resolve(layout.PackagePreviewImage);
            layout.WeatherIconPack = resolve(layout.WeatherIconPack);
            foreach (DesignerElement element in layout.Elements)
            {
                element.ImagePath = resolve(element.ImagePath);
                element.FontFile = resolve(element.FontFile);
            }
        }

        private void ApplyTemplateProject()
        {
            if (!_templateProject) return;
            if (MessageBox.Show(this, "Apply this design to the " + _weatherTheme + " " + _widgetKind +
                " widget?\r\n\r\nIts current saved layout will be backed up. Other themes are unchanged.",
                "Apply project to widget", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            if (!SaveTemplateProject(false)) return;
            try
            {
                string old = DesignerLayoutFiles.LivePath(_liveLayoutFileName);
                if (File.Exists(old)) File.Copy(old, _templateProjectPath + ".before-apply-" +
                    DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".layout.json");
                DesignerLayoutStore.PublishLive(_liveLayoutFileName, PrepareLiveProjectLayout(), null);
                XWidgetReborn.Shared.EngineClient.RefreshDesignerLayouts();
                MessageBox.Show(this, "Applied. Open the " + _weatherTheme + " " + _widgetKind +
                    " widget from the Gallery to use your design.", "Widget Templates");
            }
            catch (Exception error) { ShowProjectError(error); }
        }

        private void ProjectFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_projectSavedState == ProjectSerializer().Serialize(_layout)) return;
            DialogResult choice = MessageBox.Show(this, "Save changes to this template project before closing?",
                "Widget Templates", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            e.Cancel = choice == DialogResult.Cancel || (choice == DialogResult.Yes && !SaveTemplateProject(false));
        }

        private DesignerLayout PrepareLiveProjectLayout()
        {
            // A live widget must not depend on the removable project folder.
            DesignerLayout live = _layout.Clone();
            var copies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Func<string, string> import = delegate(string value) {
                if (string.IsNullOrWhiteSpace(value) || !Path.IsPathRooted(value)) return value;
                string result;
                if (!copies.TryGetValue(value, out result)) { result = ImportPng(value); copies[value] = result; }
                return result;
            };
            live.BackgroundImage = import(live.BackgroundImage);
            live.PackageIconImage = import(live.PackageIconImage);
            live.PackagePreviewImage = import(live.PackagePreviewImage);
            foreach (DesignerElement element in live.Elements)
            {
                element.ImagePath = import(element.ImagePath);
                if (!string.IsNullOrWhiteSpace(element.FontFile) &&
                    Path.IsPathRooted(element.FontFile))
                {
                    string extension = Path.GetExtension(element.FontFile);
                    string target = UniquePath(WidgetFontDirectory,
                        Path.GetFileNameWithoutExtension(element.FontFile) +
                        extension);
                    if (!string.Equals(Path.GetFullPath(element.FontFile),
                        Path.GetFullPath(target),
                        StringComparison.OrdinalIgnoreCase))
                        File.Copy(element.FontFile, target);
                    element.FontFile = target;
                }
            }
            if (!string.IsNullOrWhiteSpace(live.WeatherIconPack) && Path.IsPathRooted(live.WeatherIconPack))
            {
                string destination = Path.Combine(DesignerAssetDirectory, "weather-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(destination);
                for (int icon = 1; icon <= 44; icon++)
                {
                    string source = WeatherIconFile(live.WeatherIconPack, icon);
                    if (string.IsNullOrEmpty(source)) throw new InvalidDataException("Weather icon pack is incomplete.");
                    ValidateDroppedPng(source);
                    File.Copy(source, Path.Combine(destination, icon + ".png"));
                }
                live.WeatherIconPack = destination;
            }
            return live;
        }

        private void ShowProjectError(Exception error)
        { MessageBox.Show(this, error.Message, "EmilyDesk Designer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
