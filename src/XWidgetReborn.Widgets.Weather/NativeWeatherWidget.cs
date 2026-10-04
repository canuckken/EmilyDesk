using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;
using XWidgetReborn.Shared;

namespace XWidgetReborn.Widgets.Weather
{
    public sealed partial class NativeWeatherWidget :
        IWidget,
        IOfficialWidget,
        IWidgetPointerInput,
        IWidgetHotspotProvider,
        IWidgetInputRegionProvider,
        IWidgetThemeProvider,
        IWidgetStyleProvider,
        IRuntimeAwareWidget
    {
        private const string BridgeBase = AppConstants.BridgeBaseUrl;
        private readonly object _sync = new object();
        private readonly object _providerSync = new object();
        private readonly JavaScriptSerializer _json = new JavaScriptSerializer();
        private Action _invalidate;
        private IWidgetHostContext _host;
        private IRuntimeContext _runtime;
        private bool _paused;
        private bool _loading;
        private DateTime _lastAttemptUtc = DateTime.MinValue;
        private DateTime _nextAttemptUtc = DateTime.MinValue;
        private int _consecutiveFailures;
        private DateTime _lastSuccessLocal = DateTime.MinValue;
        private string _locationKey = "54704";
        private string _locationName = "Kentville, Nova Scotia";
        private string _customWeatherPageUrl = string.Empty;
        private string _activeProviderId = "open-meteo";
        private List<WeatherProviderChoice> _weatherProviders =
            DefaultWeatherProviders();
        private bool _providerRefreshInProgress;
        private bool _metric = true;
        private string _condition = "Loading weather...";
        private int _icon = 1;
        private double _temperature;
        private double _feelsLike;
        private bool _hasFeelsLike;
        private int _humidity;
        private bool _hasHumidity;
        private double _windKmh;
        private bool _hasWind;
        private string _windDirection = string.Empty;
        private double _pressureHpa;
        private bool _hasPressure;
        private double _dewPointC;
        private bool _hasDewPoint;
        private double _visibilityKm;
        private bool _hasVisibility;
        private double _uvIndex;
        private bool _hasUvIndex;
        private string _uvText = string.Empty;
        private string _sunrise = string.Empty;
        private string _sunset = string.Empty;
        private double _high;
        private double _low;
        private string _error;
        private readonly List<ForecastDay> _forecastDays = new List<ForecastDay>();
        private bool _panelOpen;
        private bool _toggleHovered;
        private bool _togglePressed;
        private bool _designerReferenceOnly;
        private float _panelProgress;
        private string _animationMode = "Smooth";
        private string _appearance = "Modern";
        private string _style = "Weather.Standard";
        private System.Windows.Forms.Timer _animationTimer;
        private DateTime _animationStartedUtc;
        private float _animationStartProgress;
        private float _animationTargetProgress;
        private int _artDecoAnimationFrameCount;
        private Size _lastRenderSize = new Size(360, 210);
        private readonly SlidePanel _artDecoSlidePanel = CreateArtDecoSlidePanel();
        private readonly SlidePanel _industrialSlidePanel = CreateIndustrialSlidePanel();
        // Steampunk uses the same approved behaviour but a separate panel
        // instance.  Its artwork has different alpha margins, so sharing the
        // Industrial instance would mutate the verified Industrial geometry.
        private readonly SlidePanel _steampunkSlidePanel = CreateIndustrialSlidePanel();
        private IndustrialTypographyLayout _lastIndustrialTypographyLayout;

        private sealed class WeatherProviderChoice
        {
            public string Id;
            public string Name;
            public bool RequiresApiKey;
            public bool HasApiKey;
        }

        private sealed class IndustrialTypographyLayout
        {
            public RectangleF TemperatureBounds;
            public RectangleF ConditionBounds;
            public RectangleF[] MetricLabelBounds;
            public RectangleF[] MetricValueBounds;
            public RectangleF[] ForecastDayBounds;
            public RectangleF[] ForecastIconBounds;
            public RectangleF[] ForecastTemperatureBounds;
            public RectangleF CurrentConditionsRegion;
            public RectangleF MetricsRegion;
            public RectangleF ForecastRegion;
            public RectangleF FooterRegion;
            public RectangleF FooterBounds;
            public float ForecastDividerY;
            public float TemperatureFontPoints;
            public float ConditionFontPoints;
            public float MetricLabelFontPoints;
            public float MetricValueFontPoints;
            public float DayFontPoints;
        }

        private const int CompactWidth = 360;
        private const int PanelWidth = 280;
        private const int PanelOverlap = 18;
        private const int WidgetHeight = 210;
        // Four days remains the starting point for newly-created layouts, but
        // saved layouts are authoritative.  The provider endpoint supplies up
        // to fifteen days, so an existing five-day (or longer) design must not
        // be silently collapsed when it is opened or rendered.
        public const int DefaultForecastDayCount = 4;
        public const int MaximumForecastDayCount = 15;
        private const int ArtDecoWidth = 500;
        // Natural forecast-panel height at the main skin's established
        // horizontal scale: round(1079 * (500 / 1559)).
        private const int ArtDecoSharedHeight = 346;
        private const float ArtDecoMainDisplayScale = 1.06F;
        private const float ArtDecoTargetVisibleOverlap = 32F;
        private const float ArtDecoVerticalVisibleInset = 11F;
        private const float ArtDecoContentInset = 18F;
        private const float ArtDecoPanelAssetWidth = 1458F;
        private const float ArtDecoPanelAssetHeight = 1079F;
        private const float ArtDecoPanelAlphaLeft = 103F;
        private const float ArtDecoPanelAlphaTop = 140F;
        private const float ArtDecoPanelAlphaRight = 1317F;
        private const float ArtDecoPanelAlphaBottom = 920F;
        private const float ArtDecoPanelOrnamentRight = 276F;
        private const float ArtDecoMainAssetWidth = 1510F;
        private const float ArtDecoMainAssetHeight = 1041F;
        private const float ArtDecoMainAlphaLeft = 19F;
        private const float ArtDecoMainAlphaTop = 74F;
        private const float ArtDecoMainAlphaRight = 1487F;
        private const float ArtDecoMainAlphaBottom = 971F;
        private const float ArtDecoDividerY = 184F;
        private const float ArtDecoCanvasWidth = 1441F;
        private const float ArtDecoCanvasHeight = 659F;
        private const int ArtDecoSurfaceWidth = 1081;
        private const int ArtDecoSurfaceHeight = 494;
        // Imported Weather layouts use a 600 x 500 main frame.  This surface
        // maps that frame to approximately 457 x 382 pixels, matching the
        // imported Calendar's visible frame at 100% without changing either
        // widget's Designer coordinates.
        private const int ImportedSurfaceWidth = 1096;
        private const int ImportedSurfaceHeight = 503;
        // Match Woodland calendar's visible frame at 100% (about 503 x 309).
        // Its 548 x 382 presentation includes different transparent margins.
        // Resize only the output surface: the 750 x 500 Designer coordinates,
        // background, content, and slide-panel geometry all remain unchanged.
        private const int WoodlandSurfaceWidth = 1141;
        private const int WoodlandSurfaceHeight = 524;
        private const float ArtDecoMainWidth = 755F;
        private const float ArtDecoMainHeight = 520.5F;
        private const float ArtDecoPanelWidth = 577.18F;
        private const float ArtDecoPanelHeight = 372.73F;
        private const float ArtDecoSlideOffset = 454.35F;
        private static readonly object ArtDecoPanelSkinSync = new object();
        private static Image _scaledArtDecoMainSkin;
        private static Image _scaledMirroredArtDecoPanelSkin;
        private static Image _scaledIndustrialWeatherSkin;
        private static Image _scaledIndustrialPanelSkin;
        private static Image _scaledSteampunkWeatherSkin;
        private static Image _scaledSteampunkPanelSkin;
        private static Image _scaledWoodlandWeatherSkin;
        private static Image _scaledWoodlandPanelSkin;
        private static Image _scaledBotanicalWeatherSkin;
        private static Image _scaledBotanicalPanelSkin;
        private static readonly Dictionary<string, Image> ImportedScaledSkins =
            new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private const string IndustrialWeatherSkinPath =
            @"Assets\Themes\Industrial\industrial-weather-skin.png";
        private const string IndustrialPanelSkinPath =
            @"Assets\Themes\Industrial\industrial-weather-details-panel.png";
        private const string WoodlandWeatherSkinPath =
            @"Assets\Themes\WoodlandNature\woodland-weather-skin.png";
        private const string WoodlandPanelSkinPath =
            @"Assets\Themes\WoodlandNature\woodland-weather-details-panel.png";
        private const string BotanicalWeatherSkinPath =
            @"Assets\Themes\BotanicalNature\botanical-weather-skin.png";
        private const string SteampunkWeatherSkinPath =
            @"Assets\Themes\Steampunk\steampunk-weather-skin.png";
        private static readonly object WeatherSymbolCacheSync = new object();
        private static readonly Dictionary<int, Bitmap> FittedWeatherSymbols =
            new Dictionary<int, Bitmap>();
        private static readonly Dictionary<int, Rectangle> FittedWeatherBounds =
            new Dictionary<int, Rectangle>();
        // Weather icon packs are stored on square transparent canvases.  Drawing
        // the complete canvas makes the visible weather artwork look much smaller
        // than the Designer element, particularly in the compact forecast row.
        // Cache the painted portion of each icon so its intended artwork, rather
        // than the empty border, fills the selected Designer bounds.
        private static readonly object WeatherIconContentBoundsSync = new object();
        private static readonly Dictionary<string, Rectangle>
            WeatherIconContentBounds = new Dictionary<string, Rectangle>(
                StringComparer.OrdinalIgnoreCase);
        private static SlidePanel CreateArtDecoSlidePanel()
        {
            float mainX = ArtDecoCanvasWidth - ArtDecoMainWidth;
            float mainY = (ArtDecoCanvasHeight - ArtDecoMainHeight) / 2F;
            float panelY = (ArtDecoCanvasHeight - ArtDecoPanelHeight) / 2F;
            return new SlidePanel
            {
                CompositionBounds = new RectangleF(
                    0F, 0F, ArtDecoCanvasWidth, ArtDecoCanvasHeight),
                ParentBounds = new RectangleF(
                    mainX, mainY, ArtDecoMainWidth, ArtDecoMainHeight),
                PanelBounds = new RectangleF(
                    mainX, panelY, ArtDecoPanelWidth, ArtDecoPanelHeight),
                OpenDirection = SlidePanelOpenDirection.Left,
                SlideOffset = ArtDecoSlideOffset,
                Duration = TimeSpan.FromMilliseconds(340),
                Easing = SlidePanelEasing.SmoothEaseInOut,
                AutoSlideOnPointerEnter = false,
                ZIndex = -1,
                ClipToParent = true,
                RelativePanelSize = .7645F,
                Scale = .45F
            };
        }

        private static SlidePanel CreateIndustrialSlidePanel()
        {
            const float mainWidth = 750F;
            const float mainHeight = 500F;
            float mainX = ArtDecoCanvasWidth - mainWidth;
            var panel = new SlidePanel
            {
                CompositionBounds = new RectangleF(
                    0F, 0F, ArtDecoCanvasWidth, ArtDecoCanvasHeight),
                ParentBounds = new RectangleF(
                    mainX, (ArtDecoCanvasHeight - mainHeight) / 2F,
                    mainWidth, mainHeight),
                PanelBounds = RectangleF.Empty,
                OpenDirection = SlidePanelOpenDirection.Left,
                Duration = TimeSpan.FromMilliseconds(340),
                Easing = SlidePanelEasing.SmoothEaseInOut,
                AutoSlideOnPointerEnter = false,
                ZIndex = -1,
                ClipToParent = true,
                Scale = 1F
            };
            panel.FitRelativeVisiblePanel(
                IndustrialMainVisibleBoundsForPath(panel.ParentBounds,
                    IndustrialWeatherSkinPath), 52F);
            return panel;
        }

        private static RectangleF IndustrialMainVisibleBoundsForPath(
            RectangleF bounds, string path)
        {
            Image source = ThemeSkinCache.Get(path);
            Rectangle alpha = ThemeSkinCache.GetAlphaBounds(path);
            return new RectangleF(
                bounds.X + alpha.X * bounds.Width / source.Width,
                bounds.Y + alpha.Y * bounds.Height / source.Height,
                alpha.Width * bounds.Width / source.Width,
                alpha.Height * bounds.Height / source.Height);
        }

        // Kept static under this established name because the release verifier
        // validates the Industrial fixed-panel geometry through reflection.
        private static RectangleF IndustrialMainVisibleBounds(RectangleF bounds)
        {
            return IndustrialMainVisibleBoundsForPath(bounds,
                IndustrialWeatherSkinPath);
        }

        private RectangleF IndustrialPresentationMainVisibleBounds(
            RectangleF bounds)
        {
            IndustrialDesignerLayout layout = UsesWoodlandNaturePresentation
                ? NatureEditableWeatherLayout()
                : IndustrialEditableWeatherLayout();
            IndustrialDesignerElement background = layout == null ||
                layout.BackgroundLayerVersion < 1 ? null :
                DesignerBackground(layout, 0);
            if (background != null && background.Visible)
                return new RectangleF(
                    bounds.X + background.Bounds.X * bounds.Width / 750F,
                    bounds.Y + background.Bounds.Y * bounds.Height / 500F,
                    background.Bounds.Width * bounds.Width / 750F,
                    background.Bounds.Height * bounds.Height / 500F);
            return IndustrialMainVisibleBoundsForPath(bounds,
                IndustrialPresentationWeatherSkinPath);
        }

        private RectangleF IndustrialMainVisibleLocalBounds()
        {
            return IndustrialPresentationMainVisibleBounds(
                new RectangleF(0, 0, 750, 500));
        }

        private static Rectangle ScaleRectangle(RectangleF rectangle, float scale)
        {
            return Rectangle.Round(new RectangleF(
                rectangle.X * scale, rectangle.Y * scale,
                rectangle.Width * scale, rectangle.Height * scale));
        }

        private enum ForecastOpeningDirection
        {
            Right,
            Left
        }

        private sealed class WeatherCompositeLayout
        {
            public SizeF CompositeSize { get; set; }
            public RectangleF MainRectangle { get; set; }
            public RectangleF PanelRectangle { get; set; }
            public RectangleF ClipRectangle { get; set; }
            public ForecastOpeningDirection Direction { get; set; }
            public float VisibleOverlap { get; set; }
            public float CanvasOverlap { get; set; }
            public RectangleF MainVisibleAlphaRectangle { get; set; }
            public RectangleF PanelVisibleAlphaRectangle { get; set; }
            public RectangleF SafeContentRectangle { get; set; }
            public float MainContentScale { get; set; }
            public float Progress { get; set; }
        }

        public string Id { get { return "native.weather"; } }
        public string Name { get { return "Weather"; } }
        public string Description { get { return "Official current-weather and forecast widget for EmilyDesk."; } }
        public string Version { get { return "1.2.9"; } }
        public Size DefaultSize { get { return new Size(360, 210); } }
        public Point DefaultLocation { get { return new Point(420, 80); } }
        public WidgetUpdateRate UpdateRate { get { return WidgetUpdateRate.Minute; } }
        public IEnumerable<string> Themes
        {
            get { return EmilyDeskThemeCatalog.Names; }
        }
        public string Theme
        {
            get { return _appearance; }
            set
            {
                string normalized =
                    NormalizeAppearance(value);
                if (_appearance == normalized)
                {
                    SaveSettings();
                    return;
                }
                _appearance = normalized;
                if (IsArtDeco || UsesIndustrialWeatherPresentation)
                    ActiveSlidePanel.SetProgress(_panelOpen ? 1F : 0F);
                SaveSettings();
                if (_host != null)
                {
                    if (IsArtDeco || UsesIndustrialWeatherPresentation)
                        _host.SetFixedCompositionSurface(
                            FixedCompositionSurfaceSize(),
                            FixedCompositionAnchorBounds());
                    else
                        _host.SetPreferredSize(CurrentPreferredSize());
                }
                ApplyWindowShape();
                RequestInvalidate();
            }
        }
        public IEnumerable<WidgetStyleMetadata> Styles
        {
            get
            {
                return new[]
                {
                    new WidgetStyleMetadata(
                        "Weather.Standard",
                        "Standard",
                        "The standard EmilyDesk Weather layout.",
                        "Weather",
                        DefaultSize,
                        new Size(300, 175),
                        "preview.png",
                        new List<string>(
                            EmilyDeskThemeCatalog.Names).ToArray())
                };
            }
        }
        public string Style
        {
            get { return _style; }
            set { ApplyStyle(value); }
        }
        public void ManageStyles()
        {
            ShowSettings();
        }

        private bool IsArtDeco
        {
            get { return _appearance == "Art Deco"; }
        }

        private bool IsVintage
        {
            get { return _appearance == "Vintage"; }
        }

        private bool IsModern
        {
            get { return _appearance == "Modern"; }
        }

        private bool IsIndustrial
        {
            get { return _appearance == "Industrial"; }
        }

        private bool IsSteampunk
        {
            get { return _appearance == "Steampunk"; }
        }

        private bool IsWoodland
        {
            get { return _appearance == "Woodland Nature"; }
        }

        private bool IsBotanical
        {
            get { return _appearance == "Botanical Nature"; }
        }

        private bool IsImportedTheme
        {
            get { return EmilyDeskThemeCatalog.Get(_appearance).IsImported; }
        }

        // Botanical intentionally reuses the approved Woodland renderer and
        // geometry.  The only presentation difference is its asset family.
        private bool UsesWoodlandNaturePresentation
        {
            get { return IsWoodland || IsBotanical; }
        }

        private bool UsesIndustrialWeatherPresentation
        {
            get { return IsImportedTheme || IsIndustrial || IsSteampunk ||
                UsesWoodlandNaturePresentation; }
        }

        private string IndustrialPresentationWeatherSkinPath
        {
            get
            {
                string imported = EmilyDeskThemeCatalog.Get(_appearance).Asset("weather");
                return IsImportedTheme && !string.IsNullOrEmpty(imported) ? imported
                    : IsWoodland ? WoodlandWeatherSkinPath
                    : IsBotanical ? BotanicalWeatherSkinPath
                    : IsSteampunk ? SteampunkWeatherSkinPath
                    : IndustrialWeatherSkinPath;
            }
        }

        private string IndustrialPresentationPanelSkinPath
        {
            // Woodland's main and details frames intentionally share the same
            // wide aspect ratio.  The earlier portrait details image stretched
            // into the slide-out bounds and made its chrome look broken.
            get
            {
                string imported = EmilyDeskThemeCatalog.Get(_appearance).Asset("weatherDetails");
                if (IsImportedTheme && !string.IsNullOrEmpty(imported)) return imported;
                return UsesWoodlandNaturePresentation || IsSteampunk
                ? IndustrialPresentationWeatherSkinPath : IndustrialPanelSkinPath; }
        }

        private string ActiveWeatherIconPack
        {
            get
            {
                string imported = EmilyDeskThemeCatalog.Get(_appearance).Asset("weatherIcons");
                return IsImportedTheme && Directory.Exists(imported) ? imported
                    : DefaultWeatherIconPack(IsWoodland ? "WoodlandNature"
                    : IsBotanical ? "BotanicalNature" : IsSteampunk
                    ? "Steampunk" : "Industrial");
            }
        }

        private IndustrialDesignerLayout IndustrialEditableWeatherLayout()
        {
            return IndustrialDesignerLayout.Current(IsImportedTheme
                ? DesignerLayoutFiles.FileName(_appearance, "weather")
                : IsSteampunk ? "steampunk-weather.layout.json"
                : "industrial-weather.layout.json");
        }

        private SlidePanel ActiveSlidePanel
        {
            get
            {
                return UsesIndustrialWeatherPresentation
                    ? IndustrialSlidePanel : _artDecoSlidePanel;
            }
        }

        private SlidePanel IndustrialSlidePanel
        {
            get { return IsSteampunk ? _steampunkSlidePanel : _industrialSlidePanel; }
        }

        public void AttachHost(IWidgetHostContext host)
        {
            _host = host;
            LoadSettings();
            _locationKey = host.GetSetting("weather.locationKey", "54704");
            _locationName = host.GetSetting("weather.locationName", "Kentville, Nova Scotia");
            _customWeatherPageUrl = (host.GetSetting(
                "weather.customWebPageUrl", string.Empty) ?? string.Empty).Trim();
            _metric = !string.Equals(host.GetSetting("weather.units", "Metric"), "Imperial", StringComparison.OrdinalIgnoreCase);
            _animationMode = NormalizeAnimationMode(
                host.GetSetting("weather.panelAnimation", "Smooth"));
            // Every live Weather widget starts compact. The Designer panel
            // state is a preview aid and must not force the live panel open.
            _panelOpen = false;
            _panelProgress = 0f;
            _artDecoSlidePanel.SetProgress(0F);
            _industrialSlidePanel.SetProgress(0F);
            _steampunkSlidePanel.SetProgress(0F);
            host.SetSetting("weather.panelOpen", "False");
            if (IsArtDeco || UsesIndustrialWeatherPresentation)
                host.SetFixedCompositionSurface(
                    FixedCompositionSurfaceSize(),
                    FixedCompositionAnchorBounds());
            else
                host.SetPreferredSize(CurrentPreferredSize());
            ApplyWindowShape();
            BeginProviderStatusRefresh();
        }

        public void AttachRuntime(IRuntimeContext runtime)
        {
            if (_runtime != null)
                _runtime.Events.Published -= RuntimeEventPublished;

            _runtime = runtime;
            if (_runtime != null)
                _runtime.Events.Published += RuntimeEventPublished;
        }

        private void RuntimeEventPublished(
            object sender,
            WidgetRuntimeEventArgs e)
        {
            if (e != null && e.Topic == "designer.saved")
            {
                ApplyActiveDesignerPanelSettings();
                RequestInvalidate();
                return;
            }
            if (e == null || !string.Equals(
                e.Topic,
                "weather.refresh",
                StringComparison.OrdinalIgnoreCase))
                return;

            WoodlandDesignerLayout.Reload();
            BotanicalDesignerLayout.Reload();
            IndustrialDesignerLayout.Reload();
            ArtDecoDesignerLayout.Reload();
            ApplyActiveDesignerPanelSettings();
            BeginProviderStatusRefresh();
            BeginRefresh(true);
        }

        private RectangleF CompositionAnchorBounds()
        {
            // Screen-edge placement is deliberately based on the approved
            // theme artwork, not on an editable Designer background layer.
            // Background editing must never reinterpret an existing Weather
            // widget's saved desktop position or move its frame off-screen.
            // Imported themes follow the same saved background-layer contract
            // as Calendar. This includes the PNG's small transparent edge so
            // both widgets have identical visual spacing at the screen top.
            if (IsImportedTheme)
                return IndustrialPresentationMainVisibleBounds(
                    IndustrialSlidePanel.ParentBounds);
            return UsesIndustrialWeatherPresentation
                ? IndustrialMainVisibleBoundsForPath(
                    IndustrialSlidePanel.ParentBounds,
                    IndustrialPresentationWeatherSkinPath)
                : ArtDecoMainVisibleBounds(_artDecoSlidePanel.ParentBounds);
        }

        private static RectangleF ArtDecoMainVisibleBounds(RectangleF bounds)
        {
            float scaleX = bounds.Width / ArtDecoMainAssetWidth;
            float scaleY = bounds.Height / ArtDecoMainAssetHeight;
            return RectangleF.FromLTRB(
                bounds.Left + ArtDecoMainAlphaLeft * scaleX,
                bounds.Top + ArtDecoMainAlphaTop * scaleY,
                bounds.Left + ArtDecoMainAlphaRight * scaleX,
                bounds.Top + ArtDecoMainAlphaBottom * scaleY);
        }

        public void Start(Action invalidate)
        {
            IndustrialDesignerLayout.Reload();
            WoodlandDesignerLayout.Reload();
            BotanicalDesignerLayout.Reload();
            ArtDecoDesignerLayout.Reload();
            ApplyActiveDesignerPanelSettings();
            ApplyConfiguredDesignerPanelState();
            _invalidate = invalidate;
            BeginRefresh(true);
        }

        public void Tick(DateTime now)
        {
            if (_paused) return;
            if (DateTime.UtcNow >= _nextAttemptUtc)
                BeginRefresh(false);
        }

        public void Pause() { _paused = true; }
        public void Resume()
        {
            IndustrialDesignerLayout.Reload();
            WoodlandDesignerLayout.Reload();
            BotanicalDesignerLayout.Reload();
            ArtDecoDesignerLayout.Reload();
            ApplyActiveDesignerPanelSettings();
            ApplyWindowShape();
            _paused = false;
            BeginRefresh(false);
        }

        public void Render(Graphics graphics, Rectangle bounds)
        {
            if (graphics == null)
                throw new ArgumentNullException("graphics");
            if (bounds.Width < 1 || bounds.Height < 1)
                return;

            WeatherSnapshot s;
            lock (_sync)
            {
                s = new WeatherSnapshot
                {
                    Location = _locationName,
                    Condition = _condition,
                    Icon = _icon,
                    Temperature = ConvertTemperature(_temperature),
                    FeelsLike = ConvertTemperature(_feelsLike),
                    HasFeelsLike = _hasFeelsLike,
                    Humidity = _humidity,
                    HasHumidity = _hasHumidity,
                    Wind = _metric ? _windKmh : _windKmh * 0.621371,
                    HasWind = _hasWind,
                    WindDirection = _windDirection,
                    Pressure = _metric ? _pressureHpa : _pressureHpa * 0.0295299830714,
                    HasPressure = _hasPressure,
                    DewPoint = ConvertTemperature(_dewPointC),
                    HasDewPoint = _hasDewPoint,
                    Visibility = _metric ? _visibilityKm : _visibilityKm * 0.621371,
                    HasVisibility = _hasVisibility,
                    UvIndex = _uvIndex,
                    HasUvIndex = _hasUvIndex,
                    UvText = _uvText,
                    Sunrise = _sunrise,
                    Sunset = _sunset,
                    High = ConvertTemperature(_high),
                    Low = ConvertTemperature(_low),
                    Loading = _loading,
                    Error = _error,
                    Updated = _lastSuccessLocal,
                    ForecastDays = new List<ForecastDay>(_forecastDays)
                };
            }

            if (IsArtDeco)
            {
                RenderArtDecoWeather(graphics, bounds, s);
                return;
            }
            if (UsesIndustrialWeatherPresentation)
            {
                RenderIndustrialWeather(graphics, bounds, s);
                return;
            }

            GraphicsState state = graphics.Save();
            try
            {
                EmilyDeskTheme theme =
                    EmilyDeskThemeCatalog.Get(_appearance);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                _lastRenderSize = bounds.Size;
                WeatherCompositeLayout layout = CalculateCompositeLayout(_panelProgress);
                float sx = bounds.Width / layout.CompositeSize.Width;
                float sy = bounds.Height / layout.CompositeSize.Height;
                graphics.ScaleTransform(sx, sy);

                // Paint the forecast panel first. The compact card is then
                // painted over its left edge so the panel appears to emerge
                // from behind the main widget rather than sitting beside it.
                if (_panelProgress > 0.001f)
                    DrawForecastPanel(graphics, s, layout);

                IndustrialDesignerLayout editable = EditableCompactWeatherLayout();
                IndustrialDesignerElement editableBackground =
                    DesignerBackground(editable, 0);
                RectangleF mainCard = IsModern || IsVintage
                    ? new RectangleF(5F, 5F, 350F, 200F)
                    : new RectangleF(1F, 1F, 358F, 208F);
                if ((editable == null || !editable.ReplaceDefaultBackground) &&
                    !SuppressDefaultDesignerBackground(editable, 0))
                using (var path = Rounded(mainCard, 24))
                {
                    if (IsModern)
                    {
                        ModernThemePainter.FillGlassCard(
                            graphics, path, mainCard);
                        DrawModernWeatherFrame(graphics);
                    }
                    else if (IsVintage)
                    {
                        VintageThemePainter.FillPaper(
                            graphics, path, mainCard);
                        VintageThemePainter.DrawDoubleBorder(
                            graphics, path,
                            RectangleF.Inflate(mainCard, -8F, -8F), 16F);
                        DrawVintageWeatherFrame(graphics);
                    }
                    else
                    {
                        using (var brush = new LinearGradientBrush(
                            new Rectangle(0, 0, 360, 210),
                            theme.SurfaceTop, theme.SurfaceBottom, 90F))
                        using (var edge = new Pen(theme.Border, 1.5F))
                        {
                            graphics.FillPath(brush, path);
                            graphics.DrawPath(edge, path);
                        }
                    }
                }

                if (editable != null)
                {
                    if (editableBackground != null &&
                        editableBackground.Visible)
                    {
                        RenderEditableDesignerBackground(graphics,
                            editableBackground.Bounds, false,
                            editableBackground.ImagePath,
                            editableBackground.Opacity,
                            editable.BackgroundLayerVersion >= 1);
                    }
                    else DesignerLayerPainter.DrawImage(graphics,
                        editable.BackgroundImage,
                        new RectangleF(0, 0, 360, 210), 1F);
                    DrawEditableWeatherLayers(graphics, s, editable, 0);
                    return;
                }
                DrawThemeWeatherSymbol(graphics,
                    new RectangleF(22, 50, 96, 92), s.Icon);

                string regularFamily = IsVintage ? "Georgia" : "Segoe UI";
                string strongFamily = IsVintage ? "Georgia" : "Segoe UI Semibold";
                Color primaryColor = IsModern
                    ? ModernThemePainter.PrimaryText : theme.PrimaryText;
                Color secondaryColor = IsModern
                    ? ModernThemePainter.SecondaryText : theme.SecondaryText;
                using (var locationFont = new Font(regularFamily, 12f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var locationSmallFont = new Font(regularFamily, 10.5f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var tempFont = new Font(strongFamily, 48f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var conditionFont = new Font(strongFamily, 16f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var detailFont = new Font(regularFamily, 12f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var detailStrongFont = new Font(strongFamily, 11.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var mutedFont = new Font(regularFamily, 9.5f, FontStyle.Italic, GraphicsUnit.Pixel))
                using (var white = new SolidBrush(primaryColor))
                using (var muted = new SolidBrush(secondaryColor))
                using (var secondary = new SolidBrush(secondaryColor))
                using (var warning = new SolidBrush(theme.Accent))
                using (var separator = new Pen(Color.FromArgb(90, theme.Border), 1f))
                {
                    DrawTextFit(graphics, s.Location, locationFont, locationSmallFont, muted, new RectangleF(22, 16, 316, 20));
                    graphics.DrawString(Math.Round(s.Temperature).ToString(CultureInfo.InvariantCulture) + Degree(), tempFont, white, 138, 42);
                    DrawTextFit(graphics, s.Condition, conditionFont, detailStrongFont, white, new RectangleF(140, 99, 198, 23));
                    graphics.DrawString("H " + Math.Round(s.High) + Degree() + "   L " + Math.Round(s.Low) + Degree(), detailFont, muted, 140, 128);

                    float separatorY = IsModern ? 150F : 154F;
                    graphics.DrawLine(separator,
                        22, separatorY, 338, separatorY);
                    if (IsVintage)
                        graphics.DrawLine(separator, 22, 157, 338, 157);
                    float metricY = IsModern
                        ? 153F : IsVintage ? 158F : 162F;
                    DrawDetailColumn(graphics, "Feels", Math.Round(s.FeelsLike) + Degree(), detailFont, detailStrongFont, secondary, white, 22, metricY, 98);
                    DrawDetailColumn(graphics, "Humidity", s.Humidity + "%", detailFont, detailStrongFont, secondary, white, 131, metricY, 98);
                    DrawDetailColumn(graphics, "Wind", Math.Round(s.Wind) + (_metric ? " km/h" : " mph"), detailFont, detailStrongFont, secondary, white, 240, metricY, 98);

                    string footer;
                    Brush footerBrush = secondary;
                    if (!string.IsNullOrEmpty(s.Error))
                    {
                        footer = "Saved weather · refresh unavailable";
                        footerBrush = warning;
                    }
                    else if (s.Loading && s.Updated == DateTime.MinValue)
                        footer = "Updating…";
                    else if (s.Updated != DateTime.MinValue)
                        footer = "Updated " + s.Updated.ToString("h:mm tt", CultureInfo.CurrentCulture);
                    else
                        footer = "Waiting for weather service";
                    graphics.DrawString(footer, mutedFont, footerBrush,
                        22, IsModern ? 188F : IsVintage ? 187F : 193F);
                }

                DrawToggleButton(graphics);
                DrawSnapshotDesignerWeather(graphics, s);
            }
            finally { graphics.Restore(state); }
        }

        private void DrawSnapshotDesignerWeather(Graphics graphics, WeatherSnapshot snapshot)
        {
            IndustrialDesignerLayout layout = IndustrialDesignerLayout.Current(
                DesignerLayoutFiles.FileName(_appearance, "weather"));
            if (layout == null || layout.Elements == null) return;
            DesignerLayerPainter.DrawImage(graphics, layout.BackgroundImage,
                new RectangleF(0, 0, 360, 210), 1F);
            foreach (IndustrialDesignerElement element in layout.Elements)
            {
                if (element == null || !element.Visible || element.Surface != 0) continue;
                if (element.Kind == 0) DrawIndustrialDesignerText(graphics, element,
                    ResolveIndustrialDesignerBinding(element, snapshot));
                else if (element.Kind == 1) DrawIndustrialDesignerWeatherImage(graphics,
                    element, snapshot, layout.WeatherIconPack);
                else if (element.Kind == 2) DrawIndustrialDivider(graphics, layout,
                    element.Id, Pens.Transparent, RectangleF.Empty, false);
            }
        }

        public void RenderIndustrialDesignerReference(
            Graphics graphics, Rectangle bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            var forecast = new List<ForecastDay>();
            for (int index = 0; index < DefaultForecastDayCount; index++)
                forecast.Add(new ForecastDay
                {
                    Date = DateTime.Today.AddDays(index),
                    Icon = index == 2 ? 12 : index == 4 ? 18 : 3,
                    HighC = 24D - index,
                    LowC = 15D - index
                });
            var snapshot = new WeatherSnapshot
            {
                Location = "Port Williams",
                Condition = "Partly Cloudy",
                Icon = 3,
                Temperature = 21D,
                FeelsLike = 20D,
                HasFeelsLike = true,
                Humidity = 68,
                HasHumidity = true,
                Wind = 12D,
                HasWind = true,
                Updated = DateTime.Today.AddHours(9).AddMinutes(30),
                ForecastDays = forecast
            };
            string previousAppearance = _appearance;
            bool previousReferenceOnly = _designerReferenceOnly;
            try
            {
                _appearance = previousAppearance == "Steampunk"
                    ? "Steampunk" : "Industrial";
                _designerReferenceOnly = true;
                DrawIndustrialWeatherMain(graphics, snapshot, bounds);
            }
            finally
            {
                _designerReferenceOnly = previousReferenceOnly;
                _appearance = previousAppearance;
            }
        }

        public void RenderSteampunkDesignerReference(
            Graphics graphics, Rectangle bounds)
        {
            string previousAppearance = _appearance;
            try
            {
                _appearance = "Steampunk";
                RenderIndustrialDesignerReference(graphics, bounds);
            }
            finally { _appearance = previousAppearance; }
        }

        public void RenderWoodlandDesignerReference(
            Graphics graphics, Rectangle bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            var forecast = new List<ForecastDay>();
            for (int index = 0; index < DefaultForecastDayCount; index++)
                forecast.Add(new ForecastDay
                {
                    Date = DateTime.Today.AddDays(index),
                    Icon = index == 2 ? 12 : 3,
                    HighC = 24D - index,
                    LowC = 15D - index
                });
            var snapshot = new WeatherSnapshot
            {
                Location = "Port Williams",
                Condition = "Partly Cloudy",
                Icon = 3,
                Temperature = 21D,
                FeelsLike = 20D,
                HasFeelsLike = true,
                Humidity = 68,
                HasHumidity = true,
                Wind = 12D,
                HasWind = true,
                Updated = DateTime.Today.AddHours(9).AddMinutes(30),
                ForecastDays = forecast
            };
            string previousAppearance = _appearance;
            bool previousReferenceOnly = _designerReferenceOnly;
            try
            {
                _appearance = "Woodland Nature";
                _designerReferenceOnly = true;
                DrawIndustrialWeatherMain(graphics, snapshot, bounds);
            }
            finally
            {
                _designerReferenceOnly = previousReferenceOnly;
                _appearance = previousAppearance;
            }
        }

        public void RenderBotanicalDesignerReference(
            Graphics graphics, Rectangle bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            var forecast = new List<ForecastDay>();
            for (int index = 0; index < DefaultForecastDayCount; index++)
                forecast.Add(new ForecastDay
                {
                    Date = DateTime.Today.AddDays(index),
                    Icon = index == 2 ? 12 : 3,
                    HighC = 24D - index,
                    LowC = 15D - index
                });
            var snapshot = new WeatherSnapshot
            {
                Location = "Port Williams", Condition = "Partly Cloudy",
                Icon = 3, Temperature = 21D, FeelsLike = 20D,
                HasFeelsLike = true, Humidity = 68, HasHumidity = true,
                Wind = 12D, HasWind = true,
                Updated = DateTime.Today.AddHours(9).AddMinutes(30),
                ForecastDays = forecast
            };
            string previousAppearance = _appearance;
            bool previousReferenceOnly = _designerReferenceOnly;
            try
            {
                _appearance = "Botanical Nature";
                _designerReferenceOnly = true;
                DrawIndustrialWeatherMain(graphics, snapshot, bounds);
            }
            finally
            {
                _designerReferenceOnly = previousReferenceOnly;
                _appearance = previousAppearance;
            }
        }

        public void RenderArtDecoDesignerReference(
            Graphics graphics, Rectangle bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            Image skin = ThemeSkinCache.Get(
                @"Assets\Themes\ArtDeco\art-deco-calendar-skin.png");
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(skin, new Rectangle(0, 0, 500, 346));
        }

        public void RenderArtDecoDesignerDetailsReference(
            Graphics graphics, Rectangle bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            Image skin = GetScaledMirroredArtDecoPanelSkin(bounds.Size);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(skin, bounds);
        }

        public void RenderArtDecoDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int horizontalPlacement, int verticalPlacement,
            float offsetX, float offsetY)
        {
            RenderArtDecoDesignerSymbol(graphics, elementId, bounds,
                horizontalPlacement, verticalPlacement, offsetX, offsetY,
                EmilyDeskThemeCatalog.Get("Art Deco").PrimaryText.ToArgb(),
                1F);
        }

        public void RenderArtDecoDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int horizontalPlacement, int verticalPlacement,
            float offsetX, float offsetY, int colorArgb, float opacity)
        {
            RenderArtDecoDesignerSymbol(graphics, elementId, bounds,
                horizontalPlacement, verticalPlacement, offsetX, offsetY,
                colorArgb, opacity, 0F);
        }

        public void RenderArtDecoDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int horizontalPlacement, int verticalPlacement,
            float offsetX, float offsetY, int colorArgb, float opacity,
            float fontSize)
        {
            if (string.Equals(elementId, "panel-button",
                StringComparison.OrdinalIgnoreCase))
            {
                string previousAppearance = _appearance;
                try
                {
                    _appearance = "Art Deco";
                    DrawToggleButton(graphics, bounds,
                        Color.FromArgb(colorArgb), opacity, fontSize);
                }
                finally { _appearance = previousAppearance; }
                return;
            }
            int icon = 3;
            if (!string.IsNullOrEmpty(elementId) &&
                elementId.StartsWith("forecast-icon-",
                    StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(elementId.Substring(
                    "forecast-icon-".Length), out index))
                    icon = index == 2 ? 12 : index == 4 ? 18 : 3;
                DrawWeatherSymbolFitted(graphics, bounds, icon,
                    horizontalPlacement, verticalPlacement,
                    offsetX, offsetY);
                return;
            }
            DrawWeatherSymbolFitted(graphics, bounds, icon,
                horizontalPlacement, verticalPlacement, offsetX, offsetY);
        }

        public void RenderIndustrialDesignerDetailsReference(
            Graphics graphics, Rectangle bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            var snapshot = new WeatherSnapshot
            {
                Pressure = 1013D, HasPressure = true,
                DewPoint = 12D, HasDewPoint = true,
                Visibility = 16D, HasVisibility = true,
                UvIndex = 3D, HasUvIndex = true, UvText = "Moderate",
                Sunrise = "6:15 AM", Sunset = "8:32 PM"
            };
            bool previousReferenceOnly = _designerReferenceOnly;
            try
            {
                _designerReferenceOnly = true;
                DrawIndustrialWeatherDetails(graphics, snapshot, bounds);
            }
            finally { _designerReferenceOnly = previousReferenceOnly; }
        }

        public void RenderSteampunkDesignerDetailsReference(
            Graphics graphics, Rectangle bounds)
        {
            string previousAppearance = _appearance;
            try
            {
                _appearance = "Steampunk";
                RenderIndustrialDesignerDetailsReference(graphics, bounds);
            }
            finally { _appearance = previousAppearance; }
        }

        public void RenderWoodlandDesignerDetailsReference(
            Graphics graphics, Rectangle bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            var snapshot = new WeatherSnapshot
            {
                Pressure = 1013D, HasPressure = true,
                DewPoint = 12D, HasDewPoint = true,
                Visibility = 16D, HasVisibility = true,
                UvIndex = 3D, HasUvIndex = true, UvText = "Moderate",
                Sunrise = "6:15 AM", Sunset = "8:32 PM"
            };
            string previousAppearance = _appearance;
            bool previousReferenceOnly = _designerReferenceOnly;
            try
            {
                _appearance = "Woodland Nature";
                _designerReferenceOnly = true;
                DrawIndustrialWeatherDetails(graphics, snapshot, bounds);
            }
            finally
            {
                _designerReferenceOnly = previousReferenceOnly;
                _appearance = previousAppearance;
            }
        }

        public void RenderBotanicalDesignerDetailsReference(
            Graphics graphics, Rectangle bounds)
        {
            if (graphics == null) throw new ArgumentNullException("graphics");
            var snapshot = new WeatherSnapshot
            {
                Pressure = 1013D, HasPressure = true,
                DewPoint = 12D, HasDewPoint = true,
                Visibility = 16D, HasVisibility = true,
                UvIndex = 3D, HasUvIndex = true, UvText = "Moderate",
                Sunrise = "6:15 AM", Sunset = "8:32 PM"
            };
            string previousAppearance = _appearance;
            bool previousReferenceOnly = _designerReferenceOnly;
            try
            {
                _appearance = "Botanical Nature";
                _designerReferenceOnly = true;
                DrawIndustrialWeatherDetails(graphics, snapshot, bounds);
            }
            finally
            {
                _designerReferenceOnly = previousReferenceOnly;
                _appearance = previousAppearance;
            }
        }

        public void RenderIndustrialDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds)
        {
            RenderIndustrialDesignerSymbol(graphics, elementId, bounds,
                EmilyDeskThemeCatalog.Get("Industrial").PrimaryText.ToArgb(),
                1F);
        }

        public void RenderIndustrialDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int colorArgb, float opacity)
        {
            RenderIndustrialDesignerSymbol(graphics, elementId, bounds,
                colorArgb, opacity, 0F);
        }

        public void RenderIndustrialDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int colorArgb, float opacity, float fontSize)
        {
            if (string.Equals(elementId, "panel-button",
                StringComparison.OrdinalIgnoreCase))
            {
                string previousAppearance = _appearance;
                try
                {
                    _appearance = "Industrial";
                    DrawToggleButton(graphics, bounds,
                        Color.FromArgb(colorArgb), opacity, fontSize);
                }
                finally { _appearance = previousAppearance; }
                return;
            }
            int icon = 3;
            if (!string.IsNullOrEmpty(elementId) &&
                elementId.StartsWith("forecast-icon-", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(elementId.Substring("forecast-icon-".Length), out index))
                    icon = index == 2 ? 12 : index == 4 ? 18 : 3;
                DrawMiniWeatherSymbol(graphics, bounds, icon);
                return;
            }
            DrawWeatherSymbolFitted(graphics, bounds, icon);
        }

        public void RenderSteampunkDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds)
        {
            RenderSteampunkDesignerSymbol(graphics, elementId, bounds,
                EmilyDeskThemeCatalog.Get("Steampunk").PrimaryText.ToArgb(),
                1F);
        }

