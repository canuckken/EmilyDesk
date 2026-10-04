using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Widgets.Clock;
using XWidgetReborn.Widgets.Calendar;
using XWidgetReborn.Widgets.Weather;
using XWidgetReborn.Widgets.RecycleBin;

namespace EmilyDesk.Designer
{
    internal static partial class DesignerSaveVerification
    {
        private static object InvokeTemplate(object target, string method, params object[] arguments)
        {
            return target.GetType().GetMethod(method, BindingFlags.Instance |
                BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, arguments);
        }

        private static void CheckTemplateProjects(string directory)
        {
            string tests = Path.Combine(directory, "templates");
            Directory.CreateDirectory(tests);
            foreach (string theme in EmilyDeskThemeCatalog.BuiltInNames)
                foreach (string kind in new[] { "clock", "calendar", "weather", "recyclebin" })
                {
                    string id = DesignerLayoutFiles.FileName(theme, kind);
                    string current = DesignerLayoutFiles.LivePath(id);
                    byte[] before = File.Exists(current) ? File.ReadAllBytes(current) : null;
                    using (DesignerForm form = DesignerForm.CreateTemplateProject(kind, theme))
                    {
                        Require(Field<bool>(form, "_templateProject"), "Template did not open as an isolated project.");
                        DesignerLayout layout = Field<DesignerLayout>(form, "_layout");
                        Require(layout.Elements.Count > 0, "Empty template: " + id);
                        string path = Path.Combine(tests, id + ".emilyproject.json");
                        form.SaveProjectFile(path);
                        using (DesignerForm loaded = DesignerForm.OpenTemplateProject(path))
                        {
                            DesignerLayout copy = Field<DesignerLayout>(loaded, "_layout");
                            Require(copy.CanvasWidth == layout.CanvasWidth && copy.CanvasHeight == layout.CanvasHeight &&
                                copy.Elements.Count == layout.Elements.Count, "Project round trip changed its geometry: " + id);
                            Require(Field<string>(loaded, "_widgetKind") == kind && Field<string>(loaded, "_weatherTheme") == theme,
                                "Reopening lost the project kind or theme.");
                        }
                    }
                    Require(before == null ? !File.Exists(current) : Equal(before, File.ReadAllBytes(current)),
                        "Creating/saving a template modified a live layout: " + id);
                }

            // All imported test artwork stays under the existing owned temp directory.
            string imagePath = Path.Combine(tests, "hand.png");
            using (var image = new Bitmap(20, 120, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(image))
            { g.Clear(Color.Transparent); g.FillRectangle(Brushes.Gold, 7, 3, 6, 112); image.Save(imagePath, ImageFormat.Png); }
            DesignerForm.ValidateDroppedPng(imagePath);
            string invalid = Path.Combine(tests, "invalid.png");
            File.WriteAllText(invalid, "Not a PNG");
            bool rejected = false;
            try { DesignerForm.ValidateDroppedPng(invalid); } catch { rejected = true; }
            Require(rejected, "Corrupt dropped artwork was accepted.");

            using (DesignerForm form = DesignerForm.CreateTemplateProject("clock", "Industrial"))
            {
                DesignerLayout layout = Field<DesignerLayout>(form, "_layout");
                DesignerLayout original = layout.Clone();
                InvokeTemplate(form, "SetBackground", imagePath);
                Require(layout.ReplaceDefaultBackground && layout.CanvasWidth == original.CanvasWidth &&
                    layout.CanvasHeight == original.CanvasHeight && layout.Elements.Count == original.Elements.Count,
                    "Dropping a background changed the template canvas or its layers.");
                for (int index = 0; index < original.Elements.Count; index++)
                    Require(layout.Elements[index].X == original.Elements[index].X &&
                        layout.Elements[index].Y == original.Elements[index].Y &&
                        layout.Elements[index].Width == original.Elements[index].Width &&
                        layout.Elements[index].Height == original.Elements[index].Height,
                        "Background replacement moved an existing element.");
                var hand = new DesignerElement { Name = "Dropped hand", Kind = DesignerElementKind.Image,
                    ImagePath = imagePath, Width = 20, Height = 120, Binding = "Clock: Minute Hand", BindingDomain = "Clock" };
                layout.Elements.Add(hand);
                Field<DesignerCanvas>(form, "_canvas").SelectedElement = hand;
                InvokeTemplate(form, "NormalizeClockRoleFromProperties");
                Require(hand.Id == "clock-minute-hand" && layout.Elements.Count(e => e.Binding == "Clock: Minute Hand") == 1,
                    "Connecting a new hand left a duplicate or non-functional runtime ID.");
                PointF pivot = (PointF)InvokeTemplate(form, "ClockCentre");
                Require(Math.Abs(hand.PivotX - pivot.X) < .01F && Math.Abs(hand.PivotY - pivot.Y) < .01F,
                    "Dropped hand did not align with the shared clock centre.");
                string path = Path.Combine(tests, "custom-clock.emilyproject.json");
                form.SaveProjectFile(path);
                using (DesignerForm loaded = DesignerForm.OpenTemplateProject(path))
                {
                    DesignerElement saved = Field<DesignerLayout>(loaded, "_layout").Elements.First(e => e.Id == hand.Id);
                    Require(saved.ImagePath != imagePath && File.Exists(saved.ImagePath) &&
                        Equal(File.ReadAllBytes(imagePath), File.ReadAllBytes(saved.ImagePath)),
                        "Project did not include an independent, exact copy of dropped artwork.");
                }
            }

            // A connected forecast uses its indexed ID, not a manually typed name.
            using (DesignerForm form = DesignerForm.CreateTemplateProject("weather", "Industrial"))
            {
                DesignerLayout layout = Field<DesignerLayout>(form, "_layout");
                var text = new DesignerElement { Name = "New Text", Kind = DesignerElementKind.Text };
                layout.Elements.Add(text);
                var roles = (System.Collections.IEnumerable)InvokeTemplate(form, "ConnectionRoles");
                foreach (object role in roles)
                    if ((string)role.GetType().GetField("Id").GetValue(role) == "forecast-low-3")
                        InvokeTemplate(form, "ConnectElement", text, role);
                Require(text.Id == "forecast-low-3" && text.Binding == "Weather: Forecast Low" &&
                    layout.Elements.Count(e => e.Id == text.Id) == 1,
                    "Connecting forecast day 4 lost its index or left a duplicate.");
            }

            CheckTemplateBackgroundTransparency(tests);

            // Verify the new empty/full renderer without querying or emptying Windows' Recycle Bin.
            using (DesignerForm form = DesignerForm.CreateTemplateProject("recyclebin", "Woodland Nature"))
            using (var bin = new NativeRecycleBinWidget())
            using (DesignerLayoutFiles.UseIsolatedDirectory(Path.Combine(tests, "bin-live")))
            {
                DesignerLayoutStore.PublishLive(DesignerLayoutFiles.FileName("Woodland Nature", "recyclebin"),
                    Field<DesignerLayout>(form, "_layout"), null);
                bin.Theme = "Woodland Nature";
                byte[] empty = Render(bin, bin.DefaultSize);
                typeof(NativeRecycleBinWidget).GetField("_isFull", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bin, true);
                Require(!Equal(empty, Render(bin, bin.DefaultSize)), "Recycle-bin template did not switch empty/full artwork.");
                var runtime = new TestRuntime();
                int invalidations = 0;
                typeof(NativeRecycleBinWidget).GetField("_invalidate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(bin, (Action)delegate { invalidations++; });
                bin.AttachRuntime(runtime);
                runtime.Publish("designer.saved", "all");
                Require(invalidations == 1, "Saving a bin design did not invalidate an unchanged live bin.");
                bin.AttachRuntime(null);
                runtime.Publish("designer.saved", "all");
                Require(invalidations == 1, "Bin retained a detached Designer event subscription.");
            }
        }

        private static void CheckTemplateBackgroundTransparency(string tests)
        {
            using (DesignerLayoutFiles.UseIsolatedDirectory(Path.Combine(tests, "transparent-live")))
            foreach (string theme in new[] { "Modern", "Vintage", "Art Deco" })
                foreach (string kind in new[] { "clock", "calendar", "weather" })
                {
                    if (theme == "Art Deco" && kind == "weather") continue;
                    using (DesignerForm form = DesignerForm.CreateTemplateProject(kind, theme))
                    {
                        DesignerLayout layout = Field<DesignerLayout>(form, "_layout");
                        layout.ReplaceDefaultBackground = true;
                        layout.BackgroundImage = string.Empty;
                        layout.Elements.Clear();
                        DesignerLayoutStore.PublishLive(DesignerLayoutFiles.FileName(theme, kind), layout, null);
                        IWidget widget = kind == "clock" ? (IWidget)new NativeClockWidget() :
                            kind == "calendar" ? (IWidget)new NativeCalendarWidget() : new NativeWeatherWidget();
                        using (widget)
                        {
                            ((IWidgetThemeProvider)widget).Theme = theme;
                            using (var stream = new MemoryStream(Render(widget, widget.DefaultSize)))
                            using (var image = new Bitmap(stream))
                            {
                                bool clear = true;
                                for (int y = 0; y < image.Height && clear; y++)
                                    for (int x = 0; x < image.Width; x++)
                                        if (image.GetPixel(x, y).A != 0) { clear = false; break; }
                                Require(clear, "Custom transparent background retained the theme frame: " + theme + " " + kind);
                            }
                        }
                    }
                }
        }
    }
}
