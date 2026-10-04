using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Widgets.Calendar;
using XWidgetReborn.Widgets.Clock;
using XWidgetReborn.Widgets.Weather;
using XWidgetReborn.Shared;

namespace EmilyDesk.Designer
{
    internal sealed partial class DesignerForm : Form
    {
        private const int ArtDecoDetailsSchemaVersion = 5;
        private const int IndustrialDetailsRowOrderVersion = 4;
        private readonly DesignerCanvas _canvas = new DesignerCanvas();
        private readonly ListBox _layers = new ListBox();
        private readonly PropertyGrid _properties = new PropertyGrid();
        private readonly Panel _propertyInspector = new Panel();
        private readonly Label _propertySelectionLabel = new Label();
        private readonly PictureBox _propertyImagePreview = new PictureBox();
        private readonly Button _propertyChangeImageButton = new Button();
        private readonly Button _propertyRestoreImageButton = new Button();
        private readonly Button _propertyClearImageButton = new Button();
        private readonly NumericUpDown _widthEditor = new NumericUpDown();
        private readonly NumericUpDown _heightEditor = new NumericUpDown();
        private readonly NumericUpDown _fontSizeEditor = new NumericUpDown();
        private readonly Label _numericPropertyLabel = new Label();
        private readonly ToolStripButton _undoButton = new ToolStripButton("Undo");
        private readonly ToolStripButton _redoButton = new ToolStripButton("Redo");
        private readonly ToolStripButton _gridButton = new ToolStripButton("Snap to Grid") { CheckOnClick = true };
        private readonly ToolStripComboBox _zoom = new ToolStripComboBox();
        private readonly ToolStripComboBox _surface = new ToolStripComboBox();
        private readonly Stack<DesignerLayout> _undo = new Stack<DesignerLayout>();
        private readonly Stack<DesignerLayout> _redo = new Stack<DesignerLayout>();
        private DesignerLayout _layout;
        private string _layoutPath;
        private string _selectedNumericPropertyName;
        private string _projectRoot;
        private bool _refreshing;
        private DesignerLayout _propertyBaseline;
        private string _liveSavedState;
        private DesignerLayout _woodlandBaseline;
        private SplitContainer _workspaceSplit;
        private readonly NativeWeatherWidget _weatherReference =
            new NativeWeatherWidget();
        private readonly NativeClockWidget _clockReference =
            new NativeClockWidget();
        private readonly NativeCalendarWidget _calendarReference =
            new NativeCalendarWidget();
        private readonly IWidgetDesignerProvider _optionalWidgetDesigner;
        private readonly string _optionalWidgetPackageRoot;
        private readonly string _widgetKind;
        private readonly string _weatherTheme;
        private string _liveLayoutFileName = "industrial-weather.layout.json";
        private PointF _contextInsertionPoint = new PointF(40F, 40F);

        private bool IsBotanicalWeatherTheme
        {
            get { return _weatherTheme == "Botanical Nature"; }
        }

        private bool IsSteampunkWeatherTheme
        {
            get { return _weatherTheme == "Steampunk"; }
        }

        private bool IsImportedWeatherTheme
        {
            get { return EmilyDeskThemeCatalog.Get(_weatherTheme).IsImported; }
        }

        private bool UsesThemeSnapshot
        {
            get
            {
                return _weatherTheme == "Modern" ||
                    _weatherTheme == "Vintage" ||
                    (_weatherTheme == "Art Deco" &&
                        _widgetKind != "weather");
            }
        }

        private bool IsNatureWeatherTheme
        {
            get { return _weatherTheme == "Woodland Nature" || IsBotanicalWeatherTheme; }
        }

        public DesignerForm(string widgetKind, string weatherTheme)
            : this(widgetKind, weatherTheme, null, null)
        {
        }

        private DesignerForm(string widgetKind, string weatherTheme,
            IWidgetDesignerProvider optionalWidgetDesigner,
            string optionalWidgetPackageRoot)
        {
            _optionalWidgetDesigner = optionalWidgetDesigner;
            _optionalWidgetPackageRoot = optionalWidgetPackageRoot;
            _widgetKind = optionalWidgetDesigner != null
                ? optionalWidgetDesigner.DesignerKind
                : string.Equals(widgetKind, "clock",
                StringComparison.OrdinalIgnoreCase) ? "clock"
                : string.Equals(widgetKind, "calendar",
                    StringComparison.OrdinalIgnoreCase) ? "calendar"
                : string.Equals(widgetKind, "recyclebin", StringComparison.OrdinalIgnoreCase) ? "recyclebin" : "weather";
            _weatherTheme = string.IsNullOrWhiteSpace(weatherTheme)
                ? "Industrial" : EmilyDeskThemeCatalog.Normalize(weatherTheme);
            Text = "EmilyDesk Designer";
            Icon = LoadIcon();
            Width = 1280;
            Height = 820;
            MinimumSize = new Size(960, 640);
            StartPosition = FormStartPosition.CenterScreen;
            if (_widgetKind == "clock") _clockReference.Theme = _weatherTheme;
            if (_widgetKind == "calendar") _calendarReference.Theme = _weatherTheme;
            if (_widgetKind == "weather") _weatherReference.Theme = _weatherTheme;
            BuildInterface();
            FormClosing += DesignerFormClosing;
            FormClosed += delegate
            {
                IDisposable disposable = _optionalWidgetDesigner as IDisposable;
                if (disposable != null) disposable.Dispose();
            };
            FindProjectRoot();
            _canvas.ReferenceRenderer = delegate(Graphics graphics, Rectangle bounds)
            {
                if (_optionalWidgetDesigner != null)
                {
                    bool hasEditableBackground = _layout != null &&
                        _layout.Elements != null &&
                        _layout.Elements.Any(IsBackgroundElement);
                    if (!hasEditableBackground)
                        _optionalWidgetDesigner.RenderDesignerBackground(
                            graphics, bounds);
                    return;
                }
                if (_widgetKind == "recyclebin")
                {
                    if (_layout != null && !string.IsNullOrWhiteSpace(_layout.BackgroundImage))
                        DesignerLayerPainter.DrawImage(graphics, ResolveLayoutAsset(_layout.BackgroundImage), bounds, 1F);
                    return;
                }
                if (_widgetKind == "weather" && !UsesThemeSnapshot &&
                    _canvas.ActiveSurface == DesignerSurface.Main && _layout != null &&
                    !string.Equals(ResolveLayoutAsset(_layout.BackgroundImage),
                        ThemeBackgroundAssetPath(), StringComparison.OrdinalIgnoreCase))
                {
                    RenderArtDecoBackground(graphics, bounds);
                    return;
                }
                if (UsesThemeSnapshot)
                {
                    if (_layout == null || !_layout.ReplaceDefaultBackground ||
                        _canvas.ActiveSurface != DesignerSurface.Main)
                    using (DesignerLayoutFiles.SuspendSavedLayouts())
                    {
                        if (_widgetKind == "clock")
                            _clockReference.RenderEditableDesignerReference(graphics, bounds);
                        else if (_widgetKind == "calendar")
                            _calendarReference.RenderEditableDesignerReference(graphics, bounds);
                        else
                            _weatherReference.RenderEditableDesignerReference(graphics, bounds,
                                _canvas.ActiveSurface == DesignerSurface.WeatherDetails);
                    }
                    if (_canvas.ActiveSurface == DesignerSurface.Main &&
                        _layout != null && !string.IsNullOrWhiteSpace(_layout.BackgroundImage))
                        DesignerLayerPainter.DrawImage(graphics, ResolveLayoutAsset(_layout.BackgroundImage),
                            bounds, 1F);
                }
                else if (_widgetKind == "clock")
                    _clockReference.RenderIndustrialDesignerReference(
                        graphics, bounds, _layout == null ? null : _layout.BackgroundImage,
                        _layout != null && _layout.ReplaceDefaultBackground);
                else if (_widgetKind == "calendar")
                    _calendarReference.RenderIndustrialDesignerReference(
                        graphics, bounds, _layout == null ? null : _layout.BackgroundImage);
                else if (_weatherTheme == "Art Deco")
                {
                    if (_canvas.ActiveSurface ==
                        DesignerSurface.WeatherDetails)
                        _weatherReference.RenderArtDecoDesignerDetailsReference(
                            graphics, bounds);
                    else
                        RenderArtDecoBackground(graphics, bounds);
                }
                else if (IsNatureWeatherTheme &&
                    _canvas.ActiveSurface == DesignerSurface.WeatherDetails)
                {
                    if (IsBotanicalWeatherTheme)
                        _weatherReference.RenderBotanicalDesignerDetailsReference(
                            graphics, bounds);
                    else
                        _weatherReference.RenderWoodlandDesignerDetailsReference(
                            graphics, bounds);
                }
                else if (IsNatureWeatherTheme)
                {
                    if (IsBotanicalWeatherTheme)
                        _weatherReference.RenderBotanicalDesignerReference(
                            graphics, bounds);
                    else
                        _weatherReference.RenderWoodlandDesignerReference(
                            graphics, bounds);
                }
                else if (IsSteampunkWeatherTheme &&
                    _canvas.ActiveSurface == DesignerSurface.WeatherDetails)
                    _weatherReference.RenderSteampunkDesignerDetailsReference(
                        graphics, bounds);
                else if (IsSteampunkWeatherTheme)
                    _weatherReference.RenderSteampunkDesignerReference(
                        graphics, bounds);
                else if (_canvas.ActiveSurface == DesignerSurface.WeatherDetails)
                    _weatherReference.RenderIndustrialDesignerDetailsReference(
                        graphics, bounds);
                else
                    _weatherReference.RenderIndustrialDesignerReference(
                        graphics, bounds);
            };
            _canvas.BoundImageRenderer = delegate(
                Graphics graphics, DesignerElement element, RectangleF bounds)
            {
                if (_optionalWidgetDesigner != null)
                {
                    var backgroundProvider = _optionalWidgetDesigner as
                        IWidgetDesignerBackgroundLayerProvider;
                    if (IsBackgroundElement(element) &&
                        backgroundProvider != null)
                        backgroundProvider.RenderDesignerBackgroundLayer(
                            graphics, Rectangle.Round(bounds),
                            ResolveLayoutAsset(element.ImagePath),
                            element.Opacity);
                    else if (!string.IsNullOrWhiteSpace(element.ImagePath))
                        DesignerLayerPainter.DrawImage(graphics,
                            ResolveLayoutAsset(element.ImagePath), bounds,
                            element.Opacity);
                    return;
                }
                if (IsBackgroundElement(element))
                {
                    if (_widgetKind == "weather")
                        _weatherReference.RenderEditableDesignerBackground(
                            graphics, bounds,
                            element.Surface ==
                                DesignerSurface.WeatherDetails,
                            element.ImagePath, element.Opacity);
                    else if (!string.IsNullOrWhiteSpace(element.ImagePath))
                        DesignerLayerPainter.DrawImageAlphaCropped(graphics,
                            ResolveLayoutAsset(element.ImagePath), bounds,
                            element.Opacity);
                    else using (DesignerLayoutFiles.SuspendSavedLayouts())
                    {
                        Rectangle destination = Rectangle.Round(bounds);
                        if (_widgetKind == "clock")
                            _clockReference.RenderEditableDesignerReference(
                                graphics, destination);
                        else if (_widgetKind == "calendar")
                            _calendarReference.RenderEditableDesignerReference(
                                graphics, destination);
                    }
                    return;
                }
                if (_widgetKind == "clock")
                {
                    PointF pivot = IsClockHand(element)
                        ? new PointF(element.PivotX, element.PivotY)
                        : ClockCentre();
                    if (DrawClockImagePreview(
                        graphics, element, bounds, pivot)) return;
                    if (UsesThemeSnapshot)
                        _clockReference.RenderEditableDesignerSymbol(graphics, element.Id,
                            bounds, element.PreviewRotation, pivot, element.ColorArgb, element.Opacity);
                    else _clockReference.RenderIndustrialDesignerSymbol(
                        graphics, element.Id, bounds,
                        element.PreviewRotation, pivot, element.Opacity);
                }
                else if (_widgetKind == "calendar" || _widgetKind == "recyclebin")
                    return;
                else
                {
                    if (DrawWeatherIconPackPreview(
                        graphics, element, bounds)) return;
                    if (UsesThemeSnapshot)
                        _weatherReference.RenderEditableDesignerSymbol(graphics, element.Id,
                            bounds, element.ColorArgb, element.Opacity, element.FontSize);
                    else if (_weatherTheme == "Art Deco")
                        _weatherReference.RenderArtDecoDesignerSymbol(
                            graphics, element.Id, bounds,
                            (int)element.ImageHorizontalPlacement,
                            (int)element.ImageVerticalPlacement,
                            element.ImageOffsetX, element.ImageOffsetY,
                            element.ColorArgb, element.Opacity,
                            element.FontSize);
                    else if (IsNatureWeatherTheme)
                    {
                        if (IsBotanicalWeatherTheme)
                            _weatherReference.RenderBotanicalDesignerSymbol(
                                graphics, element.Id, bounds,
                                element.ColorArgb, element.Opacity,
                                element.FontSize);
                        else
                            _weatherReference.RenderWoodlandDesignerSymbol(
                                graphics, element.Id, bounds,
                                element.ColorArgb, element.Opacity,
                                element.FontSize);
                    }
                    else if (IsSteampunkWeatherTheme)
                        _weatherReference.RenderSteampunkDesignerSymbol(
                            graphics, element.Id, bounds,
                            element.ColorArgb, element.Opacity,
                            element.FontSize);
                    else
                        _weatherReference.RenderIndustrialDesignerSymbol(
                            graphics, element.Id, bounds,
                            element.ColorArgb, element.Opacity,
                            element.FontSize);
                }
            };
            _canvas.BoundTextRenderer = _optionalWidgetDesigner == null
                ? (Func<DesignerElement, string>)ResolvePreviewText
                : delegate(DesignerElement element)
                {
                    return _optionalWidgetDesigner.ResolveDesignerText(
                        ToSavedLayer(element));
                };
            _canvas.TextBackgroundRenderer = delegate(Graphics graphics,
                DesignerElement element, RectangleF bounds)
            {
                var textBackground = _optionalWidgetDesigner as
                    IWidgetDesignerTextBackgroundProvider;
                if (textBackground != null)
                    textBackground.RenderDesignerTextBackground(
                        graphics, ToSavedLayer(element));
                if (UsesThemeSnapshot && _widgetKind == "calendar")
                    _calendarReference.RenderEditableButtonBackground(graphics, element.Id, bounds);
            };
            if (_optionalWidgetDesigner != null)
                LoadOptionalWidgetLayout();
            else if (_widgetKind == "recyclebin")
                LoadRecycleBinLayout();
            else if (UsesThemeSnapshot)
                LoadThemeSnapshotLayout();
            else if (_widgetKind == "clock")
                LoadIndustrialClock();
            else if (_widgetKind == "calendar")
                LoadIndustrialCalendar();
            else if (_weatherTheme == "Art Deco")
                LoadArtDecoWeather();
            else if (IsNatureWeatherTheme)
                LoadWoodlandWeather();
            else
                LoadIndustrialWeather();
            Shown += delegate { SizeWorkspacePanels(); };
            Resize += delegate { SizeWorkspacePanels(); };
        }

        internal static DesignerForm OpenOptionalWidget(
            string assemblyPath, string typeName)
        {
            if (string.IsNullOrWhiteSpace(assemblyPath) ||
                !File.Exists(assemblyPath))
                throw new FileNotFoundException(
                    "The imported widget assembly could not be found.",
                    assemblyPath);
            Assembly assembly = Assembly.LoadFrom(
                Path.GetFullPath(assemblyPath));
            Type type = assembly.GetType(typeName, true, false);
            var provider = Activator.CreateInstance(type) as
                IWidgetDesignerProvider;
            if (provider == null)
                throw new InvalidDataException(
                    "This widget does not provide an EmilyDesk Designer layout.");
            return new DesignerForm(provider.DesignerKind,
                provider.DesignerTheme, provider,
                Path.GetDirectoryName(Path.GetFullPath(assemblyPath)));
        }

        private void LoadOptionalWidgetLayout()
        {
            _liveLayoutFileName = DesignerLayoutFiles.FileName(
                _optionalWidgetDesigner.DesignerTheme,
                _optionalWidgetDesigner.DesignerKind);
            _surface.Items.Clear();
            _surface.Items.Add("Main Widget");
            _surface.SelectedIndex = 0;
            _surface.Enabled = false;
            string existingPath = DesignerLayoutFiles.LivePath(
                _liveLayoutFileName);
            _layout = File.Exists(existingPath)
                ? DesignerLayoutStore.Load(existingPath)
                : ToDesignerLayout(
                    _optionalWidgetDesigner.CreateDesignerLayout());
            if (_layout == null)
                throw new InvalidDataException(
                    "The widget did not provide a valid Designer layout.");
            // Existing calculator layouts predate the editable LCD image.
            // Add the new layer once while preserving all saved edits.
            if (_optionalWidgetDesigner.DesignerKind == "calculator" &&
                _optionalWidgetDesigner.DesignerTheme == "Ember Glow" &&
                _layout.Elements != null &&
                !_layout.Elements.Any(e => e != null && e.Id == "lcd-window") &&
                (_layout.DeletedElementIds == null ||
                    !_layout.DeletedElementIds.Contains("lcd-window")))
            {
                DesignerLayout template = ToDesignerLayout(
                    _optionalWidgetDesigner.CreateDesignerLayout());
                DesignerElement lcd = template.Elements.FirstOrDefault(
                    e => e != null && e.Id == "lcd-window");
                if (lcd != null)
                {
                    int background = _layout.Elements.FindIndex(
                        e => e != null && e.Id == "main-background");
                    _layout.Elements.Insert(background + 1, lcd);
                }
            }
            if (_optionalWidgetDesigner.DesignerKind == "currency-converter" &&
                _optionalWidgetDesigner.DesignerTheme == "Ember Glow" &&
                _layout.Elements != null)
            {
                DesignerLayout template = ToDesignerLayout(
                    _optionalWidgetDesigner.CreateDesignerLayout());
                int background = _layout.Elements.FindIndex(
                    e => e != null && e.Id == "main-background");
                foreach (string id in new[] { "amount-lcd", "result-lcd" })
                {
                    if (_layout.Elements.Any(e => e != null && e.Id == id) ||
                        (_layout.DeletedElementIds != null &&
                         _layout.DeletedElementIds.Contains(id))) continue;
                    DesignerElement lcd = template.Elements.FirstOrDefault(
                        e => e != null && e.Id == id);
                    if (lcd != null)
                        _layout.Elements.Insert(++background, lcd);
                }
            }
            _layoutPath = existingPath;
            SetLayout(_layout);
            Text = "EmilyDesk Designer - " + _layout.Name;
        }

        private static DesignerLayout ToDesignerLayout(
            SavedDesignerLayout layout)
        {
            if (layout == null) return null;
            var serializer = new System.Web.Script.Serialization
                .JavaScriptSerializer();
            serializer.MaxJsonLength = 16 * 1024 * 1024;
            return serializer.Deserialize<DesignerLayout>(
                serializer.Serialize(layout));
        }

        private static DesignerLayer ToSavedLayer(
            DesignerElement element)
        {
            if (element == null) return null;
            return new DesignerLayer
            {
                Id = element.Id,
                Name = element.Name,
                Text = element.Text,
                Binding = element.Binding,
                Kind = (int)element.Kind,
                Surface = (int)element.Surface,
                X = element.X,
                Y = element.Y,
                Width = element.Width,
                Height = element.Height,
                Visible = element.Visible,
                MouseLocked = element.MouseLocked,
                Opacity = element.Opacity,
                Scale = element.Scale,
                FontName = element.FontName,
                FontFile = element.FontFile,
                FontSize = element.FontSize,
                Bold = element.Bold,
                Italic = element.Italic,
                ColorArgb = element.ColorArgb,
                Alignment = (int)element.Alignment,
                WordWrap = element.WordWrap,
                Trimming = (int)element.Trimming,
                ImagePath = element.ImagePath,
                ImageHorizontalPlacement =
                    (int)element.ImageHorizontalPlacement,
                ImageVerticalPlacement =
                    (int)element.ImageVerticalPlacement,
                ImageOffsetX = element.ImageOffsetX,
                ImageOffsetY = element.ImageOffsetY,
                PreviewRotation = element.PreviewRotation,
                HandPivotX = element.HandPivotX,
                HandPivotY = element.HandPivotY
            };
        }

        private void LoadThemeSnapshotLayout()
        {
            string themeFile = _weatherTheme.ToLowerInvariant()
                .Replace(" ", "-");
            _liveLayoutFileName = themeFile + "-" + _widgetKind +
                ".layout.json";
            _surface.Items.Clear();
            _surface.Items.Add("Main Widget");
            if (_widgetKind == "weather") _surface.Items.Add("Weather Details");
            _surface.SelectedIndex = 0;
            _surface.Enabled = _widgetKind == "weather";

            string legacyPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            _layoutPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            string existingPath = DesignerLayoutFiles.LivePath(_liveLayoutFileName);
            if (!File.Exists(existingPath)) existingPath = null;
            if (!string.IsNullOrEmpty(existingPath))
                _layout = DesignerLayoutStore.Load(existingPath);
            else
            {
                Size canvas = SnapshotCanvasSize();
                _layout = new DesignerLayout
                {
                    SchemaVersion = 7,
                    Name = _weatherTheme + " " +
                        char.ToUpperInvariant(_widgetKind[0]) +
                        _widgetKind.Substring(1),
                    CanvasWidth = canvas.Width,
                    CanvasHeight = canvas.Height,
                    BackgroundImage = string.Empty
                };
            }
            CompleteEditableThemeLayout();
            SetLayout(_layout);
            Text = "EmilyDesk Designer - " + _layout.Name;
        }