        public void RenderSteampunkDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int colorArgb, float opacity)
        {
            RenderSteampunkDesignerSymbol(graphics, elementId, bounds,
                colorArgb, opacity, 0F);
        }

        public void RenderSteampunkDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int colorArgb, float opacity, float fontSize)
        {
            string previousAppearance = _appearance;
            try
            {
                _appearance = "Steampunk";
                if (string.Equals(elementId, "panel-button",
                    StringComparison.OrdinalIgnoreCase))
                {
                    DrawToggleButton(graphics, bounds,
                        Color.FromArgb(colorArgb), opacity, fontSize);
                    return;
                }
                RenderIndustrialDesignerSymbol(graphics, elementId, bounds);
            }
            finally { _appearance = previousAppearance; }
        }

        public void RenderWoodlandDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds)
        {
            RenderWoodlandDesignerSymbol(graphics, elementId, bounds,
                EmilyDeskThemeCatalog.Get("Woodland Nature").PrimaryText.ToArgb(),
                1F);
        }

        public void RenderWoodlandDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int colorArgb, float opacity)
        {
            RenderWoodlandDesignerSymbol(graphics, elementId, bounds,
                colorArgb, opacity, 0F);
        }

        public void RenderWoodlandDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int colorArgb, float opacity, float fontSize)
        {
            if (string.Equals(elementId, "panel-button",
                StringComparison.OrdinalIgnoreCase))
            {
                string previousAppearance = _appearance;
                try
                {
                    _appearance = "Woodland Nature";
                    DrawToggleButton(graphics, bounds,
                        Color.FromArgb(colorArgb), opacity, fontSize);
                }
                finally { _appearance = previousAppearance; }
                return;
            }
            if (!string.IsNullOrEmpty(elementId) && elementId.StartsWith(
                "details-icon-", StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(elementId.Substring(
                    "details-icon-".Length), out index))
                {
                    string[] labels = { "Feels Like", "Humidity", "Wind",
                        "Pressure", "Dew Point", "Visibility", "UV Index",
                        "Sunrise / Sunset" };
                    if (index >= 0 && index < labels.Length)
                        DrawWoodlandWeatherDetailIcon(graphics, labels[index],
                            bounds);
                }
                return;
            }
            int icon = 3;
            if (!string.IsNullOrEmpty(elementId) &&
                elementId.StartsWith("forecast-icon-",
                    StringComparison.OrdinalIgnoreCase))
            {
                int index;
                if (int.TryParse(elementId.Substring(
                    "forecast-icon-".Length), out index))
                    icon = index == 2 ? 12 : 3;
                DrawMiniWeatherSymbol(graphics, bounds, icon);
                return;
            }
            DrawWeatherSymbolFitted(graphics, bounds, icon);
        }

        public void RenderBotanicalDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds)
        {
            RenderBotanicalDesignerSymbol(graphics, elementId, bounds,
                EmilyDeskThemeCatalog.Get("Botanical Nature").PrimaryText.ToArgb(),
                1F);
        }

        public void RenderBotanicalDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int colorArgb, float opacity)
        {
            RenderBotanicalDesignerSymbol(graphics, elementId, bounds,
                colorArgb, opacity, 0F);
        }

        public void RenderBotanicalDesignerSymbol(
            Graphics graphics, string elementId, RectangleF bounds,
            int colorArgb, float opacity, float fontSize)
        {
            string previousAppearance = _appearance;
            try
            {
                _appearance = "Botanical Nature";
                RenderWoodlandDesignerSymbol(graphics, elementId, bounds,
                    colorArgb, opacity, fontSize);
            }
            finally { _appearance = previousAppearance; }
        }

        public IEnumerable<WidgetMenuCommand> GetMenuCommands()
        {
            var commands = new List<WidgetMenuCommand>
            {
                new WidgetMenuCommand("Forecast details", delegate { return _panelOpen; }, TogglePanel),
                new WidgetMenuCommand("Animation - Instant", delegate { return string.Equals(_animationMode, "Instant", StringComparison.OrdinalIgnoreCase); }, delegate { SetAnimationMode("Instant"); }),
                new WidgetMenuCommand("Animation - Quick", delegate { return string.Equals(_animationMode, "Quick", StringComparison.OrdinalIgnoreCase); }, delegate { SetAnimationMode("Quick"); }),
                new WidgetMenuCommand("Animation - Smooth", delegate { return string.Equals(_animationMode, "Smooth", StringComparison.OrdinalIgnoreCase); }, delegate { SetAnimationMode("Smooth"); }),
                new WidgetMenuCommand("Animation - Bouncy", delegate { return string.Equals(_animationMode, "Bouncy", StringComparison.OrdinalIgnoreCase); }, delegate { SetAnimationMode("Bouncy"); }),
                new WidgetMenuCommand("Edit in Designer", false, delegate
                {
                    OpenDesigner();
                }),
                new WidgetMenuCommand("Refresh weather", false, delegate { BeginRefresh(true); })
            };
            AddWeatherProviderCommands(commands);
            commands.AddRange(new[]
            {
                new WidgetMenuCommand("Open Weather: Saved custom page", false,
                    delegate { OpenCustomWeatherWebPage(); }),
                new WidgetMenuCommand("Open Weather: Set custom URL...", false,
                    delegate { ConfigureCustomWeatherPage(); }),
                new WidgetMenuCommand("Open Weather: Environment Canada (Canada)", false, delegate { OpenWeatherWebPage("Environment Canada"); }),
                new WidgetMenuCommand("Open Weather: The Weather Network", false, delegate { OpenWeatherWebPage("The Weather Network"); }),
                new WidgetMenuCommand("Open Weather: AccuWeather", false, delegate { OpenWeatherWebPage("AccuWeather"); }),
                new WidgetMenuCommand("Open Weather: meteoblue (Europe)", false, delegate { OpenWeatherWebPage("meteoblue"); }),
                new WidgetMenuCommand("Metric units", delegate { return _metric; }, delegate { SetUnits(true); }),
                new WidgetMenuCommand("Imperial units", delegate { return !_metric; }, delegate { SetUnits(false); })
            });
            return commands;
        }

        private static List<WeatherProviderChoice> DefaultWeatherProviders()
        {
            return new List<WeatherProviderChoice>
            {
                new WeatherProviderChoice { Id = "open-meteo", Name = "Open-Meteo" },
                new WeatherProviderChoice { Id = "met-norway", Name = "MET Norway" },
                new WeatherProviderChoice
                {
                    Id = "environment-canada",
                    Name = "Environment Canada (Canada only)"
                },
                new WeatherProviderChoice
                {
                    Id = "jma-model",
                    Name = "JMA Model (Japan/Asia)"
                },
                new WeatherProviderChoice
                {
                    Id = "gfs-model",
                    Name = "NOAA GFS Model (Americas/global)"
                },
                new WeatherProviderChoice
                {
                    Id = "weatherapi",
                    Name = "WeatherAPI.com",
                    RequiresApiKey = true,
                    HasApiKey = false
                }
            };
        }

        private void AddWeatherProviderCommands(
            List<WidgetMenuCommand> commands)
        {
            List<WeatherProviderChoice> providers;
            lock (_providerSync)
                providers = new List<WeatherProviderChoice>(
                    _weatherProviders);

            foreach (WeatherProviderChoice provider in providers)
            {
                string providerId = provider.Id;
                string providerName = provider.Name +
                    (provider.RequiresApiKey && !provider.HasApiKey
                        ? " (API key required)"
                        : string.Empty);
                commands.Add(new WidgetMenuCommand(
                    "Weather Provider: " + providerName,
                    delegate
                    {
                        return string.Equals(_activeProviderId,
                            providerId,
                            StringComparison.OrdinalIgnoreCase);
                    },
                    delegate { SelectWeatherProvider(providerId); }));
            }
        }

        private void BeginProviderStatusRefresh()
        {
            lock (_providerSync)
            {
                if (_providerRefreshInProgress)
                    return;
                _providerRefreshInProgress = true;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    string raw = DownloadBridge(
                        "/xwidgetbridge/providers.json");
                    IDictionary<string, object> data = AsMap(
                        _json.DeserializeObject(raw));
                    var providers = new List<WeatherProviderChoice>();
                    foreach (object value in AsList(Get(data, "providers")))
                    {
                        IDictionary<string, object> map = AsMap(value);
                        string id = Text(map, "id", string.Empty);
                        string name = Text(map, "name", string.Empty);
                        if (!string.IsNullOrEmpty(id) &&
                            !string.IsNullOrEmpty(name))
                        {
                            providers.Add(new WeatherProviderChoice
                            {
                                Id = id,
                                Name = name,
                                RequiresApiKey = Get(map, "requiresApiKey") != null &&
                                    Convert.ToBoolean(Get(map, "requiresApiKey")),
                                HasApiKey = Get(map, "hasApiKey") != null &&
                                    Convert.ToBoolean(Get(map, "hasApiKey"))
                            });
                        }
                    }

                    if (providers.Count > 0)
                    {
                        lock (_providerSync)
                        {
                            _weatherProviders = providers;
                            _activeProviderId = Text(data,
                                "activeProviderId", "open-meteo");
                        }
                    }
                }
                catch { }
                finally
                {
                    lock (_providerSync)
                        _providerRefreshInProgress = false;
                }
            });
        }

        private void SelectWeatherProvider(string providerId)
        {
            try
            {
                string raw = DownloadBridge(
                    "/xwidgetbridge/provider/select?id=" +
                    Uri.EscapeDataString(providerId));
                IDictionary<string, object> result = AsMap(
                    _json.DeserializeObject(raw));
                if (!string.Equals(Text(result, "status", string.Empty),
                    "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "EmilyDesk could not select that weather provider.");

                lock (_providerSync)
                    _activeProviderId = providerId;
                BeginProviderStatusRefresh();
                BeginRefresh(true);
            }
            catch (Exception ex)
            {
                if (_host != null)
                    _host.ReportDiagnostic(
                        "Weather provider selection failed: " + ex.Message);
            }
        }

        private static string DownloadBridge(string path)
        {
            var request = (HttpWebRequest)WebRequest.Create(
                BridgeBase + path);
            request.Timeout = 1500;
            request.ReadWriteTimeout = 1500;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
                return reader.ReadToEnd();
        }

        private void OpenWeatherWebPage(string service)
        {
            try
            {
                string location = string.IsNullOrWhiteSpace(_locationName)
                    ? "weather"
                    : _locationName;
                string url;
                if (string.Equals(service, "AccuWeather",
                    StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://www.accuweather.com/en/search-locations?query=" +
                        Uri.EscapeDataString(location);
                }
                else if (string.Equals(service, "The Weather Network",
                    StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://www.theweathernetwork.com/";
                }
                else if (string.Equals(service, "meteoblue",
                    StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://www.meteoblue.com/en/weather";
                }
                else
                {
                    url = "https://weather.gc.ca/";
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void OpenCustomWeatherWebPage()
        {
            if (string.IsNullOrWhiteSpace(_customWeatherPageUrl))
            {
                if (!ConfigureCustomWeatherPage()) return;
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _customWeatherPageUrl,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        // Some weather sites require a city search before they expose the
        // actual details page.  Store that final page directly instead of
        // forcing the user through the provider's generic landing page.
        private bool ConfigureCustomWeatherPage()
        {
            using (var form = new Form())
            using (var urlBox = new TextBox())
            using (var save = new Button())
            using (var clear = new Button())
            using (var cancel = new Button())
            {
                form.Text = "Custom Weather Web Page";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.ClientSize = new Size(560, 150);
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var description = new Label
                {
                    AutoSize = false,
                    Left = 18,
                    Top = 16,
                    Width = 524,
                    Height = 34,
                    Text = "Paste the exact weather-details page for your city. " +
                        "EmilyDesk will remember and open this address."
                };
                var label = new Label
                {
                    Text = "Weather page URL",
                    AutoSize = true,
                    Left = 18,
                    Top = 56
                };
                urlBox.Left = 18;
                urlBox.Top = 78;
                urlBox.Width = 524;
                urlBox.Text = _customWeatherPageUrl;
                save.Text = "Save";
                save.Left = 294;
                save.Top = 114;
                save.Width = 76;
                clear.Text = "Clear";
                clear.Left = 378;
                clear.Top = 114;
                clear.Width = 76;
                cancel.Text = "Cancel";
                cancel.Left = 462;
                cancel.Top = 114;
                cancel.Width = 76;

                form.Controls.AddRange(new Control[]
                {
                    description, label, urlBox, save, clear, cancel
                });
                form.AcceptButton = save;
                form.CancelButton = cancel;
                bool saved = false;
                save.Click += delegate
                {
                    string candidate = urlBox.Text.Trim();
                    Uri uri;
                    if (!Uri.TryCreate(candidate, UriKind.Absolute, out uri) ||
                        (uri.Scheme != Uri.UriSchemeHttp &&
                         uri.Scheme != Uri.UriSchemeHttps))
                    {
                        MessageBox.Show(form,
                            "Enter a complete http:// or https:// address.",
                            "Custom Weather Web Page",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    _customWeatherPageUrl = uri.AbsoluteUri;
                    SaveSettings();
                    saved = true;
                    form.Close();
                };
                clear.Click += delegate
                {
                    _customWeatherPageUrl = string.Empty;
                    SaveSettings();
                    form.Close();
                };
                cancel.Click += delegate { form.Close(); };
                form.ShowDialog();
                return saved;
            }
        }

        private void OpenDesigner()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "EmilyDesk.Designer.exe");
                if (!File.Exists(path)) return;
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--widget weather --theme \"" +
                        _appearance + "\"",
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void RenderArtDecoWeatherLegacy(
            Graphics graphics,
            Rectangle bounds,
            WeatherSnapshot snapshot)
        {
            GraphicsState state = graphics.Save();
            try
            {
                _lastRenderSize = bounds.Size;
                WeatherCompositeLayout layout = CalculateCompositeLayout(_panelProgress);
                graphics.ScaleTransform(
                    bounds.Width / layout.CompositeSize.Width,
                    bounds.Height / layout.CompositeSize.Height);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                if (_panelProgress > .001F)
                    DrawArtDecoForecastPanel(
                        graphics, snapshot, layout);
                graphics.TranslateTransform(
                    layout.MainRectangle.X, layout.MainRectangle.Y);
                Image skin = GetScaledArtDecoMainSkin(
                    layout.MainRectangle.Size, null, false);
                graphics.DrawImage(skin,
                    new RectangleF(0F, 0F,
                        layout.MainRectangle.Width,
                        layout.MainRectangle.Height));
                graphics.ScaleTransform(
                    layout.MainContentScale,
                    layout.MainContentScale);

                EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get("Art Deco");
                using (var locationFont = new Font("Georgia", 13F, FontStyle.Bold))
                using (var tempFont = new Font("Georgia", 38F, FontStyle.Bold))
                using (var conditionFont = new Font("Georgia", 11F))
                using (var detailFont = new Font("Segoe UI", 8.5F))
                using (var rangeFont = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold))
                using (var dayFont = new Font("Segoe UI", 8F, FontStyle.Bold))
                using (var primary = new SolidBrush(theme.PrimaryText))
                using (var secondary = new SolidBrush(theme.SecondaryText))
                using (var accent = new Pen(theme.Accent, 1.2F))
                {
                    DrawTextFit(graphics, DisplayCityName(snapshot.Location),
                        locationFont, conditionFont, primary,
                        new RectangleF(60, 70, 225, 22));
                    graphics.DrawString(
                        Math.Round(snapshot.Temperature).ToString(CultureInfo.InvariantCulture) + Degree(),
                        tempFont, primary, 60, 108);
                    DrawTextFit(graphics, snapshot.Condition,
                        conditionFont, detailFont, secondary,
                        new RectangleF(63, 158, 205, 18));
                    DrawMiniWeatherSymbol(graphics,
                        ArtDecoCurrentIconBounds(), snapshot.Icon);
                    graphics.DrawLine(accent, 59, ArtDecoDividerY, 441, ArtDecoDividerY);

                    int count = Math.Min(DefaultForecastDayCount,
                        snapshot.ForecastDays.Count);
                    const float forecastLeft = 61F;
                    const float forecastRegionWidth = 380F;
                    float columnWidth = forecastRegionWidth /
                        DefaultForecastDayCount;
                    float contentWidth = columnWidth * .7631579F;
                    for (int index = 0; index < DefaultForecastDayCount; index++)
                    {
                        float x = forecastLeft + index * columnWidth;
                        if (index > 0)
                            graphics.DrawLine(accent, x - 7, 190, x - 7, 242);
                        if (index >= count)
                        {
                            if (index == 0)
                                graphics.DrawString(snapshot.Loading
                                    ? "Loading forecast..."
                                    : "Forecast unavailable",
                                    detailFont, secondary, x, 205);
                            continue;
                        }
                        ForecastDay day = snapshot.ForecastDays[index];
                        string dayName = day.Date == DateTime.MinValue
                            ? "Day " + (index + 1)
                            : index == 0
                                ? "Today"
                                : day.Date.ToString("ddd", CultureInfo.CurrentCulture);
                        DrawCenteredText(graphics, dayName, dayFont, primary,
                            new RectangleF(x, 190, contentWidth, 15));
                        DrawMiniWeatherSymbol(graphics,
                            new RectangleF(x + (contentWidth - 35F) / 2F,
                                205, 35F, 25), day.Icon);
                        string range = Math.Round(ConvertTemperature(day.HighC)) + "° / " +
                            Math.Round(ConvertTemperature(day.LowC)) + "°";
                        DrawCenteredText(graphics, range, rangeFont, primary,
                            new RectangleF(x, 230, contentWidth, 15));
                    }
                    string footer = !string.IsNullOrEmpty(snapshot.Error)
                        ? "Saved weather - refresh unavailable"
                        : snapshot.Updated == DateTime.MinValue
                            ? "Waiting for weather service"
                            : "Updated " + snapshot.Updated.ToString("h:mm tt", CultureInfo.CurrentCulture);
                    DrawTextFit(graphics, footer, detailFont, detailFont,
                        secondary, new RectangleF(61, 247, 335, 16));
                }
                DrawToggleButton(graphics);
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private void RenderArtDecoWeather(
            Graphics graphics, Rectangle bounds, WeatherSnapshot snapshot)
        {
            ApplyArtDecoDesignerPanelPosition();
            GraphicsState state = graphics.Save();
            try
            {
                _lastRenderSize = bounds.Size;
                graphics.ScaleTransform(
                    bounds.Width / ArtDecoCanvasWidth,
                    bounds.Height / ArtDecoCanvasHeight);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                SlidePanelRenderer.Render(graphics, _artDecoSlidePanel,
                    delegate(Graphics g, RectangleF panelBounds)
                    {
                        DrawArtDecoWeatherDetails(g, snapshot, panelBounds);
                    },
                    delegate(Graphics g, RectangleF parentBounds)
                    {
                        DrawArtDecoWeatherMainLayer(g, snapshot, parentBounds);
                    });
            }
            finally { graphics.Restore(state); }
        }

        private void DrawArtDecoWeatherMainLayer(
            Graphics graphics, WeatherSnapshot snapshot, RectangleF bounds)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                IndustrialDesignerLayout designerLayout =
                    ArtDecoDesignerLayout.Current();
                IndustrialDesignerElement background =
                    DesignerBackground(designerLayout, 0);
                if (!SuppressDefaultDesignerBackground(designerLayout, 0))
                {
                    Image skin = GetScaledArtDecoMainSkin(bounds.Size,
                        designerLayout == null ? null : designerLayout.BackgroundImage,
                        designerLayout != null);
                    if (skin != null)
                        graphics.DrawImage(skin, 0F, 0F,
                            bounds.Width, bounds.Height);
                }
                graphics.ScaleTransform(bounds.Width / 500F, bounds.Height / 346F);
                if (designerLayout != null)
                {
                    if (background != null && background.Visible)
                        RenderEditableDesignerBackground(graphics,
                            background.Bounds, false,
                            background.ImagePath, background.Opacity,
                            designerLayout.BackgroundLayerVersion >= 1);
                    DrawArtDecoDesignerMain(graphics, snapshot,
                        designerLayout);
                    return;
                }
                EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get("Art Deco");
                using (var locationFont = new Font("Georgia", 13F, FontStyle.Bold))
                using (var tempFont = new Font("Georgia", 38F, FontStyle.Bold))
                using (var conditionFont = new Font("Georgia", 11F))
                using (var detailFont = new Font("Segoe UI", 8.5F))
                using (var rangeFont = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold))
                using (var dayFont = new Font("Segoe UI", 8F, FontStyle.Bold))
                using (var primary = new SolidBrush(theme.PrimaryText))
                using (var secondary = new SolidBrush(theme.SecondaryText))
                using (var accent = new Pen(theme.Accent, 1.2F))
                {
                    DrawTextFit(graphics, DisplayCityName(snapshot.Location),
                        locationFont, conditionFont, primary,
                        new RectangleF(60, 70, 225, 22));
                    graphics.DrawString(Math.Round(snapshot.Temperature).ToString(
                        CultureInfo.InvariantCulture) + Degree(), tempFont, primary, 60, 92);
                    DrawTextFit(graphics, snapshot.Condition, conditionFont,
                        detailFont, secondary, new RectangleF(63, 148, 205, 18));
                    RectangleF currentIconBounds = ArtDecoCurrentIconBounds();
                    if (!DrawWeatherIconPack(graphics, currentIconBounds,
                        snapshot.Icon, DefaultWeatherIconPack("ArtDeco"),
                        0, 0, 0F, 0F))
                        DrawMiniWeatherSymbol(graphics, currentIconBounds,
                            snapshot.Icon);
                    graphics.DrawLine(accent, 59, ArtDecoDividerY, 441, ArtDecoDividerY);
                    int count = Math.Min(DefaultForecastDayCount,
                        snapshot.ForecastDays.Count);
                    const float forecastLeft = 61F;
                    const float forecastRegionWidth = 380F;
                    float columnWidth = forecastRegionWidth /
                        DefaultForecastDayCount;
                    float contentWidth = columnWidth * .7631579F;
                    for (int index = 0; index < count; index++)
                    {
                        float x = forecastLeft + index * columnWidth;
                        if (index > 0) graphics.DrawLine(accent, x - 7, 190, x - 7, 242);
                        ForecastDay day = snapshot.ForecastDays[index];
                        string dayName = day.Date == DateTime.MinValue ? "Day " + (index + 1)
                            : index == 0 ? "Today" : day.Date.ToString("ddd", CultureInfo.CurrentCulture);
                        DrawCenteredText(graphics, dayName, dayFont, primary,
                            new RectangleF(x, 190, contentWidth, 15));
                        RectangleF forecastIconBounds =
                            new RectangleF(x + (contentWidth - 35F) / 2F,
                                205, 35F, 25);
                        if (!DrawWeatherIconPack(graphics,
                            forecastIconBounds, day.Icon,
                            DefaultWeatherIconPack("ArtDeco"),
                            0, 0, 0F, 0F))
                            DrawMiniWeatherSymbol(graphics,
                                forecastIconBounds, day.Icon);
                        string range = Math.Round(ConvertTemperature(day.HighC)) + Degree() +
                            " / " + Math.Round(ConvertTemperature(day.LowC)) + Degree();
                        DrawCenteredText(graphics, range, rangeFont, primary,
                            new RectangleF(x, 230, contentWidth, 15));
                    }
                    string footer = !string.IsNullOrEmpty(snapshot.Error)
                        ? "Saved weather - refresh unavailable"
                        : snapshot.Updated == DateTime.MinValue
                            ? "Waiting for weather service"
                            : "Updated " + snapshot.Updated.ToString("h:mm tt", CultureInfo.CurrentCulture);
                    DrawTextFit(graphics, footer, detailFont, detailFont,
                        secondary, new RectangleF(61, 247, 335, 16));
                }
                DrawToggleButton(graphics);
            }
            finally { graphics.Restore(state); }
        }

        private void DrawArtDecoDesignerMain(Graphics graphics,
            WeatherSnapshot snapshot, IndustrialDesignerLayout layout)
        {
            string weatherIconPack = string.IsNullOrWhiteSpace(
                layout.WeatherIconPack)
                ? DefaultWeatherIconPack("ArtDeco")
                : layout.WeatherIconPack;
            int savedForecastDays = SavedForecastDayCount(layout);
            DrawIndustrialDivider(graphics, layout, "main-divider",
                Pens.Transparent, RectangleF.Empty, false);
            for (int index = 1; index < savedForecastDays; index++)
                DrawIndustrialDivider(graphics, layout,
                    "forecast-column-" + index, Pens.Transparent,
                    RectangleF.Empty, false);

            IndustrialDesignerElement icon = layout.Find("current-icon");
            if (icon != null && icon.Visible)
            {
                if (!DrawDesignerPng(graphics, icon) &&
                    !DrawWeatherIconPack(graphics, icon.Bounds,
                        snapshot.Icon, weatherIconPack,
                        icon.ImageHorizontalPlacement,
                        icon.ImageVerticalPlacement,
                        icon.ImageOffsetX, icon.ImageOffsetY))
                    DrawWeatherSymbolFitted(graphics, icon.Bounds, snapshot.Icon,
                        icon.ImageHorizontalPlacement,
                        icon.ImageVerticalPlacement,
                        icon.ImageOffsetX, icon.ImageOffsetY);
            }
            DrawArtDecoDesignerText(graphics, layout, "location",
                DisplayCityName(snapshot.Location));
            DrawArtDecoDesignerText(graphics, layout, "temperature",
                Math.Round(snapshot.Temperature).ToString(
                    CultureInfo.InvariantCulture) + Degree());
            DrawArtDecoDesignerText(graphics, layout, "condition",
                snapshot.Condition);

            int count = Math.Min(savedForecastDays,
                snapshot.ForecastDays.Count);
            for (int index = 0; index < count; index++)
            {
                ForecastDay day = snapshot.ForecastDays[index];
                string dayName = day.Date == DateTime.MinValue
                    ? "Day " + (index + 1)
                    : index == 0 ? "Today"
                    : day.Date.ToString("ddd", CultureInfo.CurrentCulture);
                DrawArtDecoDesignerText(graphics, layout,
                    "forecast-day-" + index, dayName);
                IndustrialDesignerElement forecastIcon =
                    layout.Find("forecast-icon-" + index);
                if (forecastIcon != null && forecastIcon.Visible)
                {
                    if (!DrawDesignerPng(graphics, forecastIcon) &&
                        !DrawWeatherIconPack(graphics, forecastIcon.Bounds,
                            day.Icon, weatherIconPack,
                            forecastIcon.ImageHorizontalPlacement,
                            forecastIcon.ImageVerticalPlacement,
                            forecastIcon.ImageOffsetX,
                            forecastIcon.ImageOffsetY))
                        DrawWeatherSymbolFitted(graphics, forecastIcon.Bounds,
                            day.Icon, forecastIcon.ImageHorizontalPlacement,
                            forecastIcon.ImageVerticalPlacement,
                            forecastIcon.ImageOffsetX, forecastIcon.ImageOffsetY);
                }
                DrawArtDecoDesignerText(graphics, layout,
                    "forecast-range-" + index,
                    Math.Round(ConvertTemperature(day.HighC)) + Degree() +
                    " / " + Math.Round(ConvertTemperature(day.LowC)) +
                    Degree());
                DrawArtDecoDesignerText(graphics, layout,
                    "forecast-condition-" + index, day.Condition);
            }
            string footer = !string.IsNullOrEmpty(snapshot.Error)
                ? "Saved weather - refresh unavailable"
                : snapshot.Updated == DateTime.MinValue
                    ? "Waiting for weather service"
                    : "Updated " + snapshot.Updated.ToString(
                        "h:mm tt", CultureInfo.CurrentCulture);
            DrawArtDecoDesignerText(graphics, layout, "footer", footer);
            foreach (IndustrialDesignerElement element in layout.Elements)
            {
                if (element == null || !element.Visible ||
                    element.Surface != 0 ||
                    IsArtDecoBuiltInElement(element.Id) ||
                    IsDesignerBackground(element, 0)) continue;
                if (element.Kind == 0)
                    DrawIndustrialDesignerText(graphics, element,
                        ResolveArtDecoBinding(element, snapshot));
                else if (element.Kind == 1)
                {
                    if (!DrawDesignerPng(graphics, element) &&
                        string.Equals(element.Binding,
                        "Weather: Current Icon", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!DrawWeatherIconPack(graphics, element.Bounds,
                            snapshot.Icon, weatherIconPack,
                            element.ImageHorizontalPlacement,
                            element.ImageVerticalPlacement,
                            element.ImageOffsetX, element.ImageOffsetY))
                            DrawWeatherSymbolFitted(graphics, element.Bounds,
                                snapshot.Icon, element.ImageHorizontalPlacement,
                                element.ImageVerticalPlacement,
                                element.ImageOffsetX, element.ImageOffsetY);
                    }
                }
                else if (element.Kind == 2)
                    DrawIndustrialDivider(graphics, layout, element.Id,
                        Pens.Transparent, RectangleF.Empty, false);
            }
            IndustrialDesignerElement button = layout.Find("panel-button");
            if (button != null && button.Visible)
                DrawToggleButton(graphics, button.Bounds,
                    Color.FromArgb(button.ColorArgb), button.Opacity,
                    button.FontSize);
        }

        private static int SavedForecastDayCount(
            IndustrialDesignerLayout layout, int surface = 0)
        {
            if (layout == null || layout.Elements == null)
                return DefaultForecastDayCount;
            int count = 0;
            foreach (IndustrialDesignerElement element in layout.Elements)
            {
                if (element == null || !element.Visible ||
                    element.Surface != surface || layout.IsDeleted(element.Id))
                    continue;
                int index = UnboundedForecastIndex(element);
                if (index >= 0 && index < MaximumForecastDayCount &&
                    IsForecastElement(element))
                    count = Math.Max(count, index + 1);
            }
            return count > 0 ? count : DefaultForecastDayCount;
        }

        private static bool IsForecastElement(
            IndustrialDesignerElement element)
        {
            if (element == null) return false;
            string id = element.Id ?? string.Empty;
            return id.IndexOf("forecast-day-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("forecast-icon-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("forecast-range-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("forecast-high-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("forecast-low-", StringComparison.OrdinalIgnoreCase) >= 0 ||
                id.IndexOf("forecast-condition-", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool DrawDesignerPng(Graphics graphics,
            IndustrialDesignerElement element)
        {
            string path = ResolveDesignerAssetPath(element.ImagePath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
            try
            {
                using (Image image = Image.FromFile(path))
                {
                    RectangleF bounds = element.Bounds;
                    GraphicsState state = graphics.Save();
                    try
                    {
                        graphics.InterpolationMode =
                            InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality =
                            CompositingQuality.HighQuality;
                        graphics.DrawImage(image, bounds);
                    }
                    finally { graphics.Restore(state); }
                }
                return true;
            }
            catch { return false; }
        }

        private static bool DrawWeatherIconPack(Graphics graphics,
            RectangleF bounds, int icon, string directory,
            int horizontalPlacement, int verticalPlacement,
            float offsetX, float offsetY)
        {
            if (string.IsNullOrWhiteSpace(directory) ||
                !Directory.Exists(directory)) return false;
            string path = Path.Combine(directory,
                icon.ToString(CultureInfo.InvariantCulture) + ".png");
            if (!File.Exists(path))
                path = Path.Combine(directory,
                    icon.ToString("00", CultureInfo.InvariantCulture) + ".png");
            if (!File.Exists(path)) return false;
            try
            {
                using (Image image = Image.FromFile(path))
                {
                    Rectangle source = GetWeatherIconContentBounds(path, image);
                    // Reserve one rendered pixel inside the Art Deco icon
                    // slot.  Its aged-gold outlines are intentionally thin;
                    // without this band GDI can shave an anti-aliased edge
                    // from tall icons when they are reduced to forecast size.
                    bool artDeco = directory.IndexOf("WeatherIconPacks" +
                        Path.DirectorySeparatorChar + "ArtDeco",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                    RectangleF target = artDeco && bounds.Width > 4F &&
                        bounds.Height > 4F
                        ? RectangleF.FromLTRB(bounds.Left + 1F,
                            bounds.Top + 1F, bounds.Right - 1F,
                            bounds.Bottom - 1F)
                        : bounds;
                    float scale = Math.Min(target.Width / source.Width,
                        target.Height / source.Height);
                    float width = source.Width * scale;
                    float height = source.Height * scale;
                    float x = horizontalPlacement == 1 ? target.X
                        : horizontalPlacement == 2 ? target.Right - width
                        : target.X + (target.Width - width) / 2F;
                    float y = verticalPlacement == 1 ? target.Y
                        : verticalPlacement == 2 ? target.Bottom - height
                        : target.Y + (target.Height - height) / 2F;
                    GraphicsState state = graphics.Save();
                    try
                    {
                        graphics.InterpolationMode =
                            InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.CompositingQuality =
                            CompositingQuality.HighQuality;
                        Rectangle destination = Rectangle.Round(new RectangleF(
                            x + offsetX, y + offsetY, width, height));
                        graphics.DrawImage(image, destination, source.X,
                            source.Y, source.Width, source.Height,
                            GraphicsUnit.Pixel);
                    }
                    finally { graphics.Restore(state); }
                }
                return true;
            }
            catch { return false; }
        }

        private static Rectangle GetWeatherIconContentBounds(string path,
            Image image)
        {
            lock (WeatherIconContentBoundsSync)
            {
                Rectangle cached;
                if (WeatherIconContentBounds.TryGetValue(path, out cached))
                    return cached;

                Rectangle full = new Rectangle(0, 0, image.Width, image.Height);
                Bitmap bitmap = image as Bitmap;
                if (bitmap == null)
                {
                    WeatherIconContentBounds[path] = full;
                    return full;
                }

                int left = image.Width;
                int top = image.Height;
                int right = -1;
                int bottom = -1;
                for (int y = 0; y < image.Height; y++)
                {
                    for (int x = 0; x < image.Width; x++)
                    {
                        // Keep anti-aliased outline pixels but ignore the empty
                        // transparent border around each supplied PNG.
                        if (bitmap.GetPixel(x, y).A <= 8) continue;
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
                }

                // Art Deco's thin gold outlines extend right to the painted
                // alpha boundary.  Keeping only the generic four-pixel crop
                // margin reduced that protection to less than a display pixel
                // in the forecast row, so GDI could visibly shave an edge.
                // Preserve a larger transparent safety band for that pack.
                int padding = path.IndexOf("WeatherIconPacks" +
                    Path.DirectorySeparatorChar + "ArtDeco" +
                    Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) >= 0 ? 12 : 4;
                Rectangle content = right < left || bottom < top ? full
                    : Rectangle.FromLTRB(Math.Max(0, left - padding),
                        Math.Max(0, top - padding),
                        Math.Min(image.Width, right + padding + 1),
                        Math.Min(image.Height, bottom + padding + 1));
                WeatherIconContentBounds[path] = content;
                return content;
            }
        }

        private static string DefaultWeatherIconPack(string themeFolder)
        {
            string packaged = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Assets", "WeatherIconPacks", themeFolder);
            if (Directory.Exists(packaged)) return packaged;
            return Path.GetFullPath(Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "..", "..", "..", "..", "Assets",
                "WeatherIconPacks", themeFolder));
        }

        private static bool IsArtDecoBuiltInElement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return id == "location" || id == "temperature" ||
                id == "condition" || id == "current-icon" ||
                id == "footer" || id == "main-divider" ||
                id == "panel-button" ||
                id.StartsWith("forecast-day-",
                    StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("forecast-icon-",
                    StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("forecast-range-",
                    StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("forecast-condition-",
                    StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("forecast-column-",
                    StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveArtDecoBinding(string binding, string fallback,
            WeatherSnapshot snapshot)
        {
            if (string.Equals(binding, "Weather: Location",
                StringComparison.OrdinalIgnoreCase))
                return DisplayCityName(snapshot.Location);
            if (string.Equals(binding, "Weather: Temperature",
                StringComparison.OrdinalIgnoreCase))
                return Math.Round(snapshot.Temperature) + Degree();
            if (string.Equals(binding, "Weather: Feels Like",
                StringComparison.OrdinalIgnoreCase))
                return "Feels Like " + Math.Round(snapshot.FeelsLike) + Degree();
            if (string.Equals(binding, "Weather: Condition",
                StringComparison.OrdinalIgnoreCase)) return snapshot.Condition;
            if (string.Equals(binding, "Weather: Humidity",
                StringComparison.OrdinalIgnoreCase))
                return "Humidity " + snapshot.Humidity + "%";
            if (string.Equals(binding, "Weather: Wind",
                StringComparison.OrdinalIgnoreCase))
                return "Wind " + snapshot.WindDirection + " " +
                    Math.Round(snapshot.Wind) + (_metric ? " km/h" : " mph");
            if (string.Equals(binding, "Weather: Pressure",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasPressure ? (_metric
                    ? Math.Round(snapshot.Pressure) + " hPa"
                    : snapshot.Pressure.ToString("0.00",
                        CultureInfo.CurrentCulture) + " inHg") : fallback;
            if (string.Equals(binding, "Weather: Visibility",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasVisibility
                    ? snapshot.Visibility.ToString("0.#",
                        CultureInfo.CurrentCulture) +
                        (_metric ? " km" : " mi") : fallback;
            if (string.Equals(binding, "Weather: Dew Point",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasDewPoint
                    ? Math.Round(snapshot.DewPoint) + Degree() : fallback;
            if (string.Equals(binding, "Weather: UV Index",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasUvIndex
                    ? snapshot.UvIndex.ToString("0.#",
                        CultureInfo.CurrentCulture) +
                        (string.IsNullOrWhiteSpace(snapshot.UvText)
                            ? string.Empty : "  " + snapshot.UvText)
                    : fallback;
            if (string.Equals(binding, "Weather: Sunrise and Sunset",
                StringComparison.OrdinalIgnoreCase))
                return (snapshot.Sunrise ?? string.Empty) + " / " +
                    (snapshot.Sunset ?? string.Empty);
            if (string.Equals(binding, "Weather: Updated Time",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.Updated == DateTime.MinValue ? fallback
                    : "Updated " + snapshot.Updated.ToString("h:mm tt",
                        CultureInfo.CurrentCulture);
            return fallback;
        }

        private string ResolveArtDecoBinding(
            IndustrialDesignerElement element, WeatherSnapshot snapshot)
        {
            if (element == null) return string.Empty;
            int index = UnboundedForecastIndex(element);
            if (snapshot.ForecastDays != null && index >= 0 &&
                index < snapshot.ForecastDays.Count)
            {
                ForecastDay day = snapshot.ForecastDays[index];
                if (string.Equals(element.Binding, "Weather: Forecast Day",
                    StringComparison.OrdinalIgnoreCase))
                    return day.Date == DateTime.MinValue
                        ? "Day " + (index + 1)
                        : index == 0 ? "Today" : day.Date.ToString("ddd",
                            CultureInfo.CurrentCulture);
                if (string.Equals(element.Binding,
                    "Weather: Forecast High and Low",
                    StringComparison.OrdinalIgnoreCase))
                    return Math.Round(ConvertTemperature(day.HighC)) +
                        Degree() + " / " +
                        Math.Round(ConvertTemperature(day.LowC)) + Degree();
                if (string.Equals(element.Binding, "Weather: Forecast High",
                    StringComparison.OrdinalIgnoreCase))
                    return Math.Round(ConvertTemperature(day.HighC)) + Degree();
                if (string.Equals(element.Binding, "Weather: Forecast Low",
                    StringComparison.OrdinalIgnoreCase))
                    return Math.Round(ConvertTemperature(day.LowC)) + Degree();
                if (string.Equals(element.Binding,
                    "Weather: Forecast Condition",
                    StringComparison.OrdinalIgnoreCase))
                    return day.Condition;
            }
            return ResolveArtDecoBinding(element.Binding,
                element.Text, snapshot);
        }

        private static void DrawArtDecoDesignerText(Graphics graphics,
            IndustrialDesignerLayout layout, string id, string value)
        {
            IndustrialDesignerElement element = layout.Find(id);
            if (element != null)
                DrawIndustrialDesignerText(graphics, element, value);
        }

        private void DrawArtDecoWeatherDetails(
            Graphics graphics, WeatherSnapshot snapshot, RectangleF panelBounds)
        {
            IndustrialDesignerLayout designerLayout =
                ArtDecoDesignerLayout.Current();
            IndustrialDesignerElement background =
                DesignerBackground(designerLayout, 1);
            if (!SuppressDefaultDesignerBackground(designerLayout, 1))
            {
                Image skin = GetScaledMirroredArtDecoPanelSkin(panelBounds.Size);
                graphics.DrawImage(skin, panelBounds);
            }
            if (HasSafeArtDecoDesignerDetails(designerLayout))
            {
                DrawArtDecoDesignerDetails(
                    graphics, snapshot, panelBounds, designerLayout);
                return;
            }
            RectangleF safe = ArtDecoWeatherDetailsSafeBounds(panelBounds);
            RectangleF titleRectangle = WeatherDetailsTitleRectangle(safe);
            RectangleF dividerRectangle = WeatherDetailsDividerRectangle(safe);
            using (var teal = new Pen(Color.FromArgb(42, 154, 150), 1F))
            using (var titleFont = new Font("Georgia", 15.5F, FontStyle.Bold))
            using (var labelFont = new Font("Segoe UI", 13.25F))
            using (var valueFont = new Font("Segoe UI", 13F, FontStyle.Bold))
            using (var ivory = new SolidBrush(Color.FromArgb(247, 231, 188)))
            using (var muted = new SolidBrush(Color.FromArgb(190, 181, 157)))
            {
                graphics.DrawString("Weather Details", titleFont, ivory, titleRectangle);
                graphics.DrawLine(teal, dividerRectangle.Left, dividerRectangle.Top,
                    dividerRectangle.Right, dividerRectangle.Top);
                List<WeatherDetail> details = BuildWeatherDetails(snapshot);
                for (int index = 0; index < details.Count && index < 8; index++)
                {
                    RectangleF label = WeatherDetailsLabelRectangle(safe, index);
                    RectangleF value = WeatherDetailsValueRectangle(safe, index);
                    graphics.DrawString(details[index].Label, labelFont, muted, label);
                    DrawTextFit(graphics, details[index].Value,
                        valueFont, labelFont, ivory, value);
                    if (index / 2 < 3)
                    {
                        RectangleF separator = WeatherDetailsSeparatorRectangle(safe, index);
                        graphics.DrawLine(teal, separator.Left, separator.Top,
                            separator.Right, separator.Top);
                    }
                }
            }
        }

        private static bool HasSafeArtDecoDesignerDetails(
            IndustrialDesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return false;
            // Moving, resizing, hiding or deleting fields is a valid edit,
            // not a reason to silently replace the entire saved panel.
            return layout.Elements.Exists(delegate(IndustrialDesignerElement item) {
                return item != null && item.Surface == 1;
            }) || (layout.DeletedElementIds != null &&
                layout.DeletedElementIds.Exists(delegate(string id) {
                    return id != null && id.StartsWith("details-", StringComparison.OrdinalIgnoreCase);
                }));
        }

        private void DrawArtDecoDesignerDetails(Graphics graphics,
            WeatherSnapshot snapshot, RectangleF bounds,
            IndustrialDesignerLayout layout)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / 500F,
                    bounds.Height / 346F);
                IndustrialDesignerElement background =
                    DesignerBackground(layout, 1);
                if (background != null && background.Visible)
                    RenderEditableDesignerBackground(graphics,
                        background.Bounds, true,
                        background.ImagePath, background.Opacity,
                        layout.BackgroundLayerVersion >= 1);
                string weatherIconPack = string.IsNullOrWhiteSpace(
                    layout.WeatherIconPack)
                    ? DefaultWeatherIconPack("ArtDeco")
                    : layout.WeatherIconPack;
                foreach (IndustrialDesignerElement element in layout.Elements)
                {
                    if (element == null || !element.Visible ||
                        element.Surface != 1 ||
                        IsDesignerBackground(element, 1)) continue;
                    if (element.Kind == 0)
                        DrawIndustrialDesignerText(graphics, element,
                            ResolveArtDecoDetailBinding(element, snapshot));
                    else if (element.Kind == 1)
                    {
                        if (!DrawDesignerPng(graphics, element) &&
                            string.Equals(element.Binding,
                                "Weather: Current Icon",
                                StringComparison.OrdinalIgnoreCase))
                        {
                            if (!DrawWeatherIconPack(graphics, element.Bounds,
                                snapshot.Icon, weatherIconPack,
                                element.ImageHorizontalPlacement,
                                element.ImageVerticalPlacement,
                                element.ImageOffsetX, element.ImageOffsetY))
                                DrawWeatherSymbolFitted(graphics,
                                    element.Bounds, snapshot.Icon,
                                    element.ImageHorizontalPlacement,
                                    element.ImageVerticalPlacement,
                                    element.ImageOffsetX,
                                    element.ImageOffsetY);
                        }
                    }
                    else if (element.Kind == 2)
                        DrawIndustrialDivider(graphics, layout, element.Id,
                            Pens.Transparent, RectangleF.Empty, false);
                }
            }
            finally { graphics.Restore(state); }
        }

        private string ResolveArtDecoDetailBinding(string binding,
            string fallback, WeatherSnapshot snapshot)
        {
            if (string.Equals(binding, "Weather: Feels Like",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasFeelsLike
                    ? Math.Round(snapshot.FeelsLike) + Degree() : fallback;
            if (string.Equals(binding, "Weather: Humidity",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasHumidity
                    ? snapshot.Humidity + "%" : fallback;
            if (string.Equals(binding, "Weather: Wind",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasWind
                    ? (string.IsNullOrWhiteSpace(snapshot.WindDirection)
                        ? string.Empty : snapshot.WindDirection + "  ") +
                      Math.Round(snapshot.Wind) +
                      (_metric ? " km/h" : " mph")
                    : fallback;
            return ResolveIndustrialDetailBinding(
                binding, fallback, snapshot);
        }

        private string ResolveArtDecoDetailBinding(
            IndustrialDesignerElement element, WeatherSnapshot snapshot)
        {
            if (element == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(element.Binding) &&
                element.Binding.StartsWith("Weather: Forecast",
                    StringComparison.OrdinalIgnoreCase))
                return ResolveArtDecoBinding(element, snapshot);
            return ResolveArtDecoDetailBinding(element.Binding,
                element.Text, snapshot);
        }

        private static void DrawCenteredText(
            Graphics graphics,
            string text,
            Font font,
            Brush brush,
            RectangleF bounds)
        {
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
                graphics.DrawString(text, font, brush, bounds, format);
        }

        private void RenderIndustrialWeather(
            Graphics graphics, Rectangle bounds, WeatherSnapshot snapshot)
        {
            ApplyIndustrialDesignerPanelSettings();
            GraphicsState state = graphics.Save();
            try
            {
                _lastRenderSize = bounds.Size;
                graphics.ScaleTransform(bounds.Width / ArtDecoCanvasWidth,
                    bounds.Height / ArtDecoCanvasHeight);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                SlidePanelRenderer.Render(graphics, IndustrialSlidePanel,
                    delegate(Graphics g, RectangleF panelBounds)
                    {
                        DrawIndustrialWeatherDetails(g, snapshot, panelBounds);
                    },
                    delegate(Graphics g, RectangleF parentBounds)
                    {
                        DrawIndustrialWeatherMain(g, snapshot, parentBounds);
                    });
            }
            finally { graphics.Restore(state); }
        }

        private void ApplyIndustrialDesignerPanelSettings()
        {
            IndustrialDesignerLayout layout = UsesWoodlandNaturePresentation
                ? NatureEditableWeatherLayout() : IndustrialEditableWeatherLayout();
            IndustrialDesignerPanelSettings settings = layout == null
                ? null : layout.WeatherDetailsPanel;
            IndustrialSlidePanel.OpenPositionOffsetX = 0F;
            IndustrialSlidePanel.OpenPositionOffsetY = 0F;
            IndustrialSlidePanel.FitRelativeVisiblePanel(
                IndustrialPresentationMainVisibleBounds(
                    IndustrialSlidePanel.ParentBounds),
                settings == null ? 52F : Math.Max(0F, settings.Offset));
            if (settings == null) return;
            IndustrialSlidePanel.OpenDirection =
                (SlidePanelOpenDirection)Math.Max(0,
                    Math.Min(3, settings.Direction));
            // Direction and placement belong to the Designer layout. The
            // user's Animation choice belongs to widget state and must not be
            // overwritten on every render by the theme's saved preview mode.
            IndustrialSlidePanel.OpenPositionOffsetX = settings.PositionX;
            IndustrialSlidePanel.OpenPositionOffsetY = settings.PositionY;
        }

        private void ApplyActiveDesignerPanelSettings()
        {
            if (IsArtDeco)
                ApplyArtDecoDesignerPanelPosition();
            else if (UsesIndustrialWeatherPresentation)
                ApplyIndustrialDesignerPanelSettings();
        }

        private void ApplyConfiguredDesignerPanelState()
        {
            if (!IsArtDeco && !UsesIndustrialWeatherPresentation) return;
            IndustrialDesignerLayout layout = UsesIndustrialWeatherPresentation
                ? (UsesWoodlandNaturePresentation ? NatureEditableWeatherLayout() : IndustrialEditableWeatherLayout())
                : ArtDecoDesignerLayout.Current();
            IndustrialDesignerPanelSettings settings = layout == null
                ? null : layout.WeatherDetailsPanel;
            bool open = DesignerPanelStartsOpen(settings);
            _panelOpen = open;
            _panelProgress = open ? 1F : 0F;
            ActiveSlidePanel.SetProgress(_panelProgress);
            if (_host != null)
                _host.SetSetting("weather.panelOpen",
                    open ? "True" : "False");
        }

        private static bool DesignerPanelStartsOpen(
            IndustrialDesignerPanelSettings settings)
        {
            // The Designer state controls only the editing preview. Every live
            // Weather widget starts compact and opens its details on demand.
            return false;
        }

        private void ApplyArtDecoDesignerPanelPosition()
        {
            IndustrialDesignerLayout layout = ArtDecoDesignerLayout.Current();
            IndustrialDesignerPanelSettings settings = layout == null
                ? null : layout.WeatherDetailsPanel;
            float mainX = ArtDecoCanvasWidth - ArtDecoMainWidth;
            float panelY = (ArtDecoCanvasHeight - ArtDecoPanelHeight) / 2F;
            _artDecoSlidePanel.PanelBounds = new RectangleF(
                mainX, panelY, ArtDecoPanelWidth, ArtDecoPanelHeight);
            _artDecoSlidePanel.SlideOffset = ArtDecoSlideOffset;
            _artDecoSlidePanel.OpenPositionOffsetX = settings == null
                ? 0F : settings.PositionX;
            _artDecoSlidePanel.OpenPositionOffsetY = settings == null
                ? 0F : settings.PositionY;
        }

        private void DrawIndustrialWeatherMain(
            Graphics graphics, WeatherSnapshot snapshot, RectangleF bounds)
        {
            RectangleF visibleBounds = IndustrialPresentationMainVisibleBounds(
                bounds);
            IndustrialDesignerLayout designerLayout = IsWoodland
                ? WoodlandDesignerLayout.Current() : IsBotanical
                ? BotanicalDesignerLayout.Current() : IndustrialDesignerLayout.Current(
                    DesignerLayoutFiles.FileName(_appearance, "weather"));
            if (_designerReferenceOnly) designerLayout = null;
            IndustrialDesignerElement background =
                DesignerBackground(designerLayout, 0);
            if (!SuppressDefaultDesignerBackground(designerLayout, 0) &&
                (designerLayout == null || string.Equals(
                (designerLayout.BackgroundImage ?? "").Replace('/', '\\'),
                IndustrialPresentationWeatherSkinPath.Replace('/', '\\'),
                StringComparison.OrdinalIgnoreCase)))
                graphics.DrawImage(GetScaledIndustrialWeatherSkin(visibleBounds.Size), visibleBounds);
            else if (background == null &&
                !SuppressDefaultDesignerBackground(designerLayout, 0))
                DesignerLayerPainter.DrawImage(graphics, designerLayout.BackgroundImage, bounds, 1F);
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / 750F, bounds.Height / 500F);
                if (background != null && background.Visible)
                    RenderEditableDesignerBackground(graphics,
                        background.Bounds, false,
                        background.ImagePath, background.Opacity,
                        designerLayout.BackgroundLayerVersion >= 1);
                EmilyDeskTheme activeTheme = EmilyDeskThemeCatalog.Get(_appearance);
                Color ivory = IsImportedTheme ? activeTheme.PrimaryText : IsBotanical
                    ? Color.FromArgb(37, 63, 51) : Color.FromArgb(244, 228, 192);
                Color grey = IsImportedTheme ? activeTheme.SecondaryText : IsBotanical
                    ? Color.FromArgb(99, 117, 91) : Color.FromArgb(166, 170, 169);
                Color amber = IsImportedTheme ? activeTheme.Accent : IsBotanical
                    ? Color.FromArgb(210, 126, 139) : Color.FromArgb(224, 157, 39);
                string weatherIconPack = designerLayout == null ||
                    string.IsNullOrWhiteSpace(designerLayout.WeatherIconPack)
                    ? ActiveWeatherIconPack
                    : designerLayout.WeatherIconPack;
                if (!_designerReferenceOnly && UsesWoodlandNaturePresentation && designerLayout != null)
                {
                    DrawEditableWeatherLayers(graphics, snapshot, designerLayout, 0);
                    if (IsBotanical)
                        DrawBotanicalMainFrame(graphics,
                            IndustrialMainVisibleLocalBounds(), 10F, 28F);
                    return;
                }
                using (var cityFont = new Font("Segoe UI Semibold", 21F, FontStyle.Regular))
                using (var tempFont = new Font("Segoe UI Semibold", 50F, FontStyle.Bold))
                using (var conditionFont = new Font("Segoe UI Semibold", 16F))
                using (var detailFont = new Font("Segoe UI Semibold", 13F))
                using (var detailBold = new Font("Segoe UI Semibold", 14F, FontStyle.Bold))
                using (var dayFont = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold))
                using (var rangeFont = new Font("Segoe UI Semibold", 13.5F, FontStyle.Bold))
                using (var footerFont = new Font(
                    "Segoe UI Semibold", 11.5F, FontStyle.Regular))
                using (var primary = new SolidBrush(ivory))
                using (var secondary = new SolidBrush(grey))
                using (var metricLabelBrush = new SolidBrush(
                    IsBotanical ? Color.FromArgb(99, 117, 91)
                    : Color.FromArgb(224, 214, 194)))
                using (var footerBrush = new SolidBrush(
                    IsBotanical ? Color.FromArgb(99, 117, 91)
                    : Color.FromArgb(211, 204, 185)))
                using (var accent = new Pen(amber, 1.5F))
                {
                    RectangleF visible = IndustrialMainVisibleLocalBounds();
                    RectangleF inner = RectangleF.FromLTRB(
                        visible.Left + 30F, visible.Top + 24F,
                        visible.Right - 30F, visible.Bottom - 22F);
                    float verticalDivider = inner.Left + inner.Width * .42F;
                    // These four bands are intentionally fixed. Typography in the
                    // upper bands must never move the forecast or footer.
                    float forecastDivider = inner.Bottom - 101.67F;
                    RectangleF currentConditionsRegion = RectangleF.FromLTRB(
                        inner.Left, inner.Top, inner.Right, inner.Top + 170F);
                    RectangleF metricsRegion = RectangleF.FromLTRB(
                        verticalDivider + 24F, inner.Top + 170F,
                        inner.Right, forecastDivider);
                    // Woodland has no bottom footer text: its update stamp is
                    // deliberately in the upper-right.  Give that unused
                    // footer space to the forecast so the icons can grow and
                    // the temperature ranges can sit clearly underneath.
                    float footerTop = inner.Bottom -
                        (UsesWoodlandNaturePresentation ? 20F : 35.67F);
                    RectangleF forecastRegion = RectangleF.FromLTRB(
                        inner.Left, forecastDivider, inner.Right, footerTop);
                    RectangleF footerRegion = RectangleF.FromLTRB(
                        inner.Left, footerTop, inner.Right, inner.Bottom);
                    RectangleF iconSection = new RectangleF(
                        inner.Left + 10F, inner.Top + 14.6F,
                        verticalDivider - inner.Left - 28F, 148.8F);
                    if (!_designerReferenceOnly && designerLayout != null)
                        DrawAllIndustrialDesignerMainDividers(
                            graphics, designerLayout);
                    IndustrialDesignerElement designerIcon =
                        designerLayout == null ? null : designerLayout.Find("current-icon");
                    if (!_designerReferenceOnly)
                    {
                        if (designerIcon == null)
                        {
                            if (!DrawWeatherIconPack(graphics, iconSection,
                                snapshot.Icon, weatherIconPack, 0, 0, 0F, 0F))
                                DrawWeatherSymbolFitted(graphics, iconSection,
                                    snapshot.Icon);
                        }
                        else if (designerIcon.Visible)
                        {
                            if (!DrawDesignerPng(graphics, designerIcon) && !DrawWeatherIconPack(graphics,
                                designerIcon.Bounds, snapshot.Icon,
                                weatherIconPack,
                                designerIcon.ImageHorizontalPlacement,
                                designerIcon.ImageVerticalPlacement,
                                designerIcon.ImageOffsetX, designerIcon.ImageOffsetY))
                                DrawWeatherSymbolFitted(graphics,
                                    designerIcon.Bounds, snapshot.Icon,
                                    designerIcon.ImageHorizontalPlacement,
                                    designerIcon.ImageVerticalPlacement,
                                    designerIcon.ImageOffsetX, designerIcon.ImageOffsetY);
                        }
                    }
                    if (designerLayout == null)
                        DrawIndustrialDivider(graphics, null,
                            "main-divider", accent,
                            new RectangleF(verticalDivider, inner.Top + 10F,
                                2F, forecastDivider - 18F -
                                    (inner.Top + 10F)),
                            _designerReferenceOnly);
                    float infoLeft = verticalDivider + 24F;
                    float infoWidth = inner.Right - infoLeft;
                    RectangleF cityBounds = new RectangleF(
                        infoLeft, inner.Top + 10.67F,
                        UsesWoodlandNaturePresentation ? infoWidth - 132F : infoWidth, 30F);
                    IndustrialDesignerElement designerLocation =
                        designerLayout == null ? null : designerLayout.Find("location");
                    if (!_designerReferenceOnly)
                    {
                        if (designerLocation == null)
                            DrawTextFit(graphics, DisplayCityName(snapshot.Location),
                                cityFont, conditionFont, primary, cityBounds);
                        else
                            DrawIndustrialDesignerText(graphics, designerLocation,
                                DisplayCityName(snapshot.Location));
                    }
                    string temperatureText = Math.Round(snapshot.Temperature).ToString(
                        CultureInfo.InvariantCulture) + Degree();
                    SizeF temperatureSize = MeasureTypographic(
                        graphics, temperatureText, tempFont);
                    RectangleF temperatureBounds = new RectangleF(
                        infoLeft, inner.Top + 39F,
                        Math.Min(infoWidth, temperatureSize.Width),
                        temperatureSize.Height);
                    IndustrialDesignerElement designerTemperature =
                        designerLayout == null ? null : designerLayout.Find("temperature");
                    if (_designerReferenceOnly)
                    {
                    }
                    else if (designerTemperature == null)
                        graphics.DrawString(temperatureText, tempFont, primary,
                            temperatureBounds.Location,
                            StringFormat.GenericTypographic);
                    else
                    {
                        temperatureBounds = designerTemperature.Bounds;
                        DrawIndustrialDesignerText(
                            graphics, designerTemperature, temperatureText);
                    }
                    string conditionText = snapshot.Condition ?? string.Empty;
                    Font selectedConditionFont =
                        MeasureTypographic(graphics, conditionText,
                            conditionFont).Width <= infoWidth
                        ? conditionFont : detailBold;
                    SizeF conditionSize = MeasureTypographic(
                        graphics, conditionText, selectedConditionFont);
                    RectangleF conditionBounds = new RectangleF(
                        infoLeft, temperatureBounds.Bottom + 8.97F,
                        Math.Min(infoWidth, conditionSize.Width),
                        conditionSize.Height);
                    IndustrialDesignerElement designerCondition =
                        designerLayout == null ? null : designerLayout.Find("condition");
                    if (_designerReferenceOnly)
                    {
                    }
                    else if (designerCondition == null)
                        graphics.DrawString(conditionText, selectedConditionFont,
                            primary, conditionBounds.Location,
                            StringFormat.GenericTypographic);
                    else
                    {
                        conditionBounds = designerCondition.Bounds;
                        DrawIndustrialDesignerText(
                            graphics, designerCondition, conditionText);
                    }
                    float metricLabelHeight = MeasureTypographic(
                        graphics, "Humidity", detailFont).Height;
                    float metricValueHeight = MeasureTypographic(
                        graphics, "100%", detailBold).Height;
                    float detailsY = Math.Max(metricsRegion.Top + 3F,
                        conditionBounds.Bottom + 12.50F);
                    float metricValueTop = detailsY +
                        metricLabelHeight + 7.12F;
                    float detailWidth = (infoWidth - 20F) / 3F;
                    float[] metricOffsets = { 0F, -5F, -10F };
                    string feelsLikeValue = snapshot.HasFeelsLike
                        ? Math.Round(snapshot.FeelsLike) + Degree() : "—";
                    string humidityValue = snapshot.HasHumidity
                        ? snapshot.Humidity + "%" : "—";
                    string windValue = snapshot.HasWind
                        ? Math.Round(snapshot.Wind) +
                            (_metric ? " km/h" : " mph") : "—";
                    if (!_designerReferenceOnly)
                    {
                        DrawIndustrialDesignerMetric(graphics, designerLayout,
                            "feels-like", "Feels Like", feelsLikeValue,
                            detailFont, detailBold, metricLabelBrush, primary,
                            infoLeft + metricOffsets[0], detailsY, detailWidth);
                        DrawIndustrialDesignerMetric(graphics, designerLayout,
                            "humidity", "Humidity", humidityValue,
                            detailFont, detailBold, metricLabelBrush, primary,
                            infoLeft + detailWidth + 10F + metricOffsets[1],
                            detailsY, detailWidth);
                        DrawIndustrialDesignerMetric(graphics, designerLayout,
                            "wind", "Wind", windValue,
                            detailFont, detailBold, metricLabelBrush, primary,
                            infoLeft + (detailWidth + 10F) * 2F + metricOffsets[2],
                            detailsY, detailWidth);
                    }
                    var metricLabelBounds = new RectangleF[3];
                    var metricValueBounds = new RectangleF[3];
                    for (int metricIndex = 0; metricIndex < 3; metricIndex++)
                    {
                        float metricX = infoLeft +
                            metricIndex * (detailWidth + 10F) +
                            metricOffsets[metricIndex];
                        metricLabelBounds[metricIndex] = new RectangleF(
                            metricX, detailsY, detailWidth, metricLabelHeight);
                        metricValueBounds[metricIndex] = new RectangleF(
                            metricX, metricValueTop,
                            detailWidth, metricValueHeight);
                    }
                    int displayedForecastDays = designerLayout == null
                        ? DefaultForecastDayCount
                        : SavedForecastDayCount(designerLayout);
                    var forecastDayBounds =
                        new RectangleF[displayedForecastDays];
                    var forecastIconBounds =
                        new RectangleF[displayedForecastDays];
                    var forecastTemperatureBounds =
                        new RectangleF[displayedForecastDays];
                    float columnWidth = forecastRegion.Width /
                        displayedForecastDays;
                    for (int forecastIndex = 0;
                        forecastIndex < displayedForecastDays; forecastIndex++)
                    {
                        float forecastX = forecastRegion.Left +
                            forecastIndex * columnWidth;
                        forecastDayBounds[forecastIndex] = new RectangleF(
                            forecastX, forecastRegion.Top + 2F,
                            columnWidth, 17F);
                        float iconWidth = UsesWoodlandNaturePresentation ? 44F : 32F;
                        float iconHeight = UsesWoodlandNaturePresentation ? 40F : 28F;
                        forecastIconBounds[forecastIndex] = new RectangleF(
                            forecastX + (columnWidth - iconWidth) / 2F,
                            forecastRegion.Top + 20F,
                            iconWidth, iconHeight);
                        forecastTemperatureBounds[forecastIndex] = new RectangleF(
                            forecastX, forecastRegion.Top +
                                (UsesWoodlandNaturePresentation ? 62F : 49F),
                            columnWidth, UsesWoodlandNaturePresentation ? 18F : 16F);
                    }
                    RectangleF footerBounds = new RectangleF(
                        footerRegion.Left + 41F, footerRegion.Top,
                        footerRegion.Width - 76F, footerRegion.Height - 5F);
                    _lastIndustrialTypographyLayout =
                        new IndustrialTypographyLayout
                        {
                            TemperatureBounds = temperatureBounds,
                            ConditionBounds = conditionBounds,
                            MetricLabelBounds = metricLabelBounds,
                            MetricValueBounds = metricValueBounds,
                            ForecastDayBounds = forecastDayBounds,
                            ForecastIconBounds = forecastIconBounds,
                            ForecastTemperatureBounds = forecastTemperatureBounds,
                            CurrentConditionsRegion = currentConditionsRegion,
                            MetricsRegion = metricsRegion,
                            ForecastRegion = forecastRegion,
                            FooterRegion = footerRegion,
                            FooterBounds = footerBounds,
                            ForecastDividerY = forecastDivider,
                            TemperatureFontPoints = tempFont.SizeInPoints,
                            ConditionFontPoints = conditionFont.SizeInPoints,
                            MetricLabelFontPoints = detailFont.SizeInPoints,
                            MetricValueFontPoints = detailBold.SizeInPoints,
                            DayFontPoints = dayFont.SizeInPoints
                        };
                    if (designerLayout == null)
                        DrawIndustrialDivider(graphics, null,
                            "forecast-divider", accent,
                            new RectangleF(inner.Left, forecastDivider,
                                inner.Width, 2F), _designerReferenceOnly);

                    int count = Math.Min(displayedForecastDays,
                        snapshot.ForecastDays.Count);
                    for (int index = 0; index < displayedForecastDays; index++)
                    {
                        float x = forecastRegion.Left + index * columnWidth;
                        if (index > 0 && designerLayout == null)
                            DrawIndustrialDivider(graphics, null,
                                "forecast-column-" + index, accent,
                                new RectangleF(x, forecastRegion.Top + 8F,
                                    2F, forecastRegion.Height - 12F),
                                _designerReferenceOnly);
                        if (index >= count) continue;
                        ForecastDay day = snapshot.ForecastDays[index];
                        string name = day.Date == DateTime.MinValue ? "Day " + (index + 1)
                            : index == 0 ? "Today" : day.Date.ToString("ddd",
                                CultureInfo.CurrentCulture);
                        IndustrialDesignerElement designerDay = designerLayout == null
                            ? null : designerLayout.Find("forecast-day-" + index);
                        IndustrialDesignerElement designerForecastIcon = designerLayout == null
                            ? null : designerLayout.Find("forecast-icon-" + index);
                        string range = Math.Round(ConvertTemperature(day.HighC)) + Degree() +
                            " / " + Math.Round(ConvertTemperature(day.LowC)) + Degree();
                        IndustrialDesignerElement designerRange = designerLayout == null
                            ? null : designerLayout.Find("forecast-range-" + index);
                        IndustrialDesignerElement forecastDesignerCondition = designerLayout == null
                            ? null : designerLayout.Find("forecast-condition-" + index);
                        if (!_designerReferenceOnly)
                        {
                            if (designerDay == null)
                                DrawCenteredText(graphics, name, dayFont, primary,
                                    forecastDayBounds[index]);
                            else
                                DrawIndustrialDesignerText(graphics, designerDay, name);
                            if (designerForecastIcon == null)
                            {
                                if (!DrawWeatherIconPack(graphics,
                                    forecastIconBounds[index], day.Icon,
                                    weatherIconPack, 0, 0, 0F, 0F))
                                    DrawMiniWeatherSymbol(graphics,
                                        forecastIconBounds[index], day.Icon);
                            }
                            else if (designerForecastIcon.Visible)
                            {
                                if (!DrawDesignerPng(graphics, designerForecastIcon) && !DrawWeatherIconPack(graphics,
                                    designerForecastIcon.Bounds, day.Icon,
                                    weatherIconPack,
                                    designerForecastIcon.ImageHorizontalPlacement,
                                    designerForecastIcon.ImageVerticalPlacement,
                                    designerForecastIcon.ImageOffsetX,
                                    designerForecastIcon.ImageOffsetY))
                                    DrawWeatherSymbolFitted(graphics,
                                        designerForecastIcon.Bounds, day.Icon,
                                        designerForecastIcon.ImageHorizontalPlacement,
                                        designerForecastIcon.ImageVerticalPlacement,
                                        designerForecastIcon.ImageOffsetX,
                                        designerForecastIcon.ImageOffsetY);
                            }
                            if (designerRange == null)
                                DrawCenteredText(graphics, range, rangeFont, primary,
                                    forecastTemperatureBounds[index]);
                            else
                                DrawIndustrialDesignerText(graphics, designerRange, range);
                            if (forecastDesignerCondition != null)
                                DrawIndustrialDesignerText(graphics,
                                    forecastDesignerCondition, day.Condition);
                        }
                    }
                    string footer = !string.IsNullOrEmpty(snapshot.Error)
                        ? "Saved weather — refresh unavailable"
                        : snapshot.Updated == DateTime.MinValue ? "Waiting for weather service"
                        : "Updated " + snapshot.Updated.ToString("h:mm tt",
                            CultureInfo.CurrentCulture);
                    IndustrialDesignerElement designerFooter =
                        designerLayout == null ? null : designerLayout.Find("footer");
                    if (!_designerReferenceOnly)
                    {
                        if (UsesWoodlandNaturePresentation && designerFooter == null)
                        {
                            using (var footerFormat = new StringFormat
                            {
                                Alignment = StringAlignment.Far,
                                LineAlignment = StringAlignment.Near,
                                Trimming = StringTrimming.EllipsisCharacter,
                                FormatFlags = StringFormatFlags.NoWrap
                            })
                                graphics.DrawString(footer, footerFont,
                                    footerBrush, new RectangleF(
                                    inner.Right - 172F, inner.Top + 12F,
                                        166F, 18F), footerFormat);
                        }
                        else if (designerFooter == null)
                            DrawTextFit(graphics, footer, footerFont, footerFont,
                                footerBrush, footerBounds);
                        else
                            DrawIndustrialDesignerText(
                                graphics, designerFooter, footer);
                    }
                }
                if (!_designerReferenceOnly && designerLayout != null)
                    DrawAdditionalIndustrialDesignerMainElements(graphics,
                        snapshot, designerLayout, weatherIconPack);
                IndustrialDesignerElement designerButton =
                    designerLayout == null ? null : designerLayout.Find("panel-button");
                if (!_designerReferenceOnly)
                {
                    if (designerButton == null)
                        DrawToggleButton(graphics);
                    else if (designerButton.Visible)
                        DrawToggleButton(graphics, designerButton.Bounds,
                            Color.FromArgb(designerButton.ColorArgb),
                            designerButton.Opacity,
                            designerButton.FontSize);
                }
            }
            finally { graphics.Restore(state); }
            if (IsBotanical)
                DrawBotanicalMainFrame(
                    graphics, visibleBounds, 10F, 28F);
        }

        private void DrawIndustrialWeatherDetails(
            Graphics graphics, WeatherSnapshot snapshot, RectangleF bounds)
        {
            IndustrialDesignerLayout designerLayout =
                UsesWoodlandNaturePresentation ? NatureEditableWeatherLayout() : IndustrialEditableWeatherLayout();
            if (_designerReferenceOnly) designerLayout = null;
            IndustrialDesignerElement background =
                DesignerBackground(designerLayout, 1);
            if (!SuppressDefaultDesignerBackground(designerLayout, 1))
            {
                Image skin = GetScaledIndustrialPanelSkin(bounds.Size);
                graphics.DrawImage(skin, bounds);
            }
            if (_designerReferenceOnly) return;
            if (background != null && background.Visible)
            {
                GraphicsState backgroundState = graphics.Save();
                try
                {
                    graphics.TranslateTransform(bounds.X, bounds.Y);
                    graphics.ScaleTransform(bounds.Width / 750F,
                        bounds.Height / 500F);
                    RenderEditableDesignerBackground(graphics,
                        background.Bounds, true,
                        background.ImagePath, background.Opacity,
                        designerLayout.BackgroundLayerVersion >= 1);
                }
                finally { graphics.Restore(backgroundState); }
            }
            if (UsesWoodlandNaturePresentation && designerLayout != null)
            {
                GraphicsState editableState = graphics.Save();
                try
                {
                    graphics.TranslateTransform(bounds.X, bounds.Y);
                    graphics.ScaleTransform(bounds.Width / 750F, bounds.Height / 500F);
                    DrawEditableWeatherLayers(graphics, snapshot, designerLayout, 1);
                }
                finally { graphics.Restore(editableState); }
                return;
            }
            if (designerLayout != null &&
                designerLayout.Find("details-title") != null)
            {
                DrawIndustrialDesignerDetails(
                    graphics, snapshot, bounds, designerLayout);
                return;
            }
            RectangleF safe = RectangleF.FromLTRB(
                bounds.Left + 58F, bounds.Top + 57F,
                bounds.Right - 58F, bounds.Bottom - 24F);
            using (var titleFont = new Font("Segoe UI Semibold", 24F,
                FontStyle.Bold, GraphicsUnit.Pixel))
            using (var labelFont = new Font("Segoe UI Semibold", 16.7F,
                FontStyle.Regular, GraphicsUnit.Pixel))
            using (var valueFont = new Font("Segoe UI Semibold", 20F,
                FontStyle.Bold, GraphicsUnit.Pixel))
            using (var primary = new SolidBrush(IsBotanical
                ? Color.FromArgb(37, 63, 51)
                : Color.FromArgb(244, 228, 192)))
            using (var secondary = new SolidBrush(IsBotanical
                ? Color.FromArgb(99, 117, 91)
                : Color.FromArgb(166, 170, 169)))
            using (var line = new Pen(IsBotanical
                ? Color.FromArgb(205, 105, 126)
                : Color.FromArgb(224, 157, 39), 1.2F))
            {
                // The Woodland header is intentionally independent of the
                // metrics grid.  It sits nearer the top wood rail, while the
                // detail rows retain their established lower start point.
                float woodlandTitleTop = safe.Top - 32F;
                graphics.DrawString("Weather Details", titleFont, primary,
                    new RectangleF(safe.X, UsesWoodlandNaturePresentation
                        ? woodlandTitleTop : safe.Y, safe.Width,
                        UsesWoodlandNaturePresentation ? 28F : 25F));
                graphics.DrawLine(line, safe.Left,
                    (UsesWoodlandNaturePresentation ? woodlandTitleTop : safe.Top) +
                        (UsesWoodlandNaturePresentation ? 37F : 35F),
                    safe.Right, (UsesWoodlandNaturePresentation ? woodlandTitleTop : safe.Top) +
                        (UsesWoodlandNaturePresentation ? 37F : 35F));
                List<WeatherDetail> details = BuildIndustrialWeatherDetails(snapshot);

                if (UsesWoodlandNaturePresentation)
                {
                    // Woodland uses the wide details frame.  The panel is
                    // deliberately a fixed two-column grid: labels, values,
                    // icons, divider rules, and the centre separator all use
                    // the same coordinates so no individual metric drifts.
                    // Keep the metrics attached to the header rule.  The
                    // title moved upward independently, so using safe.Top +
                    // 50 here left a large empty band above the rows and
                    // compressed all four rows at the bottom of the panel.
                    float dataTop = safe.Top + 14F;
                    float rowHeight = (safe.Bottom - dataTop) / 4F;
                    float columnGap = 26F;
                    float columnWidth = (safe.Width - columnGap) / 2F;
                    float centreDividerX = safe.X + columnWidth +
                        columnGap / 2F;
                    using (var centreLine = new Pen(Color.FromArgb(
                        146, 184, 139, 83), 1F))
                    {
                        graphics.DrawLine(centreLine, centreDividerX,
                            dataTop - 8F, centreDividerX,
                            safe.Bottom - 4F);
                    }
                    for (int index = 0; index < details.Count && index < 8; index++)
                    {
                        int column = index % 2;
                        int row = index / 2;
                        float x = safe.X + column * (columnWidth + columnGap);
                        float y = dataTop + row * rowHeight;
                        RectangleF iconBounds = new RectangleF(x + 3F,
                            y + 4F, 26F, 26F);
                        float contentX = x + 38F;
                        float contentWidth = columnWidth - 38F;
                        DrawWoodlandWeatherDetailIcon(graphics,
                            details[index].Label, iconBounds);
                        graphics.DrawString(details[index].Label, labelFont,
                            secondary, new RectangleF(contentX, y,
                                contentWidth, 20F));
                        DrawTextFit(graphics, details[index].Value,
                            valueFont, labelFont, primary,
                            new RectangleF(contentX, y + 20F, contentWidth,
                                Math.Max(22F, rowHeight - 24F)));
                        if (row < 3)
                        {
                            // The rules belong below each row's value, not
                            // between its label and value.  Keep a small gap
                            // before the next row starts.
                            float dividerY = y + rowHeight - 2F;
                            graphics.DrawLine(line, x + 2F, dividerY,
                                x + columnWidth - 12F, dividerY);
                        }
                    }
                }
                else
                {
                    // Preserve the established Industrial details layout.
                    float rowHeight = (safe.Height - 48F) / 4F;
                    float columnWidth = (safe.Width - 28F) / 2F;
                    for (int index = 0; index < details.Count && index < 8; index++)
                    {
                        int column = index % 2;
                        int row = index / 2;
                        float x = safe.X + column * (columnWidth + 28F);
                        float y = safe.Y + 48F + row * rowHeight;
                        if (index >= 2)
                            graphics.DrawString(details[index].Label, labelFont,
                                secondary, new RectangleF(x, y, columnWidth, 23F));
                        DrawTextFit(graphics, details[index].Value,
                            valueFont, labelFont, primary,
                            new RectangleF(x, y + (index < 2 ? 5F : 24F),
                                columnWidth, 30F));
                        if (row < 3)
                            graphics.DrawLine(line, x, y + rowHeight - 4F,
                                x + columnWidth, y + rowHeight - 4F);
                    }
                }
            }
        }

        private void DrawWoodlandWeatherDetailIcon(Graphics graphics,
            string label, RectangleF bounds)
        {
            if (graphics == null || bounds.Width <= 0F || bounds.Height <= 0F)
                return;
            Color icon = IsBotanical
                ? Color.FromArgb(235, 50, 91, 66)
                : Color.FromArgb(188, 169, 224, 129);
            Color edge = IsBotanical
                ? Color.FromArgb(245, 27, 63, 43)
                : Color.FromArgb(214, 77, 126, 55);
            using (var pen = new Pen(icon, 1.3F))
            using (var edgePen = new Pen(edge, .8F))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                edgePen.StartCap = LineCap.Round;
                edgePen.EndCap = LineCap.Round;
                float x = bounds.X;
                float y = bounds.Y;
                float w = bounds.Width;
                float h = bounds.Height;
                if (string.Equals(label, "Feels Like",
                    StringComparison.OrdinalIgnoreCase))
                {
                    graphics.DrawEllipse(pen, x + w * .32F, y + h * .62F,
                        w * .36F, h * .36F);
                    graphics.DrawLine(pen, x + w * .5F, y + h * .16F,
                        x + w * .5F, y + h * .76F);
                    graphics.DrawLine(edgePen, x + w * .42F, y + h * .18F,
                        x + w * .58F, y + h * .18F);
                    return;
                }
                if (string.Equals(label, "Wind",
                    StringComparison.OrdinalIgnoreCase))
                {
                    graphics.DrawArc(pen, x + w * .03F, y + h * .14F,
                        w * .78F, h * .42F, 190F, 150F);
                    graphics.DrawArc(pen, x + w * .18F, y + h * .48F,
                        w * .72F, h * .34F, 190F, 145F);
                    return;
                }
                if (string.Equals(label, "Pressure",
                    StringComparison.OrdinalIgnoreCase))
                {
                    graphics.DrawArc(pen, x + w * .14F, y + h * .2F,
                        w * .72F, h * .72F, 200F, 140F);
                    graphics.DrawLine(pen, x + w * .5F, y + h * .58F,
                        x + w * .71F, y + h * .39F);
                    graphics.DrawLine(edgePen, x + w * .16F, y + h * .76F,
                        x + w * .84F, y + h * .76F);
                    return;
                }
                if (string.Equals(label, "Visibility",
                    StringComparison.OrdinalIgnoreCase))
                {
                    using (var path = new GraphicsPath())
                    {
                        path.AddBezier(x + w * .05F, y + h * .5F,
                            x + w * .28F, y + h * .12F,
                            x + w * .72F, y + h * .12F,
                            x + w * .95F, y + h * .5F);
                        path.AddBezier(x + w * .95F, y + h * .5F,
                            x + w * .72F, y + h * .88F,
                            x + w * .28F, y + h * .88F,
                            x + w * .05F, y + h * .5F);
                        graphics.DrawPath(pen, path);
                    }
                    graphics.DrawEllipse(edgePen, x + w * .38F, y + h * .34F,
                        w * .24F, h * .32F);
                    return;
                }
                if (string.Equals(label, "UV Index",
                    StringComparison.OrdinalIgnoreCase))
                {
                    graphics.DrawEllipse(pen, x + w * .31F, y + h * .31F,
                        w * .38F, h * .38F);
                    for (int ray = 0; ray < 8; ray++)
                    {
                        double angle = ray * Math.PI / 4D;
                        float x1 = x + w * .5F +
                            (float)Math.Cos(angle) * w * .31F;
                        float y1 = y + h * .5F +
                            (float)Math.Sin(angle) * h * .31F;
                        float x2 = x + w * .5F +
                            (float)Math.Cos(angle) * w * .46F;
                        float y2 = y + h * .5F +
                            (float)Math.Sin(angle) * h * .46F;
                        graphics.DrawLine(edgePen, x1, y1, x2, y2);
                    }
                    return;
                }
                if (string.Equals(label, "Sunrise / Sunset",
                    StringComparison.OrdinalIgnoreCase))
                {
                    graphics.DrawLine(pen, x + w * .08F, y + h * .72F,
                        x + w * .92F, y + h * .72F);
                    graphics.DrawArc(pen, x + w * .28F, y + h * .38F,
                        w * .44F, h * .48F, 180F, 180F);
                    graphics.DrawLine(edgePen, x + w * .5F, y + h * .17F,
                        x + w * .5F, y + h * .31F);
                    return;
                }

                // Dew point and humidity both use a small leaf-like water drop.
                using (var drop = new GraphicsPath())
                {
                    drop.AddBezier(x + w * .5F, y + h * .08F,
                        x + w * .22F, y + h * .44F,
                        x + w * .23F, y + h * .82F,
                        x + w * .5F, y + h * .92F);
                    drop.AddBezier(x + w * .5F, y + h * .92F,
                        x + w * .77F, y + h * .82F,
                        x + w * .78F, y + h * .44F,
                        x + w * .5F, y + h * .08F);
                    graphics.DrawPath(pen, drop);
                    graphics.DrawLine(edgePen, x + w * .5F, y + h * .32F,
                        x + w * .5F, y + h * .69F);
                }
            }
        }

        private void DrawIndustrialDesignerDetails(Graphics graphics,
            WeatherSnapshot snapshot, RectangleF bounds,
            IndustrialDesignerLayout layout)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / 750F,
                    bounds.Height / 500F);
                // Do not restrict the Industrial panel to its original six
                // named rows. Designer-added text layers are editable data
                // and must render with the same binding resolver.
                foreach (IndustrialDesignerElement element in layout.Elements)
                {
                    if (element == null || !element.Visible ||
                        element.Surface != 1 ||
                        IsDesignerBackground(element, 1)) continue;
                    if (element.Kind == 0)
                        DrawIndustrialDesignerText(graphics, element,
                            ResolveIndustrialDesignerBinding(
                                element, snapshot));
                    else if (element.Kind == 1)
                        DrawIndustrialDesignerWeatherImage(graphics, element,
                            snapshot, string.IsNullOrWhiteSpace(
                                layout.WeatherIconPack)
                                ? ActiveWeatherIconPack
                                : layout.WeatherIconPack);
                    else if (element.Kind == 2)
                        DrawIndustrialDivider(graphics, layout, element.Id,
                            Pens.Transparent, RectangleF.Empty, false);
                }
            }
            finally { graphics.Restore(state); }
        }

        private void DrawAdditionalIndustrialDesignerMainElements(
            Graphics graphics, WeatherSnapshot snapshot,
            IndustrialDesignerLayout layout, string weatherIconPack)
        {
            foreach (IndustrialDesignerElement element in layout.Elements)
            {
                if (element == null || !element.Visible ||
                    element.Surface != 0 ||
                    IsIndustrialBuiltInElement(element.Id) ||
                    IsDesignerBackground(element, 0)) continue;
                if (element.Kind == 0)
                    DrawIndustrialDesignerText(graphics, element,
                        ResolveIndustrialDesignerBinding(element, snapshot));
                else if (element.Kind == 1)
                    DrawIndustrialDesignerWeatherImage(graphics, element,
                        snapshot, weatherIconPack);
                else if (element.Kind == 2)
                    DrawIndustrialDivider(graphics, layout, element.Id,
                        Pens.Transparent, RectangleF.Empty, false);
            }
        }

        private void DrawIndustrialDesignerWeatherImage(Graphics graphics,
            IndustrialDesignerElement element, WeatherSnapshot snapshot,
            string weatherIconPack)
        {
            if (DrawDesignerPng(graphics, element)) return;
            int icon;
            if (string.Equals(element.Binding, "Weather: Current Icon",
                StringComparison.OrdinalIgnoreCase))
                icon = snapshot.Icon;
            else if (string.Equals(element.Binding,
                "Weather: Forecast Icon", StringComparison.OrdinalIgnoreCase))
            {
                int index = UnboundedForecastIndex(element);
                if (snapshot.ForecastDays == null || index < 0 ||
                    index >= snapshot.ForecastDays.Count) return;
                icon = snapshot.ForecastDays[index].Icon;
            }
            else return;

            if (!DrawWeatherIconPack(graphics, element.Bounds, icon,
                weatherIconPack, element.ImageHorizontalPlacement,
                element.ImageVerticalPlacement, element.ImageOffsetX,
                element.ImageOffsetY))
                DrawWeatherSymbolFitted(graphics, element.Bounds, icon,
                    element.ImageHorizontalPlacement,
                    element.ImageVerticalPlacement, element.ImageOffsetX,
                    element.ImageOffsetY);
        }

        private string ResolveIndustrialDesignerBinding(
            IndustrialDesignerElement element, WeatherSnapshot snapshot)
        {
            if (element == null) return string.Empty;
            int index = UnboundedForecastIndex(element);
            if (snapshot.ForecastDays != null && index >= 0 &&
                index < snapshot.ForecastDays.Count)
            {
                ForecastDay day = snapshot.ForecastDays[index];
                if (string.Equals(element.Binding, "Weather: Forecast Day",
                    StringComparison.OrdinalIgnoreCase))
                    return day.Date == DateTime.MinValue
                        ? "Day " + (index + 1)
                        : index == 0 ? "Today" : day.Date.ToString("ddd",
                            CultureInfo.CurrentCulture);
                if (string.Equals(element.Binding,
                    "Weather: Forecast High and Low",
                    StringComparison.OrdinalIgnoreCase))
                    return Math.Round(ConvertTemperature(day.HighC)) +
                        Degree() + " / " +
                        Math.Round(ConvertTemperature(day.LowC)) + Degree();
                if (string.Equals(element.Binding, "Weather: Forecast High",
                    StringComparison.OrdinalIgnoreCase))
                    return Math.Round(ConvertTemperature(day.HighC)) + Degree();
                if (string.Equals(element.Binding, "Weather: Forecast Low",
                    StringComparison.OrdinalIgnoreCase))
                    return Math.Round(ConvertTemperature(day.LowC)) + Degree();
                if (string.Equals(element.Binding,
                    "Weather: Forecast Condition",
                    StringComparison.OrdinalIgnoreCase))
                    return day.Condition;
            }
            return ResolveIndustrialDetailBinding(element.Binding,
                element.Text, snapshot);
        }

        private static int UnboundedForecastIndex(
            IndustrialDesignerElement element)
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
                    return Math.Max(0, parsed);
            }
            string name = element.Name ?? string.Empty;
            if (name.StartsWith("Forecast ",
                StringComparison.OrdinalIgnoreCase))
            {
                string remainder = name.Substring("Forecast ".Length);
                int separator = remainder.IndexOf(' ');
                int ordinal;
                if (separator > 0 && int.TryParse(
                    remainder.Substring(0, separator), out ordinal))
                    return Math.Max(0, ordinal - 1);
            }
            return -1;
        }

        private static bool IsIndustrialBuiltInElement(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return id == "current-icon" || id == "location" ||
                id == "temperature" || id == "condition" ||
                id == "feels-like" || id == "humidity" || id == "wind" ||
                id == "footer" || id == "main-divider" ||
                id == "forecast-divider" || id == "panel-button" ||
                id.StartsWith("forecast-day-",
                    StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("forecast-icon-",
                    StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("forecast-range-",
                    StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("forecast-condition-",
                    StringComparison.OrdinalIgnoreCase) ||
                id.StartsWith("forecast-column-",
                    StringComparison.OrdinalIgnoreCase);
        }

        private string ResolveIndustrialDetailBinding(string binding,
            string fallback, WeatherSnapshot snapshot)
        {
            // Designer-added weather text can use the same plain-language
            // choices as the main weather surface.  Icon choices remain image
            // layers, so text layers simply retain their own fallback for them.
            if (string.Equals(binding, "Weather: Location",
                StringComparison.OrdinalIgnoreCase))
                return DisplayCityName(snapshot.Location);
            if (string.Equals(binding, "Weather: Temperature",
                StringComparison.OrdinalIgnoreCase))
                return Math.Round(snapshot.Temperature) + Degree();
            if (string.Equals(binding, "Weather: Feels Like",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasFeelsLike
                    ? "Feels Like " + Math.Round(snapshot.FeelsLike) + Degree()
                    : fallback;
            if (string.Equals(binding, "Weather: Condition",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.Condition ?? fallback;
            if (string.Equals(binding, "Weather: Humidity",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasHumidity
                    ? "Humidity " + snapshot.Humidity + "%" : fallback;
            if (string.Equals(binding, "Weather: Wind",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.HasWind
                    ? "Wind " + (snapshot.WindDirection ?? string.Empty) +
                      " " + Math.Round(snapshot.Wind) +
                      (_metric ? " km/h" : " mph")
                    : fallback;
            if (binding == "Weather: Pressure")
                return snapshot.HasPressure ? (_metric
                    ? Math.Round(snapshot.Pressure) + " hPa"
                    : snapshot.Pressure.ToString("0.00",
                        CultureInfo.CurrentCulture) + " inHg") : fallback;
            if (binding == "Weather: Dew Point")
                return snapshot.HasDewPoint
                    ? Math.Round(snapshot.DewPoint) + Degree() : fallback;
            if (binding == "Weather: Visibility")
                return snapshot.HasVisibility
                    ? snapshot.Visibility.ToString("0.#",
                        CultureInfo.CurrentCulture) +
                        (_metric ? " km" : " mi") : fallback;
            if (binding == "Weather: UV Index")
                return snapshot.HasUvIndex
                    ? snapshot.UvIndex.ToString("0.#",
                        CultureInfo.CurrentCulture) +
                        (string.IsNullOrWhiteSpace(snapshot.UvText)
                            ? string.Empty : "  " + snapshot.UvText) : fallback;
            if (binding == "Weather: Sunrise and Sunset")
                return (snapshot.Sunrise ?? string.Empty) + " / " +
                    (snapshot.Sunset ?? string.Empty);
            if (string.Equals(binding, "Weather: Forecast Day",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.ForecastDays != null &&
                    snapshot.ForecastDays.Count > 0
                    ? snapshot.ForecastDays[0].Date.ToString("ddd",
                        CultureInfo.CurrentCulture) : fallback;
            if (string.Equals(binding, "Weather: Forecast High and Low",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.ForecastDays != null &&
                    snapshot.ForecastDays.Count > 0
                    ? Math.Round(ConvertTemperature(snapshot.ForecastDays[0].HighC)) +
                      Degree() + " / " +
                      Math.Round(ConvertTemperature(snapshot.ForecastDays[0].LowC)) +
                      Degree() : fallback;
            if (string.Equals(binding, "Weather: Forecast High",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.ForecastDays != null &&
                    snapshot.ForecastDays.Count > 0
                    ? Math.Round(ConvertTemperature(
                        snapshot.ForecastDays[0].HighC)) + Degree()
                    : fallback;
            if (string.Equals(binding, "Weather: Forecast Low",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.ForecastDays != null &&
                    snapshot.ForecastDays.Count > 0
                    ? Math.Round(ConvertTemperature(
                        snapshot.ForecastDays[0].LowC)) + Degree()
                    : fallback;
            if (string.Equals(binding, "Weather: Forecast Condition",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.ForecastDays != null &&
                    snapshot.ForecastDays.Count > 0
                    ? snapshot.ForecastDays[0].Condition : fallback;
            if (string.Equals(binding, "Weather: Updated Time",
                StringComparison.OrdinalIgnoreCase))
                return snapshot.Updated == DateTime.MinValue ? fallback
                    : "Updated " + snapshot.Updated.ToString("h:mm tt",
                        CultureInfo.CurrentCulture);
            return fallback;
        }

        private List<WeatherDetail> BuildIndustrialWeatherDetails(
            WeatherSnapshot snapshot)
        {
            // Keep the non-designer fallback identical to the editable
            // four-row Industrial template.  This is deliberately the same
            // information sequence as Art Deco, with UV bottom-left and
            // Sunrise / Sunset bottom-right.
            List<WeatherDetail> source = BuildWeatherDetails(snapshot);
            string[] order = { "Feels Like", "Humidity", "Wind", "Pressure",
                "Dew Point", "Visibility", "UV Index", "Sunrise / Sunset" };
            var result = new List<WeatherDetail>();
            foreach (string label in order)
            {
                WeatherDetail detail = source.Find(delegate(WeatherDetail item)
                {
                    return string.Equals(item.Label, label,
                        StringComparison.OrdinalIgnoreCase);
                });
                if (detail != null) result.Add(detail);
            }
            return result;
        }

        private static string DisplayCityName(string fullLocation)
        {
            if (string.IsNullOrWhiteSpace(fullLocation))
                return string.Empty;
            int separator = fullLocation.IndexOf(',');
            return (separator < 0
                ? fullLocation
                : fullLocation.Substring(0, separator)).Trim();
        }

        public void ShowSettings()
        {
            using (var form = new Form())
            using (var query = new TextBox())
            using (var results = new ListBox())
            using (var search = new Button())
            using (var metric = new RadioButton())
            using (var imperial = new RadioButton())
            using (var animation = new ComboBox())
            using (var ok = new Button())
            using (var cancel = new Button())
            {
                form.Text = "Weather Settings";
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.ClientSize = new Size(430, 370);
                form.MaximizeBox = false;
                form.MinimizeBox = false;

                var label = new Label { Text = "Search for a city", AutoSize = true, Left = 18, Top = 18 };
                query.Left = 18; query.Top = 42; query.Width = 300;
                search.Text = "Search"; search.Left = 326; search.Top = 40; search.Width = 82;
                results.Left = 18; results.Top = 78; results.Width = 390; results.Height = 160;
                metric.Text = "Metric (°C, km/h)"; metric.Left = 18; metric.Top = 252; metric.Width = 150; metric.Checked = _metric;
                imperial.Text = "Imperial (°F, mph)"; imperial.Left = 180; imperial.Top = 252; imperial.Width = 150; imperial.Checked = !_metric;
                var animationLabel = new Label { Text = "Forecast panel animation", AutoSize = true, Left = 18, Top = 291 };
                animation.DropDownStyle = ComboBoxStyle.DropDownList; animation.Left = 180; animation.Top = 286; animation.Width = 150;
                animation.Items.AddRange(new object[] { "Instant", "Quick", "Smooth", "Bouncy" });
                animation.SelectedItem = animation.Items.Contains(_animationMode) ? _animationMode : "Smooth";
                ok.Text = "OK"; ok.Left = 246; ok.Top = 330; ok.Width = 78; ok.DialogResult = DialogResult.OK;
                cancel.Text = "Cancel"; cancel.Left = 330; cancel.Top = 330; cancel.Width = 78; cancel.DialogResult = DialogResult.Cancel;
                form.Controls.AddRange(new Control[] { label, query, search, results, metric, imperial, animationLabel, animation, ok, cancel });
                form.AcceptButton = search;
                form.CancelButton = cancel;

                var choices = new List<LocationChoice>();
                search.Click += delegate
                {
                    if (string.IsNullOrWhiteSpace(query.Text)) return;
                    search.Enabled = false;
                    results.Items.Clear();
                    choices.Clear();
                    try
                    {
                        foreach (LocationChoice item in SearchLocations(query.Text.Trim()))
                        {
                            choices.Add(item);
                            results.Items.Add(item.Name);
                        }
                        if (results.Items.Count > 0) results.SelectedIndex = 0;
                        else MessageBox.Show(form, "No matching cities were found.", "Weather", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(form, "The weather service could not complete the search.\r\n\r\n" + ex.Message, "Weather", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    finally { search.Enabled = true; }
                };

                if (form.ShowDialog() != DialogResult.OK) return;
                if (results.SelectedIndex >= 0 && results.SelectedIndex < choices.Count)
                {
                    _locationKey = choices[results.SelectedIndex].Key;
                    _locationName = choices[results.SelectedIndex].Name;
                }
                _metric = metric.Checked;
                _animationMode = Convert.ToString(animation.SelectedItem, CultureInfo.InvariantCulture) ?? "Smooth";
                SaveSettings();
                BeginRefresh(true);
            }
        }

        public void ResetSettings()
        {
            _locationKey = "54704";
            _locationName = "Kentville, Nova Scotia";
            _customWeatherPageUrl = string.Empty;
            _metric = true;
            _animationMode = "Smooth";
            _appearance = "Modern";
            _style = "Weather.Standard";
            if (_panelOpen) TogglePanel();
            SaveSettings();
            BeginRefresh(true);
        }

        public void Dispose()
        {
            if (_runtime != null)
            {
                _runtime.Events.Published -= RuntimeEventPublished;
                _runtime = null;
            }
            if (_animationTimer != null)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
                _animationTimer = null;
            }
            _invalidate = null;
            _host = null;
        }

        private void SetUnits(bool metric)
        {
            _metric = metric;
            SaveSettings();
            RequestInvalidate();
        }

        private void SetAnimationMode(string mode)
        {
            _animationMode = NormalizeAnimationMode(mode);
            SaveSettings();
            RequestInvalidate();
        }

        private static string NormalizeAnimationMode(string mode)
        {
            if (string.Equals(mode, "Instant", StringComparison.OrdinalIgnoreCase))
                return "Instant";
            if (string.Equals(mode, "Quick", StringComparison.OrdinalIgnoreCase))
                return "Quick";
            if (string.Equals(mode, "Bouncy", StringComparison.OrdinalIgnoreCase))
                return "Bouncy";
            return "Smooth";
        }

        private void SaveSettings()
        {
            if (_host == null) return;
            _host.SetSetting("style", _style);
            _host.SetSetting("appearance", _appearance);
            _host.SetSetting("weather.locationKey", _locationKey);
            _host.SetSetting("weather.locationName", _locationName);
            _host.SetSetting("weather.customWebPageUrl",
                _customWeatherPageUrl ?? string.Empty);
            _host.SetSetting("weather.units", _metric ? "Metric" : "Imperial");
            _host.SetSetting("weather.panelAnimation", _animationMode);
            // Deliberately do not persist the temporary open/closed panel state.
        }

        private void LoadSettings()
        {
            if (_host == null) return;

            string legacyStyle = _host.GetSetting(
                "weather.style",
                _style);
            _style = NormalizeStyle(
                _host.GetSetting(
                    "style",
                    legacyStyle));

            string legacyAppearance = _host.GetSetting(
                "weather.theme",
                _appearance);
            _appearance = NormalizeAppearance(
                _host.GetSetting(
                    "appearance",
                    legacyAppearance));
        }

        private void ApplyStyle(string value)
        {
            string normalized =
                NormalizeStyle(value);
            _style = normalized;
            _appearance = NormalizeAppearance(
                _appearance);
            SaveSettings();
            RequestInvalidate();
        }

        private static string NormalizeStyle(
            string value)
        {
            return string.Equals(
                value,
                "Weather.Standard",
                StringComparison.OrdinalIgnoreCase)
                ? "Weather.Standard"
                : "Weather.Standard";
        }

        private static string NormalizeAppearance(
            string value)
        {
            return EmilyDeskThemeCatalog.Normalize(value);
        }

        private void BeginRefresh(bool force)
        {
            lock (_sync)
            {
                if (_loading) return;
                if (!force && DateTime.UtcNow < _nextAttemptUtc) return;
                _loading = true;
                _lastAttemptUtc = DateTime.UtcNow;
            }
            RequestInvalidate();
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    LoadWeather();
                    lock (_sync)
                    {
                        _error = null;
                        _lastSuccessLocal = DateTime.Now;
                        _consecutiveFailures = 0;
                        _nextAttemptUtc = DateTime.UtcNow.AddMinutes(30);
                    }
                }
                catch (Exception ex)
                {
                    lock (_sync)
                    {
                        _consecutiveFailures++;
                        int delay = Math.Min(30,
                            (int)Math.Pow(2,
                                Math.Min(4, _consecutiveFailures - 1)));
                        _nextAttemptUtc = DateTime.UtcNow.AddMinutes(delay);
                        _error = PlainWeatherError(ex);
                    }
                    if (_host != null) _host.ReportDiagnostic("Weather refresh failed: " + ex.Message);
                }
                finally
                {
                    lock (_sync) { _loading = false; }
                    RequestInvalidate();
                }
            });
        }

        private static string PlainWeatherError(Exception error)
        {
            if (error is WebException)
                return "Weather is temporarily offline. Showing the last update.";
            return "Weather could not update. Showing the last available information.";
        }

        private void LoadWeather()
        {
            string currentText = Download(BridgeBase + "/currentconditions/v1/" + Uri.EscapeDataString(_locationKey) + ".json?details=true");
            string forecastText = Download(BridgeBase + "/forecasts/v1/daily/15day/" + Uri.EscapeDataString(_locationKey) + ".json?metric=true");
            object currentRoot = _json.DeserializeObject(currentText);
            object forecastRoot = _json.DeserializeObject(forecastText);
            var currentList = AsList(currentRoot);
            if (currentList.Count == 0) throw new InvalidOperationException("The weather service returned no current conditions.");
            var current = AsMap(currentList[0]);
            var forecast = AsMap(forecastRoot);
            var days = AsList(Get(forecast, "DailyForecasts"));
            if (days.Count == 0) throw new InvalidOperationException("The weather service returned no forecast.");
            var day = AsMap(days[0]);
            var temperatures = AsMap(Get(day, "Temperature"));
            var sun = AsMap(Get(day, "Sun"));

            var parsedDays = new List<ForecastDay>();
            for (int i = 0;
                i < Math.Min(MaximumForecastDayCount, days.Count); i++)
            {
                var forecastDay = AsMap(days[i]);
                var temp = AsMap(Get(forecastDay, "Temperature"));
                var dayPart = AsMap(Get(forecastDay, "Day"));
                DateTime date;
                DateTime.TryParse(Text(forecastDay, "Date", ""), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date);
                parsedDays.Add(new ForecastDay
                {
                    Date = date,
                    Icon = Integer(dayPart, "Icon", 1),
                    Condition = Text(dayPart, "IconPhrase", "Forecast"),
                    HighC = MetricValue(AsMap(Get(temp, "Maximum"))),
                    LowC = MetricValue(AsMap(Get(temp, "Minimum")))
                });
            }

            lock (_sync)
            {
                _condition = Text(current, "WeatherText", "Weather unavailable");
                _icon = Integer(current, "WeatherIcon", 1);
                _temperature = MetricValue(AsMap(Get(current, "Temperature")));
                _hasFeelsLike = TryMetricValue(
                    AsMap(Get(current, "RealFeelTemperature")), out _feelsLike);
                _humidity = Integer(current, "RelativeHumidity", 0);
                _hasHumidity = Get(current, "RelativeHumidity") != null;
                var wind = AsMap(Get(current, "Wind"));
                _hasWind = TryMetricValue(AsMap(Get(wind, "Speed")), out _windKmh);
                _windDirection = Text(
                    AsMap(Get(wind, "Direction")), "Localized", string.Empty);
                _hasPressure = TryMetricValue(
                    AsMap(Get(current, "Pressure")), out _pressureHpa);
                _hasDewPoint = TryMetricValue(
                    AsMap(Get(current, "DewPoint")), out _dewPointC);
                _hasVisibility = TryMetricValue(
                    AsMap(Get(current, "Visibility")), out _visibilityKm);
                _hasUvIndex = TryNumber(current, "UVIndex", out _uvIndex);
                _uvText = Text(current, "UVIndexText", string.Empty);
                _sunrise = ProviderTime(Text(sun, "Rise", string.Empty));
                _sunset = ProviderTime(Text(sun, "Set", string.Empty));
                _low = MetricValue(AsMap(Get(temperatures, "Minimum")));
                _high = MetricValue(AsMap(Get(temperatures, "Maximum")));
                _forecastDays.Clear();
                _forecastDays.AddRange(parsedDays);
            }
        }

        private static string Download(string url)
        {
            using (var client = new WebClient())
            {
                client.Encoding = System.Text.Encoding.UTF8;
                client.Headers[HttpRequestHeader.UserAgent] = "XWidgetReborn-Weather/1.0";
                return client.DownloadString(url);
            }
        }

        private IEnumerable<LocationChoice> SearchLocations(string text)
        {
            string raw = Download(BridgeBase + "/locations/v1/cities/autocomplete.json?q=" + Uri.EscapeDataString(text));
            var list = AsList(_json.DeserializeObject(raw));
            foreach (object value in list)
            {
                var map = AsMap(value);
                string city = Text(map, "LocalizedName", "");
                var area = AsMap(Get(map, "AdministrativeArea"));
                var country = AsMap(Get(map, "Country"));
                string region = Text(area, "LocalizedName", "");
                string nation = Text(country, "LocalizedName", "");
                string name = city;
                if (!string.IsNullOrEmpty(region)) name += ", " + region;
                if (!string.IsNullOrEmpty(nation) && name.IndexOf(nation, StringComparison.OrdinalIgnoreCase) < 0) name += ", " + nation;
                yield return new LocationChoice { Key = Text(map, "Key", ""), Name = name };
            }
        }



        public IEnumerable<WidgetHotspot> GetHotspots(Size clientSize)
        {
            if (IsArtDeco || UsesIndustrialWeatherPresentation)
            {
                RectangleF main = ActiveSlidePanel.ParentBounds;
                RectangleF artDesign = ToggleControlBounds();
                float contentX = main.Width / (UsesIndustrialWeatherPresentation ? 750F : 500F);
                float contentY = main.Height / (UsesIndustrialWeatherPresentation ? 500F : 346F);
                float artSx = clientSize.Width / ArtDecoCanvasWidth;
                float artSy = clientSize.Height / ArtDecoCanvasHeight;
                Rectangle artBounds = Rectangle.Round(new RectangleF(
                    (main.X + artDesign.X * contentX) * artSx,
                    (main.Y + artDesign.Y * contentY) * artSy,
                    artDesign.Width * contentX * artSx,
                    artDesign.Height * contentY * artSy));
                artBounds.Inflate(3, 3);
                yield return new WidgetHotspot("forecast-toggle", artBounds,
                    _panelOpen ? "Hide details" : "Show details");
                yield break;
            }
            WeatherCompositeLayout layout = CalculateCompositeLayout(_panelProgress);
            float sx = clientSize.Width <= 0 ? 1f : clientSize.Width / layout.CompositeSize.Width;
            float sy = clientSize.Height <= 0 ? 1f : clientSize.Height / layout.CompositeSize.Height;
            RectangleF design = ToggleControlBounds();
            var bounds = Rectangle.Round(new RectangleF(
                (layout.MainRectangle.X +
                    design.X * layout.MainContentScale) * sx,
                (layout.MainRectangle.Y +
                    design.Y * layout.MainContentScale) * sy,
                design.Width * layout.MainContentScale * sx,
                design.Height * layout.MainContentScale * sy));
            bounds.Inflate(3, 3);
            yield return new WidgetHotspot(
                "forecast-toggle",
                bounds,
                _panelOpen ? "Hide forecast" : "Show forecast");
        }

        public bool PointerDown(Point location, WidgetPointerButton button)
        {
            if (button != WidgetPointerButton.Left) return false;
            PointF p = ToDesignPoint(location);
            if (!ToggleControlBounds().Contains(p)) return false;
            _togglePressed = true;
            RequestInvalidate();
            return true;
        }

        public void PointerMove(Point location)
        {
            PointF p = ToDesignPoint(location);
            bool hovered = ToggleControlBounds().Contains(p);
            if (hovered == _toggleHovered) return;
            _toggleHovered = hovered;
            RequestInvalidate();
        }

        public void PointerUp(Point location, WidgetPointerButton button)
        {
            PointF p = ToDesignPoint(location);
            bool activate = _togglePressed && button == WidgetPointerButton.Left &&
                ToggleControlBounds().Contains(p);
            _togglePressed = false;
            if (activate) TogglePanel();
            else RequestInvalidate();
        }

        public void PointerLeave()
        {
            if (!_toggleHovered && !_togglePressed) return;
            _toggleHovered = false;
            _togglePressed = false;
            RequestInvalidate();
        }

        public bool AcceptsInput(Point location, Size clientSize)
        {
            if (!IsArtDeco && !UsesIndustrialWeatherPresentation) return true;
            float x = location.X * ArtDecoCanvasWidth /
                Math.Max(1, clientSize.Width);
            float y = location.Y * ArtDecoCanvasHeight /
                Math.Max(1, clientSize.Height);
            PointF design = new PointF(x, y);
            return ActiveSlidePanel.ParentBounds.Contains(design) ||
                ActiveSlidePanel.PanelAcceptsInput(design);
        }

        private void TogglePanel()
        {
            _panelOpen = !_panelOpen;
            StartPanelAnimation(_panelOpen ? 1f : 0f);
        }

        private void StartPanelAnimation(float target)
        {
            if (string.Equals(_animationMode, "Instant", StringComparison.OrdinalIgnoreCase))
            {
                _panelProgress = target;
                if (IsArtDeco || UsesIndustrialWeatherPresentation) ActiveSlidePanel.SetProgress(target);
                ApplyPanelSize(IsArtDeco || UsesIndustrialWeatherPresentation);
                RequestInvalidate();
                return;
            }

            if (IsArtDeco || UsesIndustrialWeatherPresentation)
            {
                ActiveSlidePanel.Duration = TimeSpan.FromSeconds(
                    string.Equals(_animationMode, "Quick",
                        StringComparison.OrdinalIgnoreCase) ? .14D :
                    string.Equals(_animationMode, "Bouncy",
                        StringComparison.OrdinalIgnoreCase) ? .42D : .36D);
                ActiveSlidePanel.Easing =
                    string.Equals(_animationMode, "Quick",
                        StringComparison.OrdinalIgnoreCase)
                    ? SlidePanelEasing.Linear
                    : string.Equals(_animationMode, "Bouncy",
                        StringComparison.OrdinalIgnoreCase)
                        ? SlidePanelEasing.BouncyEaseOut
                        : SlidePanelEasing.SmoothEaseInOut;
                ActiveSlidePanel.SetSlided(_panelOpen, DateTime.UtcNow);
            }

            _animationStartProgress = _panelProgress;
            _animationTargetProgress = target;
            _animationStartedUtc = DateTime.UtcNow;
            if (_animationTimer == null)
            {
                _animationTimer = new System.Windows.Forms.Timer();
                _animationTimer.Interval = 15;
                _animationTimer.Tick += delegate { AnimatePanel(); };
            }
            _animationTimer.Start();
        }

        private void AnimatePanel()
        {
            if (IsArtDeco || UsesIndustrialWeatherPresentation)
            {
                _artDecoAnimationFrameCount++;
                ActiveSlidePanel.Advance(DateTime.UtcNow);
                _panelProgress = ActiveSlidePanel.Progress;
                if (!ActiveSlidePanel.IsAnimating)
                    _animationTimer.Stop();
                RequestInvalidate();
                return;
            }
            double duration = string.Equals(_animationMode, "Quick",
                StringComparison.OrdinalIgnoreCase) ? .14D :
                string.Equals(_animationMode, "Bouncy",
                    StringComparison.OrdinalIgnoreCase) ? .42D : .36D;
            double elapsed = (DateTime.UtcNow - _animationStartedUtc).TotalSeconds;
            float t = (float)Math.Max(0, Math.Min(1, elapsed / duration));
            float eased;
            if (string.Equals(_animationMode, "Quick",
                StringComparison.OrdinalIgnoreCase))
                eased = t;
            else if (string.Equals(_animationMode, "Bouncy", StringComparison.OrdinalIgnoreCase))
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float u = t - 1f;
                eased = 1f + c3 * u * u * u + c1 * u * u;
            }
            else
                eased = t < .5F
                    ? 4F * t * t * t
                    : 1F - (float)Math.Pow(-2F * t + 2F, 3F) / 2F;

            _panelProgress = _animationStartProgress + ((_animationTargetProgress - _animationStartProgress) * eased);
            _panelProgress = Math.Max(0f, Math.Min(
                IsArtDeco ? 1F : 1.08F, _panelProgress));
            if (t >= 1f)
            {
                _panelProgress = _animationTargetProgress;
                _animationTimer.Stop();
            }
            ApplyPanelSize(IsArtDeco);
            RequestInvalidate();
        }

        private Size FixedCompositionSurfaceSize()
        {
            return IsWoodland
                ? new Size(WoodlandSurfaceWidth, WoodlandSurfaceHeight)
                : IsImportedTheme
                ? new Size(ImportedSurfaceWidth, ImportedSurfaceHeight)
                : new Size(ArtDecoSurfaceWidth, ArtDecoSurfaceHeight);
        }

        private Rectangle FixedCompositionAnchorBounds()
        {
            RectangleF anchor = CompositionAnchorBounds();
            // Built-in themes retain their approved presentation and anchor.
            if (!IsWoodland && !IsImportedTheme)
                return ScaleRectangle(anchor, .75F);
            Size surface = FixedCompositionSurfaceSize();
            return Rectangle.Round(new RectangleF(
                anchor.X * surface.Width / ArtDecoCanvasWidth,
                anchor.Y * surface.Height / ArtDecoCanvasHeight,
                anchor.Width * surface.Width / ArtDecoCanvasWidth,
                anchor.Height * surface.Height / ArtDecoCanvasHeight));
        }

        private Size CurrentPreferredSize()
        {
            if (IsArtDeco || UsesIndustrialWeatherPresentation)
                return FixedCompositionSurfaceSize();
            SizeF size = CalculateCompositeLayout(_panelProgress).CompositeSize;
            return new Size((int)Math.Round(size.Width), (int)Math.Round(size.Height));
        }

        private void ApplyPanelSize(bool anchorRight)
        {
            if (_host == null) return;
            if (IsArtDeco || UsesIndustrialWeatherPresentation)
            {
                // The SlidePanel animates inside one fixed composition surface.
                // Animation ticks invalidate pixels but never resize the HWND.
                return;
            }
            WeatherCompositeLayout layout = CalculateCompositeLayout(_panelProgress);
            Size size = CurrentPreferredSize();
            if (layout.Direction == ForecastOpeningDirection.Left && anchorRight)
                _host.SetPreferredSizeAnchoredRight(size);
            else
                _host.SetPreferredSize(size);
        }

        private void ApplyWindowShape()
        {
            if (_host != null)
                _host.SetWindowShape(IsModern || IsVintage || IsArtDeco ||
                        UsesIndustrialWeatherPresentation
                    ? WidgetWindowShape.AlphaRectangle
                    : WidgetWindowShape.RoundedRectangle);
        }

        private PointF ToDesignPoint(Point location)
        {
            if (IsArtDeco || UsesIndustrialWeatherPresentation)
            {
                RectangleF main = ActiveSlidePanel.ParentBounds;
                float artX = location.X * ArtDecoCanvasWidth /
                    Math.Max(1, _lastRenderSize.Width);
                float artY = location.Y * ArtDecoCanvasHeight /
                    Math.Max(1, _lastRenderSize.Height);
                return new PointF(
                    (artX - main.X) * (UsesIndustrialWeatherPresentation ? 750F : 500F) / main.Width,
                    (artY - main.Y) * (UsesIndustrialWeatherPresentation ? 500F : 346F) / main.Height);
            }
            WeatherCompositeLayout layout = CalculateCompositeLayout(_panelProgress);
            float x = _lastRenderSize.Width <= 0
                ? location.X
                : location.X * layout.CompositeSize.Width / _lastRenderSize.Width;
            float y = _lastRenderSize.Height <= 0
                ? location.Y
                : location.Y * layout.CompositeSize.Height / _lastRenderSize.Height;
            return new PointF(
                (x - layout.MainRectangle.X) / layout.MainContentScale,
                (y - layout.MainRectangle.Y) / layout.MainContentScale);
        }

        private WeatherCompositeLayout CalculateCompositeLayout(float progress)
        {
            float amount = Math.Max(0F, Math.Min(1F, progress));
            if (!IsArtDeco)
            {
                float extension = (PanelWidth - PanelOverlap) * amount;
                return new WeatherCompositeLayout
                {
                    CompositeSize = new SizeF(CompactWidth + extension, WidgetHeight),
                    MainRectangle = new RectangleF(0F, 0F, CompactWidth, WidgetHeight),
                    PanelRectangle = new RectangleF(
                        CompactWidth - PanelOverlap, 0F, PanelWidth, WidgetHeight),
                    ClipRectangle = new RectangleF(
                        CompactWidth - PanelOverlap, 0F,
                        extension + PanelOverlap, WidgetHeight),
                    Direction = ForecastOpeningDirection.Right,
                    VisibleOverlap = PanelOverlap,
                    CanvasOverlap = PanelOverlap,
                    MainVisibleAlphaRectangle = new RectangleF(
                        0F, 0F, CompactWidth, WidgetHeight),
                    PanelVisibleAlphaRectangle = new RectangleF(
                        CompactWidth - PanelOverlap, 0F, PanelWidth, WidgetHeight),
                    SafeContentRectangle = RectangleF.Empty,
                    MainContentScale = 1F,
                    Progress = amount
                };
            }

            float artMainWidth = ArtDecoWidth * ArtDecoMainDisplayScale;
            float artMainHeight = ArtDecoSharedHeight * ArtDecoMainDisplayScale;
            float mainVisibleTop = ArtDecoMainAlphaTop *
                artMainHeight / ArtDecoMainAssetHeight;
            float mainVisibleBottom = ArtDecoMainAlphaBottom *
                artMainHeight / ArtDecoMainAssetHeight;
            float targetPanelVisibleTop = mainVisibleTop +
                ArtDecoVerticalVisibleInset;
            float targetPanelVisibleBottom = mainVisibleBottom -
                ArtDecoVerticalVisibleInset;
            float artPanelScale =
                (targetPanelVisibleBottom - targetPanelVisibleTop) /
                (ArtDecoPanelAlphaBottom - ArtDecoPanelAlphaTop);
            float artPanelWidth = ArtDecoPanelAssetWidth * artPanelScale;
            float artPanelHeight = ArtDecoPanelAssetHeight * artPanelScale;
            float artPanelTop = targetPanelVisibleTop -
                ArtDecoPanelAlphaTop * artPanelScale;
            float panelVisibleRight = ArtDecoPanelAlphaRight * artPanelScale;
            float mainVisibleLeftInset = ArtDecoMainAlphaLeft *
                artMainWidth / ArtDecoMainAssetWidth;
            float fullExtension = panelVisibleRight -
                mainVisibleLeftInset - ArtDecoTargetVisibleOverlap;
            float canvasOverlap = artPanelWidth - fullExtension;
            float artExtension = fullExtension * amount;
            var mainRectangle = new RectangleF(
                artExtension, 0F, artMainWidth, artMainHeight);
            var panelRectangle = new RectangleF(
                0F, artPanelTop, artPanelWidth, artPanelHeight);
            var mainVisibleRectangle = new RectangleF(
                mainRectangle.X + mainVisibleLeftInset,
                mainVisibleTop,
                (ArtDecoMainAlphaRight - ArtDecoMainAlphaLeft) *
                    artMainWidth / ArtDecoMainAssetWidth,
                mainVisibleBottom - mainVisibleTop);
            var panelVisibleRectangle = new RectangleF(
                ArtDecoPanelAlphaLeft * artPanelScale,
                targetPanelVisibleTop,
                (ArtDecoPanelAlphaRight - ArtDecoPanelAlphaLeft) * artPanelScale,
                targetPanelVisibleBottom - targetPanelVisibleTop);
            float safeLeft = ArtDecoPanelOrnamentRight * artPanelScale +
                ArtDecoContentInset;
            var safeContentRectangle = RectangleF.FromLTRB(
                safeLeft,
                panelVisibleRectangle.Top + ArtDecoContentInset,
                panelVisibleRectangle.Right - ArtDecoContentInset,
                panelVisibleRectangle.Bottom - ArtDecoContentInset);
            return new WeatherCompositeLayout
            {
                CompositeSize = new SizeF(
                    artMainWidth + artExtension, artMainHeight),
                MainRectangle = mainRectangle,
                PanelRectangle = panelRectangle,
                ClipRectangle = new RectangleF(
                    0F, 0F,
                    artExtension + canvasOverlap,
                    artMainHeight),
                Direction = ForecastOpeningDirection.Left,
                VisibleOverlap = ArtDecoTargetVisibleOverlap,
                CanvasOverlap = canvasOverlap,
                MainVisibleAlphaRectangle = mainVisibleRectangle,
                PanelVisibleAlphaRectangle = panelVisibleRectangle,
                SafeContentRectangle = safeContentRectangle,
                MainContentScale = ArtDecoMainDisplayScale,
                Progress = amount
            };
        }

        private static RectangleF ArtDecoCurrentIconBounds()
        {
            return new RectangleF(300F, 76F, 92F, 83F);
        }

        private RectangleF ToggleButtonBounds()
        {
            IndustrialDesignerLayout compact = EditableCompactWeatherLayout();
            if (compact != null)
            {
                IndustrialDesignerElement button = compact.Find("panel-button");
                return button != null && button.Visible ? button.Bounds : RectangleF.Empty;
            }
            if (UsesIndustrialWeatherPresentation)
            {
                IndustrialDesignerLayout layout = IsWoodland
                    ? WoodlandDesignerLayout.Current()
                    : IsBotanical
                    ? BotanicalDesignerLayout.Current()
                    : IndustrialEditableWeatherLayout();
                IndustrialDesignerElement button =
                    layout == null ? null : layout.Find("panel-button");
                if (button != null)
                    return button.Visible ? button.Bounds : RectangleF.Empty;
            }
            if (IsArtDeco)
            {
                IndustrialDesignerLayout layout =
                    ArtDecoDesignerLayout.Current();
                IndustrialDesignerElement button = layout == null
                    ? null : layout.Find("panel-button");
                if (button != null)
                    return button.Visible ? button.Bounds : RectangleF.Empty;
            }
            return IsArtDeco
                ? new RectangleF(456, 287, 22, 22)
                : UsesIndustrialWeatherPresentation
                ? new RectangleF(
                    IndustrialMainVisibleLocalBounds().Right - 60F,
                    IndustrialMainVisibleLocalBounds().Bottom - 50F,
                    28F, 28F)
                : new RectangleF(326, 181, 23, 23);
        }

        private void DrawToggleButton(Graphics graphics)
        {
            DrawToggleButton(graphics, ToggleButtonBounds());
        }

        private void DrawToggleButton(Graphics graphics, RectangleF rect)
        {
            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_appearance);
            DrawToggleButton(graphics, rect, theme.PrimaryText, 1F);
        }

        private void DrawToggleButton(Graphics graphics, RectangleF rect,
            Color labelColor, float opacity)
        {
            DrawToggleButton(graphics, rect, labelColor, opacity, 0F);
        }

        private void DrawToggleButton(Graphics graphics, RectangleF rect,
            Color labelColor, float opacity, float fontSize)
        {
            if (rect.Width <= 0F || rect.Height <= 0F) return;
            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_appearance);
            Color fill = _togglePressed
                ? Blend(theme.Accent, Color.Black, .2F)
                : (_toggleHovered
                    ? Blend(theme.Accent, Color.White, .18F)
                    : theme.Accent);
            using (var brush = new SolidBrush(fill))
            using (var edge = new Pen(theme.Border, 1f))
            using (var arrow = new Pen(
                EmilyDeskThemeCatalog.BestTextOn(fill), 2f))
            {
                graphics.FillEllipse(brush, rect);
                graphics.DrawEllipse(edge, rect);
                float cx = rect.X + rect.Width / 2f;
                float cy = rect.Y + rect.Height / 2f;
                if (_panelOpen)
                {
                    graphics.DrawLine(arrow, cx + 3, cy - 4, cx - 2, cy);
                    graphics.DrawLine(arrow, cx - 2, cy, cx + 3, cy + 4);
                }
                else
                {
                    graphics.DrawLine(arrow, cx - 3, cy - 4, cx + 2, cy);
                    graphics.DrawLine(arrow, cx + 2, cy, cx - 3, cy + 4);
                }
            }
            RectangleF labelBounds = PanelButtonLabelBounds(rect);
            using (var font = new Font("Segoe UI Semibold",
                fontSize > 0F ? Math.Max(4F, fontSize)
                    : Math.Max(8F, Math.Min(11F, rect.Height * .40F)),
                FontStyle.Regular, GraphicsUnit.Pixel))
            using (var text = new SolidBrush(Color.FromArgb(
                (int)(255F * Math.Max(0F, Math.Min(1F, opacity))),
                labelColor.R, labelColor.G, labelColor.B)))
            using (var format = new StringFormat
            {
                Alignment = StringAlignment.Far,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap
            })
                graphics.DrawString(_panelOpen ? "Close Panel" : "Open Panel",
                    font, text, labelBounds, format);
        }

        private static RectangleF PanelButtonLabelBounds(RectangleF button)
        {
            float width = Math.Max(72F, button.Height * 3.2F);
            return new RectangleF(button.Left - width - 6F, button.Top,
                width, button.Height);
        }

        private RectangleF ToggleControlBounds()
        {
            RectangleF button = ToggleButtonBounds();
            if (button.Width <= 0F || button.Height <= 0F)
                return RectangleF.Empty;
            return RectangleF.Union(button, PanelButtonLabelBounds(button));
        }

        private void DrawForecastPanel(
            Graphics graphics, WeatherSnapshot snapshot,
            WeatherCompositeLayout layout)
        {
            EmilyDeskTheme theme = EmilyDeskThemeCatalog.Get(_appearance);
            GraphicsState panelState = graphics.Save();
            try
            {
                float x = layout.PanelRectangle.X;
                graphics.SetClip(layout.ClipRectangle);
                RectangleF panelCard = IsModern || IsVintage
                    ? new RectangleF(
                        x - 18F, 5F, PanelWidth + 12F, 200F)
                    : new RectangleF(
                        x - 22F, 1F, PanelWidth + 21F, 208F);
                string regularFamily = IsVintage ? "Georgia" : "Segoe UI";
                string strongFamily = IsVintage ? "Georgia" : "Segoe UI Semibold";
                IndustrialDesignerLayout editable =
                    EditableCompactWeatherLayout();
                IndustrialDesignerElement editableBackground =
                    DesignerBackground(editable, 1);
                bool suppressBackground =
                    SuppressDefaultDesignerBackground(editable, 1);
                using (var path = Rounded(panelCard, 24))
                using (var title = new Font(strongFamily, 16f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var dayFont = new Font(strongFamily, 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var detail = new Font(regularFamily, 10f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var white = new SolidBrush(theme.PrimaryText))
                using (var muted = new SolidBrush(theme.SecondaryText))
                using (var separator = new Pen(Color.FromArgb(90, theme.Border), 1f))
                {
                    if (!suppressBackground && IsModern)
                    {
                        ModernThemePainter.FillGlassCard(
                            graphics, path, panelCard);
                        for (int row = 0; row < DefaultForecastDayCount; row++)
                            ModernThemePainter.FillInsetPanel(graphics,
                                new RectangleF(x + 14F,
                                    49F + row * 29F,
                                    PanelWidth - 30F, 24F), 6F);
                    }
                    else if (!suppressBackground && IsVintage)
                    {
                        VintageThemePainter.FillPaper(
                            graphics, path, panelCard);
                        VintageThemePainter.DrawDoubleBorder(
                            graphics, path,
                            RectangleF.Inflate(panelCard, -8F, -8F), 16F);
                    }
                    else if (!suppressBackground)
                    {
                        using (var brush = new LinearGradientBrush(
                            new RectangleF(x, 0, PanelWidth, WidgetHeight),
                            theme.SurfaceTop, theme.SurfaceBottom, 90F))
                        using (var edge = new Pen(theme.Border, 1.2F))
                        {
                            graphics.FillPath(brush, path);
                            graphics.DrawPath(edge, path);
                        }
                    }
                    if (editable != null)
                    {
                        graphics.TranslateTransform(x, 0F);
                        if (editableBackground != null &&
                            editableBackground.Visible)
                        {
                            RenderEditableDesignerBackground(graphics,
                                editableBackground.Bounds, true,
                                editableBackground.ImagePath,
                                editableBackground.Opacity,
                                editable.BackgroundLayerVersion >= 1);
                        }
                        DrawEditableWeatherLayers(graphics, snapshot, editable, 1);
                        return;
                    }
                    graphics.DrawString("4-Day Forecast", title, white,
                        x + 20, 16);
                    graphics.DrawLine(separator, x + 20, 43, x + PanelWidth - 18, 43);
                    if (IsVintage)
                        graphics.DrawLine(separator,
                            x + 20, 46, x + PanelWidth - 18, 46);

                    int count = Math.Min(DefaultForecastDayCount,
                        snapshot.ForecastDays.Count);
                    if (count == 0)
                    {
                        graphics.DrawString(snapshot.Loading ? "Loading forecast…" : "Forecast unavailable", detail, muted, x + 20, 61);
                    }
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            ForecastDay day = snapshot.ForecastDays[i];
                            float y = 52 + (i * 29);
                            string dayName = day.Date == DateTime.MinValue ? "Day " + (i + 1) : (i == 0 ? "Today" : day.Date.ToString("ddd", CultureInfo.CurrentCulture));
                            graphics.DrawString(dayName, dayFont, white, x + 20, y);
                            DrawThemeMiniWeatherSymbol(graphics,
                                new RectangleF(x + 78, y - 3, 26, 24),
                                day.Icon);
                            DrawTextFit(graphics, day.Condition, detail, detail, muted, new RectangleF(x + 111, y, 91, 18));
                            string range = Math.Round(ConvertTemperature(day.HighC)) + "° / " + Math.Round(ConvertTemperature(day.LowC)) + "°";
                            using (var right = new StringFormat { Alignment = StringAlignment.Far, FormatFlags = StringFormatFlags.NoWrap })
                                graphics.DrawString(range, dayFont, white, new RectangleF(x + 194, y, 65, 18), right);
                        }
                    }

                    graphics.DrawLine(separator, x + 20, 198, x + PanelWidth - 18, 198);
                }
            }
            finally { graphics.Restore(panelState); }
        }

        private void DrawArtDecoForecastPanel(
            Graphics graphics, WeatherSnapshot snapshot,
            WeatherCompositeLayout layout)
        {
            float panelHeight = layout.PanelRectangle.Height;
            float panelWidth = layout.PanelRectangle.Width;
            float drawX = layout.PanelRectangle.X;
            float drawY = layout.PanelRectangle.Y;
            GraphicsState panelState = graphics.Save();
            try
            {
                float revealRight = layout.PanelVisibleAlphaRectangle.Left +
                    layout.PanelVisibleAlphaRectangle.Width * layout.Progress;
                graphics.SetClip(new RectangleF(
                    0F, 0F, Math.Max(0F, revealRight),
                    layout.CompositeSize.Height));
                Image skin = GetScaledMirroredArtDecoPanelSkin(
                    layout.PanelRectangle.Size);
                graphics.DrawImage(skin,
                    new RectangleF(drawX, drawY, panelWidth, panelHeight));

                RectangleF safe = layout.SafeContentRectangle;
                RectangleF titleRectangle = WeatherDetailsTitleRectangle(safe);
                RectangleF dividerRectangle = WeatherDetailsDividerRectangle(safe);
                using (var teal = new Pen(Color.FromArgb(42, 154, 150), 1F))
                using (var titleFont = new Font("Georgia", 11.5F, FontStyle.Bold))
                using (var labelFont = new Font("Segoe UI", 7.5F))
                using (var valueFont = new Font("Segoe UI", 9F, FontStyle.Bold))
                using (var ivory = new SolidBrush(Color.FromArgb(247, 231, 188)))
                using (var muted = new SolidBrush(Color.FromArgb(190, 181, 157)))
                {
                    graphics.DrawString("Weather Details", titleFont,
                        ivory, titleRectangle);
                    graphics.DrawLine(teal, dividerRectangle.Left,
                        dividerRectangle.Top, dividerRectangle.Right,
                        dividerRectangle.Top);

                    List<WeatherDetail> details = BuildWeatherDetails(snapshot);
                    if (details.Count == 0)
                    {
                        graphics.DrawString(snapshot.Loading
                            ? "Loading weather details..."
                            : "Weather details unavailable",
                            valueFont, muted,
                            WeatherDetailsValueRectangle(safe, 0));
                    }
                    else
                    {
                        for (int index = 0; index < details.Count && index < 8; index++)
                        {
                            int row = index / 2;
                            RectangleF labelRectangle =
                                WeatherDetailsLabelRectangle(safe, index);
                            RectangleF valueRectangle =
                                WeatherDetailsValueRectangle(safe, index);
                            WeatherDetail detail = details[index];
                            graphics.DrawString(detail.Label, labelFont,
                                muted, labelRectangle);
                            DrawTextFit(graphics, detail.Value,
                                valueFont, labelFont, ivory,
                                valueRectangle);
                            if (row < 3)
                            {
                                RectangleF separator =
                                    WeatherDetailsSeparatorRectangle(safe, index);
                                graphics.DrawLine(teal, separator.Left,
                                    separator.Top, separator.Right,
                                    separator.Top);
                            }
                        }
                    }
                }
            }
            finally { graphics.Restore(panelState); }
        }

        private static RectangleF WeatherDetailsTitleRectangle(RectangleF safe)
        {
            return new RectangleF(safe.X, safe.Y, safe.Width, 28F);
        }

        private static RectangleF WeatherDetailsDividerRectangle(RectangleF safe)
        {
            return new RectangleF(safe.X, safe.Y + 35F, safe.Width, 1F);
        }

        private static RectangleF WeatherDetailsLabelRectangle(
            RectangleF safe, int index)
        {
            const float gap = 20F;
            float columnWidth = (safe.Width - gap) / 2F;
            int column = index % 2;
            int row = index / 2;
            float gridTop = safe.Y + 42F;
            float rowHeight = (safe.Height - 42F) / 4F;
            return new RectangleF(
                safe.X + column * (columnWidth + gap),
                gridTop + row * rowHeight,
                columnWidth, 18F);
        }

        private static RectangleF WeatherDetailsValueRectangle(
            RectangleF safe, int index)
        {
            RectangleF label = WeatherDetailsLabelRectangle(safe, index);
            return new RectangleF(label.X, label.Y + 19F, label.Width, 24F);
        }

        private static RectangleF WeatherDetailsSeparatorRectangle(
            RectangleF safe, int index)
        {
            const float gap = 20F;
            float columnWidth = (safe.Width - gap) / 2F;
            int row = index / 2;
            RectangleF label = WeatherDetailsLabelRectangle(safe, index);
            float rowHeight = (safe.Height - 42F) / 4F;
            return new RectangleF(
                label.X, safe.Y + 42F + (row + 1) * rowHeight - 2F,
                columnWidth, 1F);
        }

        private static RectangleF ArtDecoWeatherDetailsSafeBounds(
            RectangleF panelBounds)
        {
            return RectangleF.FromLTRB(
                panelBounds.Left + 116F, panelBounds.Top + 64F,
                panelBounds.Right - 48F, panelBounds.Bottom - 58F);
        }

        private List<WeatherDetail> BuildWeatherDetails(WeatherSnapshot snapshot)
        {
            var details = new List<WeatherDetail>();
            if (snapshot.HasFeelsLike)
                details.Add(new WeatherDetail { Label = "Feels Like",
                    Value = Math.Round(snapshot.FeelsLike) + Degree() });
            if (snapshot.HasHumidity)
                details.Add(new WeatherDetail { Label = "Humidity",
                    Value = snapshot.Humidity + "%" });
            if (snapshot.HasWind)
                details.Add(new WeatherDetail { Label = "Wind",
                    Value = (string.IsNullOrWhiteSpace(snapshot.WindDirection)
                        ? string.Empty : snapshot.WindDirection + "  ") +
                        Math.Round(snapshot.Wind) + (_metric ? " km/h" : " mph") });
            if (snapshot.HasPressure)
                details.Add(new WeatherDetail { Label = "Pressure",
                    Value = _metric
                        ? Math.Round(snapshot.Pressure) + " hPa"
                        : snapshot.Pressure.ToString("0.00", CultureInfo.CurrentCulture) + " inHg" });
            if (snapshot.HasDewPoint)
                details.Add(new WeatherDetail { Label = "Dew Point",
                    Value = Math.Round(snapshot.DewPoint) + Degree() });
            if (snapshot.HasVisibility)
                details.Add(new WeatherDetail { Label = "Visibility",
                    Value = snapshot.Visibility.ToString("0.#", CultureInfo.CurrentCulture) +
                        (_metric ? " km" : " mi") });
            if (snapshot.HasUvIndex)
                details.Add(new WeatherDetail { Label = "UV Index",
                    Value = snapshot.UvIndex.ToString("0.#", CultureInfo.CurrentCulture) +
                        (string.IsNullOrWhiteSpace(snapshot.UvText)
                            ? string.Empty : "  " + snapshot.UvText) });
            if (!string.IsNullOrWhiteSpace(snapshot.Sunrise) ||
                !string.IsNullOrWhiteSpace(snapshot.Sunset))
                details.Add(new WeatherDetail { Label = "Sunrise / Sunset",
                    Value = (snapshot.Sunrise ?? string.Empty) + " / " +
                        (snapshot.Sunset ?? string.Empty) });
            return details;
        }

        private static Image GetScaledArtDecoMainSkin(SizeF size,
            string designerPath, bool hasDesignerLayout)
        {
            if (hasDesignerLayout && string.IsNullOrWhiteSpace(designerPath))
                return null;
            if (!string.IsNullOrWhiteSpace(designerPath))
            {
                string customPath = ResolveDesignerAssetPath(designerPath);
                if (File.Exists(customPath))
                {
                    using (Image source = Image.FromFile(customPath))
                    {
                        var custom = new Bitmap(
                            Math.Max(1, (int)Math.Round(size.Width)),
                            Math.Max(1, (int)Math.Round(size.Height)),
                            System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                        using (Graphics graphics = Graphics.FromImage(custom))
                        {
                            graphics.Clear(Color.Transparent);
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            graphics.DrawImage(source,
                                new Rectangle(0, 0, custom.Width, custom.Height));
                        }
                        return custom;
                    }
                }
            }
            if (_scaledArtDecoMainSkin != null)
                return _scaledArtDecoMainSkin;
            lock (ArtDecoPanelSkinSync)
            {
                if (_scaledArtDecoMainSkin != null)
                    return _scaledArtDecoMainSkin;
                Image source = ThemeSkinCache.Get(
                    @"Assets\Themes\ArtDeco\art-deco-calendar-skin.png");
                var scaled = new Bitmap(
                    Math.Max(1, (int)Math.Round(size.Width)),
                    Math.Max(1, (int)Math.Round(size.Height)),
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(scaled))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(source,
                        new Rectangle(0, 0, scaled.Width, scaled.Height));
                }
                _scaledArtDecoMainSkin = scaled;
                return _scaledArtDecoMainSkin;
            }
        }

        private static string ResolveDesignerAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            if (path.StartsWith("theme://",
                StringComparison.OrdinalIgnoreCase))
                return EmilyDeskThemeCatalog.ResolveThemeUri(path) ??
                    string.Empty;
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                path.Replace('/', Path.DirectorySeparatorChar));
        }

        private static Image GetScaledMirroredArtDecoPanelSkin(SizeF size)
        {
            if (_scaledMirroredArtDecoPanelSkin != null)
                return _scaledMirroredArtDecoPanelSkin;
            lock (ArtDecoPanelSkinSync)
            {
                if (_scaledMirroredArtDecoPanelSkin != null)
                    return _scaledMirroredArtDecoPanelSkin;
                Image source = ThemeSkinCache.Get(
                    @"Assets\Themes\ArtDeco\art-deco-forecast-panel-skin.png");
                var mirrored = new Bitmap(
                    Math.Max(1, (int)Math.Round(size.Width)),
                    Math.Max(1, (int)Math.Round(size.Height)),
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(mirrored))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    var points = new[]
                    {
                        new PointF(mirrored.Width, 0F),
                        new PointF(0F, 0F),
                        new PointF(mirrored.Width, mirrored.Height)
                    };
                    graphics.DrawImage(source, points,
                        new RectangleF(0F, 0F, source.Width, source.Height),
                        GraphicsUnit.Pixel);
                }
                _scaledMirroredArtDecoPanelSkin = mirrored;
                return _scaledMirroredArtDecoPanelSkin;
            }
        }

        private Image GetScaledIndustrialWeatherSkin(SizeF size)
        {
            if (IsImportedTheme) return GetScaledImportedSkin(
                IndustrialPresentationWeatherSkinPath, size);
            Image cached = IsWoodland ? _scaledWoodlandWeatherSkin
                : IsBotanical ? _scaledBotanicalWeatherSkin
                : IsSteampunk ? _scaledSteampunkWeatherSkin
                : _scaledIndustrialWeatherSkin;
            if (cached != null) return cached;
            lock (ArtDecoPanelSkinSync)
            {
                cached = IsWoodland ? _scaledWoodlandWeatherSkin
                    : IsBotanical ? _scaledBotanicalWeatherSkin
                    : IsSteampunk ? _scaledSteampunkWeatherSkin
                    : _scaledIndustrialWeatherSkin;
                if (cached != null) return cached;
                cached = ScaleSkinAlphaCrop(
                    IndustrialPresentationWeatherSkinPath, size);
                if (IsWoodland) _scaledWoodlandWeatherSkin = cached;
                else if (IsBotanical) _scaledBotanicalWeatherSkin = cached;
                else if (IsSteampunk) _scaledSteampunkWeatherSkin = cached;
                else _scaledIndustrialWeatherSkin = cached;
                return cached;
            }
        }

        private Image GetScaledIndustrialPanelSkin(SizeF size)
        {
            if (IsImportedTheme) return GetScaledImportedSkin(
                IndustrialPresentationPanelSkinPath, size);
            Image cached = IsWoodland ? _scaledWoodlandPanelSkin
                : IsBotanical ? _scaledBotanicalPanelSkin
                : IsSteampunk ? _scaledSteampunkPanelSkin
                : _scaledIndustrialPanelSkin;
            if (cached != null) return cached;
            lock (ArtDecoPanelSkinSync)
            {
                cached = IsWoodland ? _scaledWoodlandPanelSkin
                    : IsBotanical ? _scaledBotanicalPanelSkin
                    : IsSteampunk ? _scaledSteampunkPanelSkin
                    : _scaledIndustrialPanelSkin;
                if (cached != null) return cached;
                cached = ScaleSkinAlphaCrop(
                    IndustrialPresentationPanelSkinPath, size);
                if (IsWoodland) _scaledWoodlandPanelSkin = cached;
                else if (IsBotanical) _scaledBotanicalPanelSkin = cached;
                else if (IsSteampunk) _scaledSteampunkPanelSkin = cached;
                else _scaledIndustrialPanelSkin = cached;
                return cached;
            }
        }

        private static Image GetScaledImportedSkin(string path, SizeF size)
        {
            ThemeSkinCache.Get(path);
            string key = path + "|" + ThemeSkinCache.LoadCount(path) + "|" +
                Math.Round(size.Width, 2) + "x" +
                Math.Round(size.Height, 2);
            lock (ArtDecoPanelSkinSync)
            {
                Image image;
                if (ImportedScaledSkins.TryGetValue(key, out image)) return image;
                image = ScaleSkinAlphaCrop(path, size);
                ImportedScaledSkins[key] = image;
                return image;
            }
        }

        private static Image ScaleSkin(string path, SizeF size)
        {
            Image source = ThemeSkinCache.Get(path);
            var scaled = new Bitmap(
                Math.Max(1, (int)Math.Round(size.Width)),
                Math.Max(1, (int)Math.Round(size.Height)),
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source,
                    new Rectangle(0, 0, scaled.Width, scaled.Height));
            }
            return scaled;
        }

        private static Image ScaleSkinCover(string path, SizeF size)
        {
            Image source = ThemeSkinCache.Get(path);
            var scaled = new Bitmap(
                Math.Max(1, (int)Math.Round(size.Width)),
                Math.Max(1, (int)Math.Round(size.Height)),
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            float destinationAspect = scaled.Width / (float)scaled.Height;
            float sourceAspect = source.Width / (float)source.Height;
            RectangleF sourceRectangle;
            if (sourceAspect > destinationAspect)
            {
                float cropWidth = source.Height * destinationAspect;
                sourceRectangle = new RectangleF(
                    (source.Width - cropWidth) / 2F, 0F,
                    cropWidth, source.Height);
            }
            else
            {
                float cropHeight = source.Width / destinationAspect;
                sourceRectangle = new RectangleF(
                    0F, (source.Height - cropHeight) / 2F,
                    source.Width, cropHeight);
            }
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source,
                    new RectangleF(0F, 0F, scaled.Width, scaled.Height),
                    sourceRectangle, GraphicsUnit.Pixel);
            }
            return scaled;
        }

        private static Image ScaleSkinAlphaCrop(string path, SizeF size)
        {
            Image source = ThemeSkinCache.Get(path);
            Rectangle alpha = ThemeSkinCache.GetAlphaBounds(path);
            var scaled = new Bitmap(
                Math.Max(1, (int)Math.Round(size.Width)),
                Math.Max(1, (int)Math.Round(size.Height)),
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics graphics = Graphics.FromImage(scaled))
            {
                graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(source,
                    new RectangleF(0F, 0F, scaled.Width, scaled.Height),
                    alpha, GraphicsUnit.Pixel);
            }
            return scaled;
        }


        private static void DrawModernWeatherFrame(Graphics graphics)
        {
            ModernThemePainter.FillInsetPanel(graphics,
                new RectangleF(15F, 43F, 112F, 108F), 15F);
            ModernThemePainter.FillInsetPanel(graphics,
                new RectangleF(16F, 152F, 328F, 35F), 9F);
            using (var halo = new SolidBrush(
                Color.FromArgb(30, ModernThemePainter.Accent)))
            using (var accent = new SolidBrush(
                ModernThemePainter.Accent))
            using (var glow = new Pen(
                Color.FromArgb(115,
                    ModernThemePainter.AccentBright), 1F))
            {
                graphics.FillEllipse(halo, 29F, 54F, 84F, 84F);
                graphics.FillRectangle(accent, 132F, 49F, 3F, 96F);
                graphics.DrawLine(glow, 20F, 39F, 340F, 39F);
                graphics.FillEllipse(accent, 18F, 36F, 6F, 6F);
            }
        }

        private static void DrawVintageWeatherFrame(Graphics graphics)
        {
            using (var rule = new Pen(
                Color.FromArgb(120, VintageThemePainter.DarkBrass), 1F))
            using (var ornament = new SolidBrush(
                Color.FromArgb(180, VintageThemePainter.Brass)))
            {
                graphics.DrawLine(rule, 20F, 39F, 340F, 39F);
                graphics.DrawLine(rule, 128F, 47F, 128F, 146F);
                graphics.FillEllipse(ornament, 17F, 36F, 6F, 6F);
                graphics.FillEllipse(ornament, 337F, 36F, 6F, 6F);
            }
        }

        private void DrawThemeMiniWeatherSymbol(
            Graphics graphics, RectangleF rect, int icon)
        {
            if (!IsVintage)
            {
                DrawMiniWeatherSymbol(graphics, rect, icon);
                return;
            }
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(rect.X, rect.Y);
                graphics.ScaleTransform(rect.Width / 96F, rect.Height / 92F);
                DrawVintageWeatherSymbol(graphics,
                    new RectangleF(0, 0, 96, 92), icon);
            }
            finally { graphics.Restore(state); }
        }

        private void DrawThemeWeatherSymbol(
            Graphics graphics, RectangleF rect, int icon)
        {
            if (IsVintage)
                DrawVintageWeatherSymbol(graphics, rect, icon);
            else
                DrawWeatherSymbol(graphics, rect, icon);
        }

        private static void DrawMiniWeatherSymbol(Graphics graphics, RectangleF rect, int icon)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform(rect.X, rect.Y);
                graphics.ScaleTransform(rect.Width / 96f, rect.Height / 92f);
                DrawWeatherSymbol(graphics, new RectangleF(0, 0, 96, 92), icon);
            }
            finally { graphics.Restore(state); }
        }

        private static void DrawWeatherSymbolFitted(
            Graphics graphics, RectangleF bounds, int icon)
        {
            DrawWeatherSymbolFitted(graphics, bounds, icon, 0, 0, 0F, 0F);
        }

        private static void DrawWeatherSymbolFitted(
            Graphics graphics, RectangleF bounds, int icon,
            int horizontalPlacement, int verticalPlacement,
            float offsetX, float offsetY)
        {
            Rectangle sourceBounds;
            Bitmap symbol = FittedWeatherSymbol(icon, out sourceBounds);
            float symbolWidth = sourceBounds.Width;
            float symbolHeight = sourceBounds.Height;
            float scale = Math.Min(
                bounds.Width / symbolWidth,
                bounds.Height / symbolHeight);
            float width = symbolWidth * scale;
            float height = symbolHeight * scale;
            float x = horizontalPlacement == 1 ? bounds.X
                : horizontalPlacement == 2 ? bounds.Right - width
                : bounds.X + (bounds.Width - width) / 2F;
            float y = verticalPlacement == 1 ? bounds.Y
                : verticalPlacement == 2 ? bounds.Bottom - height
                : bounds.Y + (bounds.Height - height) / 2F;
            GraphicsState state = graphics.Save();
            try
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.DrawImage(symbol, new RectangleF(
                        x + offsetX, y + offsetY, width, height),
                    sourceBounds, GraphicsUnit.Pixel);
            }
            finally { graphics.Restore(state); }
        }

        private static Bitmap FittedWeatherSymbol(int icon,
            out Rectangle visibleBounds)
        {
            lock (WeatherSymbolCacheSync)
            {
                Bitmap cached;
                if (FittedWeatherSymbols.TryGetValue(icon, out cached))
                {
                    visibleBounds = FittedWeatherBounds[icon];
                    return cached;
                }
                // This cache is also used for the large current-condition
                // artwork.  A 128px source was visibly soft once a widget was
                // enlarged, so rasterise the vector-style fallback at 4x and
                // then downsample it for the requested bounds.
                const float cacheScale = 4F;
                var bitmap = new Bitmap(512, 512,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.Clear(Color.Transparent);
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.InterpolationMode =
                        InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.CompositingQuality =
                        CompositingQuality.HighQuality;
                    graphics.ScaleTransform(cacheScale, cacheScale);
                    DrawWeatherSymbol(graphics,
                        new RectangleF(16F, 16F, 96F, 92F), icon);
                }
                int left = bitmap.Width;
                int top = bitmap.Height;
                int right = -1;
                int bottom = -1;
                for (int y = 0; y < bitmap.Height; y++)
                    for (int x = 0; x < bitmap.Width; x++)
                        if (bitmap.GetPixel(x, y).A != 0)
                        {
                            if (x < left) left = x;
                            if (x > right) right = x;
                            if (y < top) top = y;
                            if (y > bottom) bottom = y;
                        }
                visibleBounds = right < left || bottom < top
                    ? new Rectangle(16, 16, 96, 92)
                    : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
                FittedWeatherSymbols[icon] = bitmap;
                FittedWeatherBounds[icon] = visibleBounds;
                return bitmap;
            }
        }

        private double ConvertTemperature(double c) { return _metric ? c : c * 9.0 / 5.0 + 32.0; }
        private string Degree() { return _metric ? "°C" : "°F"; }
        private void RequestInvalidate() { var a = _invalidate; if (a != null) a(); else if (_host != null) _host.Invalidate(); }

        private static GraphicsPath Rounded(RectangleF rect, float radius)
        {
            float d = radius * 2;
            var p = new GraphicsPath();
            p.AddArc(rect.X, rect.Y, d, d, 180, 90); p.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            p.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90); p.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        private static void DrawBotanicalMainFrame(
            Graphics graphics,
            RectangleF bounds,
            float thickness,
            float radius)
        {
            RectangleF outer = RectangleF.Inflate(bounds,
                -thickness / 2F, -thickness / 2F);
            using (GraphicsPath outerPath = Rounded(outer, radius))
            using (var sage = new Pen(
                Color.FromArgb(174, 180, 139), thickness))
                graphics.DrawPath(sage, outerPath);

            RectangleF darkLine = RectangleF.Inflate(
                outer, -thickness / 2F - 1F,
                -thickness / 2F - 1F);
            using (GraphicsPath darkPath = Rounded(
                darkLine, Math.Max(4F, radius - thickness)))
            using (var darkSage = new Pen(
                Color.FromArgb(99, 117, 91), 2F))
                graphics.DrawPath(darkSage, darkPath);

            RectangleF highlight = RectangleF.Inflate(
                darkLine, -3F, -3F);
            using (GraphicsPath highlightPath = Rounded(
                highlight, Math.Max(3F,
                    radius - thickness - 3F)))
            using (var ivory = new Pen(
                Color.FromArgb(245, 243, 228), 1.5F))
                graphics.DrawPath(ivory, highlightPath);
        }

        private static Color Blend(Color first, Color second, float amount)
        {
            float value = Math.Max(0F, Math.Min(1F, amount));
            return Color.FromArgb(
                (int)(first.R + (second.R - first.R) * value),
                (int)(first.G + (second.G - first.G) * value),
                (int)(first.B + (second.B - first.B) * value));
        }

        private static void DrawWeatherSymbol(Graphics g, RectangleF r, int icon)
        {
            bool rainy = (icon >= 12 && icon <= 18) || icon == 39 || icon == 40;
            bool snowy = (icon >= 19 && icon <= 29) || icon == 43 || icon == 44;
            bool cloudy = icon >= 6;
            using (var sun = new SolidBrush(Color.FromArgb(255, 247, 191, 68)))
            using (var cloud = new SolidBrush(Color.FromArgb(250, 228, 238, 244)))
            using (var shade = new SolidBrush(Color.FromArgb(245, 174, 198, 214)))
            using (var ray = new Pen(Color.FromArgb(230, 247, 191, 68), 3f))
            using (var rain = new Pen(Color.FromArgb(255, 105, 190, 235), 3.5f))
            using (var snow = new Pen(Color.White, 2f))
            {
                if (!cloudy || icon <= 11)
                {
                    float cx = r.X + 38;
                    float cy = r.Y + 31;
                    for (int i = 0; i < 8; i++)
                    {
                        double angle = Math.PI * i / 4.0;
                        float x1 = cx + (float)Math.Cos(angle) * 32f;
                        float y1 = cy + (float)Math.Sin(angle) * 32f;
                        float x2 = cx + (float)Math.Cos(angle) * 40f;
                        float y2 = cy + (float)Math.Sin(angle) * 40f;
                        g.DrawLine(ray, x1, y1, x2, y2);
                    }
                    g.FillEllipse(sun, cx - 27, cy - 27, 54, 54);
                }
                if (cloudy)
                {
                    g.FillEllipse(shade, r.X + 19, r.Y + 39, 47, 34);
                    g.FillEllipse(cloud, r.X + 39, r.Y + 24, 46, 48);
                    g.FillEllipse(cloud, r.X + 5, r.Y + 46, 80, 37);
                }
                if (rainy)
                    for (int i = 0; i < 3; i++) g.DrawLine(rain, r.X + 24 + i * 22, r.Y + 82, r.X + 18 + i * 22, r.Y + 94);
                if (snowy)
                    for (int i = 0; i < 3; i++)
                    {
                        float x = r.X + 21 + i * 24;
                        float y = r.Y + 88;
                        g.DrawLine(snow, x - 5, y, x + 5, y);
                        g.DrawLine(snow, x, y - 5, x, y + 5);
                    }
            }
        }

        private static void DrawVintageWeatherSymbol(
            Graphics graphics, RectangleF bounds, int icon)
        {
            bool rainy = (icon >= 12 && icon <= 18) ||
                icon == 39 || icon == 40;
            bool snowy = (icon >= 19 && icon <= 29) ||
                icon == 43 || icon == 44;
            bool cloudy = icon >= 6;
            Color sunColor = Color.FromArgb(223, 164, 54);
            Color cloudColor = Color.FromArgb(242, 229, 196);
            Color shadeColor = Color.FromArgb(190, 166, 125);
            Color rainColor = Color.FromArgb(82, 105, 111);
            Color outlineColor = VintageThemePainter.DarkBrass;
            using (var sun = new SolidBrush(sunColor))
            using (var cloud = new SolidBrush(cloudColor))
            using (var shade = new SolidBrush(shadeColor))
            using (var outline = new Pen(outlineColor, 2.1F))
            using (var fineOutline = new Pen(
                Color.FromArgb(175, outlineColor), 1.1F))
            using (var ray = new Pen(VintageThemePainter.Brass, 2.4F))
            using (var rain = new Pen(rainColor, 3F))
            using (var snow = new Pen(
                Color.FromArgb(235, 244, 236, 210), 2F))
            {
                if (!cloudy || icon <= 11)
                {
                    float centerX = bounds.X + 38F;
                    float centerY = bounds.Y + 31F;
                    for (int rayIndex = 0; rayIndex < 12; rayIndex++)
                    {
                        double angle = Math.PI * rayIndex / 6D;
                        graphics.DrawLine(ray,
                            centerX + (float)Math.Cos(angle) * 31F,
                            centerY + (float)Math.Sin(angle) * 31F,
                            centerX + (float)Math.Cos(angle) * 40F,
                            centerY + (float)Math.Sin(angle) * 40F);
                    }
                    graphics.FillEllipse(sun,
                        centerX - 25F, centerY - 25F, 50F, 50F);
                    graphics.DrawEllipse(outline,
                        centerX - 25F, centerY - 25F, 50F, 50F);
                    graphics.DrawEllipse(fineOutline,
                        centerX - 19F, centerY - 19F, 38F, 38F);
                }
                if (cloudy)
                {
                    RectangleF backCloud = new RectangleF(
                        bounds.X + 19F, bounds.Y + 39F, 47F, 34F);
                    RectangleF tallCloud = new RectangleF(
                        bounds.X + 39F, bounds.Y + 24F, 46F, 48F);
                    RectangleF frontCloud = new RectangleF(
                        bounds.X + 5F, bounds.Y + 46F, 80F, 37F);
                    graphics.FillEllipse(shade, backCloud);
                    graphics.DrawEllipse(fineOutline, backCloud);
                    graphics.FillEllipse(cloud, tallCloud);
                    graphics.DrawEllipse(outline, tallCloud);
                    graphics.FillEllipse(cloud, frontCloud);
                    graphics.DrawEllipse(outline, frontCloud);
                    graphics.DrawLine(outline,
                        bounds.X + 15F, bounds.Y + 81F,
                        bounds.X + 77F, bounds.Y + 81F);
                }
                if (rainy)
                    for (int index = 0; index < 3; index++)
                        graphics.DrawLine(rain,
                            bounds.X + 24F + index * 22F,
                            bounds.Y + 82F,
                            bounds.X + 18F + index * 22F,
                            bounds.Y + 94F);
                if (snowy)
                    for (int index = 0; index < 3; index++)
                    {
                        float x = bounds.X + 21F + index * 24F;
                        float y = bounds.Y + 88F;
                        graphics.DrawLine(snow, x - 5F, y, x + 5F, y);
                        graphics.DrawLine(snow, x, y - 5F, x, y + 5F);
                        graphics.DrawLine(snow,
                            x - 3.5F, y - 3.5F, x + 3.5F, y + 3.5F);
                        graphics.DrawLine(snow,
                            x + 3.5F, y - 3.5F, x - 3.5F, y + 3.5F);
                    }
            }
        }

        private static void DrawTextFit(Graphics graphics, string text, Font preferred, Font fallback, Brush brush, RectangleF bounds)
        {
            string value = text ?? string.Empty;
            Font chosen = graphics.MeasureString(value, preferred).Width <= bounds.Width ? preferred : fallback;
            using (var format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                graphics.DrawString(value, chosen, brush, bounds, format);
            }
        }

        private static void DrawDetailColumn(Graphics graphics, string label, string value, Font labelFont, Font valueFont, Brush labelBrush, Brush valueBrush, float x, float y, float width)
        {
            using (var centered = new StringFormat())
            {
                centered.Alignment = StringAlignment.Center;
                centered.LineAlignment = StringAlignment.Near;
                centered.Trimming = StringTrimming.EllipsisCharacter;
                centered.FormatFlags = StringFormatFlags.NoWrap;
                graphics.DrawString(label, labelFont, labelBrush, new RectangleF(x, y, width, 16), centered);
                graphics.DrawString(value, valueFont, valueBrush, new RectangleF(x, y + 14, width, 17), centered);
            }
        }

        private static void DrawIndustrialDivider(
            Graphics graphics, IndustrialDesignerLayout layout,
            string id, Pen fallbackPen, RectangleF fallbackBounds,
            bool referenceOnly)
        {
            if (referenceOnly) return;
            IndustrialDesignerElement element =
                layout == null ? null : layout.Find(id);
            if (element == null)
            {
                if (fallbackBounds.Width >= fallbackBounds.Height)
                    graphics.DrawLine(fallbackPen, fallbackBounds.Left,
                        fallbackBounds.Top + fallbackBounds.Height / 2F,
                        fallbackBounds.Right,
                        fallbackBounds.Top + fallbackBounds.Height / 2F);
                else
                    graphics.DrawLine(fallbackPen,
                        fallbackBounds.Left + fallbackBounds.Width / 2F,
                        fallbackBounds.Top,
                        fallbackBounds.Left + fallbackBounds.Width / 2F,
                        fallbackBounds.Bottom);
                return;
            }
            if (!element.Visible) return;
            DrawIndustrialDesignerDividerElement(graphics, element);
        }

        private static void DrawAllIndustrialDesignerMainDividers(
            Graphics graphics, IndustrialDesignerLayout layout)
        {
            if (graphics == null || layout == null ||
                layout.Elements == null) return;

            foreach (IndustrialDesignerElement element in layout.Elements)
            {
                if (element == null || !element.Visible ||
                    element.Surface != 0 || element.Kind != 2) continue;
                DrawIndustrialDesignerDividerElement(graphics, element);
            }
        }

        private static void DrawIndustrialDesignerDividerElement(
            Graphics graphics, IndustrialDesignerElement element)
        {
            RectangleF bounds = element.Bounds;
            Color color = Color.FromArgb(element.ColorArgb);
            color = Color.FromArgb(
                (int)(255F * Math.Max(0F, Math.Min(1F, element.Opacity))),
                color.R, color.G, color.B);
            float thickness = Math.Max(1F,
                Math.Min(bounds.Width, bounds.Height));
            using (var pen = new Pen(color, thickness))
            {
                if (bounds.Width >= bounds.Height)
                    graphics.DrawLine(pen, bounds.Left,
                        bounds.Top + bounds.Height / 2F,
                        bounds.Right, bounds.Top + bounds.Height / 2F);
                else
                    graphics.DrawLine(pen,
                        bounds.Left + bounds.Width / 2F, bounds.Top,
                        bounds.Left + bounds.Width / 2F, bounds.Bottom);
            }
        }

        private void DrawIndustrialDesignerMetric(
            Graphics graphics, IndustrialDesignerLayout layout,
            string id, string label, string value,
            Font labelFont, Font valueFont,
            Brush labelBrush, Brush valueBrush,
            float x, float y, float width)
        {
            IndustrialDesignerElement element =
                layout == null ? null : layout.Find(id);
            if (element == null)
            {
                DrawIndustrialMetricColumn(graphics, label, value,
                    labelFont, valueFont, labelBrush, valueBrush,
                    x, y, width);
                return;
            }
            // Woodland's current conditions are deliberately label/value
            // pairs.  The Designer stores one movable metric box per pair,
            // but it must not flatten that box into an ellipsized sentence.
            // Use the saved box as the anchor while retaining the approved
            // two-line typography.
            if (UsesWoodlandNaturePresentation)
            {
                RectangleF bounds = element.Bounds;
                DrawIndustrialMetricColumn(graphics, label, value,
                    labelFont, valueFont, labelBrush, valueBrush,
                    bounds.X, bounds.Y, bounds.Width);
                return;
            }
            DrawIndustrialDesignerText(
                graphics, element, label + " " + value);
        }

        private static void DrawIndustrialDesignerText(
            Graphics graphics, IndustrialDesignerElement element, string text)
        {
            if (element == null || !element.Visible) return;
            FontStyle style = FontStyle.Regular;
            if (element.Bold) style |= FontStyle.Bold;
            if (element.Italic) style |= FontStyle.Italic;
            float fontSize = Math.Max(4F,
                element.FontSize * Math.Max(.05F, element.Scale));
            Color color = Color.FromArgb(element.ColorArgb);
            color = Color.FromArgb(
                (int)(255F * Math.Max(0F, Math.Min(1F, element.Opacity))),
                color.R, color.G, color.B);
            using (var font = DesignerFontResolver.Create(
                element.FontName, element.FontFile, fontSize, style))
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat())
            {
                format.Alignment = element.Alignment == 1
                    ? StringAlignment.Center
                    : element.Alignment == 2
                        ? StringAlignment.Far : StringAlignment.Near;
                format.LineAlignment = StringAlignment.Center;
                if (element.WordWrap == false)
                    format.FormatFlags |= StringFormatFlags.NoWrap;
                format.Trimming = ToDesignerStringTrimming(
                    element.Trimming);
                graphics.DrawString(text ?? string.Empty, font, brush,
                    element.Bounds, format);
            }
        }

        private static StringTrimming ToDesignerStringTrimming(int trimming)
        {
            switch (trimming)
            {
                case 1: return StringTrimming.Character;
                case 2: return StringTrimming.Word;
                case 3: return StringTrimming.EllipsisCharacter;
                case 4: return StringTrimming.EllipsisWord;
                case 5: return StringTrimming.EllipsisPath;
                default: return StringTrimming.None;
            }
        }

        private static void DrawIndustrialMetricColumn(
            Graphics graphics, string label, string value,
            Font labelFont, Font valueFont,
            Brush labelBrush, Brush valueBrush,
            float x, float y, float width)
        {
            using (var centered = new StringFormat())
            {
                centered.Alignment = StringAlignment.Center;
                centered.LineAlignment = StringAlignment.Near;
                centered.Trimming = StringTrimming.EllipsisCharacter;
                centered.FormatFlags = StringFormatFlags.NoWrap;
                float labelHeight = MeasureTypographic(
                    graphics, label, labelFont).Height;
                float valueHeight = MeasureTypographic(
                    graphics, value, valueFont).Height;
                graphics.DrawString(label, labelFont, labelBrush,
                    new RectangleF(x, y, width, labelHeight), centered);
                graphics.DrawString(value, valueFont, valueBrush,
                    new RectangleF(x, y + labelHeight + 7.12F,
                        width, valueHeight), centered);
            }
        }

        private static SizeF MeasureTypographic(
            Graphics graphics, string text, Font font)
        {
            return graphics.MeasureString(
                text ?? string.Empty, font,
                PointF.Empty, StringFormat.GenericTypographic);
        }

        private static string Trim(string value, int max) { if (string.IsNullOrEmpty(value) || value.Length <= max) return value; return value.Substring(0, max - 1) + "…"; }
        private static IDictionary<string, object> AsMap(object value) { return value as IDictionary<string, object> ?? new Dictionary<string, object>(); }
        private static IList AsList(object value) { return value as IList ?? new object[0]; }
        private static object Get(IDictionary<string, object> map, string key) { object value; return map != null && map.TryGetValue(key, out value) ? value : null; }
        private static string Text(IDictionary<string, object> map, string key, string fallback) { object v = Get(map, key); return v == null ? fallback : Convert.ToString(v, CultureInfo.InvariantCulture); }
        private static int Integer(IDictionary<string, object> map, string key, int fallback) { object v = Get(map, key); int x; return v != null && int.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), out x) ? x : fallback; }
        private static double Number(IDictionary<string, object> map, string key, double fallback) { object v = Get(map, key); double x; return v != null && double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out x) ? x : fallback; }
        private static bool TryNumber(
            IDictionary<string, object> map, string key, out double value)
        {
            value = 0D;
            object raw = Get(map, key);
            return raw != null && double.TryParse(
                Convert.ToString(raw, CultureInfo.InvariantCulture),
                NumberStyles.Any, CultureInfo.InvariantCulture, out value);
        }
        private static bool TryMetricValue(
            IDictionary<string, object> unit, out double value)
        {
            value = 0D;
            var metric = AsMap(Get(unit, "Metric"));
            if (metric.Count > 0)
                return TryNumber(metric, "Value", out value);
            return TryNumber(unit, "Value", out value);
        }
        private static string ProviderTime(string raw)
        {
            DateTime value;
            return !string.IsNullOrWhiteSpace(raw) && DateTime.TryParse(
                raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out value)
                ? value.ToString("h:mm tt", CultureInfo.CurrentCulture)
                : string.Empty;
        }
        private static double MetricValue(IDictionary<string, object> unit)
        {
            var metric = AsMap(Get(unit, "Metric"));
            if (metric.Count > 0) return Number(metric, "Value", 0);
            return Number(unit, "Value", 0);
        }

        private sealed class LocationChoice { public string Key; public string Name; }
        private sealed class ForecastDay
        {
            public DateTime Date; public int Icon; public string Condition; public double HighC; public double LowC;
        }
        private sealed class WeatherSnapshot
        {
            public string Location; public string Condition; public int Icon; public double Temperature; public double FeelsLike;
            public bool HasFeelsLike; public int Humidity; public bool HasHumidity;
            public double Wind; public bool HasWind; public string WindDirection;
            public double Pressure; public bool HasPressure; public double DewPoint; public bool HasDewPoint;
            public double Visibility; public bool HasVisibility; public double UvIndex; public bool HasUvIndex; public string UvText;
            public string Sunrise; public string Sunset;
            public double High; public double Low; public bool Loading; public string Error; public DateTime Updated;
            public List<ForecastDay> ForecastDays;
        }
        private sealed class WeatherDetail
        {
            public string Label;
            public string Value;
        }
    }
}
