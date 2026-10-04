using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using XWidgetReborn.Widgets.Weather;

namespace EmilyDesk.Designer
{
    internal sealed partial class DesignerForm
    {
        private string ConnectionDomain
        { get { return _widgetKind == "clock" ? "Clock" : _widgetKind == "calendar" ? "Calendar" :
            _widgetKind == "recyclebin" ? "Recycle Bin" : "Weather"; } }

        private sealed class ConnectionRole
        {
            public string Label, Id, Binding;
            public bool Image;
            public ConnectionRole(string label, string id, string binding, bool image)
            { Label = label; Id = id; Binding = binding; Image = image; }
        }

        private IEnumerable<ConnectionRole> ConnectionRoles()
        {
            if (_widgetKind == "clock")
            {
                yield return new ConnectionRole("Hour hand", "clock-hour-hand", "Clock: Hour Hand", true);
                yield return new ConnectionRole("Minute hand", "clock-minute-hand", "Clock: Minute Hand", true);
                yield return new ConnectionRole("Second hand", "clock-second-hand", "Clock: Second Hand", true);
                yield return new ConnectionRole("Centre pivot", "clock-centre-pivot", "Clock: Centre Pivot", true);
                yield return new ConnectionRole("Date", "clock-date", "Clock: Date", false);
            }
            else if (_widgetKind == "calendar")
            {
                yield return new ConnectionRole("Month and year", "month-title", "Calendar: Month and Year", false);
                yield return new ConnectionRole("Previous month button", "previous-button", "Calendar: Previous Month", false);
                yield return new ConnectionRole("Next month button", "next-button", "Calendar: Next Month", false);
                yield return new ConnectionRole("Today button", "today-button", "Calendar: Today", false);
                yield return new ConnectionRole("Full date", "footer", "Calendar: Full Date", false);
            }
            else if (_widgetKind == "recyclebin")
            {
                yield return new ConnectionRole("Empty bin artwork", "bin-empty", "Recycle Bin: Empty", true);
                yield return new ConnectionRole("Full bin artwork", "bin-full", "Recycle Bin: Full", true);
            }
            else
            {
                yield return new ConnectionRole("Current weather icon", "current-icon", "Weather: Current Icon", true);
                yield return new ConnectionRole("Weather details button", "panel-button", "Control: Weather Details Button", true);
                string[] ids = { "location", "temperature", "condition", "feels-like", "humidity", "wind", "footer" };
                string[] labels = { "Location", "Temperature", "Condition", "Feels Like", "Humidity", "Wind", "Updated Time" };
                for (int index = 0; index < ids.Length; index++)
                    yield return new ConnectionRole(labels[index], ids[index], "Weather: " + labels[index], false);
                for (int day = 0;
                    day < NativeWeatherWidget.MaximumForecastDayCount; day++)
                {
                    string prefix = "Forecast " + (day + 1) + " - ";
                    yield return new ConnectionRole(prefix + "icon", "forecast-icon-" + day, "Weather: Forecast Icon", true);
                    yield return new ConnectionRole(prefix + "day", "forecast-day-" + day, "Weather: Forecast Day", false);
                    yield return new ConnectionRole(prefix + "high / low", "forecast-range-" + day, "Weather: Forecast High and Low", false);
                    yield return new ConnectionRole(prefix + "high", "forecast-high-" + day, "Weather: Forecast High", false);
                    yield return new ConnectionRole(prefix + "low", "forecast-low-" + day, "Weather: Forecast Low", false);
                    yield return new ConnectionRole(prefix + "condition", "forecast-condition-" + day, "Weather: Forecast Condition", false);
                }
            }
        }

        private ToolStripMenuItem CreateConnectAsMenu()
        {
            var menu = new ToolStripMenuItem("Connect as");
            menu.DropDownOpening += delegate {
                menu.DropDownItems.Clear();
                DesignerElement selected = _canvas.SelectedElement;
                if (selected == null) { menu.DropDownItems.Add("Select a layer first").Enabled = false; return; }
                bool image = selected.Kind == DesignerElementKind.Image;
                menu.DropDownItems.Add(image ? "Decorative image (not connected)" : "Static text (not connected)", null,
                    delegate { DisconnectElement(selected); });
                if (image && !string.IsNullOrEmpty(selected.ImagePath))
                    menu.DropDownItems.Add("Widget background", null, delegate {
                        CaptureUndo(); _layout.BackgroundImage = selected.ImagePath;
                        _layout.ReplaceDefaultBackground = true;
                        _layout.Elements.Remove(selected);
                        RememberRemovedRole(selected.Id);
                        RefreshConnectedElement(null); _canvas.RefreshReference();
                    });
                foreach (ConnectionRole role in ConnectionRoles())
                {
                    if (role.Image != image || selected.Kind == DesignerElementKind.Divider) continue;
                    if (_widgetKind == "weather" && selected.Surface == DesignerSurface.WeatherDetails &&
                        role.Id == "panel-button") continue;
                    ConnectionRole captured = role;
                    menu.DropDownItems.Add(role.Label, null, delegate {
                        CaptureUndo(); ConnectElement(selected, captured); RefreshConnectedElement(selected);
                    });
                }
            };
            return menu;
        }