        private Size SnapshotCanvasSize()
        {
            if (_widgetKind == "clock")
                return _weatherTheme == "Art Deco"
                    ? new Size(360, 360) : new Size(300, 300);
            if (_widgetKind == "calendar")
                return _weatherTheme == "Art Deco"
                    ? new Size(522, 360) : new Size(420, 390);
            return new Size(360, 210);
        }

        private void RenderArtDecoBackground(Graphics graphics,
            Rectangle bounds)
        {
            if (_layout == null)
            {
                _weatherReference.RenderArtDecoDesignerReference(
                    graphics, bounds);
                return;
            }
            if (string.IsNullOrWhiteSpace(_layout.BackgroundImage)) return;
            string path = ResolveLayoutAsset(_layout.BackgroundImage);
            if (File.Exists(path))
            {
                using (Image image = Image.FromFile(path))
                    graphics.DrawImage(image, bounds);
                return;
            }
            _weatherReference.RenderArtDecoDesignerReference(
                graphics, bounds);
        }

        private string ResolvePreviewText(DesignerElement element)
        {
            if (element == null) return string.Empty;
            string binding = element.Binding ?? string.Empty;
            if (string.Equals(binding, "Weather: Location",
                StringComparison.OrdinalIgnoreCase)) return "Port Williams";
            if (string.Equals(binding, "Weather: Temperature",
                StringComparison.OrdinalIgnoreCase)) return "21\u00b0C";
            if (string.Equals(binding, "Weather: Feels Like",
                StringComparison.OrdinalIgnoreCase))
                return WoodlandMainMetricText(element, "Feels Like", "20\u00b0C");
            if (string.Equals(binding, "Weather: Condition",
                StringComparison.OrdinalIgnoreCase)) return "Partly Cloudy";
            if (string.Equals(binding, "Weather: Humidity",
                StringComparison.OrdinalIgnoreCase))
                return WoodlandMainMetricText(element, "Humidity", "68%");
            if (string.Equals(binding, "Weather: Wind",
                StringComparison.OrdinalIgnoreCase))
                return WoodlandMainMetricText(element, "Wind", "NW 12 km/h");
            if (string.Equals(binding, "Weather: Pressure",
                StringComparison.OrdinalIgnoreCase)) return "1013 hPa";
            if (string.Equals(binding, "Weather: Dew Point",
                StringComparison.OrdinalIgnoreCase)) return "12\u00b0C";
            if (string.Equals(binding, "Weather: Visibility",
                StringComparison.OrdinalIgnoreCase)) return "16 km";
            if (string.Equals(binding, "Weather: UV Index",
                StringComparison.OrdinalIgnoreCase)) return "3  Moderate";
            if (string.Equals(binding, "Weather: Sunrise and Sunset",
                StringComparison.OrdinalIgnoreCase)) return "6:15 AM / 8:32 PM";
            int forecastIndex = Math.Max(0, DesignerForecastIndex(element));
            if (string.Equals(binding, "Weather: Forecast Day",
                StringComparison.OrdinalIgnoreCase))
                return forecastIndex == 0 ? "Today" :
                    DateTime.Today.AddDays(forecastIndex).ToString("ddd");
            if (string.Equals(binding, "Weather: Forecast High and Low",
                StringComparison.OrdinalIgnoreCase))
                return (24 - forecastIndex) + "\u00b0C / " +
                    (15 - forecastIndex) + "\u00b0C";
            if (string.Equals(binding, "Weather: Forecast High",
                StringComparison.OrdinalIgnoreCase))
                return (24 - forecastIndex) + "\u00b0C";
            if (string.Equals(binding, "Weather: Forecast Low",
                StringComparison.OrdinalIgnoreCase))
                return (15 - forecastIndex) + "\u00b0C";
            if (string.Equals(binding, "Weather: Forecast Condition",
                StringComparison.OrdinalIgnoreCase)) return "Partly Cloudy";
            if (string.Equals(binding, "Weather: Updated Time",
                StringComparison.OrdinalIgnoreCase)) return "Updated 9:30 AM";
            return element.Text ?? string.Empty;
        }

        // Woodland's three main-panel metrics are a small two-line unit.  The
        // old one-line preview text was saved with ellipsis trimming and then
        // passed straight through to the running widget.
        private string WoodlandMainMetricText(DesignerElement element,
            string label, string value)
        {
            if (UsesThemeSnapshot || (IsNatureWeatherTheme && element != null &&
                element.Surface == DesignerSurface.WeatherDetails)) return value;
            if (IsNatureWeatherTheme && element != null &&
                element.Surface == DesignerSurface.Main &&
                (string.Equals(element.Id, "feels-like",
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(element.Id, "humidity",
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(element.Id, "wind",
                    StringComparison.OrdinalIgnoreCase)))
                return label + Environment.NewLine + value;
            return label + " " + value;
        }

        private string ResolveLayoutAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string themed = EmilyDeskThemeCatalog.ResolveThemeUri(path);
            if (!string.IsNullOrWhiteSpace(themed) &&
                !string.Equals(themed, path, StringComparison.Ordinal)) return themed;
            if (Path.IsPathRooted(path)) return path;
            if (!string.IsNullOrWhiteSpace(_optionalWidgetPackageRoot))
            {
                string packageAsset = Path.Combine(
                    _optionalWidgetPackageRoot,
                    path.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(packageAsset)) return packageAsset;
            }
            return Path.Combine(_projectRoot ?? string.Empty,
                path.Replace('/', Path.DirectorySeparatorChar));
        }

        private void BuildInterface()
        {
            var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 4, 6, 4) };
            var fileMenu = new ToolStripDropDownButton("File");
            fileMenu.DropDownItems.Add("Widget Templates...", null, delegate { ShowTemplatePicker(); });
            fileMenu.DropDownItems.Add("New", null, delegate { NewLayout(); });
            fileMenu.DropDownItems.Add("Open...", null, delegate { OpenLayout(); });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Save", null, delegate { SaveLayout(false); });
            fileMenu.DropDownItems.Add("Save As...", null, delegate { SaveLayout(true); });
            tools.Items.Add(fileMenu);
            AddProjectTools(tools);
            tools.Items.Add(new ToolStripSeparator());
            _undoButton.Click += delegate { Undo(); };
            _redoButton.Click += delegate { Redo(); };
            tools.Items.Add(_undoButton);
            tools.Items.Add(_redoButton);
            tools.Items.Add(new ToolStripSeparator());
            var widgetMenu = new ToolStripDropDownButton("Widget");
            widgetMenu.DropDownItems.Add("Choose Background PNG...", null,
                delegate { ChooseBackground(); });
            widgetMenu.DropDownItems.Add("Use Theme Background", null,
                delegate { UseThemeBackground(); });
            widgetMenu.DropDownItems.Add("Remove Background", null,
                delegate { RemoveBackground(); });
            widgetMenu.DropDownItems.Add(new ToolStripSeparator());
            widgetMenu.DropDownItems.Add("Set Package Preview Image...", null,
                delegate { SetPackageArtwork("preview.png"); });
            widgetMenu.DropDownItems.Add("Set Package Icon Image...", null,
                delegate { SetPackageArtwork("icon.png"); });
            widgetMenu.DropDownItems.Add("Restore Automatically Generated Package Images", null,
                delegate { _layout.PackagePreviewImage = null; _layout.PackageIconImage = null; SaveLayout(false); });
            tools.Items.Add(widgetMenu);
            var imageMenu = new ToolStripDropDownButton("Images");
            imageMenu.DropDownItems.Add("Add PNG...", null,
                delegate { AddImage(); });
            imageMenu.DropDownItems.Add("Replace Selected Image...", null,
                delegate { ReplaceSelectedImage(); });
            imageMenu.DropDownItems.Add("Restore Selected Theme Image", null,
                delegate { RestoreSelectedThemeImage(); });
            imageMenu.DropDownItems.Add("Open Selected Image Folder", null,
                delegate { OpenSelectedImageFolder(); });
            imageMenu.DropDownItems.Add(new ToolStripSeparator());
            imageMenu.DropDownItems.Add("Tip: drag a PNG onto the canvas").Enabled = false;
            tools.Items.Add(imageMenu);
            var fontMenu = new ToolStripDropDownButton("Fonts");
            fontMenu.DropDownItems.Add("Choose System Font...", null,
                delegate { ChooseSystemFont(); });
            fontMenu.DropDownItems.Add("Choose Font from Widget Folder...", null,
                delegate { ChooseWidgetFont(); });
            fontMenu.DropDownItems.Add("Open Widget Font Folder", null,
                delegate { OpenFolder(WidgetFontDirectory); });
            fontMenu.DropDownItems.Add(new ToolStripSeparator());
            fontMenu.DropDownItems.Add("Use System Font Only", null,
                delegate { ClearWidgetFontFile(); });
            tools.Items.Add(fontMenu);
            var weatherIconsMenu = new ToolStripDropDownButton("Weather Icons");
            weatherIconsMenu.Enabled = _widgetKind == "weather";
            weatherIconsMenu.DropDownItems.Add("Choose Installed Icon Pack...", null,
                delegate { ChooseWeatherIconPack(); });
            weatherIconsMenu.DropDownItems.Add("Import Icon Pack Folder...", null,
                delegate { ImportWeatherIconPack(); });
            weatherIconsMenu.DropDownItems.Add("Open Icon Pack Folder", null,
                delegate { OpenFolder(WeatherIconPacksDirectory); });
            weatherIconsMenu.DropDownItems.Add(new ToolStripSeparator());
            weatherIconsMenu.DropDownItems.Add("Restore Theme Icons", null,
                delegate { SetWeatherIconPack(string.Empty); });
            tools.Items.Add(weatherIconsMenu);
            var wallpaperMenu = new ToolStripDropDownButton("Wallpaper");
            var themeWallpapers = new ToolStripMenuItem("Choose Theme Wallpaper");
            foreach (string themeName in EmilyDeskThemeCatalog.BuiltInNames)
            {
                string selectedTheme = themeName;
                themeWallpapers.DropDownItems.Add(selectedTheme, null,
                    delegate { ApplyThemeWallpaper(selectedTheme); });
            }
            wallpaperMenu.DropDownItems.Add(themeWallpapers);
            wallpaperMenu.DropDownItems.Add("Choose Personal Image...", null,
                delegate { ChoosePersonalWallpaper(false); });
            wallpaperMenu.DropDownItems.Add("Import Wallpaper...", null,
                delegate { ChoosePersonalWallpaper(true); });
            wallpaperMenu.DropDownItems.Add("Open Wallpaper Folder", null,
                delegate { OpenFolder(ImportedWallpapersDirectory); });
            wallpaperMenu.DropDownItems.Add(new ToolStripSeparator());
            wallpaperMenu.DropDownItems.Add("Restore Previous Windows Wallpaper", null,
                delegate { RestorePreviousWallpaper(); });
            tools.Items.Add(wallpaperMenu);
            var layersMenu = new ToolStripDropDownButton("Layers");
            layersMenu.DropDownItems.Add("Add Text Layer", null,
                delegate { AddText(); });
            if (_widgetKind == "weather")
                layersMenu.DropDownItems.Add(
                    CreateWeatherFieldMenu("Add or Select Weather Field"));
            layersMenu.DropDownItems.Add("Add PNG...", null,
                delegate { AddImage(); });
            layersMenu.DropDownItems.Add("Add Divider Layer", null,
                delegate { AddDivider(); });
            layersMenu.DropDownItems.Add(new ToolStripSeparator());
            layersMenu.DropDownItems.Add("Duplicate", null,
                delegate { DuplicateSelected(); });
            layersMenu.DropDownItems.Add("Delete", null,
                delegate { DeleteSelected(); });
            layersMenu.DropDownItems.Add(new ToolStripSeparator());
            layersMenu.DropDownItems.Add("Move Up", null,
                delegate { MoveLayer(1); });
            layersMenu.DropDownItems.Add("Move Down", null,
                delegate { MoveLayer(-1); });
            layersMenu.DropDownItems.Add("Show / Hide", null,
                delegate { ToggleVisibility(); });
            layersMenu.DropDownItems.Add("Lock / Unlock Cursor Movement", null,
                delegate { ToggleMouseLock(); });
            tools.Items.Add(layersMenu);
            tools.Items.Add(new ToolStripSeparator());
            tools.Items.Add(new ToolStripLabel("  Edit:"));
            _surface.DropDownStyle = ComboBoxStyle.DropDownList;
            _surface.Items.Add(_widgetKind == "recyclebin" ? "Empty Bin" : "Main Widget");
            _surface.Items.Add(_widgetKind == "recyclebin" ? "Full Bin" : "Weather Details");
            _canvas.StateSurfaces = _widgetKind == "recyclebin";
            _surface.SelectedIndex = 0;
            _surface.SelectedIndexChanged += delegate
            {
                if (_layout == null) return;
                _canvas.ActiveSurface = _surface.SelectedIndex == 1
                    ? DesignerSurface.WeatherDetails : DesignerSurface.Main;
                RefreshLayers();
                _properties.SelectedObject = _widgetKind != "recyclebin" && _canvas.ActiveSurface ==
                    DesignerSurface.WeatherDetails
                    ? (object)_layout.WeatherDetailsPanel : null;
                UpdatePropertyInspector();
            };
            tools.Items.Add(_surface);
            tools.Items.Add(new ToolStripSeparator());
            _gridButton.CheckedChanged += delegate
            {
                if (_layout == null || _refreshing) return;
                CaptureUndo();
                _layout.GridEnabled = _gridButton.Checked;
                _canvas.Invalidate();
            };
            tools.Items.Add(_gridButton);
            tools.Items.Add(new ToolStripLabel("  Preview:"));
            foreach (string value in new[] { "50%", "75%", "100%", "125%", "150%", "175%", "200%" })
                _zoom.Items.Add(value);
            _zoom.DropDownStyle = ComboBoxStyle.DropDownList;
            _zoom.SelectedItem = "100%";
            _zoom.SelectedIndexChanged += delegate
            {
                int percent;
                if (int.TryParse(_zoom.Text.TrimEnd('%'), out percent))
                    _canvas.Zoom = percent / 100F;
            };
            tools.Items.Add(_zoom);

