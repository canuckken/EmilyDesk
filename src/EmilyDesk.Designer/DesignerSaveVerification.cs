using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Widgets.Clock;
using XWidgetReborn.Widgets.Calendar;
using XWidgetReborn.Widgets.Weather;

namespace EmilyDesk.Designer
{
    internal static partial class DesignerSaveVerification
    {
        private static int _checks;
        private static void Require(bool condition, string message)
        {
            _checks++;
            if (!condition) throw new InvalidOperationException(message);
        }

        public static void Run()
        {
            string temporary = Path.Combine(Path.GetTempPath(),
                "EmilyDesk-DesignerSave-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            string oldIgnore = Environment.GetEnvironmentVariable("EMILYDESK_IGNORE_DESIGNER_LAYOUT");
            try
            {
                Environment.SetEnvironmentVariable("EMILYDESK_IGNORE_DESIGNER_LAYOUT", null);
                using (DesignerLayoutFiles.UseIsolatedDirectory(temporary))
                {
                    CheckFallbackPaths(temporary);
                    CheckLegacyWoodlandHandMigration();
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string theme in EmilyDeskThemeCatalog.BuiltInNames)
                        foreach (string kind in new[] { "clock", "calendar", "weather" })
                        {
                            string fileName = DesignerLayoutFiles.FileName(theme, kind);
                            Require(names.Add(fileName), "Themes share a layout filename: " + fileName);
                            CheckWidget(theme, kind, fileName, temporary);
                        }
                    Require(names.Count == 21, "Not all 21 editable theme/widget routes were checked.");
                    CheckWoodlandPresentationSize();
                    CheckWeatherBackgroundSizing();
                    CheckUniversalBackgroundSelection();
                    CheckClockHandEditing();
                    CheckLargeClockHandCache(temporary);
                    CheckAtomicArtworkSave(temporary);
                    CheckLegacyOverlayMigration(temporary);
                    CheckBundledLayouts(temporary);
                    CheckTemplateProjects(temporary);
                    CheckVisualColorPicker();
                    CheckFriendlyBooleanEditing();
                }
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "designer-save-tests.txt"), "PASS: " + _checks +
                    " Designer persistence/render checks across all 21 existing editable widgets and 28 template routes.\r\n" +
                    "Isolated temporary layouts only; no user layouts or live-engine commands were used.\r\n");
            }
            finally
            {
                Environment.SetEnvironmentVariable("EMILYDESK_IGNORE_DESIGNER_LAYOUT", oldIgnore);
                // This directory was created above with a unique, owned name.
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            }
        }

        private static void CheckVisualColorPicker()
        {
            Color[] colours =
            {
                Color.FromArgb(255, 73, 38, 18),
                Color.FromArgb(142, 247, 226, 190),
                Color.FromArgb(255, 45, 184, 174),
                Color.Black,
                Color.White
            };
            foreach (Color colour in colours)
            {
                float hue;
                float saturation;
                float value;
                ColorPickerMath.RgbToHsv(colour, out hue,
                    out saturation, out value);
                Color roundTrip = ColorPickerMath.FromHsv(
                    hue, saturation, value);
                Require(Math.Abs(roundTrip.R - colour.R) <= 1 &&
                    Math.Abs(roundTrip.G - colour.G) <= 1 &&
                    Math.Abs(roundTrip.B - colour.B) <= 1,
                    "Visual colour picker changed an RGB value during HSV conversion.");
            }

            Color parsed;
            Require(ColorPickerMath.TryParseExactValue(
                "#8E49A2C7", out parsed) && parsed.A == 142 &&
                parsed.R == 73 && parsed.G == 162 && parsed.B == 199,
                "Visual colour picker did not accept #AARRGGBB input.");
            Require(ColorPickerMath.TryParseExactValue(
                "73, 38, 18, 210", out parsed) && parsed.A == 210 &&
                parsed.R == 73 && parsed.G == 38 && parsed.B == 18,
                "Visual colour picker did not accept R, G, B, A input.");
            Require(!ColorPickerMath.TryParseExactValue(
                "300, 38, 18", out parsed),
                "Visual colour picker accepted an out-of-range channel.");
            Color dark = ColorPickerMath.FromHsv(28F, .7F, .3F);
            Color light = ColorPickerMath.FromHsv(28F, .7F, .8F);
            Require(light.R > dark.R && light.G > dark.G &&
                light.B > dark.B,
                "Moving upward in the visual colour field did not lighten the colour.");

            using (var dialog = new VisualColorPickerDialog(
                Color.FromArgb(177, 73, 38, 18)))
                Require(dialog.SelectedColor.A == 177 &&
                    Math.Abs(dialog.SelectedColor.R - 73) <= 1 &&
                    Math.Abs(dialog.SelectedColor.G - 38) <= 1 &&
                    Math.Abs(dialog.SelectedColor.B - 18) <= 1,
                    "Visual colour picker did not preserve its initial ARGB colour.");
        }

        private static void CheckFriendlyBooleanEditing()
        {
            foreach (string propertyName in new[]
                { "Visible", "MouseLocked", "Bold", "Italic", "WordWrap" })
            {
                PropertyDescriptor property = TypeDescriptor.GetProperties(
                    typeof(DesignerElement))[propertyName];
                Require(property != null && !property.IsBrowsable,
                    "Advanced settings still exposes " + propertyName +
                    " as a True/False value.");
            }

            using (var form = new DesignerForm("weather", "Industrial"))
            {
                CheckBox visible = Field<CheckBox>(form,
                    "_friendlyVisible");
                CheckBox locked = Field<CheckBox>(form,
                    "_friendlyLocked");
                CheckBox bold = Field<CheckBox>(form,
                    "_friendlyBold");
                CheckBox italic = Field<CheckBox>(form,
                    "_friendlyItalic");
                CheckBox wrap = Field<CheckBox>(form,
                    "_friendlyWrap");
                Button colour = Field<Button>(form,
                    "_friendlyColour");
                Require(visible.AutoSize && locked.AutoSize && bold.AutoSize &&
                    italic.AutoSize && wrap.AutoSize,
                    "A friendly Boolean setting is not presented as a fitted checkbox.");
                Require(visible.Text == "Show this layer" &&
                    locked.Text == "Lock mouse movement" &&
                    bold.Text == "Bold" && italic.Text == "Italic" &&
                    wrap.Text == "Wrap text",
                    "A friendly checkbox reverted to a raw True/False label.");
                TableLayoutPanel textGrid = colour.Parent as TableLayoutPanel;
                Require(textGrid != null && textGrid.GetRow(colour) == 2 &&
                    textGrid.GetColumn(colour) == 1 &&
                    colour.Text == "Choose colour..." &&
                    textGrid.RowStyles.Count == 6,
                    "The visible colour row is missing or squeezed out of the Text section.");
            }
        }

        private static void CheckFallbackPaths(string directory)
        {
            string shared = Path.Combine(directory, "shared.json");
            string local = Path.Combine(directory, "local.json");
            File.WriteAllText(shared, "{\"CanvasWidth\":100}");
            File.WriteAllText(local, "{\"CanvasWidth\":200}");
            File.SetLastWriteTimeUtc(shared, DateTime.UtcNow.AddMinutes(-2));
            Require(DesignerLayoutFiles.NewestPath(shared, local) == local,
                "A stale shared layout hid a newer user-local save.");
            File.SetLastWriteTimeUtc(shared, DateTime.UtcNow.AddMinutes(2));
            Require(DesignerLayoutFiles.NewestPath(shared, local) == shared,
                "The newest shared layout was ignored.");
            Require(DesignerLayoutFiles.LoadPath<SavedDesignerLayout>(shared).CanvasWidth == 100,
                "Shared layout did not load.");
            File.WriteAllText(local, "invalid json");
            Require(DesignerLayoutFiles.LoadPath<SavedDesignerLayout>(local) == null,
                "A broken layout reused a different theme/path's cache.");
        }

        private static void CheckLegacyWoodlandHandMigration()
        {
            string fileName = DesignerLayoutFiles.FileName(
                "Woodland Nature", "clock");
            string path = DesignerLayoutFiles.LocalPath(fileName);
            try
            {
                using (var form = new DesignerForm(
                    "clock", "Woodland Nature"))
                {
                    DesignerLayout layout = Field<DesignerLayout>(
                        form, "_layout");
                    DesignerElement hour = FindLayer(layout,
                        "clock-hour-hand");
                    DesignerElement minute = FindLayer(layout,
                        "clock-minute-hand");
                    hour.ImagePath = @"C:\Users\Test\AppData\Local\EmilyDesk\DesignerAssets\Clock\WoodlandNature\hour.png";
                    hour.X = 142.121246F; hour.Y = 90.5F;
                    hour.Width = 75.7575F; hour.Height = 86F;
                    hour.HandPivotX = .5F; hour.HandPivotY = 1F;
                    minute.ImagePath =
                        "Assets/Themes/WoodlandNature/woodland-custom-minute-hand.png";
                    minute.X = 149.136F; minute.Y = 62.5F;
                    minute.Width = 61.728F; minute.Height = 114F;
                    minute.HandPivotX = .5F; minute.HandPivotY = 1F;
                    DesignerLayoutStore.PublishLive(fileName,
                        layout, null);
                }
                using (var reopened = new DesignerForm(
                    "clock", "Woodland Nature"))
                {
                    DesignerLayout layout = Field<DesignerLayout>(
                        reopened, "_layout");
                    DesignerElement hour = FindLayer(layout,
                        "clock-hour-hand");
                    DesignerElement minute = FindLayer(layout,
                        "clock-minute-hand");
                    Require(hour.ImagePath.EndsWith(
                            "woodland-hour-hand.png",
                            StringComparison.OrdinalIgnoreCase) &&
                        Math.Abs(hour.Width - 13.5F) < .01F &&
                        Math.Abs(hour.Height - 72F) < .01F &&
                        Math.Abs(hour.PivotX - 180F) < .01F &&
                        Math.Abs(hour.PivotY - 176.5F) < .01F,
                        "Designer did not replace the legacy Woodland hour leaf.");
                    Require(minute.ImagePath.EndsWith(
                            "woodland-minute-hand.png",
                            StringComparison.OrdinalIgnoreCase) &&
                        Math.Abs(minute.Width - 8.5F) < .01F &&
                        Math.Abs(minute.Height - 100F) < .01F &&
                        Math.Abs(minute.PivotX - 180F) < .01F &&
                        Math.Abs(minute.PivotY - 176.5F) < .01F,
                        "Designer did not replace the portable Woodland minute leaf.");
                    using (Bitmap preview = Field<DesignerCanvas>(
                        reopened, "_canvas").RenderPackageArtwork())
                        Require(preview != null && preview.Width == 360 &&
                            preview.Height == 360,
                            "Migrated Woodland clock preview did not render.");
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                DesignerLayoutFiles.Reload();
            }
        }

        private static void CheckClockHandEditing()
        {
            using (var form = new DesignerForm("clock", "Woodland Nature"))
            {
                DesignerLayout layout = Field<DesignerLayout>(form, "_layout");
                DesignerElement hand = FindLayer(layout, "clock-minute-hand");
                Require(hand != null, "Clock hand movement check has no minute hand.");
                DesignerCanvas canvas = Field<DesignerCanvas>(form, "_canvas");
                canvas.SelectedElement = hand;
                PointF before = new PointF(hand.PivotX, hand.PivotY);
                PointF boundsBefore = new PointF(hand.X, hand.Y);
                DesignerElement centre = FindLayer(layout,
                    "clock-centre-pivot");
                Require(centre != null,
                    "Clock hand movement check has no centre pivot.");
                PointF centreBefore = new PointF(
                    centre.PivotX, centre.PivotY);
                DesignerElement hour = FindLayer(layout,
                    "clock-hour-hand");
                PointF hourBefore = new PointF(hour.PivotX, hour.PivotY);
                layout.GridEnabled = false;
                Point dragStart = Point.Round(new PointF(
                    hand.X + hand.Width * hand.Scale * .25F,
                    hand.Y + hand.Height * hand.Scale * .25F));
                InvokeMouse(canvas, "OnMouseDown", new MouseEventArgs(
                    MouseButtons.Left, 1, dragStart.X, dragStart.Y, 0));
                InvokeMouse(canvas, "OnMouseMove", new MouseEventArgs(
                    MouseButtons.Left, 0, dragStart.X + 17,
                    dragStart.Y + 13, 0));
                InvokeMouse(canvas, "OnMouseUp", new MouseEventArgs(
                    MouseButtons.Left, 1, dragStart.X + 17,
                    dragStart.Y + 13, 0));
                float movedX = hand.PivotX - before.X;
                float movedY = hand.PivotY - before.Y;
                Require(Math.Abs(movedX) + Math.Abs(movedY) > 5F &&
                    Math.Abs((hand.X - boundsBefore.X) - movedX) < .01F &&
                    Math.Abs((hand.Y - boundsBefore.Y) - movedY) < .01F,
                    "Mouse dragging a clock hand did not move its PNG and pivot.");
                Require(Math.Abs(hour.PivotX - hourBefore.X) < .01F &&
                    Math.Abs(hour.PivotY - hourBefore.Y) < .01F,
                    "Moving one hand incorrectly moved another hand.");
                Require(Math.Abs(centre.PivotX - centreBefore.X) < .01F &&
                    Math.Abs(centre.PivotY - centreBefore.Y) < .01F,
                    "Moving one hand incorrectly moved the clock hub.");
                PointF rotationStart = InvokePoint(canvas,
                    "ClockHandRotationHandle", hand);
                PointF handPivot = new PointF(hand.PivotX, hand.PivotY);
                PointF rotationEnd = RotatePoint(rotationStart,
                    handPivot, -30F);
                InvokeMouse(canvas, "OnMouseDown", new MouseEventArgs(
                    MouseButtons.Left, 1,
                    (int)Math.Round(rotationStart.X),
                    (int)Math.Round(rotationStart.Y), 0));
                InvokeMouse(canvas, "OnMouseMove", new MouseEventArgs(
                    MouseButtons.Left, 0,
                    (int)Math.Round(rotationEnd.X),
                    (int)Math.Round(rotationEnd.Y), 0));
                InvokeMouse(canvas, "OnMouseUp", new MouseEventArgs(
                    MouseButtons.Left, 1,
                    (int)Math.Round(rotationEnd.X),
                    (int)Math.Round(rotationEnd.Y), 0));
                Require(Math.Abs(hand.PreviewRotation + 30F) < 2F,
                    "Dragging the clock-hand rotation handle did not preserve counter-clockwise rotation.");
                TrackBar rotation = Field<TrackBar>(form,
                    "_friendlyRotationSlider");
                NumericUpDown exact = Field<NumericUpDown>(form,
                    "_friendlyRotation");
                rotation.Value = -137;
                Require(Math.Abs(hand.PreviewRotation + 137F) < .01F &&
                    exact.Value == -137M,
                    "Negative rotation slider and numeric value are not synchronized.");
                PointF pivotBefore = new PointF(hand.PivotX, hand.PivotY);
                typeof(DesignerForm).GetMethod("SetFriendlyHandPivot",
                    BindingFlags.NonPublic | BindingFlags.Instance).Invoke(
                        form, new object[] { .5F, .5F });
                Require(Math.Abs(hand.PivotX - pivotBefore.X) < .01F &&
                    Math.Abs(hand.PivotY - pivotBefore.Y) < .01F &&
                    hand.HandPivotX == .5F && hand.HandPivotY == .5F,
                    "Changing the hand's image pivot moved its dial position.");
                Publish(form, DesignerLayoutFiles.FileName(
                    "Woodland Nature", "clock"), null);
                SavedDesignerLayout saved = DesignerLayoutFiles.Load<
                    SavedDesignerLayout>(DesignerLayoutFiles.FileName(
                        "Woodland Nature", "clock"));
                DesignerLayer savedHand = saved.Elements.Find(
                    delegate(DesignerLayer item)
                    {
                        return item != null && item.Id ==
                            "clock-minute-hand";
                    });
                Require(saved.ClockHandEditVersion == 1 &&
                    savedHand != null &&
                    Math.Abs(savedHand.PreviewRotation + 137F) < .01F &&
                    savedHand.HandPivotX == .5F &&
                    savedHand.HandPivotY == .5F,
                    "Saved runtime layout lost hand rotation or PNG pivot.");
                DesignerElement savedBackground = FindLayer(layout,
                    "main-background");
                using (var widget = new NativeClockWidget())
                {
                    ((IWidgetThemeProvider)widget).Theme =
                        "Woodland Nature";
                    Rectangle snap = ((IWidgetSnapBoundsProvider)widget)
                        .GetSnapBounds(new Size(360, 360));
                    Require(savedBackground != null &&
                        Math.Abs(snap.X - savedBackground.X) <= 1F &&
                        Math.Abs(snap.Y - savedBackground.Y) <= 1F &&
                        Math.Abs(snap.Width - savedBackground.Width) <= 1F &&
                        Math.Abs(snap.Height - savedBackground.Height) <= 1F,
                        "Clock snapping did not use its editable background " +
                        "frame as the stable anchor.");
                }
            }
        }

        private static void CheckLargeClockHandCache(string directory)
        {
            string path = Path.Combine(directory, "large-clock-hand.png");
            using (var bitmap = new Bitmap(1200, 1600))
                bitmap.Save(path, ImageFormat.Png);
            MethodInfo resolve = typeof(NativeClockWidget).GetMethod(
                "ResolveClockImage", BindingFlags.NonPublic |
                BindingFlags.Static, null,
                new[] { typeof(string), typeof(string),
                    typeof(int), typeof(int) }, null);
            Require(resolve != null,
                "Sized clock-hand image cache is missing.");
            Image image = (Image)resolve.Invoke(null,
                new object[] { path, null, 160, 240 });
            Require(image != null && image.Width == 160 &&
                image.Height == 240,
                "Large clock-hand PNG was retained at full resolution.");
        }

        private static void InvokeMouse(DesignerCanvas canvas,
            string methodName, MouseEventArgs arguments)
        {
            MethodInfo method = typeof(DesignerCanvas).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Require(method != null, methodName + " is missing.");
            method.Invoke(canvas, new object[] { arguments });
        }

        private static PointF InvokePoint(DesignerCanvas canvas,
            string methodName, DesignerElement element)
        {
            MethodInfo method = typeof(DesignerCanvas).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Require(method != null, methodName + " is missing.");
            return (PointF)method.Invoke(canvas,
                new object[] { element });
        }

        private static PointF RotatePoint(PointF point,
            PointF pivot, float degrees)
        {
            double radians = degrees * Math.PI / 180D;
            float x = point.X - pivot.X;
            float y = point.Y - pivot.Y;
            return new PointF(
                pivot.X + x * (float)Math.Cos(radians) -
                    y * (float)Math.Sin(radians),
                pivot.Y + x * (float)Math.Sin(radians) +
                    y * (float)Math.Cos(radians));
        }

        private static void CheckAtomicArtworkSave(string directory)
        {
            string path = Path.Combine(directory, "artwork-save.png");
            MethodInfo save = typeof(DesignerForm).GetMethod(
                "SavePngAtomically", BindingFlags.NonPublic |
                BindingFlags.Static);
            Require(save != null, "Atomic package artwork writer is missing.");
            using (var bitmap = new Bitmap(24, 24))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap))
                    graphics.Clear(Color.CornflowerBlue);
                save.Invoke(null, new object[] { bitmap, path });
                save.Invoke(null, new object[] { bitmap, path });
            }
            using (Image image = Image.FromFile(path))
                Require(image.Width == 24 && image.Height == 24,
                    "Package artwork replacement produced an invalid PNG.");
        }

        private static T Field<T>(object target, string name)
        {
            return (T)target.GetType().GetField(name,
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        }

        private static void Publish(DesignerForm form, string fileName, string export)
        {
            typeof(DesignerForm).GetMethod("CaptureWoodlandChanges",
                BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null);
            DesignerLayoutStore.PublishLive(fileName, Field<DesignerLayout>(form, "_layout"), export);
        }

        private static void CheckWidget(string theme, string kind, string fileName, string directory)
        {
            string context = theme + " " + kind;
            using (var form = new DesignerForm(kind, theme))
            using (IWidget widget = kind == "clock" ? (IWidget)new NativeClockWidget() :
                kind == "calendar" ? (IWidget)new NativeCalendarWidget() : new NativeWeatherWidget())
            {
                Require(Field<string>(form, "_liveLayoutFileName") == fileName,
                    context + " Designer/runtime filenames differ.");
                DesignerLayout layout = Field<DesignerLayout>(form, "_layout");
                Require(layout.Elements != null && layout.Elements.Count >=
                    (kind == "calendar" ? 50 : kind == "weather" ? 15 : 5),
                    context + " has no complete editable built-in layer set.");
                ((IWidgetThemeProvider)widget).Theme = theme;
                var events = new TestRuntime();
                ((IRuntimeAwareWidget)widget).AttachRuntime(events);
                int invalidations = 0;
                widget.GetType().GetField("_invalidate", BindingFlags.NonPublic |
                    BindingFlags.Instance).SetValue(widget, (Action)delegate { invalidations++; });
                // No Start/AttachHost: never launch a weather fetch or access
                // persisted host settings while verifying rendering.
                Size size = kind == "weather" ?
                    (theme == "Woodland Nature" ? new Size(1141, 524) : new Size(1081, 494)) :
                    new Size(layout.CanvasWidth, layout.CanvasHeight);
                CheckExistingLayerEdits(form, widget, theme, kind, fileName, size);
                Publish(form, fileName, null);
                byte[] baseline = Render(widget, size);
                var layer = new DesignerElement {
                    Id = "save-check-text", Name = "Save Check", Text = "SAVE TEST",
                    Binding = "None", Kind = DesignerElementKind.Text,
                    X = layout.CanvasWidth * .55F, Y = layout.CanvasHeight * .25F,
                    Width = 200F, Height = 48F, FontSize = 19F, Bold = true,
                    ColorArgb = Color.Magenta.ToArgb(), Visible = true,
                    Opacity = 1F, Scale = 1F
                };
                layout.Elements.Add(layer);
                string export = Path.Combine(directory, fileName + ".export");
                Publish(form, fileName, export);
                events.Publish("designer.saved", "all");
                Require(invalidations > 0, context + " did not redraw after save.");
                Require(File.Exists(export) && File.Exists(DesignerLayoutFiles.LivePath(fileName)),
                    context + " Save As did not export and publish.");
                byte[] edited = Render(widget, size);
                Require(!Equal(baseline, edited), context + " ignored a newly saved text layer.");
                layer.FontSize = 31F; layer.ColorArgb = Color.Lime.ToArgb();
                Publish(form, fileName, null);
                byte[] restyled = Render(widget, size);
                Require(!Equal(edited, restyled), context + " ignored saved font size/colour.");
                layer.Visible = false;
                Publish(form, fileName, null);
                byte[] hidden = Render(widget, size);
                Require(!Equal(restyled, hidden), context + " ignored layer visibility.");
                layout.Elements.Remove(layer);
                layout.DeletedElementIds.Add(layer.Id);
                Publish(form, fileName, null);
                Require(Equal(hidden, Render(widget, size)), context + " restored a deleted layer.");
                var picture = new DesignerElement {
                    Id = "save-check-image", Name = "Image Check", Binding = "None",
                    Kind = DesignerElementKind.Image, X = layer.X, Y = layer.Y,
                    Width = 48F, Height = 48F, Visible = true, Opacity = 1F, Scale = 1F,
                    ImagePath = Path.Combine(directory, "image-check.png")
                };
                if (!File.Exists(picture.ImagePath))
                    using (var sample = new Bitmap(16, 16))
                    using (Graphics g = Graphics.FromImage(sample))
                    {
                        g.Clear(Color.OrangeRed);
                        sample.Save(picture.ImagePath, ImageFormat.Png);
                    }
                layout.Elements.Add(picture);
                Publish(form, fileName, null);
                byte[] withImage = Render(widget, size);
                Require(!Equal(hidden, withImage), context + " ignored a saved image layer.");
                picture.Kind = DesignerElementKind.Divider;
                picture.Width = 100F; picture.Height = 5F; picture.ColorArgb = Color.Cyan.ToArgb();
                Publish(form, fileName, null);
                Require(!Equal(withImage, Render(widget, size)), context + " ignored a saved divider layer.");
                DesignerElement button = layout.Elements.Find(delegate(DesignerElement item) {
                    return item.Id == (kind == "calendar" ? "previous-button" : "panel-button");
                });
                if (button != null)
                {
                    byte[] beforeButton = Render(widget, size);
                    button.FontSize += 8F; button.ColorArgb = Color.Magenta.ToArgb();
                    Publish(form, fileName, null);
                    Require(!Equal(beforeButton, Render(widget, size)),
                        context + " ignored saved button font size/colour.");
                }
                using (var reopened = new DesignerForm(kind, theme))
                {
                    DesignerLayout reloaded = Field<DesignerLayout>(reopened, "_layout");
                    Require(reloaded.DeletedElementIds.Contains(layer.Id), context + " lost edits on reopen.");
                    if (button != null)
                        Require(reloaded.Elements.Find(delegate(DesignerElement item) {
                            return item.Id == button.Id; }).FontSize == button.FontSize,
                            context + " overwrote saved button styling on reopen.");
                }
                using (DesignerLayoutFiles.SuspendSavedLayouts())
                    Require(SavedDesignerLayout.Current(theme, kind) == null,
                        context + " preview painted saved overlays twice.");
                Require(((IWidgetThemeProvider)widget).Theme == theme,
                    context + " fell back to another theme during rendering.");
            }
        }

        private static byte[] Render(IWidget widget, Size size)
        {
            using (var image = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(image))
            using (var stream = new MemoryStream())
            {
                graphics.Clear(Color.Transparent);
                widget.Render(graphics, new Rectangle(Point.Empty, size));
                image.Save(stream, ImageFormat.Png);
                return stream.ToArray();
            }
        }
        private static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static void CheckWeatherBackgroundSizing()
        {
            using (var widget = new NativeWeatherWidget())
            using (var image = new Bitmap(750, 500,
                PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(image))
            {
                ((IWidgetThemeProvider)widget).Theme = "Woodland Nature";
                graphics.Clear(Color.Transparent);
                widget.RenderEditableDesignerBackground(graphics,
                    new RectangleF(0F, 0F, 750F, 500F), true,
                    @"Assets\Themes\WoodlandNature\woodland-weather-skin.png",
                    1F);
                Rectangle visible = AlphaBounds(image);
                Require(visible.Width >= 740 && visible.Height >= 490,
                    "The selectable Woodland details background retained " +
                    "transparent source margins and rendered too small.");
            }
        }

        private static void CheckUniversalBackgroundSelection()
        {
            string[] themes = { "Industrial", "Art Deco", "Steampunk",
                "Woodland Nature", "Botanical Nature", "Modern",
                "Vintage" };
            foreach (string theme in themes)
                foreach (string kind in new[] { "clock", "calendar",
                    "weather" })
                    using (var form = new DesignerForm(kind, theme))
                    {
                        DesignerLayout layout = Field<DesignerLayout>(form,
                            "_layout");
                        DesignerCanvas canvas = Field<DesignerCanvas>(form,
                            "_canvas");
                        DesignerElement background = FindLayer(layout,
                            "main-background");
                        Require(background != null,
                            theme + " " + kind +
                            " has no selectable background layer.");
                        if (!string.IsNullOrWhiteSpace(
                            background.ImagePath))
                            Require(background.Width <
                                layout.CanvasWidth - .1F ||
                                background.Height <
                                    layout.CanvasHeight - .1F ||
                                background.X > .1F || background.Y > .1F,
                                theme + " " + kind +
                                " still selects the transparent source canvas.");

                        DesignerElement foreground = layout.Elements.FindLast(
                            delegate(DesignerElement item)
                            {
                                if (item == null || !item.Visible ||
                                    item.Surface != DesignerSurface.Main ||
                                    item == background) return false;
                                PointF centre = new PointF(
                                    item.X + item.Width * item.Scale / 2F,
                                    item.Y + item.Height * item.Scale / 2F);
                                return new RectangleF(background.X,
                                    background.Y, background.Width,
                                    background.Height).Contains(centre);
                            });
                        Require(foreground != null,
                            theme + " " + kind +
                            " has no foreground hit-test sample.");
                        canvas.SelectedElement = background;
                        Point click = Point.Round(new PointF(
                            foreground.X + foreground.Width *
                                foreground.Scale / 2F,
                            foreground.Y + foreground.Height *
                                foreground.Scale / 2F));
                        InvokeMouse(canvas, "OnMouseDown", new MouseEventArgs(
                            MouseButtons.Left, 1, click.X, click.Y, 0));
                        InvokeMouse(canvas, "OnMouseUp", new MouseEventArgs(
                            MouseButtons.Left, 1, click.X, click.Y, 0));
                        Require(canvas.SelectedElement != background,
                            theme + " " + kind +
                            " background blocked a foreground layer click.");

                        if (kind == "calendar")
                        {
                            Point frame = Point.Round(new PointF(
                                background.X + 2F,
                                background.Y + 2F));
                            InvokeMouse(canvas, "OnMouseDown",
                                new MouseEventArgs(MouseButtons.Left, 1,
                                    frame.X, frame.Y, 0));
                            InvokeMouse(canvas, "OnMouseUp",
                                new MouseEventArgs(MouseButtons.Left, 1,
                                    frame.X, frame.Y, 0));
                            Require(canvas.SelectedElement == background,
                                theme +
                                " calendar frame did not select its background.");
                        }
                    }
        }

        private static Rectangle AlphaBounds(Bitmap image)
        {
            int left = image.Width;
            int top = image.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                    if (image.GetPixel(x, y).A > 8)
                    {
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
            return right < left || bottom < top
                ? Rectangle.Empty
                : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private sealed class TestRuntime : IRuntimeContext, IWidgetEventBus
        {
            public string EngineVersion { get { return "verification"; } }
            public IWidgetLogger Log { get { return null; } }
            public IWidgetSettings Settings { get { return null; } }
            public IWidgetWeatherService Weather { get { return null; } }
            public IWidgetEventBus Events { get { return this; } }
            public event EventHandler<WidgetRuntimeEventArgs> Published;
            public void Publish(string topic, string payload)
            {
                if (Published != null) Published(this, new WidgetRuntimeEventArgs(topic, payload));
            }
        }
    }
}