        private void RememberRemovedRole(string id)
        {
            if (_layout.DeletedElementIds == null) _layout.DeletedElementIds = new List<string>();
            if (!string.IsNullOrEmpty(id) && !_layout.DeletedElementIds.Contains(id)) _layout.DeletedElementIds.Add(id);
        }

        private void DisconnectElement(DesignerElement element)
        {
            CaptureUndo();
            RememberRemovedRole(element.Id);
            element.Id = Guid.NewGuid().ToString("N");
            element.Binding = "None"; element.BindingDomain = ConnectionDomain;
            RefreshConnectedElement(element);
        }

        private void ConnectElement(DesignerElement element, ConnectionRole role)
        {
            PointF centre = ClockCentre();
            string previousId = element.Id;
            string roleId = _widgetKind == "weather" && element.Surface == DesignerSurface.WeatherDetails
                ? "details-custom-" + role.Id : role.Id;
            // Index-bearing forecast IDs are essential: changing only the
            // displayed binding would otherwise connect every column to today.
            _layout.Elements.RemoveAll(delegate(DesignerElement item) {
                return item != element && item.Surface == element.Surface &&
                    (item.Id == roleId || (_widgetKind == "clock" && item.Binding == role.Binding));
            });
            if (previousId != roleId) RememberRemovedRole(previousId);
            element.Id = roleId; element.Binding = role.Binding;
            element.BindingDomain = ConnectionDomain; element.Name = role.Label;
            element.Kind = role.Image ? DesignerElementKind.Image : DesignerElementKind.Text;
            element.Visible = true;
            if (_layout.DeletedElementIds != null)
                _layout.DeletedElementIds.RemoveAll(delegate(string id) { return id == roleId; });
            if (_widgetKind == "recyclebin")
            {
                element.Surface = role.Id == "bin-full" ? DesignerSurface.WeatherDetails : DesignerSurface.Main;
                _layout.Elements.RemoveAll(delegate(DesignerElement item) {
                    return item != element && item.Id == roleId;
                });
                _surface.SelectedIndex = element.Surface == DesignerSurface.Main ? 0 : 1;
            }
            if (_widgetKind == "clock")
            {
                element.Surface = DesignerSurface.Main;
                if (IsClockHand(element))
                {
                    element.HandPivotX = .5F;
                    element.HandPivotY = .5F;
                    element.PivotX = centre.X;
                    element.PivotY = centre.Y;
                    element.PreviewRotation = 0F;
                    _layout.ClockHandEditVersion = 1;
                }
                else if (IsClockCentre(element))
                { element.X = centre.X - element.Width * element.Scale / 2F; element.Y = centre.Y - element.Height * element.Scale / 2F; }
            }
            if (!role.Image)
            {
                if (role.Binding == "Calendar: Previous Month") element.Text = "\u2039";
                else if (role.Binding == "Calendar: Next Month") element.Text = "\u203a";
                else if (role.Binding == "Calendar: Today") element.Text = "Today";
                else if (role.Binding == "Calendar: Month and Year") element.Text = DateTime.Now.ToString("MMMM yyyy");
                else if (role.Binding == "Calendar: Full Date") element.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy");
                else if (role.Binding == "Clock: Date") element.Text = DateTime.Now.ToString("MMM d");
                else element.Text = ResolvePreviewText(element);
            }
        }

        private void RefreshConnectedElement(DesignerElement element)
        {
            RefreshLayers(); _canvas.SelectedElement = element;
            SyncSelection(); _properties.Refresh(); _canvas.Invalidate();
        }

        private void NormalizeClockRoleFromProperties()
        {
            DesignerElement element = _canvas.SelectedElement;
            if (element == null) return;
            ConnectionRole role = ConnectionRoles().FirstOrDefault(delegate(ConnectionRole value) {
                return value.Binding == element.Binding;
            });
            if (role != null) ConnectElement(element, role);
            else if (element.Binding == "None")
            { RememberRemovedRole(element.Id); element.Id = Guid.NewGuid().ToString("N"); }
            SyncSelection();
        }

