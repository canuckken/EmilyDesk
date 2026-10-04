using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using XWidgetReborn.WidgetSdk;

namespace EmilyDesk.Designer
{
    internal static partial class DesignerSaveVerification
    {
        private static DesignerElement FindLayer(DesignerLayout layout, string id)
        {
            return layout.Elements.Find(delegate(DesignerElement item) {
                return item != null && item.Id == id;
            });
        }

        private static void CheckExistingLayerEdits(DesignerForm form, IWidget widget,
            string theme, string kind, string fileName, Size size)
        {
            string context = theme + " " + kind;
            DesignerLayout layout = Field<DesignerLayout>(form, "_layout");
            string id = kind == "clock" ? "clock-hour-hand" : kind == "calendar" ? "month-title" : "location";
            DesignerElement item = FindLayer(layout, id);
            Require(item != null, context + " is missing built-in " + id);
            DesignerElement original = FindLayer(layout.Clone(), id);
            Publish(form, fileName, null);
            byte[] before = Render(widget, size);
            if (kind == "clock") item.Visible = false;
            else
            {
                item.FontSize += 5F;
                item.ColorArgb = Color.Magenta.ToArgb();
                item.X += 6F;
            }
            Publish(form, fileName, null);
            Require(!Equal(before, Render(widget, size)), context + " ignored an existing layer edit.");
            using (var reopened = new DesignerForm(kind, theme))
            {
                DesignerElement read = FindLayer(Field<DesignerLayout>(reopened, "_layout"), id);
                Require(read != null && read.FontSize == item.FontSize && read.ColorArgb == item.ColorArgb &&
                    read.X == item.X && read.Visible == item.Visible,
                    context + " reset an existing layer on reopen.");
            }
            int position = layout.Elements.IndexOf(item);
            layout.Elements.Remove(item);
            layout.DeletedElementIds.Add(id);
            Publish(form, fileName, null);
            byte[] deleted = Render(widget, size);
            using (var reopened = new DesignerForm(kind, theme))
                Require(FindLayer(Field<DesignerLayout>(reopened, "_layout"), id) == null,
                    context + " recreated an intentionally deleted built-in.");
            layout.DeletedElementIds.Remove(id);
            layout.Elements.Insert(position, original);
            Publish(form, fileName, null);
            Require(!Equal(deleted, Render(widget, size)), context + " ignored a restored built-in.");
            if (kind != "weather") return;

            DesignerElement title = FindLayer(layout, "details-title");
            Require(title != null, context + " has no editable details panel.");
            DesignerElement originalTitle = FindLayer(layout.Clone(), title.Id);
            SetWeatherPanel(widget, true);
            byte[] panelBefore = Render(widget, size);
            title.Text = "EDITED PANEL";
            title.FontSize += 3;
            title.ColorArgb = Color.Lime.ToArgb();
            // Do not call CaptureWoodlandChanges here: the document, not a
            // changed-ID hint, must control the next runtime rendering.
            layout.WoodlandChangedElementIds.Clear();
            DesignerLayoutStore.PublishLive(fileName, layout, null);
            Require(!Equal(panelBefore, Render(widget, size)), context + " ignored saved details-panel edits.");
            using (var reopened = new DesignerForm(kind, theme))
            {
                DesignerElement read = FindLayer(Field<DesignerLayout>(reopened, "_layout"), title.Id);
                Require(read != null && read.Text == title.Text && read.FontSize == title.FontSize &&
                    read.ColorArgb == title.ColorArgb, context + " reset details edits on reopen.");
            }
            position = layout.Elements.IndexOf(title);
            layout.Elements[position] = originalTitle;
            Publish(form, fileName, null);
            SetWeatherPanel(widget, false);
        }

        private static void SetWeatherPanel(IWidget widget, bool open)
        {
            Type type = widget.GetType();
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            type.GetField("_panelOpen", flags).SetValue(widget, open);
            type.GetField("_panelProgress", flags).SetValue(widget, open ? 1F : 0F);
            PropertyInfo property = type.GetProperty("ActiveSlidePanel", flags);
            if (property != null)
            {
                object panel = property.GetValue(widget, null);
                panel.GetType().GetMethod("SetProgress").Invoke(panel, new object[] { open ? 1F : 0F });
            }
        }

        private static void CheckLegacyOverlayMigration(string directory)
        {
            string isolated = Path.Combine(directory, "legacy-overlays");
            Directory.CreateDirectory(isolated);
            using (DesignerLayoutFiles.UseIsolatedDirectory(isolated))
                foreach (string theme in new[] { "Art Deco", "Vintage", "Modern" })
                    foreach (string kind in new[] { "clock", "calendar", "weather" })
                    {
                        if (theme == "Art Deco" && kind == "weather") continue;
                        var old = new DesignerLayout { SchemaVersion = 7, Name = theme + " " + kind,
                            CanvasWidth = kind == "clock" ? (theme == "Art Deco" ? 360 : 300)
                                : kind == "calendar" ? (theme == "Art Deco" ? 522 : 420) : 360,
                            CanvasHeight = kind == "clock" ? (theme == "Art Deco" ? 360 : 300)
                                : kind == "calendar" ? (theme == "Art Deco" ? 360 : 390) : 210 };
                        old.Elements.Add(new DesignerElement { Id = "owner-layer", Name = "Owner Layer",
                            Text = "Keep my text", Binding = "None", Kind = DesignerElementKind.Text,
                            X = 70, Y = 80, Width = 120, Height = 30, FontSize = 19,
                            ColorArgb = Color.OrangeRed.ToArgb() });
                        string deletedId = kind == "clock" ? "clock-date" : "footer";
                        old.DeletedElementIds.Add(deletedId);
                        DesignerLayoutStore.PublishLive(DesignerLayoutFiles.FileName(theme, kind), old, null);
                        using (var form = new DesignerForm(kind, theme))
                        {
                            DesignerLayout migrated = Field<DesignerLayout>(form, "_layout");
                            Require(migrated.EditableLayerVersion == 1 && migrated.Elements.Count > 5,
                                theme + " " + kind + " did not gain editable built-ins.");
                            DesignerElement owner = FindLayer(migrated, "owner-layer");
                            Require(owner != null && owner.Text == "Keep my text" && owner.FontSize == 19 &&
                                owner.ColorArgb == Color.OrangeRed.ToArgb(), "Migration discarded an owner overlay.");
                            Require(FindLayer(migrated, deletedId) == null, "Migration recreated a deleted layer.");
                        }
                    }
        }
    }
}