            var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 220, FixedPanel = FixedPanel.Panel1 };
            _workspaceSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel2
            };
            split.Panel2.Controls.Add(_workspaceSplit);

            var layerHeader = new Label { Text = "Layers", Dock = DockStyle.Top, Height = 34, Padding = new Padding(10, 10, 0, 0), Font = new Font(Font, FontStyle.Bold) };
            _layers.Dock = DockStyle.Fill;
            _layers.IntegralHeight = false;
            ConfigureFriendlyLayerList();
            _layers.SelectedIndexChanged += delegate
            {
                if (_refreshing) return;
                _canvas.SelectedElement = _layers.SelectedItem as DesignerElement;
            };
            _layers.KeyDown += LayersKeyDown;
            _layers.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right) return;
                int index = _layers.IndexFromPoint(e.Location);
                _layers.SelectedIndex = index >= 0 ? index : -1;
            };
            _layers.ContextMenuStrip = CreateObjectContextMenu();
            var layerButtons = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 68,
                Padding = new Padding(4),
                ColumnCount = 2,
                RowCount = 2
            };
            layerButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layerButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layerButtons.Controls.Add(NewLayerButton("Up", delegate { MoveLayer(1); }), 0, 0);
            layerButtons.Controls.Add(NewLayerButton("Down", delegate { MoveLayer(-1); }), 1, 0);
            layerButtons.Controls.Add(NewLayerButton("Duplicate", delegate { DuplicateSelected(); }), 0, 1);
            layerButtons.Controls.Add(NewLayerButton("Delete", delegate { DeleteSelected(); }), 1, 1);
            split.Panel1.Controls.Add(_layers);
            split.Panel1.Controls.Add(layerButtons);
            split.Panel1.Controls.Add(layerHeader);

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.FromArgb(42, 44, 48), Padding = new Padding(30) };
            _canvas.Location = new Point(30, 30);
            _canvas.AllowDrop = true;
            _canvas.DragEnter += CanvasDragEnter;
            _canvas.DragDrop += CanvasDragDrop;
            _canvas.ContextMenuStrip = CreateObjectContextMenu();
            _canvas.SelectionChanged += delegate { SyncSelection(); };
            _canvas.EditBeginning += delegate { CaptureUndo(); };
            _canvas.LayoutChanged += delegate
            {
                // Clock hands keep independent image positions and pivot
                // points so curved or asymmetrical PNGs can be aligned.
            };
            _canvas.EditCompleted += delegate
            {
                _properties.Refresh();
                RefreshLayers();
                UpdatePropertyInspector();
            };
            scroll.Controls.Add(_canvas);
            scroll.AllowDrop = true;
            scroll.DragEnter += CanvasDragEnter;
            scroll.DragDrop += CanvasDragDrop;
            _workspaceSplit.Panel1.Controls.Add(scroll);

            var propertyHeader = new Label { Text = "Edit Selected Layer", Dock = DockStyle.Top, Height = 34, Padding = new Padding(10, 10, 0, 0), Font = new Font(Font, FontStyle.Bold) };
            _properties.Dock = DockStyle.Fill;
            _properties.ToolbarVisible = false;
            _properties.HelpVisible = true;
            _properties.PropertySort = PropertySort.Categorized;
            _properties.SelectedGridItemChanged += delegate
            {
                GridItem item = _properties.SelectedGridItem;
                PropertyDescriptor descriptor = item == null ? null : item.PropertyDescriptor;
                if (IsNumericProperty(descriptor))
                    _selectedNumericPropertyName = descriptor.Name;
                UpdateNumericEditors();
            };
            _properties.PropertyValueChanged += delegate(object sender,
                PropertyValueChangedEventArgs eventArgs)
            {
                if (_refreshing) return;
                if (eventArgs.ChangedItem != null && eventArgs.ChangedItem.PropertyDescriptor != null &&
                    eventArgs.ChangedItem.PropertyDescriptor.Name == "Binding")
                    ApplyWeatherDetailsFieldStyle(_canvas.SelectedElement);
                if (_propertyBaseline != null)
                {
                    _undo.Push(_propertyBaseline);
                    _redo.Clear();
                    UpdateUndoButtons();
                }
                _canvas.Invalidate();
                if (_widgetKind == "clock" && eventArgs.ChangedItem != null && eventArgs.ChangedItem.PropertyDescriptor != null &&
                    eventArgs.ChangedItem.PropertyDescriptor.Name == "Binding")
                    NormalizeClockRoleFromProperties();
                if (_widgetKind == "recyclebin" && eventArgs.ChangedItem != null && eventArgs.ChangedItem.PropertyDescriptor != null &&
                    eventArgs.ChangedItem.PropertyDescriptor.Name == "Binding")
                    NormalizeClockRoleFromProperties();
                RefreshLayers();
                UpdatePropertyInspector();
                _propertyBaseline = _layout.Clone();
            };
            ConfigurePropertyInspector();
            // Retain the legacy numeric controls for the optional advanced
            // property view; the friendly inspector is the visible editor.
            ConfigureNumericEditor(_widthEditor, "Width");
            ConfigureNumericEditor(_heightEditor, "Height");
            ConfigureNumericEditor(_fontSizeEditor, "Font Size");
            _workspaceSplit.Panel2.Controls.Add(_propertyInspector);
            _workspaceSplit.Panel2.Controls.Add(propertyHeader);

            Controls.Add(split);
            Controls.Add(tools);
            tools.Dock = DockStyle.Top;
        }

        private ContextMenuStrip CreateObjectContextMenu()
        {
            var menu = new ContextMenuStrip();
            var addText = new ToolStripMenuItem("Add Text Layer Here");
            var addImage = new ToolStripMenuItem("Add PNG Here...");
            var addDivider = new ToolStripMenuItem("Add Divider Here");
            ToolStripMenuItem addWeather = _widgetKind == "weather"
                ? CreateWeatherFieldMenu("Add or Select Weather Field")
                : null;
            var bringFront = new ToolStripMenuItem("Bring to Front");
            var sendBack = new ToolStripMenuItem("Send to Back");
            var moveForward = new ToolStripMenuItem("Move Forward");
            var moveBackward = new ToolStripMenuItem("Move Backward");
            var duplicate = new ToolStripMenuItem("Clone Object");
            var delete = new ToolStripMenuItem("Delete");
            var replaceImage = new ToolStripMenuItem("Change PNG...");
            var restoreImage = new ToolStripMenuItem("Restore Theme Image");
            var visibility = new ToolStripMenuItem("Hide Object");
            var cursorLock = new ToolStripMenuItem("Lock Cursor Movement");

            addText.Click += delegate
                { AddTextAt(_contextInsertionPoint); };
            addImage.Click += delegate
                { AddImageAt(_contextInsertionPoint); };
            addDivider.Click += delegate
                { AddDividerAt(_contextInsertionPoint); };
            bringFront.Click += delegate { MoveSelectedToEdge(true); };
            sendBack.Click += delegate { MoveSelectedToEdge(false); };
            moveForward.Click += delegate { MoveLayer(1); };
            moveBackward.Click += delegate { MoveLayer(-1); };
            duplicate.Click += delegate { DuplicateSelected(); };
            delete.Click += delegate { DeleteSelected(); };
            replaceImage.Click += delegate { ReplaceSelectedImage(); };
            restoreImage.Click += delegate { RestoreSelectedThemeImage(); };
            visibility.Click += delegate { ToggleVisibility(); };
            cursorLock.Click += delegate { ToggleMouseLock(); };

            menu.Items.Add(addText);
            if (addWeather != null) menu.Items.Add(addWeather);
            menu.Items.Add(addImage);
            menu.Items.Add(addDivider);
            menu.Items.Add(CreateConnectAsMenu());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.AddRange(new ToolStripItem[]
            {
                bringFront, sendBack, moveForward, moveBackward,
                new ToolStripSeparator(), duplicate, delete,
                new ToolStripSeparator(), replaceImage, restoreImage,
                new ToolStripSeparator(), visibility, cursorLock
            });
            menu.Opening += delegate(object sender, CancelEventArgs e)
            {
                if (menu.SourceControl == _canvas)
                {
                    Point client = _canvas.PointToClient(Cursor.Position);
                    PointF design = _canvas.DesignPoint(client);
                    _contextInsertionPoint = new PointF(
                        Math.Max(0F, Math.Min(
                            _layout.CanvasWidth - 10F, design.X)),
                        Math.Max(0F, Math.Min(
                            _layout.CanvasHeight - 10F, design.Y)));
                }
                DesignerElement selected = _canvas.SelectedElement;
                bool available = selected != null;
                bool image = available &&
                    selected.Kind == DesignerElementKind.Image;
                bringFront.Enabled = available;
                sendBack.Enabled = available;
                moveForward.Enabled = available;
                moveBackward.Enabled = available;
                duplicate.Enabled = available;
                delete.Enabled = available;
                visibility.Enabled = available;
                cursorLock.Enabled = available;
                replaceImage.Visible = image;
                restoreImage.Visible = image;
                visibility.Text = available && !selected.Visible
                    ? "Show Object" : "Hide Object";
                cursorLock.Text = available && selected.MouseLocked
                    ? "Unlock Cursor Movement" : "Lock Cursor Movement";
                restoreImage.Enabled = image &&
                    !string.IsNullOrWhiteSpace(selected.Binding);
                e.Cancel = false;
            };
            return menu;
        }

        private ToolStripMenuItem CreateWeatherFieldMenu(string text)
        {
            var root = new ToolStripMenuItem(text);
            var current = new ToolStripMenuItem("Current Conditions");
            AddWeatherFieldMenuItem(current, "Location", "location",
                "Weather: Location", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(current, "Temperature", "temperature",
                "Weather: Temperature", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(current, "Condition", "condition",
                "Weather: Condition", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(current, "Feels Like", "feels-like",
                "Weather: Feels Like", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(current, "Humidity", "humidity",
                "Weather: Humidity", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(current, "Wind", "wind",
                "Weather: Wind", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(current, "Current Weather Icon",
                "current-icon", "Weather: Current Icon",
                DesignerElementKind.Image, -1);
            AddWeatherFieldMenuItem(current, "Updated Time", "footer",
                "Weather: Updated Time", DesignerElementKind.Text, -1);
            root.DropDownItems.Add(current);

            var details = new ToolStripMenuItem("Additional Details");
            AddWeatherFieldMenuItem(details, "Pressure", "custom-pressure",
                "Weather: Pressure", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(details, "Dew Point", "custom-dew-point",
                "Weather: Dew Point", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(details, "Visibility", "custom-visibility",
                "Weather: Visibility", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(details, "UV Index", "custom-uv-index",
                "Weather: UV Index", DesignerElementKind.Text, -1);
            AddWeatherFieldMenuItem(details, "Sunrise / Sunset",
                "custom-sunrise-sunset",
                "Weather: Sunrise and Sunset",
                DesignerElementKind.Text, -1);
            root.DropDownItems.Add(details);

            var forecast = new ToolStripMenuItem("Forecast Days");
            for (int index = 0;
                index < NativeWeatherWidget.MaximumForecastDayCount; index++)
            {
                int forecastIndex = index;
                var day = new ToolStripMenuItem(
                    "Forecast Day " + (index + 1));
                AddWeatherFieldMenuItem(day, "Day Name",
                    "forecast-day-" + index, "Weather: Forecast Day",
                    DesignerElementKind.Text, forecastIndex);
                AddWeatherFieldMenuItem(day, "Weather Icon",
                    "forecast-icon-" + index, "Weather: Forecast Icon",
                    DesignerElementKind.Image, forecastIndex);
                AddWeatherFieldMenuItem(day, "High / Low",
                    "forecast-range-" + index,
                    "Weather: Forecast High and Low",
                    DesignerElementKind.Text, forecastIndex);
                AddWeatherFieldMenuItem(day, "High Temperature",
                    "forecast-high-" + index,
                    "Weather: Forecast High",
                    DesignerElementKind.Text, forecastIndex);
                AddWeatherFieldMenuItem(day, "Low Temperature",
                    "forecast-low-" + index,
                    "Weather: Forecast Low",
                    DesignerElementKind.Text, forecastIndex);
                AddWeatherFieldMenuItem(day, "Condition",
                    "forecast-condition-" + index,
                    "Weather: Forecast Condition",
                    DesignerElementKind.Text, forecastIndex);
                forecast.DropDownItems.Add(day);
            }
            root.DropDownItems.Add(forecast);
            return root;
        }

        private void AddWeatherFieldMenuItem(ToolStripMenuItem parent,
            string text, string id, string binding,
            DesignerElementKind kind, int forecastIndex)
        {
            parent.DropDownItems.Add(text, null, delegate
            {
                AddOrSelectWeatherField(id, text, binding,
                    kind, forecastIndex);
            });
        }

        private void ConfigurePropertyInspector()
        {
            ConfigureFriendlyInspector();
        }

        private static void ConfigureInspectorButton(Button button,
            string text)
        {
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(2, 0, 2, 0);
        }

        private void UpdatePropertyInspector()
        {
            RefreshFriendlyInspector();
        }

        private void SizeWorkspacePanels()
        {
            if (_workspaceSplit == null || _workspaceSplit.Width < 800) return;
            int target = _workspaceSplit.Width - 370;
            int maximum = _workspaceSplit.Width - 340;
            _workspaceSplit.SplitterDistance = Math.Max(
                480, Math.Min(maximum, target));
        }

        private void FindProjectRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "EmilyDesk.sln")))
                directory = directory.Parent;
            _projectRoot = directory == null ? Environment.CurrentDirectory : directory.FullName;
            _canvas.ProjectRoot = _projectRoot;
        }

        private void LoadIndustrialWeather()
        {
            _liveLayoutFileName = IsImportedWeatherTheme
                ? DesignerLayoutFiles.FileName(_weatherTheme, "weather")
                : IsSteampunkWeatherTheme
                ? "steampunk-weather.layout.json"
                : "industrial-weather.layout.json";
            string legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            _layoutPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "DesignerLayouts",
                _liveLayoutFileName);
            string seedPath = Path.Combine(_projectRoot, "Widgets", "Weather",
                "Layouts", "industrial-weather.layout.json");
            string existingPath = DesignerLayoutFiles.LivePath(_liveLayoutFileName);
            if (!File.Exists(existingPath)) existingPath = null;
            _layout = !string.IsNullOrEmpty(existingPath)
                ? DesignerLayoutStore.Load(existingPath)
                : File.Exists(seedPath)
                    ? DesignerLayoutStore.Load(seedPath)
                    : CreateIndustrialLayout();
            if (_layout.WeatherDetailsPanel == null)
                _layout.WeatherDetailsPanel = new DesignerPanelSettings();
            if (!HasCompleteIndustrialLayerSet(_layout))
                _layout = CreateIndustrialLayout();
            else if (!HasElementOrDeletion(_layout, "details-title"))
                AddWeatherDetailsElements(_layout);
            MigrateIndustrialWeatherDetailsRowOrder(_layout);
            EnsureForecastConditionElements(_layout, false);
            if (IsSteampunkWeatherTheme && string.IsNullOrEmpty(existingPath))
            {
                _layout.Name = "Steampunk Weather";
                _layout.BackgroundImage =
                    "Assets/Themes/Steampunk/steampunk-weather-skin.png";
                _layout.WeatherIconPack = Path.Combine(_projectRoot, "Assets",
                    "WeatherIconPacks", "Steampunk");
            }
            SetLayout(_layout);
        }

        private void LoadWoodlandWeather()
        {
            // Do not reuse the old file name. Earlier releases stored an
            // Industrial-shaped layout there, which made a saved Woodland
            // document replace the finished Woodland geometry at runtime.
            _liveLayoutFileName = IsBotanicalWeatherTheme
                ? "botanical-weather.v2.layout.json"
                : "woodland-weather.v2.layout.json";
            string legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            _layoutPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "DesignerLayouts",
                _liveLayoutFileName);
            string existingPath = DesignerLayoutFiles.LivePath(_liveLayoutFileName);
            if (!File.Exists(existingPath)) existingPath = null;
            DesignerLayout baseline = IsBotanicalWeatherTheme
                ? CreateBotanicalWeatherLayout()
                : CreateWoodlandLayout();
            _layout = !string.IsNullOrEmpty(existingPath)
                ? DesignerLayoutStore.Load(existingPath)
                : baseline.Clone();
            int expectedLayoutVersion = IsBotanicalWeatherTheme ? 2 : 1;
            if (_layout == null) _layout = baseline.Clone();
            _layout.WoodlandLayoutVersion = expectedLayoutVersion;
            if (_layout.WeatherDetailsPanel == null)
                _layout.WeatherDetailsPanel = new DesignerPanelSettings();
            _layout.Name = IsBotanicalWeatherTheme
                ? "Botanical Nature Weather" : "Woodland Nature Weather";
            _woodlandBaseline = baseline.Clone();
            SetLayout(_layout);
        }

        // A metric is intentionally a single editable layer, but it always
        // draws as a label above its value.  Keep existing placement/style
        // edits while correcting the old single-line/ellipsis format.
        private static void NormalizeWoodlandMainMetrics(DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return;
            string[] metricIds = { "feels-like", "humidity", "wind" };
            foreach (string id in metricIds)
            {
                DesignerElement element = FindElement(layout, id);
                if (element == null || element.Surface != DesignerSurface.Main)
                    continue;
                element.WordWrap = true;
                element.Trimming = DesignerTextTrimming.None;
            }
        }

        // Row rules are decorative separators, not text underlines.  Keep
        // them below each complete label/value pair in both the saved Designer
        // layout and the live Woodland panel.
        private static void NormalizeWoodlandDetailsDividers(
            DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return;
            const float dataTop = 71F;
            const float rowHeight = 101.25F;
            for (int index = 0; index < 8; index++)
            {
                int row = index / 2;
                if (row >= 3) continue;
                DesignerElement divider = FindElement(layout,
                    "details-row-divider-" + index);
                if (divider == null || divider.Surface !=
                    DesignerSurface.WeatherDetails) continue;
                divider.Y = dataTop + (row + 1) * rowHeight - 2F;
            }
        }

        // Industrial originally used a three-row, six-slot panel.  That
        // omitted Feels Like, Humidity and Wind, and later row-order repairs
        // could only shuffle the remaining slots.  Upgrade it once to the
        // same complete four-row data set used by Art Deco:
        // Feels Like/Humidity, Wind/Pressure, Dew Point/Visibility,
        // UV Index/Sunrise / Sunset.
        private static void MigrateIndustrialWeatherDetailsRowOrder(
            DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null ||
                layout.IndustrialDetailsLayoutVersion >=
                    IndustrialDetailsRowOrderVersion) return;
            if (FindElement(layout, "details-title") == null) return;

            string[] labels = { "Feels Like", "Humidity", "Wind",
                "Pressure", "Dew Point", "Visibility", "UV Index",
                "Sunrise / Sunset" };
            string[] values = { "20\u00B0", "68%", "NW  12 km/h",
                "1013 hPa", "12\u00B0", "16 km", "3  Moderate",
                "6:15 AM / 8:32 PM" };
            string[] bindings = { "Weather: Feels Like", "Weather: Humidity",
                "Weather: Wind", "Weather: Pressure", "Weather: Dew Point",
                "Weather: Visibility", "Weather: UV Index",
                "Weather: Sunrise and Sunset" };

            // Industrial's frame is generous but the old layout ran its
            // left-hand details directly into the frame.  Keep both columns
            // on the same clean grid, with deliberately shorter separators.
            const float leftColumnX = 58F;
            const float rightColumnX = 404F;
            const float columnWidth = 280F;
            const float rowHeight = 82F;
            for (int index = 0; index < labels.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? leftColumnX : rightColumnX;
                float y = 112F + row * rowHeight;
                DesignerElement label = FindElement(layout,
                    "details-label-" + index);
                if (label == null)
                {
                    label = CreateDetailsText("details-label-" + index,
                        "Detail " + (index + 1) + " Label", labels[index],
                        "None", x, y, columnWidth, 18F, 13F, false);
                    layout.Elements.Add(label);
                }
                label.Text = labels[index];
                ConfigureIndustrialDetailsText(label,
                    "Detail " + (index + 1) + " Label", "None", x, y,
                    columnWidth, 18F, 13F, false);
                // The first row is intentionally rendered as two concise
                // combined values ("Feels Like 24°", "Humidity 58%").
                // Leaving these old captions visible produces the duplicate
                // text seen in migrated layouts.
                label.Visible = index >= 2;

                DesignerElement value = FindElement(layout,
                    "details-value-" + index);
                if (value == null)
                {
                    value = CreateDetailsText("details-value-" + index,
                        "Detail " + (index + 1) + " Value", values[index],
                        bindings[index], x, y + 19F, columnWidth, 23F, 16F,
                        true);
                    layout.Elements.Add(value);
                }
                value.Text = values[index];
                ConfigureIndustrialDetailsText(value,
                    "Detail " + (index + 1) + " Value", bindings[index],
                    x, y + 19F, columnWidth, 23F, 16F, true);
            }

            // The first three rows have a divider in each column.  Update
            // legacy divider positions and add the previously missing third
            // pair so text is never mistaken for a separator.
            for (int index = 0; index < 6; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? leftColumnX : rightColumnX;
                float y = 112F + row * rowHeight + 76F;
                DesignerElement divider = FindElement(layout,
                    "details-row-divider-" + index);
                if (divider == null)
                {
                    divider = CreateDetailsDivider("details-row-divider-" + index,
                        "Detail Row Divider " + (index + 1), x, y,
                        columnWidth, 2F);
                    layout.Elements.Add(divider);
                }
                divider.Surface = DesignerSurface.WeatherDetails;
                divider.Kind = DesignerElementKind.Divider;
                divider.X = x;
                divider.Y = y;
                divider.Width = columnWidth;
                divider.Height = 2F;
                divider.Visible = true;
            }
            // Earlier manual attempts could leave a default "New Text"
            // layer bound to the same sunrise/sunset field.  Hide only that
            // exact duplicate; deliberately named custom layers remain safe.
            foreach (DesignerElement element in layout.Elements)
            {
                if (element == null || element.Surface !=
                    DesignerSurface.WeatherDetails ||
                    element.Kind != DesignerElementKind.Text ||
                    !string.Equals(element.Name, "New Text",
                        StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(element.Binding,
                    "Weather: Sunrise and Sunset",
                    StringComparison.OrdinalIgnoreCase))
                    element.Visible = false;
            }
            layout.IndustrialDetailsLayoutVersion =
                IndustrialDetailsRowOrderVersion;
        }

        private static void ConfigureIndustrialDetailsText(
            DesignerElement element, string name, string binding, float x,
            float y, float width, float height, float fontSize, bool bold)
        {
            element.Name = name;
            element.Kind = DesignerElementKind.Text;
            element.Surface = DesignerSurface.WeatherDetails;
            element.Binding = binding;
            element.X = x;
            element.Y = y;
            element.Width = width;
            element.Height = height;
            element.FontName = "Segoe UI Semibold";
            element.FontSize = fontSize;
            element.Bold = bold;
            element.Italic = false;
            element.ColorArgb = Color.FromArgb(244, 228, 192).ToArgb();
            element.Alignment = DesignerTextAlignment.Left;
            element.WordWrap = false;
            element.Trimming = DesignerTextTrimming.None;
            element.Visible = true;
        }

        private static DesignerElement FindElement(DesignerLayout layout,
            string id)
        {
            return layout.Elements.Find(delegate(DesignerElement element)
            {
                return element != null && string.Equals(element.Id, id,
                    StringComparison.OrdinalIgnoreCase);
            });
        }

        private static bool IsElementDeleted(DesignerLayout layout,
            string id)
        {
            return layout != null && layout.DeletedElementIds != null &&
                layout.DeletedElementIds.Exists(delegate(string deletedId)
                {
                    return string.Equals(deletedId, id,
                        StringComparison.OrdinalIgnoreCase);
                });
        }

        private static bool HasElementOrDeletion(DesignerLayout layout,
            string id)
        {
            return IsElementDeleted(layout, id) ||
                FindElement(layout, id) != null;
        }

        private void LoadArtDecoWeather()
        {
            _liveLayoutFileName = "art-deco-weather.layout.json";
            _surface.Items.Clear();
            _surface.Items.Add("Main Widget");
            _surface.Items.Add("Weather Details");
            _surface.SelectedIndex = 0;
            _surface.Enabled = true;
            string legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            _layoutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            string existingPath = DesignerLayoutFiles.LivePath(_liveLayoutFileName);
            if (!File.Exists(existingPath)) existingPath = null;
            _layout = !string.IsNullOrEmpty(existingPath)
                ? DesignerLayoutStore.Load(existingPath)
                : CreateArtDecoWeatherLayout();
            if (!HasCompleteArtDecoWeatherLayerSet(_layout))
                _layout = CreateArtDecoWeatherLayout();
            if (_layout.WeatherDetailsPanel == null)
                _layout.WeatherDetailsPanel = new DesignerPanelSettings();
            if (_layout.SchemaVersion < ArtDecoDetailsSchemaVersion ||
                !HasCompleteArtDecoDetailsLayerSet(_layout))
            {
                _layout.Elements.RemoveAll(delegate(DesignerElement element)
                {
                    return element != null &&
                        (element.Surface == DesignerSurface.WeatherDetails ||
                         (!string.IsNullOrEmpty(element.Id) &&
                          element.Id.StartsWith("details-",
                              StringComparison.OrdinalIgnoreCase)));
                });
                AddArtDecoWeatherDetailsElements(_layout);
                _layout.SchemaVersion = Math.Max(_layout.SchemaVersion,
                    ArtDecoDetailsSchemaVersion);
            }
            EnsureForecastConditionElements(_layout, true);
            if (_layout.BackgroundImage == null)
                _layout.BackgroundImage =
                    "Assets/Themes/ArtDeco/art-deco-calendar-skin.png";
            _layout.CanvasWidth = 500;
            _layout.CanvasHeight = 346;
            SetLayout(_layout);
            Text = "EmilyDesk Designer â€” Art Deco Weather";
        }

        private static bool HasCompleteArtDecoWeatherLayerSet(
            DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return false;
            string[] required = { "current-icon", "location", "temperature",
                "condition", "forecast-day-0", "forecast-icon-0",
                "forecast-range-0", "footer", "main-divider", "panel-button" };
            foreach (string id in required)
                if (!HasElementOrDeletion(layout, id)) return false;
            return true;
        }

        private static bool HasCompleteArtDecoDetailsLayerSet(
            DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return false;
            string[] required = { "details-title", "details-header-divider",
                "details-label-0", "details-value-0", "details-label-1",
                "details-value-1", "details-label-2", "details-value-2",
                "details-label-3", "details-value-3", "details-label-4",
                "details-value-4", "details-label-5", "details-value-5",
                "details-label-6", "details-value-6", "details-label-7",
                "details-value-7", "details-row-divider-0",
                "details-row-divider-5" };
            foreach (string id in required)
            {
                if (IsElementDeleted(layout, id)) continue;
                DesignerElement element = layout.Elements.Find(
                    delegate(DesignerElement candidate)
                    {
                        return candidate != null &&
                            string.Equals(candidate.Id, id,
                                StringComparison.OrdinalIgnoreCase);
                    });
                if (element == null ||
                    element.Surface != DesignerSurface.WeatherDetails)
                    return false;
            }
            return true;
        }

        private static DesignerLayout CreateArtDecoWeatherLayout()
        {
            var layout = new DesignerLayout
            {
                SchemaVersion = ArtDecoDetailsSchemaVersion,
                Name = "Art Deco Weather",
                CanvasWidth = 500,
                CanvasHeight = 346,
                BackgroundImage =
                    "Assets/Themes/ArtDeco/art-deco-calendar-skin.png",
                GridSize = 5
            };
            DesignerElement divider = CreateDividerElement(
                "main-divider", "Forecast Top Divider", 59, 184, 382, 2);
            divider.ColorArgb = Color.FromArgb(42, 154, 150).ToArgb();
            layout.Elements.Add(divider);
            for (int index = 1; index < 5; index++)
            {
                DesignerElement column = CreateDividerElement(
                    "forecast-column-" + index,
                    "Forecast Column Divider " + index,
                    54F + index * 76F, 190, 2, 52);
                column.ColorArgb = Color.FromArgb(42, 154, 150).ToArgb();
                layout.Elements.Add(column);
            }
            layout.Elements.Add(CreateImageElement("current-icon",
                "Current Weather Icon", "Weather: Current Icon",
                300, 76, 92, 83));
            DesignerElement location = CreateTextElement("location", "City",
                "Port Williams", "Weather: Location", 60, 70, 225, 22, 13, true);
            location.FontName = "Georgia";
            location.Alignment = DesignerTextAlignment.Left;
            layout.Elements.Add(location);
            DesignerElement temperature = CreateTextElement("temperature",
                "Temperature", "21Â°", "Weather: Temperature",
                60, 92, 225, 55, 38, true);
            temperature.FontName = "Georgia";
            temperature.Alignment = DesignerTextAlignment.Left;
            layout.Elements.Add(temperature);
            DesignerElement condition = CreateTextElement("condition", "Condition",
                "Partly Cloudy", "Weather: Condition", 63, 148, 205, 18, 11, false);
            condition.FontName = "Georgia";
            condition.Alignment = DesignerTextAlignment.Left;
            layout.Elements.Add(condition);
            string[] days = { "Today", "Mon", "Tue", "Wed", "Thu" };
            for (int index = 0; index < 5; index++)
            {
                float x = 61F + index * 76F;
                layout.Elements.Add(CreateTextElement("forecast-day-" + index,
                    "Forecast " + (index + 1) + " Day", days[index],
                    "Weather: Forecast Day", x, 190, 58, 15, 8, true));
                layout.Elements.Add(CreateImageElement("forecast-icon-" + index,
                    "Forecast " + (index + 1) + " Icon", "Weather: Forecast Icon",
                    x + 15, 205, 28, 25));
                layout.Elements.Add(CreateTextElement("forecast-range-" + index,
                    "Forecast " + (index + 1) + " High / Low",
                    (24 - index) + "Â° / " + (15 - index) + "Â°",
                    "Weather: Forecast High and Low", x, 230, 58, 15, 9.25F, true));
                layout.Elements.Add(CreateTextElement("forecast-condition-" + index,
                    "Forecast " + (index + 1) + " Condition", "Cloudy",
                    "Weather: Forecast Condition", x, 246, 58, 13, 7.25F, false));
            }
            DesignerElement footer = CreateTextElement("footer", "Updated Time",
                "Updated 9:30 AM", "Weather: Updated Time",
                61, 263, 335, 16, 8.5F, false);
            footer.Alignment = DesignerTextAlignment.Left;
            layout.Elements.Add(footer);
            layout.Elements.Add(CreateImageElement("panel-button",
                "Weather Details Button", "Control: Weather Details Button",
                456, 287, 22, 22));
            AddArtDecoWeatherDetailsElements(layout);
            return layout;
        }

        private void LoadIndustrialClock()
        {
            _liveLayoutFileName = DesignerLayoutFiles.FileName(_weatherTheme, "clock");
            _surface.Items.Clear();
            _surface.Items.Add("Main Widget");
            _surface.SelectedIndex = 0;
            _surface.Enabled = false;

            string legacyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            _layoutPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            string seedPath = Path.Combine(_projectRoot, "Widgets", "Clock",
                "Layouts", _liveLayoutFileName);
            string existingPath = DesignerLayoutFiles.LivePath(_liveLayoutFileName);
            if (!File.Exists(existingPath)) existingPath = null;
            _layout = !string.IsNullOrEmpty(existingPath)
                ? DesignerLayoutStore.Load(existingPath)
                : File.Exists(seedPath)
                    ? DesignerLayoutStore.Load(seedPath)
                    : CreateIndustrialClockLayout();
            if (_layout == null || _layout.Elements == null)
                _layout = CreateIndustrialClockLayout();
            else if (_layout.SchemaVersion < 4)
                MigrateIndustrialClockLayout(_layout);
            if (IsSteampunkWeatherTheme && string.IsNullOrEmpty(existingPath))
            {
                _layout.Name = "Steampunk Clock";
                _layout.BackgroundImage =
                    "Assets/Themes/Steampunk/steampunk-clock-skin.png";
            }
            if (IsNatureWeatherTheme && string.IsNullOrEmpty(existingPath))
            {
                _layout.Name = _weatherTheme + " Clock";
                _layout.BackgroundImage = IsBotanicalWeatherTheme
                    ? "Assets/Themes/BotanicalNature/botanical-clock-skin.png"
                    : "Assets/Themes/WoodlandNature/woodland-clock-skin.png";
            }
            if ((IsNatureWeatherTheme || IsSteampunkWeatherTheme) && string.IsNullOrEmpty(existingPath))
            {
                DesignerElement date = FindElement(_layout, "clock-date");
                if (date != null) date.Visible = false;
            }
            SetLayout(_layout);
        }

        private void LoadIndustrialCalendar()
        {
            _liveLayoutFileName = DesignerLayoutFiles.FileName(_weatherTheme, "calendar");
            _surface.Items.Clear();
            _surface.Items.Add("Main Widget");
            _surface.SelectedIndex = 0;
            _surface.Enabled = false;

            string legacyPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            _layoutPath = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "DesignerLayouts", _liveLayoutFileName);
            string existingPath = DesignerLayoutFiles.LivePath(_liveLayoutFileName);
            if (!File.Exists(existingPath)) existingPath = null;
            DesignerLayout baseline = CreateThemedCalendarLayout();
            _layout = !string.IsNullOrEmpty(existingPath)
                ? DesignerLayoutStore.Load(existingPath)
                : baseline.Clone();
            if (_layout == null || _layout.Elements == null)
                _layout = baseline.Clone();
            else if (_layout.SchemaVersion < 5)
                MigrateIndustrialCalendarLayout(_layout);
            _layout.Name = _weatherTheme + " Calendar";
            SetLayout(_layout);
            Text = "EmilyDesk Designer - " + _layout.Name;
        }

        private DesignerLayout CreateThemedCalendarLayout()
        {
            DesignerLayout layout = IsBotanicalWeatherTheme
                ? CreateBotanicalCalendarLayout() : CreateIndustrialCalendarLayout();
            layout.Name = _weatherTheme + " Calendar";
            if (IsNatureWeatherTheme)
            {
                layout.BackgroundImage = IsBotanicalWeatherTheme
                    ? "Assets/Themes/BotanicalNature/botanical-calendar-skin.png"
                    : "Assets/Themes/WoodlandNature/woodland-calendar-skin.png";
                foreach (DesignerElement layer in layout.Elements)
                {
                    layer.FontName = "Georgia";
                    layer.FontSize = layer.Id == "month-title" ||
                        layer.Id == "previous-button" || layer.Id == "next-button"
                        ? 21F : layer.Id.StartsWith("weekday-") ? 10.5F : 11.5F;
                }
            }
            else if (IsSteampunkWeatherTheme)
                layout.BackgroundImage = "Assets/Themes/Steampunk/steampunk-calendar-skin.png";
            return layout;
        }

        private static bool HasCompleteIndustrialCalendarLayerSet(
            DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return false;
            string[] required = { "previous-button", "month-title",
                "next-button", "today-button", "footer", "weekday-0",
                "weekday-6", "date-0", "date-41" };
            foreach (string id in required)
                if (!HasElementOrDeletion(layout, id)) return false;
            return true;
        }

        private static DesignerLayout CreateIndustrialCalendarLayout()
        {
            var layout = new DesignerLayout
            {
                SchemaVersion = 5,
                Name = "Industrial Calendar",
                CanvasWidth = 540,
                CanvasHeight = 360,
                BackgroundImage =
                    "Assets/Themes/Industrial/industrial-calendar-skin.png",
                GridSize = 5
            };
            Color primary = Color.FromArgb(244, 228, 192);
            Color secondary = Color.FromArgb(157, 161, 160);
            Color footerText = Color.FromArgb(192, 192, 192);
            Color weekend = Color.FromArgb(224, 157, 39);

            DesignerElement previous = CreateTextElement(
                "previous-button", "Previous Month", "\u2039",
                "Calendar: Previous Month", 52, 70, 38, 30, 18, true);
            previous.ColorArgb = primary.ToArgb();
            layout.Elements.Add(previous);

            DesignerElement month = CreateTextElement(
                "month-title", "Month and Year", "August 2026",
                "Calendar: Month and Year", 135, 61, 270, 31, 18, true);
            month.ColorArgb = primary.ToArgb();
            layout.Elements.Add(month);

            DesignerElement next = CreateTextElement(
                "next-button", "Next Month", "\u203A",
                "Calendar: Next Month", 450, 70, 38, 30, 18, true);
            next.ColorArgb = primary.ToArgb();
            layout.Elements.Add(next);

            DesignerElement today = CreateTextElement(
                "today-button", "Today", "Today", "Calendar: Today",
                410, 300, 78, 18, 10, false);
            today.ColorArgb = footerText.ToArgb();
            layout.Elements.Add(today);

            DesignerElement footer = CreateTextElement(
                "footer", "Full Date", "Saturday, August 15, 2026",
                "Calendar: Full Date", 50, 300, 275, 18, 10, false);
            footer.ColorArgb = footerText.ToArgb();
            layout.Elements.Add(footer);

            string[] weekdays = { "Sun", "Mon", "Tue", "Wed", "Thu",
                "Fri", "Sat" };
            const float gridLeft = 50F;
            const float cellWidth = 440F / 7F;
            const float dateHeight = 26F;
            for (int column = 0; column < 7; column++)
            {
                DesignerElement weekday = CreateTextElement(
                    "weekday-" + column, "Weekday " + (column + 1),
                    weekdays[column], "Calendar: Weekday",
                    gridLeft + column * cellWidth, 102, cellWidth, 25,
                    9, true);
                weekday.ColorArgb = (column == 0 || column == 6
                    ? weekend : secondary).ToArgb();
                layout.Elements.Add(weekday);
            }
            for (int index = 0; index < 42; index++)
            {
                int row = index / 7;
                int column = index % 7;
                DesignerElement date = CreateTextElement(
                    "date-" + index, "Date " + (index + 1),
                    (index + 1).ToString(), "Calendar: Date",
                    gridLeft + column * cellWidth,
                    127F + row * dateHeight, cellWidth, dateHeight,
                    10, false);
                date.ColorArgb = primary.ToArgb();
                layout.Elements.Add(date);
            }
            return layout;
        }

        // Botanical panels use a light parchment background.  Woodland's
        // ivory Designer text is therefore unreadable there, even though the
        // live renderer correctly uses Botanical's dark ink palette.
        private static DesignerLayout CreateBotanicalCalendarLayout()
        {
            DesignerLayout layout = CreateIndustrialCalendarLayout();
            layout.SchemaVersion = 6;
            layout.Name = "Botanical Nature Calendar";
            layout.BackgroundImage =
                "Assets/Themes/BotanicalNature/botanical-calendar-skin.png";
            ApplyBotanicalDesignerTextPalette(layout, true);
            return layout;
        }

        private static void MigrateIndustrialCalendarLayout(
            DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return;
            MigrateIndustrialCalendarElement(layout, "month-title",
                135F, 59F, 270F, 31F,
                135F, 61F, 270F, 31F);
            MigrateIndustrialCalendarElement(layout, "today-button",
                410F, 291F, 78F, 23F,
                410F, 281F, 78F, 19F);
            MigrateIndustrialCalendarElement(layout, "previous-button",
                52F, 61F, 38F, 30F,
                52F, 70F, 38F, 30F);
            MigrateIndustrialCalendarElement(layout, "next-button",
                450F, 61F, 38F, 30F,
                450F, 70F, 38F, 30F);
            MigrateIndustrialCalendarElement(layout, "today-button",
                410F, 281F, 78F, 19F,
                410F, 300F, 78F, 18F);
            MigrateIndustrialCalendarElement(layout, "footer",
                70F, 292F, 325F, 20F,
                70F, 282F, 325F, 17F);
            MigrateIndustrialCalendarElement(layout, "footer",
                70F, 282F, 325F, 17F,
                65F, 300F, 275F, 18F);
            MigrateIndustrialCalendarColour(layout, "today-button",
                410F, 300F, 78F, 18F,
                Color.FromArgb(244, 228, 192).ToArgb(),
                Color.FromArgb(192, 192, 192).ToArgb());
            MigrateIndustrialCalendarElement(layout, "footer",
                65F, 300F, 275F, 18F,
                50F, 300F, 275F, 18F);
            MigrateIndustrialCalendarColour(layout, "footer",
                50F, 300F, 275F, 18F,
                Color.FromArgb(157, 161, 160).ToArgb(),
                Color.FromArgb(192, 192, 192).ToArgb());
            layout.SchemaVersion = Math.Max(layout.SchemaVersion, 5);
        }

        private static void MigrateIndustrialCalendarColour(
            DesignerLayout layout, string id,
            float x, float y, float width, float height,
            int oldArgb, int newArgb)
        {
            DesignerElement element = layout.Elements.Find(
                delegate(DesignerElement candidate)
                {
                    return candidate != null && string.Equals(
                        candidate.Id, id, StringComparison.OrdinalIgnoreCase);
                });
            if (element == null || element.ColorArgb != oldArgb ||
                Math.Abs(element.X - x) > .01F ||
                Math.Abs(element.Y - y) > .01F ||
                Math.Abs(element.Width - width) > .01F ||
                Math.Abs(element.Height - height) > .01F) return;
            element.ColorArgb = newArgb;
        }

        private static void MigrateIndustrialCalendarElement(
            DesignerLayout layout, string id,
            float oldX, float oldY, float oldWidth, float oldHeight,
            float newX, float newY, float newWidth, float newHeight)
        {
            DesignerElement element = layout.Elements.Find(
                delegate(DesignerElement candidate)
                {
                    return candidate != null && string.Equals(
                        candidate.Id, id, StringComparison.OrdinalIgnoreCase);
                });
            if (element == null ||
                Math.Abs(element.X - oldX) > .01F ||
                Math.Abs(element.Y - oldY) > .01F ||
                Math.Abs(element.Width - oldWidth) > .01F ||
                Math.Abs(element.Height - oldHeight) > .01F) return;
            element.X = newX;
            element.Y = newY;
            element.Width = newWidth;
            element.Height = newHeight;
        }

        private static void MigrateIndustrialClockLayout(
            DesignerLayout layout)
        {
            DesignerElement centre = layout.Elements.Find(
                delegate(DesignerElement candidate)
                {
                    return string.Equals(candidate.Id, "clock-centre-pivot",
                        StringComparison.OrdinalIgnoreCase);
                });
            if (centre == null)
            {
                centre = CreateImageElement("clock-centre-pivot",
                    "Centre Pivot", "Clock: Centre Pivot",
                    170F, 166.5F, 20F, 20F);
                layout.Elements.Add(centre);
            }
            string[] ids = { "clock-hour-hand", "clock-minute-hand",
                "clock-second-hand" };
            float[] rotations = { 302.4F, 61.2F, 129.6F };
            for (int index = 0; index < ids.Length; index++)
            {
                DesignerElement element = layout.Elements.Find(
                    delegate(DesignerElement candidate)
                    {
                        return string.Equals(candidate.Id, ids[index],
                            StringComparison.OrdinalIgnoreCase);
                    });
                if (element == null) continue;
                element.PivotX = centre.X + centre.Width * centre.Scale / 2F;
                element.PivotY = centre.Y + centre.Height * centre.Scale / 2F;
                element.PreviewRotation = rotations[index];
            }
            layout.SchemaVersion = 4;
        }

        private static bool HasCompleteIndustrialClockLayerSet(
            DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return false;
            string[] required = { "clock-hour-hand", "clock-minute-hand",
                "clock-second-hand", "clock-date" };
            foreach (string id in required)
                if (!HasElementOrDeletion(layout, id)) return false;
            return true;
        }

        private static DesignerLayout CreateIndustrialClockLayout()
        {
            var layout = new DesignerLayout
            {
                SchemaVersion = 4,
                Name = "Industrial Clock",
                CanvasWidth = 360,
                CanvasHeight = 360,
                BackgroundImage =
                    "Assets/Themes/Industrial/industrial-clock-skin.png",
                GridSize = 5
            };
            DesignerElement hour = CreateImageElement("clock-hour-hand",
                "Hour Hand", "Clock: Hour Hand", 174.5F, 103.5F, 11F, 86F);
            hour.PreviewRotation = 302.4F;
            layout.Elements.Add(hour);
            DesignerElement minute = CreateImageElement("clock-minute-hand",
                "Minute Hand", "Clock: Minute Hand", 176F, 68.5F, 8F, 121F);
            minute.PreviewRotation = 61.2F;
            layout.Elements.Add(minute);
            DesignerElement second = CreateImageElement("clock-second-hand",
                "Second Hand", "Clock: Second Hand", 179F, 60.5F, 2F, 133F);
            second.PreviewRotation = 129.6F;
            layout.Elements.Add(second);
            DesignerElement date = CreateTextElement("clock-date", "Date",
                "Aug 9", "Clock: Date", 133F, 232F, 94F, 24F, 9F, false);
            layout.Elements.Add(date);
            layout.Elements.Add(CreateImageElement("clock-centre-pivot",
                "Centre Pivot", "Clock: Centre Pivot",
                170F, 166.5F, 20F, 20F));
            return layout;
        }

        private static bool HasCompleteIndustrialLayerSet(DesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return false;
            string[] required =
            {
                "current-icon", "location", "temperature", "condition",
                "feels-like", "humidity", "wind", "forecast-day-0",
                "forecast-icon-0", "forecast-range-0", "forecast-day-3",
                "forecast-icon-3", "forecast-range-3", "footer",
                "main-divider", "forecast-divider", "forecast-column-1",
                "forecast-column-3", "panel-button"
            };
            foreach (string id in required)
                if (!HasElementOrDeletion(layout, id)) return false;
            return true;
        }

        private static void EnsureForecastConditionElements(
            DesignerLayout layout, bool artDeco)
        {
            if (layout == null || layout.Elements == null) return;
            bool added = false;
            int count = artDeco ? 5 :
                NativeWeatherWidget.DefaultForecastDayCount;
            for (int index = 0; index < count; index++)
            {
                string id = "forecast-condition-" + index;
                if (IsElementDeleted(layout, id)) continue;
                if (layout.Elements.Exists(delegate(DesignerElement element)
                {
                    return string.Equals(element.Id, id,
                        StringComparison.OrdinalIgnoreCase);
                })) continue;
                float x = artDeco ? 61F + index * 76F
                    : 66F + index * 154.25F;
                layout.Elements.Add(CreateTextElement(id,
                    "Forecast " + (index + 1) + " Condition", "Cloudy",
                    "Weather: Forecast Condition", x,
                    artDeco ? 246F : 389F,
                    artDeco ? 58F : 154.25F,
                    artDeco ? 13F : 15F,
                    artDeco ? 7.25F : 10F, false));
                added = true;
            }
            if (!added) return;
            DesignerElement footer = layout.Elements.Find(
                delegate(DesignerElement element)
                {
                    return string.Equals(element.Id, "footer",
                        StringComparison.OrdinalIgnoreCase);
                });
            if (footer != null &&
                Math.Abs(footer.Y - (artDeco ? 247F : 389F)) < 0.1F)
                footer.Y = artDeco ? 263F : 407F;
        }

        private static DesignerLayout CreateIndustrialLayout()
        {
            var layout = new DesignerLayout
            {
                Name = "Industrial Weather",
                CanvasWidth = 750,
                CanvasHeight = 500,
                BackgroundImage = "Assets/Themes/Industrial/industrial-weather-skin.png",
                GridSize = 10
            };
            layout.Elements.Add(CreateDividerElement(
                "main-divider", "Main Vertical Divider", 325, 88, 2, 217));
            layout.Elements.Add(CreateDividerElement(
                "forecast-divider", "Forecast Top Divider", 66, 322, 617, 2));
            for (int divider = 1;
                divider < NativeWeatherWidget.DefaultForecastDayCount;
                divider++)
                layout.Elements.Add(CreateDividerElement(
                    "forecast-column-" + divider,
                    "Forecast Column Divider " + divider,
                    66F + divider * 154.25F, 331, 2, 54));
            layout.Elements.Add(CreateImageElement("current-icon", "Current Weather Icon",
                "Weather: Current Icon", 76, 93, 221, 149));
            layout.Elements.Add(CreateTextElement("location", "City", "Port Williams",
                "Weather: Location", 349, 89, 334, 30, 21, false));
            layout.Elements.Add(CreateTextElement("temperature", "Temperature", "21Â°",
                "Weather: Temperature", 349, 117, 150, 68, 50, true));
            layout.Elements.Add(CreateTextElement("condition", "Condition", "Partly Cloudy",
                "Weather: Condition", 349, 185, 334, 34, 16, false));
            layout.Elements.Add(CreateTextElement("feels-like", "Feels Like", "Feels Like 20Â°",
                "Weather: Feels Like", 349, 232, 104, 38, 13, false));
            layout.Elements.Add(CreateTextElement("humidity", "Humidity", "Humidity 68%",
                "Weather: Humidity", 459, 232, 104, 38, 13, false));
            layout.Elements.Add(CreateTextElement("wind", "Wind", "Wind 12 km/h",
                "Weather: Wind", 569, 232, 104, 38, 13, false));
            string[] days = { "Today", "Mon", "Tue", "Wed" };
            for (int index = 0;
                index < NativeWeatherWidget.DefaultForecastDayCount;
                index++)
            {
                float x = 66F + index * 154.25F;
                layout.Elements.Add(CreateTextElement("forecast-day-" + index,
                    "Forecast " + (index + 1) + " Day", days[index],
                    "Weather: Forecast Day", x, 325, 154.25F, 17, 13.5F, true));
                layout.Elements.Add(CreateImageElement("forecast-icon-" + index,
                    "Forecast " + (index + 1) + " Icon", "Weather: Forecast Icon",
                    x + 61.125F, 343, 32, 28));
                layout.Elements.Add(CreateTextElement("forecast-range-" + index,
                    "Forecast " + (index + 1) + " High / Low", (24 - index) + "Â° / " + (15 - index) + "Â°",
                    "Weather: Forecast High and Low", x, 372, 154.25F, 16, 13.5F, true));
                layout.Elements.Add(CreateTextElement("forecast-condition-" + index,
                    "Forecast " + (index + 1) + " Condition", "Cloudy",
                    "Weather: Forecast Condition", x, 389, 154.25F, 15, 10F, false));
            }
            layout.Elements.Add(CreateTextElement("footer", "Updated Time", "Updated 9:30 AM",
                "Weather: Updated Time", 107, 407, 541, 31, 11.5F, false));
            layout.Elements.Add(CreateImageElement("panel-button", "Weather Details Button",
                "Control: Weather Details Button", 653, 396, 28, 28));
            AddWeatherDetailsElements(layout);
            return layout;
        }

        private static DesignerLayout CreateWoodlandLayout()
        {
            var layout = new DesignerLayout
            {
                Name = "Woodland Nature Weather",
                CanvasWidth = 750,
                CanvasHeight = 500,
                WoodlandLayoutVersion = 1,
                BackgroundImage =
                    "Assets/Themes/WoodlandNature/woodland-weather-skin.png"
            };
            // These coordinates mirror the approved Woodland renderer. They
            // are deliberately not based on Industrial's older canvas.
            const float left = 86.5F;
            const float top = 82F;
            const float right = 661F;
            const float bottom = 425F;
            const float dividerX = 327.8F;
            const float infoLeft = 351.8F;
            const float infoWidth = 309.2F;
            const float forecastTop = 323.33F;
            const float forecastBottom = 405F;
            const float columnWidth = 143.625F;

            layout.Elements.Add(CreateWoodlandDivider("main-divider",
                "Main Vertical Divider", dividerX, top + 10F, 2F,
                forecastTop - 18F - (top + 10F)));
            layout.Elements.Add(CreateWoodlandImage("current-icon",
                "Current Weather Icon", "Weather: Current Icon",
                96.5F, 96.6F, 203.3F, 148.8F));
            layout.Elements.Add(CreateWoodlandText("location", "City",
                "Port Williams", "Weather: Location", infoLeft,
                top + 10.67F, infoWidth - 132F, 30F, 21F, false,
                DesignerTextAlignment.Left));
            layout.Elements.Add(CreateWoodlandText("temperature",
                "Temperature", "21°", "Weather: Temperature", infoLeft,
                top + 39F, infoWidth, 62F, 50F, true,
                DesignerTextAlignment.Left));
            layout.Elements.Add(CreateWoodlandText("condition", "Condition",
                "Partly Cloudy", "Weather: Condition", infoLeft, 190F,
                infoWidth, 24F, 16F, false, DesignerTextAlignment.Left));
            layout.Elements.Add(CreateWoodlandText("feels-like", "Feels Like",
                "Feels Like 20°", "Weather: Feels Like", infoLeft, 255F,
                96F, 42F, 13F, false, DesignerTextAlignment.Center));
            layout.Elements.Add(CreateWoodlandText("humidity", "Humidity",
                "Humidity 68%", "Weather: Humidity", infoLeft + 101F, 255F,
                96F, 42F, 13F, false, DesignerTextAlignment.Center));
            layout.Elements.Add(CreateWoodlandText("wind", "Wind",
                "Wind 12 km/h", "Weather: Wind", infoLeft + 202F, 255F,
                96F, 42F, 13F, false, DesignerTextAlignment.Center));
            NormalizeWoodlandMainMetrics(layout);
            layout.Elements.Add(CreateWoodlandDivider("forecast-divider",
                "Forecast Top Divider", left, forecastTop, right - left, 2F));

            for (int index = 0;
                index < NativeWeatherWidget.DefaultForecastDayCount;
                index++)
            {
                float x = left + index * columnWidth;
                if (index > 0)
                    layout.Elements.Add(CreateWoodlandDivider(
                        "forecast-column-" + index,
                        "Forecast Column Divider", x, forecastTop + 8F,
                        2F, forecastBottom - forecastTop - 12F));
                layout.Elements.Add(CreateWoodlandText("forecast-day-" + index,
                    "Forecast " + (index + 1) + " Day", index == 0 ? "Today" : "Day",
                    "Weather: Forecast Day", x, forecastTop + 2F,
                    columnWidth, 17F, 13.5F, true,
                    DesignerTextAlignment.Center));
                layout.Elements.Add(CreateWoodlandImage("forecast-icon-" + index,
                    "Forecast " + (index + 1) + " Icon",
                    "Weather: Forecast Icon", x + (columnWidth - 44F) / 2F,
                    forecastTop + 20F, 44F, 40F));
                layout.Elements.Add(CreateWoodlandText("forecast-range-" + index,
                    "Forecast " + (index + 1) + " High / Low", "24° / 15°",
                    "Weather: Forecast High and Low", x, forecastTop + 62F,
                    columnWidth, 18F, 13.5F, true,
                    DesignerTextAlignment.Center));
            }
            layout.Elements.Add(CreateWoodlandText("footer", "Updated Time",
                "Updated 9:30 AM", "Weather: Updated Time", right - 172F,
                top + 12F, 166F, 18F, 11.5F, false,
                DesignerTextAlignment.Right));
            layout.Elements.Add(CreateWoodlandImage("panel-button",
                "Weather Details Button", "Control: Weather Details Button",
                right - 60F, bottom - 50F, 28F, 28F));

            // The details surface mirrors the final, deliberately uncluttered
            // panel: title/header plus text and icons, with no row dividers.
            AddWoodlandDetailsElements(layout);
            return layout;
        }

        private static DesignerLayout CreateBotanicalWeatherLayout()
        {
            DesignerLayout layout = CreateWoodlandLayout();
            layout.WoodlandLayoutVersion = 2;
            layout.Name = "Botanical Nature Weather";
            layout.BackgroundImage =
                "Assets/Themes/BotanicalNature/botanical-weather-skin.png";
            ApplyBotanicalDesignerTextPalette(layout, false);
            return layout;
        }

        private static void ApplyBotanicalDesignerTextPalette(
            DesignerLayout layout, bool calendar)
        {
            if (layout == null || layout.Elements == null) return;

            Color ink = Color.FromArgb(37, 63, 51);
            Color sage = Color.FromArgb(99, 117, 91);
            Color rose = Color.FromArgb(210, 126, 139);
            foreach (DesignerElement element in layout.Elements)
            {
                if (element == null || element.Kind != DesignerElementKind.Text)
                    continue;

                // Keep the weekend distinction on the calendar, while every
                // other text layer receives the readable Botanical ink tone.
                if (calendar && (string.Equals(element.Id, "weekday-0",
                    StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(element.Id, "weekday-6",
                    StringComparison.OrdinalIgnoreCase)))
                {
                    element.ColorArgb = rose.ToArgb();
                }
                else if (calendar && (string.Equals(element.Id, "footer",
                    StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(element.Id, "today-button",
                    StringComparison.OrdinalIgnoreCase)))
                {
                    element.ColorArgb = sage.ToArgb();
                }
                else
                {
                    element.ColorArgb = ink.ToArgb();
                }
            }
        }

        private static DesignerElement CreateWoodlandText(string id,
            string name, string text, string binding, float x, float y,
            float width, float height, float fontSize, bool bold,
            DesignerTextAlignment alignment)
        {
            DesignerElement element = CreateTextElement(id, name, text,
                binding, x, y, width, height, fontSize, bold);
            element.Alignment = alignment;
            element.FontName = "Segoe UI Semibold";
            element.ColorArgb = Color.FromArgb(244, 228, 192).ToArgb();
            element.WordWrap = false;
            element.Trimming = DesignerTextTrimming.EllipsisCharacter;
            return element;
        }

        private static DesignerElement CreateWoodlandImage(string id,
            string name, string binding, float x, float y, float width,
            float height)
        {
            return CreateImageElement(id, name, binding, x, y, width, height);
        }

        private static DesignerElement CreateWoodlandDivider(string id,
            string name, float x, float y, float width, float height)
        {
            DesignerElement element = CreateDividerElement(id, name, x, y,
                width, height);
            element.ColorArgb = Color.FromArgb(224, 157, 39).ToArgb();
            return element;
        }

        private static void AddWoodlandDetailsElements(DesignerLayout layout)
        {
            DesignerElement title = CreateWoodlandText("details-title",
                "Panel Title", "Weather Details", "None", 58F, 25F,
                634F, 28F, 24F, true, DesignerTextAlignment.Left);
            title.Surface = DesignerSurface.WeatherDetails;
            layout.Elements.Add(title);
            DesignerElement header = CreateWoodlandDivider(
                "details-header-divider", "Header Divider", 58F, 62F,
                634F, 2F);
            header.Surface = DesignerSurface.WeatherDetails;
            layout.Elements.Add(header);
            DesignerElement centre = CreateWoodlandDivider(
                "details-centre-divider", "Centre Divider", 375F, 63F,
                1F, 409F);
            centre.Surface = DesignerSurface.WeatherDetails;
            centre.ColorArgb = Color.FromArgb(146, 184, 139, 83).ToArgb();
            layout.Elements.Add(centre);
            string[] labels = { "Feels Like", "Humidity", "Wind", "Pressure",
                "Dew Point", "Visibility", "UV Index", "Sunrise / Sunset" };
            string[] values = { "20°", "68%", "NW  12 km/h", "1013 hPa",
                "12°", "16 km", "3  Moderate", "6:15 AM / 8:32 PM" };
            string[] bindings = { "Weather: Feels Like", "Weather: Humidity",
                "Weather: Wind", "Weather: Pressure", "Weather: Dew Point",
                "Weather: Visibility", "Weather: UV Index",
                "Weather: Sunrise and Sunset" };
            const float dataTop = 71F;
            const float rowHeight = 101.25F;
            for (int index = 0; index < labels.Length; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float baseX = column == 0 ? 58F : 388F;
                float y = dataTop + row * rowHeight;
                DesignerElement icon = CreateWoodlandImage("details-icon-" + index,
                    "Detail " + (index + 1) + " Icon", "Weather: Detail Icon",
                    baseX + 3F, y + 4F, 26F, 26F);
                icon.Surface = DesignerSurface.WeatherDetails;
                layout.Elements.Add(icon);
                DesignerElement label = CreateWoodlandText("details-label-" + index,
                    "Detail " + (index + 1) + " Label", labels[index], "None",
                    baseX + 38F, y, 266F, 20F, 16.7F, false,
                    DesignerTextAlignment.Left);
                label.Surface = DesignerSurface.WeatherDetails;
                label.ColorArgb = Color.FromArgb(166, 170, 169).ToArgb();
                layout.Elements.Add(label);
                DesignerElement value = CreateWoodlandText("details-value-" + index,
                    "Detail " + (index + 1) + " Value", values[index],
                    bindings[index], baseX + 38F, y + 20F, 266F,
                    62F, 20F, true, DesignerTextAlignment.Left);
                value.Surface = DesignerSurface.WeatherDetails;
                layout.Elements.Add(value);
                if (row < 3)
                {
                    DesignerElement rule = CreateWoodlandDivider(
                        "details-row-divider-" + index,
                        "Detail Row Divider " + (index + 1),
                        baseX + 2F, y + rowHeight - 2F, 292F, 2F);
                    rule.Surface = DesignerSurface.WeatherDetails;
                    layout.Elements.Add(rule);
                }
            }
        }

        private static void AddWeatherDetailsElements(DesignerLayout layout)
        {
            layout.Elements.Add(CreateDetailsText("details-title", "Panel Title",
                "Weather Details", "None", 44, 49, 664, 25, 24, true));
            layout.Elements.Add(CreateDetailsDivider("details-header-divider",
                "Header Divider", 44, 84, 664, 2));
            string[] labels = { "Feels Like", "Humidity", "Wind",
                "Pressure", "Dew Point", "Visibility", "UV Index",
                "Sunrise / Sunset" };
            string[] values = { "20Â°", "68%", "NW  12 km/h", "1013 hPa",
                "12Â°", "16 km", "3  Moderate", "6:15 AM / 8:32 PM" };
            string[] bindings = { "Weather: Feels Like", "Weather: Humidity",
                "Weather: Wind", "Weather: Pressure", "Weather: Dew Point",
                "Weather: Visibility", "Weather: UV Index",
                "Weather: Sunrise and Sunset" };
            const float leftColumnX = 58F;
            const float rightColumnX = 404F;
            const float columnWidth = 280F;
            const float rowHeight = 82F;
            for (int index = 0; index < 8; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float x = column == 0 ? leftColumnX : rightColumnX;
                float y = 112 + row * rowHeight;
                DesignerElement label = CreateDetailsText("details-label-" + index,
                    "Detail " + (index + 1) + " Label", labels[index], "None",
                    x, y, columnWidth, 18, 13F, false);
                label.Visible = index >= 2;
                layout.Elements.Add(label);
                layout.Elements.Add(CreateDetailsText("details-value-" + index,
                    "Detail " + (index + 1) + " Value", values[index], bindings[index],
                    x, y + 19, columnWidth, 23, 16F, true));
                if (row < 3)
                    layout.Elements.Add(CreateDetailsDivider(
                        "details-row-divider-" + index,
                        "Detail Row Divider " + (index + 1),
                        x, y + rowHeight - 6, columnWidth, 2));
            }
        }

        private static void AddArtDecoWeatherDetailsElements(
            DesignerLayout layout)
        {
            DesignerElement title = CreateDetailsText("details-title",
                "Panel Title", "Weather Details", "None",
                100, 58, 358, 22, 15.5F, true);
            title.FontName = "Georgia";
            layout.Elements.Add(title);
            DesignerElement header = CreateDetailsDivider(
                "details-header-divider", "Header Divider",
                100, 87, 358, 1.5F);
            header.ColorArgb = Color.FromArgb(42, 154, 150).ToArgb();
            layout.Elements.Add(header);

            string[] labels = { "Feels Like", "Humidity", "Wind",
                "Pressure", "Dew Point", "Visibility", "UV Index",
                "Sunrise / Sunset" };
            string[] values = { "20\u00B0", "68%", "NW  12 km/h",
                "1013 hPa", "12\u00B0", "16 km", "3  Moderate",
                "6:15 AM / 8:32 PM" };
            string[] bindings = { "Weather: Feels Like",
                "Weather: Humidity", "Weather: Wind", "Weather: Pressure",
                "Weather: Dew Point", "Weather: Visibility",
                "Weather: UV Index", "Weather: Sunrise and Sunset" };
            const float columnWidth = 160F;
            for (int index = 0; index < 8; index++)
            {
                int column = index % 2;
                int row = index / 2;
                float x = 100F + column * 198F;
                float y = 98F + row * 48F;
                DesignerElement label = CreateDetailsText(
                    "details-label-" + index,
                    "Detail " + (index + 1) + " Label", labels[index],
                    "None", x, y, columnWidth, 14, 10, false);
                label.ColorArgb = Color.FromArgb(190, 181, 157).ToArgb();
                layout.Elements.Add(label);
                layout.Elements.Add(CreateDetailsText(
                    "details-value-" + index,
                    "Detail " + (index + 1) + " Value", values[index],
                    bindings[index], x, y + 14F, columnWidth, 20, 11, true));
                if (row < 3)
                {
                    DesignerElement divider = CreateDetailsDivider(
                        "details-row-divider-" + index,
                        "Detail Row Divider " + (index + 1),
                        x, y + 45F, columnWidth, 1.5F);
                    divider.ColorArgb =
                        Color.FromArgb(42, 154, 150).ToArgb();
                    layout.Elements.Add(divider);
                }
            }
        }

        private static DesignerElement CreateDetailsText(string id, string name,
            string text, string binding, float x, float y, float width,
            float height, float fontSize, bool bold)
        {
            DesignerElement element = CreateTextElement(id, name, text, binding,
                x, y, width, height, fontSize, bold);
            element.Surface = DesignerSurface.WeatherDetails;
            element.Alignment = DesignerTextAlignment.Left;
            return element;
        }

        private static DesignerElement CreateDetailsDivider(string id,
            string name, float x, float y, float width, float height)
        {
            DesignerElement element = CreateDividerElement(id, name, x, y,
                width, height);
            element.Surface = DesignerSurface.WeatherDetails;
            return element;
        }

        private static DesignerElement CreateDividerElement(string id,
            string name, float x, float y, float width, float height)
        {
            return new DesignerElement
            {
                Id = id, Name = name, Kind = DesignerElementKind.Divider,
                Binding = "Layout: Divider", X = x, Y = y,
                Width = width, Height = height,
                ColorArgb = Color.FromArgb(224, 157, 39).ToArgb()
            };
        }

        private static DesignerElement CreateImageElement(string id, string name,
            string binding, float x, float y, float width, float height)
        {
            return new DesignerElement
            {
                Id = id, Name = name, Kind = DesignerElementKind.Image,
                Binding = binding,
                BindingDomain = binding != null && binding.StartsWith("Clock:",
                    StringComparison.OrdinalIgnoreCase) ? "Clock"
                    : binding != null && binding.StartsWith("Calendar:",
                        StringComparison.OrdinalIgnoreCase) ? "Calendar"
                    : "Weather",
                X = x, Y = y, Width = width, Height = height
            };
        }

        private static DesignerElement CreateTextElement(string id, string name, string text,
            string binding, float x, float y, float width, float height,
            float fontSize, bool bold)
        {
            return new DesignerElement
            {
                Id = id, Name = name, Kind = DesignerElementKind.Text,
                Text = text, Binding = binding,
                BindingDomain = binding != null && binding.StartsWith("Clock:",
                    StringComparison.OrdinalIgnoreCase) ? "Clock"
                    : binding != null && binding.StartsWith("Calendar:",
                        StringComparison.OrdinalIgnoreCase) ? "Calendar"
                    : "Weather",
                X = x, Y = y,
                Width = width, Height = height, FontSize = fontSize,
                Bold = bold, Alignment = DesignerTextAlignment.Center,
                ColorArgb = Color.FromArgb(232, 225, 190).ToArgb()
            };
        }

        private void SetLayout(DesignerLayout layout)
        {
            _refreshing = true;
            _layout = layout;
            if (_layout.DeletedElementIds == null)
                _layout.DeletedElementIds = new List<string>();
            if (_layout.Elements == null)
                _layout.Elements = new List<DesignerElement>();
            _layout.Elements.RemoveAll(delegate(DesignerElement element)
            {
                return element != null &&
                    IsElementDeleted(_layout, element.Id);
            });
            EnsureEditableBackgroundLayers();
            EnsureCalendarMoveGroups();
            PrepareClockHandEditing();
            // Saved styles are authoritative. First-use defaults are assigned
            // when a field is added/selected, never every time it is reopened.
            _canvas.CurrentLayout = layout;
            _gridButton.Checked = layout.GridEnabled;
            _properties.SelectedObject = null;
            RefreshLayers();
            UpdatePropertyInspector();
            _undo.Clear();
            _redo.Clear();
            _propertyBaseline = layout.Clone();
            _liveSavedState = SerializeLayoutState(layout);
            _refreshing = false;
            UpdateUndoButtons();
        }

        private void EnsureEditableBackgroundLayers()
        {
            if (_layout == null || _widgetKind == "recyclebin") return;
            EnsureEditableBackgroundLayer("main-background",
                "Main Widget Background", DesignerSurface.Main,
                string.IsNullOrWhiteSpace(_layout.BackgroundImage)
                    ? ThemeBackgroundAssetPath() : _layout.BackgroundImage);
            if (_widgetKind == "weather")
                EnsureEditableBackgroundLayer("details-background",
                    "Weather Details Background",
                    DesignerSurface.WeatherDetails,
                    ThemeDetailsBackgroundAssetPath());
            _layout.BackgroundLayerVersion = Math.Max(
                _layout.BackgroundLayerVersion, 1);
        }

        private void EnsureEditableBackgroundLayer(string id, string name,
            DesignerSurface surface, string imagePath)
        {
            string resolvedImage = ResolveLayoutAsset(imagePath);
            if (string.IsNullOrWhiteSpace(imagePath) ||
                !File.Exists(resolvedImage))
            {
                if (_weatherTheme != "Modern" &&
                    _weatherTheme != "Vintage") return;
                imagePath = string.Empty;
            }
            if (IsElementDeleted(_layout, id)) return;
            DesignerElement existing = _layout.Elements.Find(
                delegate(DesignerElement element)
                {
                    return element != null && string.Equals(element.Id, id,
                        StringComparison.OrdinalIgnoreCase);
                });
            if (existing != null)
            {
                // Version zero stored the complete transparent PNG canvas as
                // the main background rectangle. Convert that rectangle to
                // its painted alpha frame before the cropped renderer is
                // enabled; the artwork remains in exactly the same place.
                if (_layout.BackgroundLayerVersion < 1 &&
                    (surface == DesignerSurface.Main ||
                     _weatherTheme == "Art Deco") &&
                    !string.IsNullOrWhiteSpace(existing.ImagePath))
                {
                    RectangleF mapped = DesignerLayerPainter.AlphaMappedBounds(
                        ResolveLayoutAsset(existing.ImagePath),
                        new RectangleF(existing.X, existing.Y,
                            existing.Width * Math.Max(.05F, existing.Scale),
                            existing.Height * Math.Max(.05F, existing.Scale)));
                    existing.X = mapped.X;
                    existing.Y = mapped.Y;
                    existing.Width = mapped.Width;
                    existing.Height = mapped.Height;
                    existing.Scale = 1F;
                }
                MoveBackgroundBehindSurface(existing, surface);
                return;
            }
            var background = new DesignerElement
            {
                Id = id,
                Name = name,
                Kind = DesignerElementKind.Image,
                Binding = surface == DesignerSurface.Main
                    ? "Layout: Main Background"
                    : "Layout: Weather Details Background",
                BindingDomain = _widgetKind == "clock" ? "Clock" :
                    _widgetKind == "calendar" ? "Calendar" : "Weather",
                Surface = surface,
                ImagePath = imagePath,
                X = 0F,
                Y = 0F,
                Width = _layout.CanvasWidth,
                Height = _layout.CanvasHeight,
                Visible = true,
                Opacity = 1F,
                Scale = 1F
            };
            if ((surface == DesignerSurface.Main ||
                 _weatherTheme == "Art Deco") &&
                !string.IsNullOrWhiteSpace(imagePath))
            {
                RectangleF mapped = DesignerLayerPainter.AlphaMappedBounds(
                    resolvedImage, new RectangleF(0F, 0F,
                        _layout.CanvasWidth, _layout.CanvasHeight));
                background.X = mapped.X;
                background.Y = mapped.Y;
                background.Width = mapped.Width;
                background.Height = mapped.Height;
            }
            int insertAt = _layout.Elements.FindIndex(
                delegate(DesignerElement element)
                {
                    return element != null && element.Surface == surface;
                });
            _layout.Elements.Insert(insertAt < 0 ? 0 : insertAt, background);
        }

        private void MoveBackgroundBehindSurface(DesignerElement background,
            DesignerSurface surface)
        {
            int current = _layout.Elements.IndexOf(background);
            int first = _layout.Elements.FindIndex(delegate(DesignerElement item)
            {
                return item != null && item.Surface == surface;
            });
            if (current < 0 || first < 0 || current == first) return;
            _layout.Elements.RemoveAt(current);
            if (current < first) first--;
            _layout.Elements.Insert(Math.Max(0, first), background);
        }

        private static bool IsBackgroundElement(DesignerElement element)
        {
            return element != null &&
                (string.Equals(element.Id, "main-background",
                    StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(element.Id, "details-background",
                    StringComparison.OrdinalIgnoreCase));
        }

        private void RefreshLayers()
        {
            DesignerElement selected = _canvas.SelectedElement;
            _refreshing = true;
            _layers.Items.Clear();
            if (_layout != null)
                for (int i = _layout.Elements.Count - 1; i >= 0; i--)
                    if (_layout.Elements[i].Surface == _canvas.ActiveSurface)
                        _layers.Items.Add(_layout.Elements[i]);
            if (selected != null) _layers.SelectedItem = selected;
            _refreshing = false;
        }

        private void SyncSelection()
        {
            _refreshing = true;
            _layers.SelectedItem = _canvas.SelectedElement;
            _selectedNumericPropertyName = null;
            DesignerElement selected = _canvas.SelectedElement;
            _properties.SelectedObject = IsClockHand(selected)
                ? (object)new ClockHandProperties(selected)
                : IsClockCentre(selected)
                    ? (object)new ClockCentreProperties(selected)
                    : selected;
            if (_widgetKind != "recyclebin" && _properties.SelectedObject == null && _layout != null &&
                _canvas.ActiveSurface == DesignerSurface.WeatherDetails)
                _properties.SelectedObject = _layout.WeatherDetailsPanel;
            _propertyBaseline = _layout == null ? null : _layout.Clone();
            UpdateNumericEditors();
            UpdatePropertyInspector();
            _refreshing = false;
        }

        private static bool IsClockHand(DesignerElement element)
        {
            return element != null && !string.IsNullOrEmpty(element.Binding) &&
                (element.Binding.Equals("Clock: Hour Hand",
                    StringComparison.OrdinalIgnoreCase) ||
                 element.Binding.Equals("Clock: Minute Hand",
                    StringComparison.OrdinalIgnoreCase) ||
                 element.Binding.Equals("Clock: Second Hand",
                    StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsClockCentre(DesignerElement element)
        {
            return element != null && string.Equals(element.Id,
                "clock-centre-pivot", StringComparison.OrdinalIgnoreCase);
        }

        private bool DrawClockImagePreview(Graphics graphics,
            DesignerElement element, RectangleF bounds, PointF pivot)
        {
            if (element == null || string.IsNullOrWhiteSpace(
                element.ImagePath)) return false;
            string path = ResolveLayoutAsset(element.ImagePath);
            if (!File.Exists(path)) return false;

            Image image = _canvas.GetPreviewImage(path);
            using (var attributes = new ImageAttributes())
            {
                var matrix = new ColorMatrix();
                matrix.Matrix33 = Math.Max(0F,
                    Math.Min(1F, element.Opacity));
                attributes.SetColorMatrix(matrix);
                if (IsClockCentre(element))
                {
                    graphics.DrawImage(image, Rectangle.Round(bounds),
                        0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, attributes);
                    return true;
                }
                if (!IsClockHand(element)) return false;

                bool second = string.Equals(element.Id,
                    "clock-second-hand",
                    StringComparison.OrdinalIgnoreCase);
                float tail = second ? 17F : 13F;
                float height = Math.Max(tail + 3F, bounds.Height);
                float length = height - tail;
                float anchorX = element.HandPivotX.HasValue
                    ? bounds.Width * Math.Max(0F, Math.Min(1F,
                        element.HandPivotX.Value))
                    : bounds.Width / 2F;
                float anchorY = element.HandPivotY.HasValue
                    ? height * Math.Max(0F, Math.Min(1F,
                        element.HandPivotY.Value))
                    : length;
                GraphicsState state = graphics.Save();
                try
                {
                    graphics.TranslateTransform(pivot.X, pivot.Y);
                    graphics.RotateTransform(element.PreviewRotation);
                    var destination = Rectangle.Round(new RectangleF(
                        -anchorX, -anchorY,
                        bounds.Width, height));
                    graphics.DrawImage(image, destination,
                        0, 0, image.Width, image.Height,
                        GraphicsUnit.Pixel, attributes);
                }
                finally { graphics.Restore(state); }
            }
            return true;
        }

        private PointF ClockCentre()
        {
            if (_layout != null && _layout.Elements != null)
            {
                DesignerElement centre = _layout.Elements.Find(
                    delegate(DesignerElement candidate)
                    {
                        return IsClockCentre(candidate);
                    });
                if (centre != null)
                    return new PointF(
                        centre.X + centre.Width * centre.Scale / 2F,
                        centre.Y + centre.Height * centre.Scale / 2F);
            }
            if (UsesThemeSnapshot)
                return _weatherTheme == "Art Deco" ? new PointF(180F, 174F)
                    : new PointF(150F, _weatherTheme == "Vintage" ? 149F : 140F);
            return new PointF(180F, 176.5F);
        }

        private void PrepareClockHandEditing()
        {
            if (_widgetKind != "clock" || _layout == null ||
                _layout.Elements == null) return;
            bool firstUpgrade = _layout.ClockHandEditVersion < 1;
            foreach (DesignerElement element in _layout.Elements)
            {
                if (!IsClockHand(element)) continue;
                MigrateLegacyWoodlandClockHand(element);
                if (firstUpgrade) element.PreviewRotation = 0F;
                if (!element.HandPivotX.HasValue &&
                    !element.HandPivotY.HasValue &&
                    !string.IsNullOrWhiteSpace(element.ImagePath) &&
                    Path.IsPathRooted(element.ImagePath))
                {
                    PointF pivot = new PointF(
                        element.PivotX, element.PivotY);
                    element.HandPivotX = .5F;
                    element.HandPivotY = .5F;
                    element.PivotX = pivot.X;
                    element.PivotY = pivot.Y;
                }
            }
            _layout.ClockHandEditVersion = 1;
        }

        private void MigrateLegacyWoodlandClockHand(
            DesignerElement element)
        {
            if (_weatherTheme != "Woodland Nature" ||
                element == null ||
                string.IsNullOrWhiteSpace(element.ImagePath)) return;
            string normalized = element.ImagePath.Replace('/', '\\');
            bool hour = string.Equals(element.Binding,
                "Clock: Hour Hand", StringComparison.OrdinalIgnoreCase);
            bool minute = string.Equals(element.Binding,
                "Clock: Minute Hand", StringComparison.OrdinalIgnoreCase);
            if (!hour && !minute) return;

            // Builds before the Woodland hand redesign copied hour.png and
            // Min.png into DesignerAssets. Build 1362 also made portable
            // copies of those same leaf files. The live clock already
            // substitutes the current cream hands, but the Designer used to
            // draw the legacy PNG directly and then publish it to Gallery and
            // the Widget Dock. Normalize the saved layer itself so the live
            // widget, Designer and every generated preview use one image.
            bool legacyDesignerAsset = normalized.IndexOf(
                    "\\DesignerAssets\\Clock\\WoodlandNature\\",
                    StringComparison.OrdinalIgnoreCase) >= 0 &&
                (normalized.EndsWith("\\hour.png",
                    StringComparison.OrdinalIgnoreCase) ||
                 normalized.EndsWith("\\min.png",
                    StringComparison.OrdinalIgnoreCase));
            bool portableLeafCopy = normalized.EndsWith(
                    "\\WoodlandNature\\woodland-custom-hour-hand.png",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.EndsWith(
                    "\\WoodlandNature\\woodland-custom-minute-hand.png",
                    StringComparison.OrdinalIgnoreCase);
            if (!legacyDesignerAsset && !portableLeafCopy) return;

            PointF pivot = new PointF(element.PivotX, element.PivotY);
            element.ImagePath = hour
                ? "Assets/Themes/WoodlandNature/woodland-hour-hand.png"
                : "Assets/Themes/WoodlandNature/woodland-minute-hand.png";
            element.Scale = 1F;
            element.Width = hour ? 13.5F : 8.5F;
            element.Height = hour ? 72F : 100F;
            element.HandPivotX = .5F;
            element.HandPivotY = 1F;
            element.PivotX = pivot.X;
            element.PivotY = pivot.Y;
        }

        private void MoveClockCentreTo(PointF centre)
        {
            if (_layout == null || _layout.Elements == null) return;
            DesignerElement pivot = _layout.Elements.Find(
                delegate(DesignerElement candidate)
                {
                    return IsClockCentre(candidate);
                });
            if (pivot == null) return;
            pivot.X = centre.X - pivot.Width * pivot.Scale / 2F;
            pivot.Y = centre.Y - pivot.Height * pivot.Scale / 2F;
        }

        private sealed class ClockHandProperties
        {
            private readonly DesignerElement _element;
            public ClockHandProperties(DesignerElement element)
            { _element = element; }

            [Category("Layer")]
            public string Name
            { get { return _element.Name; } set { _element.Name = value; } }
            [Browsable(false)]
            public bool Visible
            { get { return _element.Visible; } set { _element.Visible = value; } }
            [Category("Clock hand"), DisplayName("Connected as"),
             TypeConverter(typeof(WeatherBindingConverter))]
            public string Binding
            { get { return _element.Binding; } set { _element.Binding = value; } }
            [Category("Clock hand"), DisplayName("Dial pivot X")]
            public float PivotX
            { get { return _element.PivotX; } set { _element.PivotX = value; } }
            [Category("Clock hand"), DisplayName("Dial pivot Y")]
            public float PivotY
            { get { return _element.PivotY; } set { _element.PivotY = value; } }
            [Category("Clock hand"), DisplayName("Length")]
            public float Length
            {
                get { return _element.HandLength; }
                set
                {
                    PointF pivot = new PointF(
                        _element.PivotX, _element.PivotY);
                    _element.HandLength = value;
                    _element.PivotX = pivot.X;
                    _element.PivotY = pivot.Y;
                }
            }
            [Category("Clock hand")]
            public float Thickness
            {
                get { return _element.Thickness; }
                set
                {
                    PointF pivot = new PointF(
                        _element.PivotX, _element.PivotY);
                    _element.Thickness = value;
                    _element.PivotX = pivot.X;
                    _element.PivotY = pivot.Y;
                }
            }
            [Category("Image"), DisplayName("PNG file")]
            public string ImagePath
            { get { return _element.ImagePath; } set { _element.ImagePath = value; } }
            [Category("Preview"), DisplayName("Rotation (degrees)")]
            public float Rotation
            { get { return _element.PreviewRotation; } set { _element.PreviewRotation = value; } }
            [Category("Clock hand"), DisplayName("PNG pivot across")]
            public float? ImagePivotX
            {
                get { return _element.HandPivotX; }
                set
                {
                    float pivot = _element.PivotX;
                    _element.HandPivotX = value;
                    _element.PivotX = pivot;
                }
            }
            [Category("Clock hand"), DisplayName("PNG pivot down")]
            public float? ImagePivotY
            {
                get { return _element.HandPivotY; }
                set
                {
                    float pivot = _element.PivotY;
                    _element.HandPivotY = value;
                    _element.PivotY = pivot;
                }
            }
            [Category("Appearance")]
            public float Opacity
            { get { return _element.Opacity; } set { _element.Opacity = value; } }
        }

        private sealed class ClockCentreProperties
        {
            private readonly DesignerElement _element;
            public ClockCentreProperties(DesignerElement element)
            { _element = element; }

            [Category("Layer")]
            public string Name
            { get { return _element.Name; } set { _element.Name = value; } }
            [Browsable(false)]
            public bool Visible
            { get { return _element.Visible; } set { _element.Visible = value; } }
            [Category("Centre pivot"), DisplayName("Centre X")]
            public float CentreX
            {
                get { return _element.X + _element.Width * _element.Scale / 2F; }
                set { _element.X = value - _element.Width * _element.Scale / 2F; }
            }
            [Category("Centre pivot"), DisplayName("Centre Y")]
            public float CentreY
            {
                get { return _element.Y + _element.Height * _element.Scale / 2F; }
                set { _element.Y = value - _element.Height * _element.Scale / 2F; }
            }
            [Category("Centre pivot"), DisplayName("Dot size")]
            public float Size
            {
                get { return _element.Width * _element.Scale; }
                set
                {
                    float centreX = CentreX;
                    float centreY = CentreY;
                    _element.Width = Math.Max(4F, value) /
                        Math.Max(.1F, _element.Scale);
                    _element.Height = _element.Width;
                    CentreX = centreX;
                    CentreY = centreY;
                }
            }
            [Category("Appearance")]
            public float Opacity
            { get { return _element.Opacity; } set { _element.Opacity = value; } }
        }

        private void CaptureUndo()
        {
            if (_layout == null) return;
            _undo.Push(_layout.Clone());
            _redo.Clear();
            _propertyBaseline = _layout.Clone();
            UpdateUndoButtons();
        }

        private void Undo()
        {
            if (_undo.Count == 0) return;
            _redo.Push(_layout.Clone());
            _layout = _undo.Pop();
            _canvas.CurrentLayout = _layout;
            RefreshLayers();
            _propertyBaseline = _layout.Clone();
            UpdateUndoButtons();
        }

        private void Redo()
        {
            if (_redo.Count == 0) return;
            _undo.Push(_layout.Clone());
            _layout = _redo.Pop();
            _canvas.CurrentLayout = _layout;
            RefreshLayers();
            _propertyBaseline = _layout.Clone();
            UpdateUndoButtons();
        }

        private void UpdateUndoButtons()
        {
            _undoButton.Enabled = _undo.Count > 0;
            _redoButton.Enabled = _redo.Count > 0;
        }

        private void AddText()
        {
            AddTextAt(new PointF(40F, 40F));
        }

        private void AddTextAt(PointF point)
        {
            CaptureUndo();
            var element = new DesignerElement
            {
                Name = "New Text",
                Kind = DesignerElementKind.Text,
                BindingDomain = ConnectionDomain,
                X = point.X,
                Y = point.Y,
                Surface = _canvas.ActiveSurface
            };
            _layout.Elements.Add(element);
            RefreshLayers();
            _canvas.SelectedElement = element;
        }

        private void AddDivider()
        {
            AddDividerAt(new PointF(40F, 40F));
        }

        private void AddDividerAt(PointF point)
        {
            CaptureUndo();
            DesignerElement element = CreateDividerElement(
                Guid.NewGuid().ToString("N"), "New Divider",
                point.X, point.Y, 180F, 2F);
            element.Surface = _canvas.ActiveSurface;
            element.BindingDomain = ConnectionDomain;
            _layout.Elements.Add(element);
            RefreshLayers();
            _canvas.SelectedElement = element;
        }

        private void AddOrSelectWeatherField(string requestedId,
            string menuName, string binding, DesignerElementKind kind,
            int forecastIndex)
        {
            DesignerSurface surface = _canvas.ActiveSurface;
            DesignerElement existing = _layout.Elements.FirstOrDefault(
                delegate(DesignerElement candidate)
                {
                    if (candidate == null || candidate.Surface != surface ||
                        candidate.Kind != kind) return false;
                    if (forecastIndex >= 0)
                        return string.Equals(candidate.Id, requestedId,
                            StringComparison.OrdinalIgnoreCase) ||
                            (string.Equals(candidate.Binding, binding,
                                StringComparison.OrdinalIgnoreCase) &&
                             DesignerForecastIndex(candidate) ==
                                forecastIndex);
                    return string.Equals(candidate.Id, requestedId,
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(candidate.Binding, binding,
                            StringComparison.OrdinalIgnoreCase);
                });
            if (existing != null)
            {
                if (!existing.Visible)
                {
                    CaptureUndo();
                    existing.Visible = true;
                }
                _canvas.SelectedElement = existing;
                RefreshLayers();
                _properties.Refresh();
                return;
            }

            CaptureUndo();
            DesignerElement template = _layout.Elements.FirstOrDefault(
                delegate(DesignerElement candidate)
                {
                    return candidate != null &&
                        candidate.Surface == surface &&
                        candidate.Kind == kind &&
                        string.Equals(candidate.Binding, binding,
                            StringComparison.OrdinalIgnoreCase);
                });
            DesignerElement element;
            if (template == null)
            {
                element = new DesignerElement
                {
                    Kind = kind,
                    Surface = surface,
                    Width = kind == DesignerElementKind.Image ? 56F : 160F,
                    Height = kind == DesignerElementKind.Image ? 56F : 34F,
                    Alignment = DesignerTextAlignment.Center,
                    WordWrap = false
                };
                if (IsBotanicalWeatherTheme)
                    element.ColorArgb = Color.FromArgb(37, 63, 51).ToArgb();
                else
                    element.ColorArgb = Color.FromArgb(244, 228, 192).ToArgb();
            }
            else
            {
                var serializer = new System.Web.Script.Serialization
                    .JavaScriptSerializer();
                element = serializer.Deserialize<DesignerElement>(
                    serializer.Serialize(template));
            }

            string id = surface == DesignerSurface.Main
                ? requestedId
                : "details-" + requestedId;
            string name = forecastIndex >= 0
                ? "Forecast " + (forecastIndex + 1) + " " + menuName
                : menuName;
            element.Id = id;
            element.Name = name;
            element.Kind = kind;
            element.Surface = surface;
            element.Binding = binding;
            element.BindingDomain = "Weather";
            element.Visible = true;
            element.MouseLocked = false;
            element.X = Math.Max(0F, Math.Min(
                _layout.CanvasWidth - element.Width,
                _contextInsertionPoint.X));
            element.Y = Math.Max(0F, Math.Min(
                _layout.CanvasHeight - element.Height,
                _contextInsertionPoint.Y));
            if (kind == DesignerElementKind.Text)
                element.Text = ResolvePreviewText(element);

            if (_layout.DeletedElementIds != null)
                _layout.DeletedElementIds.RemoveAll(delegate(string deletedId)
                {
                    return string.Equals(deletedId, id,
                        StringComparison.OrdinalIgnoreCase);
                });
            _layout.Elements.Add(element);
            RefreshLayers();
            _canvas.SelectedElement = element;
            _properties.Refresh();
            _canvas.Invalidate();
        }

        private static int DesignerForecastIndex(DesignerElement element)
        {
            if (element == null) return -1;
            string id = element.Id ?? string.Empty;
            string[] markers = { "forecast-day-", "forecast-icon-",
                "forecast-range-", "forecast-high-", "forecast-low-",
                "forecast-condition-" };
            foreach (string marker in markers)
            {
                int markerIndex = id.IndexOf(marker,
                    StringComparison.OrdinalIgnoreCase);
                int parsed;
                if (markerIndex >= 0 && int.TryParse(id.Substring(
                    markerIndex + marker.Length), out parsed))
                    return parsed;
            }
            string name = element.Name ?? string.Empty;
            if (!name.StartsWith("Forecast ",
                StringComparison.OrdinalIgnoreCase)) return -1;
            string remainder = name.Substring("Forecast ".Length);
            int separator = remainder.IndexOf(' ');
            int ordinal;
            return separator > 0 && int.TryParse(
                remainder.Substring(0, separator), out ordinal)
                ? ordinal - 1 : -1;
        }

        // A weather field added to a Details panel should look like the
        // existing values immediately.  It is still fully editable; this only
        // supplies a sensible first style for a brand-new generic text layer.
        private void ApplyWeatherDetailsFieldStyle(DesignerElement element)
        {
            if (element == null || element.Kind != DesignerElementKind.Text ||
                element.Surface != DesignerSurface.WeatherDetails ||
                !string.Equals(element.Name, "New Text",
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(element.Binding) ||
                !element.Binding.StartsWith("Weather:",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.Binding, "Weather: Current Icon",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(element.Binding, "Weather: Forecast Icon",
                    StringComparison.OrdinalIgnoreCase))
                return;

            DesignerElement template = _layout.Elements.FirstOrDefault(
                delegate(DesignerElement candidate)
                {
                    return candidate != null &&
                        candidate.Surface == DesignerSurface.WeatherDetails &&
                        candidate.Kind == DesignerElementKind.Text &&
                        candidate.Id != null && candidate.Id.StartsWith(
                            "details-value-", StringComparison.OrdinalIgnoreCase);
                });
            if (template == null) return;

            element.FontName = template.FontName;
            element.FontFile = template.FontFile;
            element.FontSize = template.FontSize;
            element.Bold = template.Bold;
            element.Italic = template.Italic;
            element.ColorArgb = template.ColorArgb;
            element.Alignment = template.Alignment;
            element.WordWrap = template.WordWrap;
            element.Trimming = template.Trimming;
            element.Width = template.Width;
            element.Height = template.Height;
            element.Text = ResolvePreviewText(element);
        }

        private void AddImage()
        {
            AddImageAt(new PointF(40F, 40F));
        }

        private void AddImageAt(PointF point)
        {
            using (var dialog = NewPngDialog("Add PNG image"))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                TryDropAction(delegate
                    { AddImageFromPath(dialog.FileName, point); });
            }
        }

        private void ReplaceSelectedImage()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || selected.Kind != DesignerElementKind.Image)
            {
                MessageBox.Show(this, "Select an image layer first.",
                    "Replace Image", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            using (var dialog = NewPngDialog("Replace selected image"))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                TryDropAction(delegate
                    { ReplaceImage(selected, dialog.FileName); });
            }
        }

        private void ClearSelectedImage()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || selected.Kind != DesignerElementKind.Image)
                return;
            CaptureUndo();
            selected.ImagePath = string.Empty;
            _properties.Refresh();
            _canvas.Invalidate();
            UpdatePropertyInspector();
        }

        private OpenFileDialog NewPngDialog(string title)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "PNG images (*.png)|*.png",
                Title = title
            };
            DesignerElement selected = _canvas.SelectedElement;
            string selectedPath = selected == null
                ? string.Empty : ResolveLayoutAsset(selected.ImagePath);
            if (!string.IsNullOrWhiteSpace(selectedPath) &&
                File.Exists(selectedPath))
                dialog.InitialDirectory = Path.GetDirectoryName(selectedPath);
            else
            {
                string imageFolder = DefaultImageDirectory(selected);
                if (Directory.Exists(imageFolder))
                    dialog.InitialDirectory = imageFolder;
            }
            return dialog;
        }

        private string DefaultImageDirectory(DesignerElement selected)
        {
            string binding = selected == null
                ? string.Empty : selected.Binding ?? string.Empty;
            if (binding.Equals("Weather: Current Icon",
                    StringComparison.OrdinalIgnoreCase) ||
                binding.Equals("Weather: Forecast Icon",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (IsImportedWeatherTheme)
                {
                    string importedPack = EmilyDeskThemeCatalog.Get(
                        _weatherTheme).Asset("weatherIcons");
                    if (Directory.Exists(importedPack)) return importedPack;
                }
                if (_layout != null &&
                    !string.IsNullOrWhiteSpace(_layout.WeatherIconPack) &&
                    Directory.Exists(_layout.WeatherIconPack))
                    return _layout.WeatherIconPack;
                string packaged = Path.Combine(_projectRoot, "Assets",
                    "WeatherIconPacks",
                    _weatherTheme == "Art Deco" ? "ArtDeco"
                    : _weatherTheme == "Woodland Nature"
                        ? "WoodlandNature"
                        : IsBotanicalWeatherTheme ? "BotanicalNature"
                        : IsSteampunkWeatherTheme ? "Steampunk"
                        : "Industrial");
                if (Directory.Exists(packaged)) return packaged;
                return Path.Combine(_projectRoot, "Assets", "WeatherIconPacks");
            }
            if (_widgetKind == "clock" && selected != null &&
                !string.IsNullOrWhiteSpace(selected.Binding) &&
                selected.Binding.StartsWith("Clock:",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (IsImportedWeatherTheme)
                {
                    string importedClock = EmilyDeskThemeCatalog.Get(
                        _weatherTheme).Asset("clock");
                    if (File.Exists(importedClock)) return Path.GetDirectoryName(importedClock);
                }
                string themeFolder = _weatherTheme == "Art Deco" ? "ArtDeco" :
                    _weatherTheme == "Woodland Nature" ? "WoodlandNature" :
                    IsBotanicalWeatherTheme ? "BotanicalNature" :
                    IsSteampunkWeatherTheme ? "Steampunk" :
                    _weatherTheme;
                string clockFolder = Path.Combine(_projectRoot, "Assets",
                    "Themes", themeFolder);
                if (Directory.Exists(clockFolder)) return clockFolder;
            }
            return DesignerAssetDirectory;
        }

        private string WidgetFontDirectory
        {
            get
            {
                string folder = Path.Combine(DesignerAssetDirectory, "Fonts");
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        private void ChooseSystemFont()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || selected.Kind != DesignerElementKind.Text)
            {
                MessageBox.Show(this, "Select a text layer first.",
                    "Choose Font", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            FontStyle style = selected.Bold ? FontStyle.Bold : FontStyle.Regular;
            if (selected.Italic) style |= FontStyle.Italic;
            using (var dialog = new FontDialog
            {
                Font = new Font(string.IsNullOrWhiteSpace(selected.FontName)
                    ? "Segoe UI" : selected.FontName,
                    Math.Max(4F, selected.FontSize), style),
                ShowEffects = false
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                CaptureUndo();
                selected.FontName = dialog.Font.FontFamily.Name;
                selected.FontSize = dialog.Font.Size;
                selected.Bold = dialog.Font.Bold;
                selected.Italic = dialog.Font.Italic;
                selected.FontFile = string.Empty;
                _properties.Refresh();
                _canvas.Invalidate();
                UpdateNumericEditors();
                UpdatePropertyInspector();
            }
        }

        private void ChooseWidgetFont()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || selected.Kind != DesignerElementKind.Text)
            {
                MessageBox.Show(this, "Select a text layer first.",
                    "Choose Widget Font", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            using (var dialog = new OpenFileDialog
            {
                Filter = "Font files (*.ttf;*.otf)|*.ttf;*.otf",
                Title = "Choose font from this widget's font folder",
                InitialDirectory = WidgetFontDirectory
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string source = Path.GetFullPath(dialog.FileName);
                    string target = source.StartsWith(
                        Path.GetFullPath(WidgetFontDirectory) +
                            Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)
                        ? source : UniquePath(WidgetFontDirectory,
                            Path.GetFileName(source));
                    if (!string.Equals(source, target,
                        StringComparison.OrdinalIgnoreCase))
                        File.Copy(source, target);
                    string family = DesignerFontResolver.FamilyName(target);
                    if (string.IsNullOrWhiteSpace(family))
                        throw new InvalidDataException(
                            "EmilyDesk could not read that TTF or OTF font.");
                    CaptureUndo();
                    selected.FontFile = target;
                    selected.FontName = family;
                    _properties.Refresh();
                    _canvas.Invalidate();
                    UpdatePropertyInspector();
                }
                catch (Exception error)
                {
                    ShowActionError("Choose Widget Font", error);
                }
            }
        }

        private void ClearWidgetFontFile()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || selected.Kind != DesignerElementKind.Text)
                return;
            CaptureUndo();
            selected.FontFile = string.Empty;
            _properties.Refresh();
            _canvas.Invalidate();
            UpdatePropertyInspector();
        }

        private string DesignerAssetDirectory
        {
            get
            {
                string widgetFolder = _widgetKind == "clock" ? "Clock" :
                    _widgetKind == "calendar" ? "Calendar" : _widgetKind == "recyclebin" ? "RecycleBin" : "Weather";
                string themeFolder = _weatherTheme == "Art Deco" ? "ArtDeco" :
                    _weatherTheme == "Woodland Nature" ? "WoodlandNature" :
                    IsBotanicalWeatherTheme ? "BotanicalNature" :
                    IsSteampunkWeatherTheme ? "Steampunk" :
                    _weatherTheme == "Vintage" ? "Vintage" :
                    _weatherTheme == "Modern" ? "Modern" : "Industrial";
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk", "DesignerAssets", widgetFolder,
                    themeFolder);
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        private void OpenSelectedImageFolder()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || selected.Kind != DesignerElementKind.Image)
            {
                MessageBox.Show(this, "Select an image layer first.",
                    "Open Image Folder", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            string path = ResolveLayoutAsset(selected.ImagePath);
            string folder = !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? Path.GetDirectoryName(path) : DefaultImageDirectory(selected);
            OpenFolder(folder);
        }

        private void RestoreSelectedThemeImage()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || selected.Kind != DesignerElementKind.Image ||
                string.IsNullOrWhiteSpace(selected.Binding))
            {
                MessageBox.Show(this,
                    "Select a built-in image layer such as a weather icon or panel button.",
                    "Restore Theme Image", MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }
            CaptureUndo();
            selected.ImagePath = string.Empty;
            _properties.Refresh();
            _canvas.Invalidate();
            UpdatePropertyInspector();
        }

        private void ToggleMouseLock()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null) return;
            CaptureUndo();
            selected.MouseLocked = !selected.MouseLocked;
            _properties.Refresh();
            _canvas.Invalidate();
            RefreshLayers();
            UpdatePropertyInspector();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            if (_properties.ContainsFocus &&
                (key == Keys.Up || key == Keys.Down) &&
                AdjustSelectedNumericProperty(key == Keys.Up ? 1F : -1F,
                    (keyData & Keys.Shift) == Keys.Shift))
                return true;
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void AddNumericRow(TableLayoutPanel panel, string label, Control editor, int row)
        {
            panel.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft }, 0, row);
            panel.Controls.Add(editor, 1, row);
        }

        private void ConfigureNumericEditor(NumericUpDown editor, string propertyName)
        {
            editor.Minimum = 0;
            editor.Maximum = 10000;
            editor.DecimalPlaces = 2;
            editor.Increment = 1;
            editor.Dock = DockStyle.Fill;
            editor.ValueChanged += delegate
            {
                if (_refreshing || _properties.SelectedObject == null) return;
                PropertyDescriptor property = TypeDescriptor.GetProperties(
                    _properties.SelectedObject)[propertyName.Replace(" ", "")];
                if (property == null || property.IsReadOnly) return;
                CaptureUndo();
                property.SetValue(_properties.SelectedObject,
                    Convert.ChangeType(editor.Value, property.PropertyType));
                _properties.Refresh();
                _canvas.Invalidate();
                RefreshLayers();
                _propertyBaseline = _layout.Clone();
            };
        }

        private void UpdateNumericEditors()
        {
            object target = _properties.SelectedObject;
            _refreshing = true;
            UpdateNumericEditor(_widthEditor, target, "Width");
            UpdateNumericEditor(_heightEditor, target, "Height");
            UpdateNumericEditor(_fontSizeEditor, target, "FontSize");
            _refreshing = false;
        }

        private static void UpdateNumericEditor(NumericUpDown editor, object target, string propertyName)
        {
            PropertyDescriptor property = target == null ? null :
                TypeDescriptor.GetProperties(target)[propertyName];
            bool enabled = property != null && !property.IsReadOnly &&
                (property.PropertyType == typeof(float) || property.PropertyType == typeof(double) ||
                 property.PropertyType == typeof(int) || property.PropertyType == typeof(decimal));
            editor.Enabled = enabled;
            if (enabled)
            {
                decimal value = Convert.ToDecimal(property.GetValue(target));
                editor.Value = Math.Max(editor.Minimum, Math.Min(editor.Maximum, value));
            }
        }

        private bool AdjustSelectedNumericProperty(float direction, bool largeStep)
        {
            object target = _properties.SelectedObject;
            if (target == null) return false;

            GridItem item = _properties.SelectedGridItem;
            PropertyDescriptor descriptor = item == null ? null : item.PropertyDescriptor;
            if (!IsNumericProperty(descriptor) &&
                !string.IsNullOrEmpty(_selectedNumericPropertyName))
                descriptor = TypeDescriptor.GetProperties(target)[_selectedNumericPropertyName];
            if (!IsNumericProperty(descriptor) || descriptor.IsReadOnly)
                return false;

            Type type = descriptor.PropertyType;
            float step = largeStep ? 10F : 1F;
            object current = descriptor.GetValue(target);
            bool allowNegative = string.Equals(descriptor.Name, "PositionX",
                    StringComparison.Ordinal) ||
                string.Equals(descriptor.Name, "PositionY",
                    StringComparison.Ordinal);
            CaptureUndo();
            if (type == typeof(float))
            {
                float value = (float)current + direction * step;
                descriptor.SetValue(target,
                    allowNegative ? value : Math.Max(0F, value));
            }
            else if (type == typeof(double))
                descriptor.SetValue(target, Math.Max(0D,
                    (double)current + direction * step));
            else
                descriptor.SetValue(target, Math.Max(0,
                    (int)current + (int)(direction * step)));
            _selectedNumericPropertyName = descriptor.Name;
            _properties.Refresh();
            UpdateNumericEditors();
            _canvas.Invalidate();
            RefreshLayers();
            _propertyBaseline = _layout.Clone();
            UpdateNumericPropertyLabel();
            return true;
        }

        private static bool IsNumericProperty(PropertyDescriptor descriptor)
        {
            if (descriptor == null) return false;
            Type type = descriptor.PropertyType;
            return type == typeof(float) || type == typeof(double) ||
                type == typeof(int);
        }

        private void UpdateNumericPropertyLabel()
        {
            GridItem item = _properties.SelectedGridItem;
            PropertyDescriptor descriptor = item == null
                ? null : item.PropertyDescriptor;
            if (!IsNumericProperty(descriptor) &&
                _properties.SelectedObject != null &&
                !string.IsNullOrEmpty(_selectedNumericPropertyName))
                descriptor = TypeDescriptor.GetProperties(
                    _properties.SelectedObject)[_selectedNumericPropertyName];
            _numericPropertyLabel.Text = IsNumericProperty(descriptor)
                ? "Adjust " + descriptor.DisplayName
                : "Select a numeric property";
        }

        private void ChooseBackground()
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "PNG images (*.png)|*.png",
                Title = "Choose widget background"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SetBackground(ImportPng(dialog.FileName));
            }
        }

        private void UseThemeBackground()
        {
            SetBackground(UsesThemeSnapshot ? string.Empty : ThemeBackgroundAssetPath());
            _layout.ReplaceDefaultBackground = false;
            _canvas.RefreshReference();
            _propertyBaseline = _layout.Clone();
        }

        private string ThemeBackgroundAssetPath()
        {
            if (_widgetKind == "recyclebin") return string.Empty;
            if (_optionalWidgetDesigner != null &&
                !string.IsNullOrWhiteSpace(_optionalWidgetPackageRoot) &&
                Directory.Exists(_optionalWidgetPackageRoot))
            {
                string[] skins = Directory.GetFiles(
                    _optionalWidgetPackageRoot, "*-skin.png",
                    SearchOption.TopDirectoryOnly);
                if (skins.Length == 1)
                    return Path.GetFileName(skins[0]);
            }
            if (IsImportedWeatherTheme)
            {
                string key = _widgetKind == "weather" ? "weather"
                    : _widgetKind == "calendar" ? "calendar" : "clock";
                return EmilyDeskThemeCatalog.Get(_weatherTheme).Asset(key) ?? string.Empty;
            }
            if (_weatherTheme == "Art Deco" && _widgetKind == "weather")
                return "Assets/Themes/ArtDeco/art-deco-weather-skin.png";
            string folder = _weatherTheme.Replace(" ", "");
            string prefix = IsBotanicalWeatherTheme ? "botanical" :
                _weatherTheme == "Woodland Nature" ? "woodland" :
                _weatherTheme.ToLowerInvariant().Replace(" ", "-");
            return "Assets/Themes/" + folder + "/" + prefix + "-" + _widgetKind + "-skin.png";
        }

        private string ThemeDetailsBackgroundAssetPath()
        {
            if (IsImportedWeatherTheme)
                return EmilyDeskThemeCatalog.Get(_weatherTheme).Asset("weatherDetails")
                    ?? ThemeBackgroundAssetPath();
            if (_weatherTheme == "Art Deco")
                return "Assets/Themes/ArtDeco/art-deco-forecast-panel-skin.png";
            if (_weatherTheme == "Industrial")
                return "Assets/Themes/Industrial/industrial-weather-details-panel.png";
            return ThemeBackgroundAssetPath();
        }

        private void RemoveBackground()
        {
            SetBackground(string.Empty);
        }

        private void SetBackground(string path)
        {
            if (_layout == null) return;
            CaptureUndo();
            _layout.BackgroundImage = path ?? string.Empty;
            _layout.ReplaceDefaultBackground = true;
            DesignerElement background = _layout.Elements.Find(
                delegate(DesignerElement item)
                {
                    return item != null && string.Equals(item.Id,
                        "main-background", StringComparison.OrdinalIgnoreCase);
                });
            if (background == null)
            {
                EnsureEditableBackgroundLayers();
                background = _layout.Elements.Find(
                    delegate(DesignerElement item)
                    {
                        return item != null && string.Equals(item.Id,
                            "main-background",
                            StringComparison.OrdinalIgnoreCase);
                    });
            }
            if (background != null)
            {
                // Replacing artwork must not replace the layer geometry. The
                // user may already have moved, resized, scaled, or faded the
                // background. Newly-created and legacy layers receive their
                // alpha-mapped frame in EnsureEditableBackgroundLayer.
                background.ImagePath = path ?? string.Empty;
                MoveBackgroundBehindSurface(background,
                    DesignerSurface.Main);
            }
            _layout.BackgroundLayerVersion = Math.Max(
                _layout.BackgroundLayerVersion, 1);
            _canvas.RefreshReference();
            RefreshLayers();
            _propertyBaseline = _layout.Clone();
        }

        private void CanvasDragEnter(object sender, DragEventArgs e)
        {
            string[] paths = DroppedPngs(e.Data);
            e.Effect = paths.Length > 0 && (e.AllowedEffect & DragDropEffects.Copy) != 0
                ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void CanvasDragDrop(object sender, DragEventArgs e)
        {
            ShowArtworkDropMenu(DroppedPngs(e.Data), new Point(e.X, e.Y));
        }

        private static bool IsPng(string path)
        {
            return File.Exists(path) && string.Equals(Path.GetExtension(path),
                ".png", StringComparison.OrdinalIgnoreCase);
        }

        private void AddImageFromPath(string source, PointF position)
        {
            string destination = ImportPng(source);
            CaptureUndo();
            using (Image image = Image.FromFile(destination))
            {
                float fit = Math.Min(1F, 300F /
                    Math.Max(image.Width, image.Height));
                var element = new DesignerElement
                {
                    Name = Path.GetFileNameWithoutExtension(destination),
                    Kind = DesignerElementKind.Image,
                    BindingDomain = ConnectionDomain,
                    Surface = _canvas.ActiveSurface,
                    ImagePath = destination,
                    X = Math.Max(0F, position.X),
                    Y = Math.Max(0F, position.Y),
                    Width = Math.Max(10F, image.Width * fit),
                    Height = Math.Max(10F, image.Height * fit)
                };
                _layout.Elements.Add(element);
                RefreshLayers();
                _canvas.SelectedElement = element;
            }
        }

        private void ReplaceImage(DesignerElement element, string source)
        {
            string imported = ImportPng(source);
            CaptureUndo();
            PointF handPivot = IsClockHand(element)
                ? new PointF(element.PivotX, element.PivotY)
                : PointF.Empty;
            element.ImagePath = imported;
            using (Image image = Image.FromFile(element.ImagePath))
            {
                float longest = Math.Max(element.Width, element.Height);
                float fit = longest / Math.Max(image.Width, image.Height);
                element.Width = Math.Max(10F, image.Width * fit);
                element.Height = Math.Max(10F, image.Height * fit);
            }
            if (IsClockHand(element))
            {
                // Imported hand PNGs use an XWidget-style center pivot by
                // default. The pivot controls can place it anywhere else.
                element.HandPivotX = .5F;
                element.HandPivotY = .5F;
                element.PivotX = handPivot.X;
                element.PivotY = handPivot.Y;
                element.PreviewRotation = 0F;
                _layout.ClockHandEditVersion = 1;
            }
            _canvas.SelectedElement = element;
            _properties.Refresh();
            _canvas.Invalidate();
        }

        private string ImportPng(string source)
        {
            ValidateDroppedPng(source);
            string imports = DesignerAssetDirectory;
            string fullSource = Path.GetFullPath(source);
            if (string.Equals(Path.GetDirectoryName(fullSource), imports,
                StringComparison.OrdinalIgnoreCase))
                return fullSource;
            string destination = UniquePath(imports, Path.GetFileName(source));
            File.Copy(source, destination);
            return destination;
        }

        private static string WeatherIconPacksDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk", "WeatherIconPacks");
            }
        }

        private static string ImportedWallpapersDirectory
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk", "Wallpapers");
            }
        }

        private void ChooseWeatherIconPack()
        {
            Directory.CreateDirectory(WeatherIconPacksDirectory);
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Choose an installed EmilyDesk weather icon pack",
                SelectedPath = WeatherIconPacksDirectory,
                ShowNewFolderButton = false
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ValidateWeatherIconPack(dialog.SelectedPath);
                    SetWeatherIconPack(dialog.SelectedPath);
                }
                catch (Exception ex) { ShowActionError("Weather Icons", ex); }
            }
        }

        private void ImportWeatherIconPack()
        {
            using (var dialog = new FolderBrowserDialog
            {
                Description = "Choose a folder containing numbered weather PNG files",
                ShowNewFolderButton = false
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    ValidateWeatherIconPack(dialog.SelectedPath);
                    Directory.CreateDirectory(WeatherIconPacksDirectory);
                    string name = SafeFolderName(new DirectoryInfo(
                        dialog.SelectedPath).Name);
                    string destination = UniqueDirectory(
                        WeatherIconPacksDirectory, name);
                    CopyDirectory(dialog.SelectedPath, destination);
                    SetWeatherIconPack(destination);
                    MessageBox.Show(this,
                        "The icon pack was imported and selected.",
                        "Weather Icons", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                catch (Exception ex) { ShowActionError("Weather Icons", ex); }
            }
        }

        private void SetWeatherIconPack(string path)
        {
            if (_layout == null) return;
            CaptureUndo();
            _layout.WeatherIconPack = path ?? string.Empty;
            _canvas.Invalidate();
            _propertyBaseline = _layout.Clone();
        }

        private static void ValidateWeatherIconPack(string directory)
        {
            if (!Directory.Exists(directory) ||
                Directory.GetFiles(directory, "*.png").Length == 0)
                throw new InvalidDataException(
                    "The selected folder does not contain PNG weather icons.");
        }

        private void ApplyThemeWallpaper(string themeName)
        {
            try
            {
                EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(themeName);
                string fileName = theme.WallpaperFileName;
                string path = theme.IsImported
                    ? theme.Asset("wallpaper1Wide")
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                        "Wallpapers", fileName);
                if (!theme.IsImported && !File.Exists(path))
                    path = Path.Combine(_projectRoot, "Wallpapers", fileName);
                DesignerWallpaperManager.Apply(path);
            }
            catch (Exception ex) { ShowActionError("Wallpaper", ex); }
        }

        private void ChoosePersonalWallpaper(bool import)
        {
            using (var dialog = new OpenFileDialog
            {
                Filter = "Wallpaper images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*",
                Title = import ? "Import wallpaper" : "Choose personal wallpaper"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path = dialog.FileName;
                if (import)
                {
                    Directory.CreateDirectory(ImportedWallpapersDirectory);
                    string destination = UniquePath(ImportedWallpapersDirectory,
                        Path.GetFileName(path));
                    File.Copy(path, destination);
                    path = destination;
                }
                try { DesignerWallpaperManager.Apply(path); }
                catch (Exception ex) { ShowActionError("Wallpaper", ex); }
            }
        }

        private void RestorePreviousWallpaper()
        {
            try { DesignerWallpaperManager.RestorePrevious(); }
            catch (Exception ex) { ShowActionError("Wallpaper", ex); }
        }

        private bool DrawWeatherIconPackPreview(Graphics graphics,
            DesignerElement element, RectangleF bounds)
        {
            if (_layout == null || element == null ||
                element.Kind != DesignerElementKind.Image) return false;
            // The weather-details button is an image layer so it can be
            // positioned in the Designer, but it is rendered by the native
            // toggle-button painter.  Do not substitute a forecast icon for
            // it in the Designer preview.
            if (string.Equals(element.Id, "panel-button",
                StringComparison.OrdinalIgnoreCase)) return false;
            if (!string.IsNullOrEmpty(element.Id) && element.Id.StartsWith(
                "details-icon-", StringComparison.OrdinalIgnoreCase))
                return false;
            string weatherIconPack = _layout.WeatherIconPack;
            if (IsImportedWeatherTheme && (string.IsNullOrWhiteSpace(
                weatherIconPack) || !Directory.Exists(weatherIconPack)))
                weatherIconPack = EmilyDeskThemeCatalog.Get(
                    _weatherTheme).Asset("weatherIcons");
            string builtInPack = string.Equals(_weatherTheme, "Art Deco",
                StringComparison.OrdinalIgnoreCase) ? "ArtDeco"
                : string.Equals(_weatherTheme, "Woodland Nature",
                    StringComparison.OrdinalIgnoreCase) ? "WoodlandNature"
                : IsBotanicalWeatherTheme ? "BotanicalNature"
                : IsSteampunkWeatherTheme ? "Steampunk"
                : "Industrial";
            if (string.IsNullOrWhiteSpace(weatherIconPack) ||
                !Directory.Exists(weatherIconPack))
                weatherIconPack = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "Assets", "WeatherIconPacks", builtInPack);
            if (!Directory.Exists(weatherIconPack))
                weatherIconPack = Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "Assets",
                    "WeatherIconPacks", builtInPack));
            if (string.IsNullOrWhiteSpace(weatherIconPack)) return false;
            int icon = 3;
            if (!string.IsNullOrEmpty(element.Id) &&
                element.Id.StartsWith("forecast-icon-",
                    StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(element.Id.Substring(
                    "forecast-icon-".Length), out index))
                    icon = index == 2 ? 12 : index == 4 ? 18 : 3;
            }
            string path = WeatherIconFile(weatherIconPack, icon);
            if (string.IsNullOrEmpty(path)) return false;
            using (Image image = Image.FromFile(path))
                graphics.DrawImage(image, bounds);
            return true;
        }

        private static string WeatherIconFile(string directory, int icon)
        {
            if (!Directory.Exists(directory)) return string.Empty;
            string path = Path.Combine(directory,
                icon.ToString() + ".png");
            if (File.Exists(path)) return path;
            path = Path.Combine(directory,
                icon.ToString("00") + ".png");
            return File.Exists(path) ? path : string.Empty;
        }

        private void ShowActionError(string title, Exception exception)
        {
            MessageBox.Show(this, exception.Message, title,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void OpenFolder(string path)
        {
            Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination,
                    Path.GetFileName(file)));
            foreach (string child in Directory.GetDirectories(source))
                CopyDirectory(child, Path.Combine(destination,
                    new DirectoryInfo(child).Name));
        }

        private static string UniqueDirectory(string parent, string name)
        {
            string path = Path.Combine(parent, name);
            if (!Directory.Exists(path)) return path;
            int number = 2;
            while (Directory.Exists(path))
                path = Path.Combine(parent, name + "-" + number++);
            return path;
        }

        private static string SafeFolderName(string name)
        {
            foreach (char character in Path.GetInvalidFileNameChars())
                name = name.Replace(character, '-');
            return string.IsNullOrWhiteSpace(name)
                ? "Imported Weather Icons" : name;
        }

        private void DuplicateSelected()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null) return;
            CaptureUndo();
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            DesignerElement copy = serializer.Deserialize<DesignerElement>(serializer.Serialize(selected));
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name += " Copy";
            copy.X += 10; copy.Y += 10;
            _layout.Elements.Add(copy);
            RefreshLayers();
            _canvas.SelectedElement = copy;
        }

        private void DeleteSelected()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null) return;
            CaptureUndo();
            if (_layout.DeletedElementIds == null)
                _layout.DeletedElementIds = new List<string>();
            if (!string.IsNullOrWhiteSpace(selected.Id) &&
                !IsElementDeleted(_layout, selected.Id))
                _layout.DeletedElementIds.Add(selected.Id);
            _layout.Elements.Remove(selected);
            _canvas.SelectedElement = null;
            RefreshLayers();
        }

        private void MoveLayer(int direction)
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null) return;
            int index = _layout.Elements.IndexOf(selected);
            int target = index + direction;
            while (target >= 0 && target < _layout.Elements.Count &&
                _layout.Elements[target].Surface != selected.Surface)
                target += direction;
            if (target < 0 || target >= _layout.Elements.Count) return;
            CaptureUndo();
            DesignerElement neighbour = _layout.Elements[target];
            _layout.Elements[target] = selected;
            _layout.Elements[index] = neighbour;
            RefreshLayers();
            _canvas.Invalidate();
        }

        private void MoveSelectedToEdge(bool front)
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null || _layout == null) return;
            int current = _layout.Elements.IndexOf(selected);
            if (current < 0) return;
            CaptureUndo();
            _layout.Elements.RemoveAt(current);
            if (front)
            {
                int last = _layout.Elements.FindLastIndex(
                    delegate(DesignerElement element)
                    {
                        return element.Surface == selected.Surface;
                    });
                _layout.Elements.Insert(last + 1, selected);
            }
            else
            {
                int first = _layout.Elements.FindIndex(
                    delegate(DesignerElement element)
                    {
                        return element.Surface == selected.Surface;
                    });
                _layout.Elements.Insert(first < 0 ? 0 : first, selected);
            }
            RefreshLayers();
            _canvas.Invalidate();
        }

        private void ToggleVisibility()
        {
            DesignerElement selected = _canvas.SelectedElement;
            if (selected == null) return;
            CaptureUndo();
            selected.Visible = !selected.Visible;
            RefreshLayers();
            _canvas.Invalidate();
            _properties.Refresh();
        }

        private void SaveLayout(bool choosePath)
        {
            if (_templateProject) { SaveTemplateProject(choosePath); return; }
            string exportPath = null;
            if (choosePath)
            {
                using (var dialog = new SaveFileDialog {
                    Filter = "EmilyDesk layout (*.layout.json)|*.layout.json",
                    FileName = _liveLayoutFileName })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    exportPath = dialog.FileName;
                }
            }
            try
            {
                CaptureWoodlandChanges();
                // Save As exports a copy AND publishes the live theme layout.
                // Always write user-local data; a protected shared installation
                // must not make a successful save invisible to the live widget.
                _layoutPath = DesignerLayoutStore.PublishLive(_liveLayoutFileName, _layout, exportPath);
                _liveSavedState = SerializeLayoutState(_layout);
                bool notified = EngineClient.RefreshDesignerLayouts();
                Text = "EmilyDesk Designer - " + _layout.Name +
                    (notified ? " - Saved; live refresh requested" : " - Saved (applies when EmilyDesk starts)");
            }
            catch (Exception error)
            {
                MessageBox.Show(this, "The layout was not applied.\n\n" + error.Message,
                    "Save layout", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try { PublishPackageArtwork(); }
            catch (Exception error)
            {
                MessageBox.Show(this, "The live layout was saved, but package artwork could not be updated.\n\n" +
                    error.Message, "Package artwork", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string SerializeLayoutState(DesignerLayout layout)
        {
            var serializer = new System.Web.Script.Serialization
                .JavaScriptSerializer();
            serializer.MaxJsonLength = 16 * 1024 * 1024;
            return layout == null ? string.Empty : serializer.Serialize(layout);
        }

        private void DesignerFormClosing(object sender,
            FormClosingEventArgs e)
        {
            if (_templateProject) return;
            e.Cancel = !ConfirmSaveLiveEdits();
        }

        private bool ConfirmSaveLiveEdits()
        {
            if (_layout == null ||
                _liveSavedState == SerializeLayoutState(_layout)) return true;
            DialogResult choice = MessageBox.Show(this,
                "Save your widget edits?",
                "EmilyDesk Designer", MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);
            if (choice == DialogResult.Cancel) return false;
            if (choice == DialogResult.No) return true;
            if (choice == DialogResult.Yes)
            {
                string before = _liveSavedState;
                SaveLayout(false);
                return before != _liveSavedState;
            }
            return true;
        }

        private void CaptureWoodlandChanges()
        {
            if (!IsNatureWeatherTheme || _layout == null ||
                _woodlandBaseline == null) return;
            _layout.WoodlandLayoutVersion = IsBotanicalWeatherTheme ? 2 : 1;
            _layout.WoodlandChangedElementIds = new List<string>();
            foreach (DesignerElement element in _layout.Elements)
            {
                if (element == null || string.IsNullOrWhiteSpace(element.Id))
                    continue;
                DesignerElement baseline = _woodlandBaseline.Elements.Find(
                    delegate(DesignerElement item)
                    {
                        return item != null && string.Equals(item.Id,
                            element.Id, StringComparison.OrdinalIgnoreCase);
                    });
                if (baseline == null || !SameWoodlandElement(element, baseline))
                    _layout.WoodlandChangedElementIds.Add(element.Id);
            }
        }

        private static bool SameWoodlandElement(DesignerElement left,
            DesignerElement right)
        {
            return left.Kind == right.Kind && left.Surface == right.Surface &&
                left.Name == right.Name && left.X == right.X && left.Y == right.Y &&
                left.Width == right.Width && left.Height == right.Height &&
                left.Visible == right.Visible && left.Opacity == right.Opacity &&
                left.Scale == right.Scale && left.Text == right.Text &&
                left.Binding == right.Binding && left.FontName == right.FontName &&
                left.FontFile == right.FontFile &&
                left.FontSize == right.FontSize && left.Bold == right.Bold &&
                left.Italic == right.Italic && left.ColorArgb == right.ColorArgb &&
                left.Alignment == right.Alignment &&
                left.WordWrap == right.WordWrap && left.Trimming == right.Trimming &&
                left.ImagePath == right.ImagePath &&
                left.ImageHorizontalPlacement == right.ImageHorizontalPlacement &&
                left.ImageVerticalPlacement == right.ImageVerticalPlacement &&
                left.ImageOffsetX == right.ImageOffsetX &&
                left.ImageOffsetY == right.ImageOffsetY &&
                left.PreviewRotation == right.PreviewRotation &&
                left.HandPivotX == right.HandPivotX &&
                left.HandPivotY == right.HandPivotY;
        }

        private void SetPackageArtwork(string fileName)
        {
            using (var dialog = NewPngDialog("Choose " + fileName))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (fileName == "preview.png") _layout.PackagePreviewImage = dialog.FileName;
                else _layout.PackageIconImage = dialog.FileName;
                SaveLayout(false);
            }
        }

        private void PublishPackageArtwork()
        {
            if (_widgetKind == "recyclebin") return;
            if (_optionalWidgetDesigner != null)
            {
                PublishOptionalPackageArtwork();
                return;
            }
            string folderName = _widgetKind == "weather" ? "Weather" :
                _widgetKind == "clock" ? "Clock" : "Calendar";
            string packageFolder;
            if (File.Exists(Path.Combine(_projectRoot, "EmilyDesk.sln")))
                packageFolder = Path.Combine(_projectRoot, "Widgets", folderName);
            else
                packageFolder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.CommonApplicationData),
                    "EmilyDesk", "DesignerLayouts", "Artwork",
                    folderName, SafeArtworkName(_weatherTheme));
            Directory.CreateDirectory(packageFolder);
            int previewWidth = _widgetKind == "clock" ? 512 : 720;
            int previewHeight = _widgetKind == "clock" ? 512 : 420;
            PublishPackageImage(_layout.PackagePreviewImage,
                Path.Combine(packageFolder, "preview.png"), previewWidth, previewHeight);
            PublishPackageImage(_layout.PackageIconImage,
                Path.Combine(packageFolder, "icon.png"), 256, 256);
        }

        private void PublishOptionalPackageArtwork()
        {
            if (string.IsNullOrWhiteSpace(_optionalWidgetPackageRoot) ||
                !Directory.Exists(_optionalWidgetPackageRoot)) return;
            using (Bitmap snapshot = _canvas.RenderPackageArtwork())
            {
                if (snapshot == null) return;
                using (var preview = new Bitmap(640, 400,
                    PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(preview))
                {
                    graphics.Clear(Color.FromArgb(232, 236, 241));
                    DrawImageToFit(graphics, snapshot,
                        new Rectangle(18, 12, 604, 376));
                    SavePngAtomically(preview, Path.Combine(
                        _optionalWidgetPackageRoot, "preview.png"));
                }
                using (var icon = new Bitmap(256, 256,
                    PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(icon))
                {
                    graphics.Clear(Color.Transparent);
                    DrawImageToFit(graphics, snapshot,
                        new Rectangle(8, 8, 240, 240));
                    SavePngAtomically(icon, Path.Combine(
                        _optionalWidgetPackageRoot, "icon.png"));
                }
            }
        }

        private static void DrawImageToFit(Graphics graphics,
            Image image, Rectangle available)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float scale = Math.Min(
                available.Width / (float)image.Width,
                available.Height / (float)image.Height);
            int width = Math.Max(1,
                (int)Math.Round(image.Width * scale));
            int height = Math.Max(1,
                (int)Math.Round(image.Height * scale));
            var target = new Rectangle(
                available.X + (available.Width - width) / 2,
                available.Y + (available.Height - height) / 2,
                width, height);
            graphics.DrawImage(image, target);
        }

        private void PublishPackageImage(string selectedImage, string destination,
            int width, int height)
        {
            if (!string.IsNullOrWhiteSpace(selectedImage) && File.Exists(selectedImage))
            {
                using (var source = new Bitmap(selectedImage))
                using (var image = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(source, new Rectangle(0, 0, width, height));
                    SavePngAtomically(image, destination);
                }
                return;
            }
            using (var snapshot = _canvas.RenderPackageArtwork())
            {
                if (snapshot == null) return;
                // The live Designer canvas is authoritative. In particular,
                // clock hands can be edited after the reference renderer was
                // created, so using that older renderer here produced stale
                // Gallery and Widget Dock preview images.
                using (var image = new Bitmap(width, height))
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.DrawImage(snapshot, new Rectangle(0, 0, width, height));
                    SavePngAtomically(image, destination);
                }
            }
        }

        private static string SafeArtworkName(string value)
        {
            string result = string.IsNullOrWhiteSpace(value)
                ? "Default" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                result = result.Replace(invalid, '-');
            return result;
        }

        private static void SavePngAtomically(Image image,
            string destination)
        {
            string folder = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(folder);
            string temporary = Path.Combine(folder,
                "." + Path.GetFileName(destination) + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary,
                    FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    image.Save(stream,
                        System.Drawing.Imaging.ImageFormat.Png);
                if (File.Exists(destination))
                    File.Replace(temporary, destination, null);
                else
                    File.Move(temporary, destination);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private void OpenLayout()
        {
            if (_templateProject) { OpenProjectFromMenu(); return; }
            using (var dialog = new OpenFileDialog { Filter = "EmilyDesk layout (*.layout.json)|*.layout.json" })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (!ConfirmSaveLiveEdits()) return;
                _layoutPath = dialog.FileName;
                SetLayout(DesignerLayoutStore.Load(_layoutPath));
            }
        }

        private void NewLayout()
        {
            if (_templateProject) { ShowTemplatePicker(); return; }
            if (!ConfirmSaveLiveEdits()) return;
            _layoutPath = null;
            SetLayout(new DesignerLayout());
        }

        private void LayersKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            int amount = e.Shift ? 10 : 1;
            if (e.KeyCode == Keys.Left) _canvas.NudgeSelected(-amount, 0);
            else if (e.KeyCode == Keys.Right) _canvas.NudgeSelected(amount, 0);
            else if (e.KeyCode == Keys.Up) _canvas.NudgeSelected(0, -amount);
            else if (e.KeyCode == Keys.Down) _canvas.NudgeSelected(0, amount);
            else return;
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private static ToolStripButton NewButton(string text, EventHandler handler)
        {
            var button = new ToolStripButton(text);
            button.Click += handler;
            return button;
        }

        private static Button NewLayerButton(string text, EventHandler handler)
        {
            var button = new Button { Text = text, Dock = DockStyle.Fill, Margin = new Padding(2) };
            button.Click += handler;
            return button;
        }

        private string MakeRelative(string path)
        {
            Uri root = new Uri(_projectRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(root.MakeRelativeUri(new Uri(path)).ToString());
        }

        private static string UniquePath(string directory, string fileName)
        {
            string path = Path.Combine(directory, fileName);
            if (!File.Exists(path)) return path;
            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            int number = 2;
            while (File.Exists(path)) path = Path.Combine(directory, name + "-" + number++ + extension);
            return path;
        }

        private Icon LoadIcon()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "EmilyDeskIconV2.ico");
                return File.Exists(path) ? new Icon(path) : null;
            }
            catch { return null; }
        }
    }
}