        internal static void ValidateDroppedPng(string path)
        {
            if (!IsPng(path)) throw new InvalidDataException("Choose a PNG image. Transparent PNGs keep clean widget edges.");
            using (Image image = Image.FromFile(path))
            {
                if (image.RawFormat.Guid != System.Drawing.Imaging.ImageFormat.Png.Guid ||
                    image.Width < 1 || image.Height < 1)
                    throw new InvalidDataException("Choose a valid PNG image.");
            }
        }

        private static string[] DroppedPngs(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return new string[0];
            string[] paths = data.GetData(DataFormats.FileDrop) as string[];
            return paths == null ? new string[0] : paths.Where(IsPng).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private void ShowArtworkDropMenu(string[] paths, Point screenPoint)
        {
            if (_layout == null || paths.Length == 0) return;
            if (paths.Length > 32) { ShowProjectError(new InvalidDataException("Drop up to 32 PNGs at a time.")); return; }
            try { foreach (string path in paths) ValidateDroppedPng(path); }
            catch (Exception error) { ShowProjectError(error); return; }
            Point client = _canvas.PointToClient(screenPoint);
            PointF position = _canvas.DesignPoint(client);
            position.X = Math.Max(0F, Math.Min(_layout.CanvasWidth - 10F, position.X));
            position.Y = Math.Max(0F, Math.Min(_layout.CanvasHeight - 10F, position.Y));
            DesignerElement target = _canvas.ElementAtClientPoint(client);
            var menu = new ContextMenuStrip();
            menu.Items.Add("Add " + paths.Length + " image layer" + (paths.Length == 1 ? "" : "s"), null,
                delegate { TryDropAction(delegate { ImportArtworkBatch(paths, position, null); }); });
            if (paths.Length == 1)
            {
                string path = paths[0];
                menu.Items.Add("Set as widget background", null,
                    delegate { TryDropAction(delegate { SetBackground(ImportPng(path)); }); });
                if (target != null && target.Kind == DesignerElementKind.Image)
                    menu.Items.Add("Replace image: " + target.Name, null,
                        delegate { TryDropAction(delegate { ReplaceImage(target, path); }); });
                var connect = new ToolStripMenuItem("Add and connect as");
                foreach (ConnectionRole role in ConnectionRoles())
                {
                    if (!role.Image) continue;
                    if (_canvas.ActiveSurface == DesignerSurface.WeatherDetails && role.Id == "panel-button") continue;
                    ConnectionRole captured = role;
                    connect.DropDownItems.Add(role.Label, null,
                        delegate { TryDropAction(delegate { ImportArtworkBatch(paths, position, captured); }); });
                }
                if (connect.DropDownItems.Count > 0) menu.Items.Add(connect);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Cancel");
            menu.Closed += delegate {
                if (IsHandleCreated && !IsDisposed)
                    BeginInvoke((Action)delegate { menu.Dispose(); });
                else menu.Dispose();
            };
            menu.Show(screenPoint);
        }

        private void TryDropAction(Action action)
        { try { action(); } catch (Exception error) { ShowProjectError(error); } }

        private void ImportArtworkBatch(string[] paths, PointF position, ConnectionRole role)
        {
            // Decode/import the whole batch before changing the document.
            var added = new List<DesignerElement>();
            for (int index = 0; index < paths.Length; index++)
            {
                string imported = ImportPng(paths[index]);
                using (Image image = Image.FromFile(imported))
                {
                    float maximum = Math.Min(_layout.CanvasWidth, _layout.CanvasHeight) * .45F;
                    float fit = Math.Min(1F, maximum / Math.Max(image.Width, image.Height));
                    added.Add(new DesignerElement { Name = Path.GetFileNameWithoutExtension(paths[index]),
                        Kind = DesignerElementKind.Image, Binding = "None", BindingDomain = ConnectionDomain,
                        ImagePath = imported, Surface = _canvas.ActiveSurface,
                        X = Math.Min(_layout.CanvasWidth - 10F, position.X + index * 16F),
                        Y = Math.Min(_layout.CanvasHeight - 10F, position.Y + index * 16F),
                        Width = Math.Max(1F, image.Width * fit), Height = Math.Max(1F, image.Height * fit) });
                }
            }
            CaptureUndo();
            foreach (DesignerElement element in added)
            { _layout.Elements.Add(element); if (role != null) ConnectElement(element, role); }
            RefreshConnectedElement(added.LastOrDefault());
        }
    }
}
