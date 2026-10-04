using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using XWidgetReborn.WidgetSdk;

namespace EmilyDesk.Widgets.CurrencyConverter
{
    public sealed class CurrencyConverterWidget :
        IWidget,
        IOfficialWidget,
        IWidgetDoubleClickInput,
        IWidgetPointerInput,
        IWidgetSnapBoundsProvider,
        IWidgetDesignerProvider,
        IWidgetDesignerBackgroundLayerProvider,
        IWidgetDesignerTextBackgroundProvider,
        IWidgetThemeProvider,
        IRuntimeAwareWidget
    {
#if ART_DECO_WIDGET
        private const int BaseWidth = 550;
        private const int BaseHeight = 363;
        private const int PreferredWidth = 460;
        private const int PreferredHeight = 304;
        private const string WidgetId = "utility.art-deco-currency-converter";
        private const string WidgetName = "Art Deco Currency Converter";
        private const string WidgetVersion = "1.0.3";
        private const string ThemeName = "Art Deco";
        private const string SkinFileName = "art-deco-currency-converter-skin.png";
        private const int TitlePlateX = 120, TitlePlateY = 52,
            TitlePlateWidth = 310, TitlePlateHeight = 34;
        private const int TitleX = 133, TitleY = 53, TitleWidth = 284;
        private const int SubtitleY = 69;
        private const int AmountLabelX = 62, AmountLabelY = 82;
        private const int AmountPanelX = 50, AmountPanelY = 98,
            AmountPanelWidth = 450, AmountPanelHeight = 62;
        private const int ValueX = 70, ValueWidth = 332;
        private const int AmountValueY = 105;
        private const int BadgeX = 420, FromBadgeY = 109,
            ToBadgeY = 198, BadgeWidth = 62, BadgeHeight = 40;
        private const int ConvertLabelX = 201, ConvertLabelY = 160,
            ConvertLabelWidth = 148;
        private const int DividerY = 176, DividerLeftEnd = 190,
            DividerRightStart = 360;
        private const int ResultPanelY = 184, ResultPanelHeight = 66,
            ResultValueY = 190;
        private const int RateX = 65, RateY = 253, RateWidth = 420;
        private const int StatusY = 274;
        private const int InstructionX = 185, InstructionY = 295,
            InstructionWidth = 180;
#elif EMBER_GLOW_WIDGET
        private const int BaseWidth = 550;
        private const int BaseHeight = 363;
        private const int PreferredWidth = BaseWidth;
        private const int PreferredHeight = BaseHeight;
        private const string WidgetId = "utility.ember-glow-currency-converter";
        private const string WidgetName = "Ember Glow Currency Converter";
        private const string WidgetVersion = "1.0.13";
        private const string ThemeName = "Ember Glow";
        private const string SkinFileName = "ember-glow-currency-converter-skin.png";
        private const string LcdFileName = "ember-glow-calculator-lcd.png";
        private const int TitlePlateX = 0, TitlePlateY = 0,
            TitlePlateWidth = 0, TitlePlateHeight = 0;
        private const int TitleX = 133, TitleY = 42, TitleWidth = 284;
        private const int SubtitleY = 59;
        private const int AmountLabelX = 58, AmountLabelY = 75;
        private const int AmountPanelX = 48, AmountPanelY = 65,
            AmountPanelWidth = 454, AmountPanelHeight = 74;
        private const int ValueX = 68, ValueWidth = 332;
        private const int AmountValueY = 75;
        private const int BadgeX = 419, FromBadgeY = 82,
            ToBadgeY = 221, BadgeWidth = 65, BadgeHeight = 41;
        private const int ConvertLabelX = 201, ConvertLabelY = 164,
            ConvertLabelWidth = 148;
        private const int DividerY = 171, DividerLeftEnd = 231,
            DividerRightStart = 319;
        private const int ResultPanelY = 204, ResultPanelHeight = 74,
            ResultValueY = 211;
        private const int RateX = 65, RateY = 263, RateWidth = 420;
        private const int StatusY = 279;
        private const int InstructionX = 185, InstructionY = 290,
            InstructionWidth = 180;
#elif INDUSTRIAL_WIDGET
        private const int BaseWidth = 550;
        private const int BaseHeight = 363;
        private const int PreferredWidth = 460;
        private const int PreferredHeight = 304;
        private const string WidgetId = "utility.industrial-currency-converter";
        private const string WidgetName = "Industrial Currency Converter";
        private const string WidgetVersion = "1.0.2";
        private const string ThemeName = "Industrial";
        private const string SkinFileName = "industrial-currency-converter-skin.png";
        private const int TitlePlateX = 120, TitlePlateY = 14,
            TitlePlateWidth = 310, TitlePlateHeight = 42;
        private const int TitleX = 133, TitleY = 18, TitleWidth = 284;
        private const int SubtitleY = 38;
        private const int AmountLabelX = 62, AmountLabelY = 67;
        private const int AmountPanelX = 50, AmountPanelY = 88,
            AmountPanelWidth = 450, AmountPanelHeight = 70;
        private const int ValueX = 70, ValueWidth = 332;
        private const int AmountValueY = 97;
        private const int BadgeX = 420, FromBadgeY = 101,
            ToBadgeY = 205, BadgeWidth = 62, BadgeHeight = 44;
        private const int ConvertLabelX = 201, ConvertLabelY = 161,
            ConvertLabelWidth = 148;
        private const int DividerY = 175, DividerLeftEnd = 190,
            DividerRightStart = 360;
        private const int ResultPanelY = 190, ResultPanelHeight = 74,
            ResultValueY = 198;
        private const int RateX = 65, RateY = 270, RateWidth = 420;
        private const int StatusY = 291;
        private const int InstructionX = 185, InstructionY = 318,
            InstructionWidth = 180;
#elif WOODLAND_WIDGET
        private const int BaseWidth = 550;
        private const int BaseHeight = 363;
        private const int PreferredWidth = 460;
        private const int PreferredHeight = 304;
        private const string WidgetId = "utility.woodland-currency-converter";
        private const string WidgetName = "Woodland Nature Currency Converter";
        private const string WidgetVersion = "1.0.1";
        private const string ThemeName = "Woodland Nature";
        private const string SkinFileName = "woodland-currency-converter-skin.png";
        private const int TitlePlateX = 120, TitlePlateY = 14,
            TitlePlateWidth = 310, TitlePlateHeight = 42;
        private const int TitleX = 133, TitleY = 18, TitleWidth = 284;
        private const int SubtitleY = 38;
        private const int AmountLabelX = 62, AmountLabelY = 67;
        private const int AmountPanelX = 50, AmountPanelY = 88,
            AmountPanelWidth = 450, AmountPanelHeight = 70;
        private const int ValueX = 70, ValueWidth = 332;
        private const int AmountValueY = 97;
        private const int BadgeX = 420, FromBadgeY = 101,
            ToBadgeY = 205, BadgeWidth = 62, BadgeHeight = 44;
        private const int ConvertLabelX = 201, ConvertLabelY = 161,
            ConvertLabelWidth = 148;
        private const int DividerY = 175, DividerLeftEnd = 190,
            DividerRightStart = 360;
        private const int ResultPanelY = 190, ResultPanelHeight = 74,
            ResultValueY = 198;
        private const int RateX = 65, RateY = 270, RateWidth = 420;
        private const int StatusY = 291;
        private const int InstructionX = 185, InstructionY = 318,
            InstructionWidth = 180;
#elif BOTANICAL_WIDGET
        private const int BaseWidth = 550;
        private const int BaseHeight = 363;
        private const int PreferredWidth = 460;
        private const int PreferredHeight = 304;
        private const string WidgetId = "utility.botanical-currency-converter";
        private const string WidgetName = "Botanical Nature Currency Converter";
        private const string WidgetVersion = "1.0.0";
        private const string ThemeName = "Botanical Nature";
        private const string SkinFileName = "botanical-currency-converter-skin.png";
        private const int TitlePlateX = 120, TitlePlateY = 14,
            TitlePlateWidth = 310, TitlePlateHeight = 42;
        private const int TitleX = 133, TitleY = 18, TitleWidth = 284;
        private const int SubtitleY = 38;
        private const int AmountLabelX = 62, AmountLabelY = 67;
        private const int AmountPanelX = 50, AmountPanelY = 88,
            AmountPanelWidth = 450, AmountPanelHeight = 70;
        private const int ValueX = 70, ValueWidth = 332;
        private const int AmountValueY = 97;
        private const int BadgeX = 420, FromBadgeY = 101,
            ToBadgeY = 205, BadgeWidth = 62, BadgeHeight = 44;
        private const int ConvertLabelX = 201, ConvertLabelY = 161,
            ConvertLabelWidth = 148;
        private const int DividerY = 175, DividerLeftEnd = 190,
            DividerRightStart = 360;
        private const int ResultPanelY = 190, ResultPanelHeight = 74,
            ResultValueY = 198;
        private const int RateX = 65, RateY = 270, RateWidth = 420;
        private const int StatusY = 291;
        private const int InstructionX = 185, InstructionY = 318,
            InstructionWidth = 180;
#else
        private const int BaseWidth = 420;
        private const int BaseHeight = 390;
        private const int PreferredWidth = BaseWidth;
        private const int PreferredHeight = BaseHeight;
        private const string WidgetId = "utility.currency-converter";
        private const string WidgetName = "Steampunk Currency Converter";
        private const string WidgetVersion = "1.0.2";
        private const string ThemeName = "Steampunk";
        private const string SkinFileName = "steampunk-currency-converter-skin.png";
        private const int TitlePlateX = 90, TitlePlateY = 23,
            TitlePlateWidth = 240, TitlePlateHeight = 39;
        private const int TitleX = 108, TitleY = 28, TitleWidth = 204;
        private const int SubtitleY = 46;
        private const int AmountLabelX = 52, AmountLabelY = 78;
        private const int AmountPanelX = 45, AmountPanelY = 98,
            AmountPanelWidth = 330, AmountPanelHeight = 76;
        private const int ValueX = 60, ValueWidth = 226;
        private const int AmountValueY = 109;
        private const int BadgeX = 300, FromBadgeY = 113,
            ToBadgeY = 225, BadgeWidth = 58, BadgeHeight = 44;
        private const int ConvertLabelX = 151, ConvertLabelY = 177,
            ConvertLabelWidth = 118;
        private const int DividerY = 191, DividerLeftEnd = 148,
            DividerRightStart = 272;
        private const int ResultPanelY = 207, ResultPanelHeight = 80,
            ResultValueY = 218;
        private const int RateX = 54, RateY = 293, RateWidth = 312;
        private const int StatusY = 313;
        private const int InstructionX = 130, InstructionY = 337,
            InstructionWidth = 160;
#endif
        private static readonly string[] Currencies =
        {
            "AED", "AFN", "ALL", "AMD", "ANG", "AOA", "ARS", "AUD",
            "AWG", "AZN", "BAM", "BBD", "BDT", "BGN", "BHD", "BIF",
            "BMD", "BND", "BOB", "BRL", "BSD", "BTN", "BWP", "BYN",
            "BZD", "CAD", "CDF", "CHF", "CLF", "CLP", "CNH", "CNY",
            "COP", "CRC", "CUP", "CVE", "CZK", "DJF", "DKK", "DOP",
            "DZD", "EGP", "ERN", "ETB", "EUR", "FJD", "FKP", "FOK",
            "GBP", "GEL", "GGP", "GHS", "GIP", "GMD", "GNF", "GTQ",
            "GYD", "HKD", "HNL", "HRK", "HTG", "HUF", "IDR", "ILS",
            "IMP", "INR", "IQD", "IRR", "ISK", "JEP", "JMD", "JOD", "JPY",
            "KES", "KGS", "KHR", "KID", "KMF", "KRW", "KWD", "KYD",
            "KZT", "LAK", "LBP", "LKR", "LRD", "LSL", "LYD", "MAD",
            "MDL", "MGA", "MKD", "MMK", "MNT", "MOP", "MRU", "MUR",
            "MVR", "MWK", "MXN", "MYR", "MZN", "NAD", "NGN", "NIO",
            "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP",
            "PKR", "PLN", "PYG", "QAR", "RON", "RSD", "RUB", "RWF",
            "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLE", "SLL",
            "SOS", "SRD", "SSP", "STN", "SYP", "SZL", "THB", "TJS",
            "TMT", "TND", "TOP", "TRY", "TTD", "TVD", "TWD", "TZS",
            "UAH", "UGX", "USD", "UYU", "UZS", "VES", "VND", "VUV",
            "WST", "XAF", "XCD", "XCG", "XDR", "XOF", "XPF", "YER", "ZAR",
            "ZMW", "ZWG", "ZWL"
        };

        private readonly object _sync = new object();
        private Action _invalidate;
        private IWidgetHostContext _host;
        private IRuntimeContext _runtime;
        private string _from = "CAD";
        private string _to = "USD";
        private decimal _amount = 1M;
        private decimal _rate;
        private string _rateDate = string.Empty;
        private string _status = "Retrieving current rate...";
        private DateTime _lastAttemptUtc = DateTime.MinValue;
        private bool _refreshing;
        private bool _paused;
        private bool _disposed;
        private Image _panelSkin;
        private bool _panelSkinAttempted;
        private Image _swapButton;
        private bool _swapButtonAttempted;
#if EMBER_GLOW_WIDGET
        private Image _reflectiveRim;
        private bool _reflectiveRimAttempted;
        private bool _drawingDesignerBackground;
#endif
        private Size _lastRenderSize = new Size(BaseWidth, BaseHeight);
        private bool _swapPressed;

        public string Id { get { return WidgetId; } }
        public string Name { get { return WidgetName; } }
        public string Description
        {
            get { return "Converts 165+ currencies using searchable daily reference exchange rates."; }
        }
        public string Version { get { return WidgetVersion; } }
        public Size DefaultSize
        { get { return new Size(PreferredWidth, PreferredHeight); } }
        public Point DefaultLocation { get { return new Point(820, 110); } }
        public WidgetUpdateRate UpdateRate { get { return WidgetUpdateRate.Minute; } }
        public IEnumerable<string> Themes
        {
            get { return OptionalWidgetThemeCatalog.AvailableThemes(WidgetId); }
        }
        public string Theme
        {
            get { return ThemeName; }
            set
            {
                string target = OptionalWidgetThemeCatalog.TargetWidgetId(
                    WidgetId, value);
                if (_runtime == null || string.IsNullOrWhiteSpace(target) ||
                    string.Equals(target, WidgetId,
                        StringComparison.OrdinalIgnoreCase))
                    return;
                Point position = _runtime.Settings.GetPosition(DefaultLocation);
                _runtime.Events.Publish("widget.switch-theme",
                    WidgetId + "\t" + target + "\t" +
                    position.X.ToString(CultureInfo.InvariantCulture) + "\t" +
                    position.Y.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void AttachHost(IWidgetHostContext host)
        {
            _host = host;
            if (_host == null) return;
            _from = NormalizeCurrency(_host.GetSetting("from", "CAD"), "CAD");
            _to = NormalizeCurrency(_host.GetSetting("to", "USD"), "USD");
            decimal amount;
            if (decimal.TryParse(
                _host.GetSetting("amount", "1"),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount) && amount > 0M)
                _amount = amount;
            _host.SetPreferredSize(DefaultSize);
            _host.SetWindowShape(WidgetWindowShape.AlphaRectangle);
        }

        public void AttachRuntime(IRuntimeContext runtime)
        {
            if (_runtime != null)
                _runtime.Events.Published -= DesignerSaved;
            _runtime = runtime;
            if (_runtime != null)
                _runtime.Events.Published += DesignerSaved;
        }

        private void DesignerSaved(object sender, WidgetRuntimeEventArgs e)
        {
            if (e != null && e.Topic == "designer.saved")
                RequestInvalidate();
        }

        public void Start(Action invalidate)
        {
            _invalidate = invalidate;
            BeginRefresh(true);
        }

        public void Tick(DateTime now)
        {
            if (_paused) return;
            if (DateTime.UtcNow.Subtract(_lastAttemptUtc).TotalHours >= 24)
                BeginRefresh(false);
        }

        public void Pause() { _paused = true; }
        public void Resume()
        {
            _paused = false;
            BeginRefresh(false);
            RequestInvalidate();
        }

        private void BeginRefresh(bool force)
        {
            string from;
            string to;
            lock (_sync)
            {
                if (_disposed) return;
                if (_refreshing)
                {
                    if (force) _lastAttemptUtc = DateTime.MinValue;
                    return;
                }
                if (!force && DateTime.UtcNow.Subtract(
                        _lastAttemptUtc).TotalMinutes < 5)
                    return;
                _refreshing = true;
                _lastAttemptUtc = DateTime.UtcNow;
                _status = "Retrieving current rate...";
                from = _from;
                to = _to;
            }
            RequestInvalidate();
            ThreadPool.QueueUserWorkItem(delegate
            {
                decimal rate = 0M;
                string date = string.Empty;
                try
                {
                    if (string.Equals(from, to, StringComparison.Ordinal))
                    {
                        rate = 1M;
                        date = DateTime.Today.ToString("yyyy-MM-dd");
                    }
                    else
                    {
                        ServicePointManager.SecurityProtocol |=
                            (SecurityProtocolType)3072;
                        using (var client = new WebClient())
                        {
                            client.Headers[HttpRequestHeader.UserAgent] =
                                "EmilyDesk Currency Converter/1.0.1";
                            string json = client.DownloadString(
                                "https://open.er-api.com/v6/latest/" +
                                Uri.EscapeDataString(from));
                            var serializer = new JavaScriptSerializer();
                            var root = serializer.DeserializeObject(json)
                                as Dictionary<string, object>;
                            object resultValue;
                            if (root == null ||
                                !root.TryGetValue("result", out resultValue) ||
                                !string.Equals(Convert.ToString(resultValue),
                                    "success", StringComparison.OrdinalIgnoreCase))
                                throw new InvalidOperationException(
                                    "The rate service did not accept the request.");
                            object ratesValue;
                            Dictionary<string, object> rates = null;
                            if (root.TryGetValue("rates", out ratesValue))
                                rates = ratesValue as Dictionary<string, object>;
                            object rateValue;
                            if (rates == null ||
                                !rates.TryGetValue(to, out rateValue))
                                throw new InvalidOperationException(
                                    "The rate service returned no matching currency.");
                            rate = Convert.ToDecimal(
                                rateValue,
                                CultureInfo.InvariantCulture);
                            object dateValue;
                            if (root.TryGetValue(
                                "time_last_update_utc", out dateValue))
                            {
                                DateTime updated;
                                string raw = Convert.ToString(dateValue,
                                    CultureInfo.InvariantCulture);
                                date = DateTime.TryParse(raw, out updated)
                                    ? updated.ToString("yyyy-MM-dd") : raw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "Currency rate refresh failed: " + ex.Message);
                }

                bool refreshAgain;
                lock (_sync)
                {
                    if (_disposed) return;
                    refreshAgain = from != _from || to != _to;
                    if (from == _from && to == _to && rate > 0M)
                    {
                        _rate = rate;
                        _rateDate = date;
                        _status = "Rates By Exchange Rate API • " + date;
                    }
                    else if (rate <= 0M)
                    {
                        _status = _rate > 0M
                            ? "Could not update; showing the last rate"
                            : "Rate unavailable - check the internet connection";
                    }
                    _refreshing = false;
                }
                RequestInvalidate();
                if (refreshAgain) BeginRefresh(true);
            });
        }

        public void Render(Graphics graphics, Rectangle bounds)
        {
            _lastRenderSize = bounds.Size;
            string from;
            string to;
            string status;
            decimal amount;
            decimal rate;
            bool refreshing;
            lock (_sync)
            {
                from = _from;
                to = _to;
                amount = _amount;
                rate = _rate;
                status = _status;
                refreshing = _refreshing;
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.TextRenderingHint =
                System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            GraphicsState state = graphics.Save();
            float scale = Math.Min(
                bounds.Width / (float)BaseWidth,
                bounds.Height / (float)BaseHeight);
            graphics.TranslateTransform(
                bounds.Left + (bounds.Width - BaseWidth * scale) / 2F,
                bounds.Top + (bounds.Height - BaseHeight * scale) / 2F);
            graphics.ScaleTransform(scale, scale);
            try
            {
                SavedDesignerLayout layout = SavedDesignerLayout.Current(
                    DesignerTheme, DesignerKind);
                if (layout == null)
                    DrawConverter(graphics, from, to, amount, rate, status,
                        refreshing, true);
                else
                    DrawSavedLayout(graphics, layout);
                if (_paused)
                    using (var paused = new SolidBrush(
                        Color.FromArgb(155, 20, 12, 9)))
                        graphics.FillRectangle(paused, 1, 1,
                            BaseWidth - 2, BaseHeight - 2);
            }
            finally { graphics.Restore(state); }
        }

        private void DrawConverter(
            Graphics graphics,
            string from,
            string to,
            decimal amount,
            decimal rate,
            string status,
            bool refreshing,
            bool drawText)
        {
            DrawConverterBackground(graphics, null);
            DrawConverterChrome(graphics, from, to, amount, rate,
                status, refreshing, drawText);
        }

        private void DrawConverterBackground(
            Graphics graphics, string imagePath)
        {
            if (!string.IsNullOrWhiteSpace(imagePath) &&
                DesignerLayerPainter.DrawImage(graphics,
                    ResolvePackageAsset(imagePath),
                    new RectangleF(0, 0, BaseWidth, BaseHeight), 1F))
                return;
            Image skin = PanelSkin;
            if (skin != null)
                graphics.DrawImage(skin,
                    new Rectangle(0, 0, BaseWidth, BaseHeight));
            else DrawFallbackPanel(graphics);
        }

        private void DrawConverterChrome(
            Graphics graphics,
            string from,
            string to,
            decimal amount,
            decimal rate,
            string status,
            bool refreshing,
            bool drawText)
        {
#if INDUSTRIAL_WIDGET && !ART_DECO_WIDGET
            DrawIndustrialTitlePlate(graphics,
                new Rectangle(TitlePlateX, TitlePlateY,
                    TitlePlateWidth, TitlePlateHeight));
#elif WOODLAND_WIDGET
            DrawWoodlandTitlePlate(graphics,
                new Rectangle(TitlePlateX, TitlePlateY,
                    TitlePlateWidth, TitlePlateHeight));
#elif BOTANICAL_WIDGET
            DrawBotanicalTitlePlate(graphics,
                new Rectangle(TitlePlateX, TitlePlateY,
                    TitlePlateWidth, TitlePlateHeight));
#endif

            Color titleColor = TitleInk;
            Color labelColor = LabelInk;
            Color valueColor = ValueInk;
            Color resultColor = AccentInk;
            Color mutedColor = MutedInk;

            if (!drawText)
            {
                DrawValuePanel(graphics, new Rectangle(AmountPanelX,
                    AmountPanelY, AmountPanelWidth, AmountPanelHeight));
#if !EMBER_GLOW_WIDGET
                DrawCurrencyBadge(graphics, string.Empty,
                    new Rectangle(BadgeX, FromBadgeY,
                        BadgeWidth, BadgeHeight));
#endif
                using (var divider = new Pen(
                    DividerInk, 1.5F))
                {
                    graphics.DrawLine(divider, AmountLabelX, DividerY,
                        DividerLeftEnd, DividerY);
                    graphics.DrawLine(divider, DividerRightStart, DividerY,
                        BaseWidth - AmountLabelX, DividerY);
                }
                DrawValuePanel(graphics, new Rectangle(AmountPanelX,
                    ResultPanelY, AmountPanelWidth, ResultPanelHeight));
#if !EMBER_GLOW_WIDGET
                DrawCurrencyBadge(graphics, string.Empty,
                    new Rectangle(BadgeX, ToBadgeY,
                        BadgeWidth, BadgeHeight));
#endif
            }

            if (drawText)
            using (var title = new Font("Georgia", 11.5F, FontStyle.Bold))
            using (var subtitle = new Font(
                "Segoe UI", 6.5F, FontStyle.Bold))
            using (var label = new Font("Segoe UI", 10.5F, FontStyle.Bold))
            using (var amountFont = new Font(
                "Georgia", 25F, FontStyle.Bold))
            using (var resultFont = new Font(
                "Georgia", 27F, FontStyle.Bold))
            using (var rateFont = new Font(
                "Segoe UI", 10.5F, FontStyle.Bold))
            using (var small = new Font(
                "Segoe UI", 9F, FontStyle.Regular))
            using (var instruction = new Font(
                "Segoe UI", 7.5F, FontStyle.Bold))
            {
#if !EMBER_GLOW_WIDGET
                DrawText(graphics, "CURRENCY", title,
                    titleColor,
                    new Rectangle(TitleX, TitleY, TitleWidth, 22),
                    StringAlignment.Center);
                DrawText(graphics, "CONVERTER", subtitle,
                    titleColor,
                    new Rectangle(TitleX, SubtitleY, TitleWidth, 13),
                    StringAlignment.Center);

                DrawText(graphics, "AMOUNT", label, labelColor,
                    new Rectangle(AmountLabelX, AmountLabelY, 130, 20),
                    StringAlignment.Near);
#endif
                Rectangle amountPanel = new Rectangle(AmountPanelX,
                    AmountPanelY, AmountPanelWidth, AmountPanelHeight);
                DrawValuePanel(graphics, amountPanel);
                DrawFittedText(graphics, FormatAmount(amount), amountFont,
                    valueColor,
                    new Rectangle(ValueX, AmountValueY, ValueWidth, 52),
                    StringAlignment.Near, 15F);
                DrawCurrencyBadge(graphics, from,
                    new Rectangle(BadgeX, FromBadgeY,
                        BadgeWidth, BadgeHeight));

                using (var divider = new Pen(
                    DividerInk, 1.5F))
                {
                    graphics.DrawLine(divider, AmountLabelX, DividerY,
                        DividerLeftEnd, DividerY);
                    graphics.DrawLine(divider, DividerRightStart, DividerY,
                        BaseWidth - AmountLabelX, DividerY);
                }
#if !EMBER_GLOW_WIDGET
                DrawText(graphics, "CONVERTS TO", label, labelColor,
                    new Rectangle(ConvertLabelX, ConvertLabelY,
                        ConvertLabelWidth, 28),
                    StringAlignment.Center);
#endif

                Rectangle resultPanel = new Rectangle(AmountPanelX,
                    ResultPanelY, AmountPanelWidth, ResultPanelHeight);
                DrawValuePanel(graphics, resultPanel);
                DrawFittedText(graphics,
                    rate > 0M
                        ? FormatAmount(amount * rate)
                        : (refreshing ? "Retrieving..." : "Unavailable"),
                    resultFont,
                    rate > 0M ? resultColor : valueColor,
                    new Rectangle(ValueX, ResultValueY, ValueWidth, 56),
                    StringAlignment.Near, 15F);
                DrawCurrencyBadge(graphics, to,
                    new Rectangle(BadgeX, ToBadgeY,
                        BadgeWidth, BadgeHeight));

#if !EMBER_GLOW_WIDGET
                DrawText(graphics,
                    rate > 0M
                        ? "1 " + from + " = " +
                            rate.ToString("0.######") + " " + to
                        : status,
                    rateFont, valueColor,
                    new Rectangle(RateX, RateY, RateWidth, 21),
                    StringAlignment.Center);
                DrawText(graphics,
                    rate > 0M
                        ? status
                        : "Daily reference rates  •  Right-click to retry",
                    small, mutedColor,
                    new Rectangle(RateX, StatusY, RateWidth, 19),
                    StringAlignment.Center);
                DrawText(graphics, "DOUBLE-CLICK TO CHANGE",
                    instruction, AccentInk,
                    new Rectangle(InstructionX, InstructionY,
                        InstructionWidth, 20),
                    StringAlignment.Center);
#endif
            }
#if EMBER_GLOW_WIDGET
            Image swap = SwapButton;
            if (swap != null)
                graphics.DrawImage(swap, new Rectangle(249, 151, 52, 52));
#endif

        }

        private void DrawDesignerLayout(Graphics graphics,
            SavedDesignerLayout layout)
        {
            if (layout == null || layout.Elements == null) return;
            foreach (DesignerLayer layer in layout.Elements)
            {
                if (layer == null || IsBackgroundLayer(layer)) continue;
                if (IsLcdLayer(layer))
                {
                    if (layer.Visible)
                        DesignerLayerPainter.DrawImage(graphics,
                            ResolvePackageAsset(layer.ImagePath),
                            layer.Bounds, layer.Opacity);
                    continue;
                }
                RenderDesignerTextBackground(graphics, layer);
                float originalSize = layer.FontSize;
                try
                {
                    if (string.Equals(layer.Binding, "Currency: Amount",
                            StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(layer.Binding, "Currency: Result",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        string text = ResolveDesignerText(layer);
                        float size = originalSize;
                        while (size > 15F)
                        {
                            using (Font font = DesignerFontResolver.Create(
                                layer.FontName, layer.FontFile,
                                size * Math.Max(.05F, layer.Scale),
                                (layer.Bold ? FontStyle.Bold :
                                    FontStyle.Regular) |
                                (layer.Italic ? FontStyle.Italic :
                                    FontStyle.Regular)))
                                if (graphics.MeasureString(text, font).Width <=
                                    layer.Bounds.Width - 2F) break;
                            size = Math.Max(15F, size - 1F);
                        }
                        layer.FontSize = size;
                    }
                    DesignerLayerPainter.Draw(graphics, layer,
                        ResolveDesignerText);
                }
                finally { layer.FontSize = originalSize; }
            }
        }

        private static bool IsLcdLayer(DesignerLayer layer)
        {
#if EMBER_GLOW_WIDGET
            return layer != null && (string.Equals(layer.Id, "amount-lcd",
                StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layer.Id, "result-lcd",
                StringComparison.OrdinalIgnoreCase));
#else
            return false;
#endif
        }

        public void RenderDesignerTextBackground(Graphics graphics,
            DesignerLayer layer)
        {
#if EMBER_GLOW_WIDGET
            if (layer == null || !layer.Visible || layer.Kind != 0) return;
            if (string.Equals(layer.Id, "from", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(layer.Id, "to", StringComparison.OrdinalIgnoreCase))
                DrawCurrencyBadge(graphics, string.Empty,
                    Rectangle.Round(layer.Bounds));
#endif
        }

        private void DrawValuePanel(
            Graphics graphics,
            Rectangle bounds)
        {
#if EMBER_GLOW_WIDGET
            if (_drawingDesignerBackground) return;
            if (!DesignerLayerPainter.DrawImage(graphics,
                    ResolvePackageAsset(LcdFileName), bounds, 1F))
                using (GraphicsPath path = RoundedRectangle(bounds, 14))
                using (var brush = new SolidBrush(Color.FromArgb(20, 11, 8)))
                    graphics.FillPath(brush, path);
#else
            using (GraphicsPath path = RoundedRectangle(bounds, 10))
            using (var glow = new Pen(
                PanelGlow, 5F))
            using (var brush = new LinearGradientBrush(
                bounds,
                PanelTop,
                PanelBottom,
                90F))
            using (var border = new Pen(
                PanelBorder, 1.5F))
            {
                graphics.DrawPath(glow, path);
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
            }
#endif
        }

        private static void DrawCurrencyBadge(
            Graphics graphics,
            string currency,
            Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 8))
            using (var brush = new LinearGradientBrush(
                bounds,
                BadgeTop,
                BadgeBottom,
                90F))
            using (var border = new Pen(
                AccentInk, 1.5F))
            using (var font = new Font(
                "Georgia", 13F, FontStyle.Bold))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                DrawText(graphics, currency, font,
                    ValueInk,
                    bounds, StringAlignment.Center);
            }
        }

        public bool PointerDoubleClick(
            Point location,
            WidgetPointerButton button)
        {
            if (button != WidgetPointerButton.Left) return false;
            ShowSettings();
            return true;
        }

        public bool PointerDown(Point location, WidgetPointerButton button)
        {
#if EMBER_GLOW_WIDGET
            if (button == WidgetPointerButton.Left && !_paused)
            {
                Point p = ToBase(location);
                if (new Rectangle(249, 151, 52, 52).Contains(p))
                {
                    _swapPressed = true;
                    return true;
                }
            }
#endif
            return false;
        }

        public void PointerMove(Point location) { }

        public void PointerUp(Point location, WidgetPointerButton button)
        {
#if EMBER_GLOW_WIDGET
            bool activate = _swapPressed && button == WidgetPointerButton.Left &&
                new Rectangle(249, 151, 52, 52).Contains(ToBase(location));
            _swapPressed = false;
            if (activate) SwapCurrencies();
#endif
        }

        public void PointerLeave() { _swapPressed = false; }

        private Point ToBase(Point location)
        {
            return new Point(
                _lastRenderSize.Width <= 0 ? location.X :
                    (int)Math.Round(location.X * BaseWidth /
                        (double)_lastRenderSize.Width),
                _lastRenderSize.Height <= 0 ? location.Y :
                    (int)Math.Round(location.Y * BaseHeight /
                        (double)_lastRenderSize.Height));
        }

        private void SwapCurrencies()
        {
            lock (_sync)
            {
                string temporary = _from;
                _from = _to;
                _to = temporary;
                _rate = 0M;
                _lastAttemptUtc = DateTime.MinValue;
            }
            SaveSettings();
            BeginRefresh(true);
        }

        public void ShowSettings()
        {
            using (var form = new Form
            {
                Text = WidgetName + " Settings",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterScreen,
                ClientSize = new Size(370, 244),
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                Font = new Font("Segoe UI", 9F)
            })
            using (var from = CurrencyList(_from, 115, 28))
            using (var to = CurrencyList(_to, 115, 72))
            using (var amount = new NumericUpDown
            {
                Location = new Point(115, 116),
                Size = new Size(220, 26),
                DecimalPlaces = 2,
                Minimum = 0.01M,
                Maximum = 1000000000M,
                ThousandsSeparator = true,
                Value = Math.Max(0.01M, Math.Min(1000000000M, _amount))
            })
            {
                form.Controls.Add(Label("From", 24, 31));
                form.Controls.Add(Label("To", 24, 75));
                form.Controls.Add(Label("Amount", 24, 119));
                form.Controls.Add(from);
                form.Controls.Add(to);
                form.Controls.Add(amount);
                form.Controls.Add(new Label
                {
                    Text = "Type a currency code to search (for example PHP).",
                    AutoSize = true,
                    ForeColor = Color.DimGray,
                    Location = new Point(24, 154)
                });
                var cancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(153, 193),
                    Size = new Size(86, 32)
                };
                var save = new Button
                {
                    Text = "Save",
                    DialogResult = DialogResult.OK,
                    Location = new Point(249, 193),
                    Size = new Size(86, 32)
                };
                form.Controls.Add(cancel);
                form.Controls.Add(save);
                form.CancelButton = cancel;
                form.AcceptButton = save;
                if (form.ShowDialog() != DialogResult.OK) return;
                lock (_sync)
                {
                    _from = NormalizeCurrency(from.Text, "CAD");
                    _to = NormalizeCurrency(to.Text, "USD");
                    _amount = amount.Value;
                    _rate = 0M;
                    _lastAttemptUtc = DateTime.MinValue;
                }
                SaveSettings();
                BeginRefresh(true);
            }
        }

        public void ResetSettings()
        {
            lock (_sync)
            {
                _from = "CAD";
                _to = "USD";
                _amount = 1M;
                _rate = 0M;
                _lastAttemptUtc = DateTime.MinValue;
            }
            if (_host != null)
            {
                _host.Scale = 1F;
                _host.Opacity = 1D;
            }
            SaveSettings();
            BeginRefresh(true);
        }

        public IEnumerable<WidgetMenuCommand> GetMenuCommands()
        {
            return new[]
            {
                new WidgetMenuCommand(
                    "Swap currencies",
                    false,
                    SwapCurrencies),
                new WidgetMenuCommand(
                    "Refresh exchange rate",
                    false,
                    delegate { BeginRefresh(true); }),
                new WidgetMenuCommand(
                    "Rates By Exchange Rate API",
                    false,
                    OpenRateProvider),
                new WidgetMenuCommand(
                    "Edit in Designer", false, OpenDesigner)
            };
        }

        public string DesignerKind { get { return "currency-converter"; } }
        public string DesignerTheme { get { return ThemeName; } }

        public SavedDesignerLayout CreateDesignerLayout()
        {
            SavedDesignerLayout layout = NewDesignerLayout(
                WidgetName, BaseWidth, BaseHeight);
            AddBackground(layout);
#if EMBER_GLOW_WIDGET
            AddLcd(layout, "amount-lcd", "Amount LCD Screen",
                AmountPanelY);
            AddLcd(layout, "result-lcd", "Result LCD Screen",
                ResultPanelY);
#endif
#if !EMBER_GLOW_WIDGET
            AddText(layout, "title", "Title", "CURRENCY", "None",
                TitleX, TitleY, TitleWidth, 22, "Georgia", 11.5F, true,
                TitleInk, 1);
            AddText(layout, "subtitle", "Subtitle", "CONVERTER", "None",
                TitleX, SubtitleY, TitleWidth, 13, "Segoe UI", 6.5F, true,
                TitleInk, 1);
            AddText(layout, "amount-label", "Amount Label", "AMOUNT", "None",
                AmountLabelX, AmountLabelY, 130, 20, "Segoe UI", 10.5F, true,
                LabelInk, 0);
#endif
            AddText(layout, "amount", "Amount", "1.00", "Currency: Amount",
                ValueX, AmountValueY, ValueWidth, 52, "Georgia", 25F, true,
                ValueInk, 0);
            AddText(layout, "from", "Source Currency", "CAD", "Currency: From",
                BadgeX, FromBadgeY, BadgeWidth, BadgeHeight,
                "Georgia", 13F, true,
                ValueInk, 1);
#if !EMBER_GLOW_WIDGET
            AddText(layout, "converts-to", "Converts To Label", "CONVERTS TO", "None",
                ConvertLabelX, ConvertLabelY, ConvertLabelWidth, 28,
                "Segoe UI", 10.5F, true,
                LabelInk, 1);
#endif
            AddText(layout, "result", "Converted Amount", "0.7221", "Currency: Result",
                ValueX, ResultValueY, ValueWidth, 56, "Georgia", 27F, true,
                AccentInk, 0);
            AddText(layout, "to", "Target Currency", "USD", "Currency: To",
                BadgeX, ToBadgeY, BadgeWidth, BadgeHeight,
                "Georgia", 13F, true,
                ValueInk, 1);
#if !EMBER_GLOW_WIDGET
            AddText(layout, "rate", "Exchange Rate", "1 CAD = 0.7221 USD", "Currency: Rate",
                RateX, RateY, RateWidth, 21, "Segoe UI", 10.5F, true,
                ValueInk, 1);
            AddText(layout, "status", "Rate Status", "Rates By Exchange Rate API", "Currency: Status",
                RateX, StatusY, RateWidth, 19, "Segoe UI", 9F, false,
                MutedInk, 1);
            AddText(layout, "instruction", "Instruction", "DOUBLE-CLICK TO CHANGE", "None",
                InstructionX, InstructionY, InstructionWidth, 20,
                "Segoe UI", 7.5F, true,
                AccentInk, 1);
#endif
            return layout;
        }

#if EMBER_GLOW_WIDGET
        private static void AddLcd(SavedDesignerLayout layout,
            string id, string name, float y)
        {
            layout.Elements.Add(new DesignerLayer
            {
                Id = id, Name = name, Binding = "Currency: LCD Screen",
                Kind = 1, Surface = 0, X = AmountPanelX, Y = y,
                Width = AmountPanelWidth, Height = AmountPanelHeight,
                Visible = true, Opacity = 1F, Scale = 1F,
                ImagePath = LcdFileName
            });
        }
#endif

        public void RenderDesignerBackground(Graphics graphics,
            Rectangle bounds)
        {
            RenderDesignerBackgroundLayer(
                graphics, bounds, SkinFileName, 1F);
        }

        public void RenderDesignerBackgroundLayer(
            Graphics graphics,
            Rectangle bounds,
            string imagePath,
            float opacity)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;
            using (var surface = new Bitmap(BaseWidth, BaseHeight,
                PixelFormat.Format32bppArgb))
            using (Graphics layer = Graphics.FromImage(surface))
            {
                layer.SmoothingMode = SmoothingMode.AntiAlias;
                layer.CompositingQuality = CompositingQuality.HighQuality;
                layer.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                layer.PixelOffsetMode = PixelOffsetMode.HighQuality;
                layer.TextRenderingHint =
                    System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                DrawConverterBackground(layer, imagePath);
#if EMBER_GLOW_WIDGET
                _drawingDesignerBackground = true;
                try
                {
#endif
                DrawConverterChrome(layer, "CAD", "USD", 1M,
                    .7221M, "Rates By Exchange Rate API", false, false);
#if EMBER_GLOW_WIDGET
                }
                finally { _drawingDesignerBackground = false; }
#endif
                using (var attributes = new ImageAttributes())
                {
                    attributes.SetColorMatrix(new ColorMatrix
                    {
                        Matrix33 = Math.Max(0F, Math.Min(1F, opacity))
                    });
                    graphics.DrawImage(surface, bounds, 0, 0,
                        surface.Width, surface.Height,
                        GraphicsUnit.Pixel, attributes);
                }
            }
        }

        private void DrawSavedLayout(
            Graphics graphics, SavedDesignerLayout layout)
        {
            DesignerLayer background = null;
            if (layout != null && layout.Elements != null)
                background = layout.Elements.Find(delegate(DesignerLayer item)
                {
                    return IsBackgroundLayer(item);
                });
            if (background == null)
                DrawConverter(graphics, string.Empty, string.Empty, 0M,
                    0M, string.Empty, false, false);
            else if (background.Visible)
                RenderDesignerBackgroundLayer(graphics,
                    Rectangle.Round(background.Bounds),
                    background.ImagePath, background.Opacity);
#if EMBER_GLOW_WIDGET
            // Saved layouts from earlier versions have no editable LCD layers.
            // Show the new screen art until Designer adds the missing layers.
            if (background != null && layout != null && layout.Elements != null)
            {
                if (!layout.Elements.Exists(IsAmountLcdLayer))
                    DrawValuePanel(graphics, new Rectangle(AmountPanelX,
                        AmountPanelY, AmountPanelWidth, AmountPanelHeight));
                if (!layout.Elements.Exists(IsResultLcdLayer))
                    DrawValuePanel(graphics, new Rectangle(AmountPanelX,
                        ResultPanelY, AmountPanelWidth, ResultPanelHeight));
            }
#endif
            DrawDesignerLayout(graphics, layout);
        }

#if EMBER_GLOW_WIDGET
        private static bool IsAmountLcdLayer(DesignerLayer layer)
        {
            return layer != null && layer.Id == "amount-lcd";
        }

        private static bool IsResultLcdLayer(DesignerLayer layer)
        {
            return layer != null && layer.Id == "result-lcd";
        }
#endif

        private static bool IsBackgroundLayer(DesignerLayer layer)
        {
            return layer != null && string.Equals(layer.Id,
                "main-background", StringComparison.OrdinalIgnoreCase);
        }

        private string ResolvePackageAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            if (Path.IsPathRooted(path)) return path;
            string folder = Path.GetDirectoryName(
                GetType().Assembly.Location) ?? string.Empty;
            string candidate = Path.Combine(folder,
                path.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(candidate) ? candidate : path;
        }

        public string ResolveDesignerText(DesignerLayer layer)
        {
            if (layer == null) return string.Empty;
            string from;
            string to;
            string status;
            decimal amount;
            decimal rate;
            bool refreshing;
            lock (_sync)
            {
                from = _from; to = _to; status = _status;
                amount = _amount; rate = _rate; refreshing = _refreshing;
            }
            if (_host == null && rate <= 0M)
            {
                from = "CAD"; to = "USD"; amount = 1M; rate = .7221M;
                status = "Rates By Exchange Rate API";
                refreshing = false;
            }
            switch (layer.Binding ?? string.Empty)
            {
                case "Currency: Amount": return FormatAmount(amount);
                case "Currency: From": return from;
                case "Currency: To": return to;
                case "Currency: Result":
                    return rate > 0M ? FormatAmount(amount * rate) :
                        (refreshing ? "Retrieving..." : "Unavailable");
                case "Currency: Rate":
                    return rate > 0M ? "1 " + from + " = " +
                        rate.ToString("0.######") + " " + to : status;
                case "Currency: Status":
                    return rate > 0M ? status :
                        "Daily reference rates  •  Right-click to retry";
                default: return layer.Text;
            }
        }

        private void OpenDesigner()
        {
            try
            {
                string path = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "EmilyDesk.Designer.exe");
                if (!File.Exists(path)) throw new FileNotFoundException(
                    "EmilyDesk Designer is not installed.");
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = "--optional-widget-assembly \"" +
                        GetType().Assembly.Location +
                        "\" --type \"" +
                        GetType().FullName + "\"",
                    WorkingDirectory = Path.GetDirectoryName(path),
                    UseShellExecute = true
                });
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "EmilyDesk Designer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static void OpenRateProvider()
        {
            try
            {
                Process.Start(new ProcessStartInfo(
                    "https://www.exchangerate-api.com")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, "Currency rate provider",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static SavedDesignerLayout NewDesignerLayout(
            string name, int width, int height)
        {
            return new SavedDesignerLayout
            {
                Name = name, CanvasWidth = width, CanvasHeight = height,
                EditableLayerVersion = 1,
                BackgroundLayerVersion = 1,
                Elements = new List<DesignerLayer>(),
                DeletedElementIds = new List<string>()
            };
        }

        private static void AddBackground(SavedDesignerLayout layout)
        {
            layout.Elements.Add(new DesignerLayer
            {
                Id = "main-background",
                Name = "Widget Background",
                Binding = "Layout: Main Background",
                Kind = 1,
                Surface = 0,
                X = 0F,
                Y = 0F,
                Width = BaseWidth,
                Height = BaseHeight,
                Visible = true,
                Opacity = 1F,
                Scale = 1F,
                ImagePath = SkinFileName
            });
        }

        private static void AddText(SavedDesignerLayout layout,
            string id, string name, string text, string binding,
            float x, float y, float width, float height,
            string font, float size, bool bold, Color color, int alignment)
        {
            layout.Elements.Add(new DesignerLayer
            {
                Id = id, Name = name, Text = text, Binding = binding,
                Kind = 0, Surface = 0, X = x, Y = y,
                Width = width, Height = height, Visible = true,
                Opacity = 1F, Scale = 1F, FontName = font,
                FontSize = size, Bold = bold,
                ColorArgb = color.ToArgb(), Alignment = alignment,
                WordWrap = false, Trimming = 3
            });
        }

        private void SaveSettings()
        {
            if (_host == null) return;
            _host.SetSetting("from", _from);
            _host.SetSetting("to", _to);
            _host.SetSetting(
                "amount",
                _amount.ToString(CultureInfo.InvariantCulture));
        }

        private static ComboBox CurrencyList(string selected, int x, int y)
        {
            var result = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(220, 26),
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                MaxDropDownItems = 18
            };
            result.Items.AddRange(Currencies);
            result.Text = NormalizeCurrency(selected, "CAD");
            return result;
        }

        private static Label Label(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Location = new Point(x, y)
            };
        }

        private static string NormalizeCurrency(string value, string fallback)
        {
            value = (value ?? string.Empty).Trim().ToUpperInvariant();
            foreach (string currency in Currencies)
                if (string.Equals(value, currency,
                    StringComparison.OrdinalIgnoreCase))
                    return currency;
            return fallback;
        }

        private static string FormatAmount(decimal value)
        {
            return value.ToString(
                Math.Abs(value) >= 1000M ? "N2" : "0.00##",
                CultureInfo.CurrentCulture);
        }

        private Image PanelSkin
        {
            get
            {
                if (_panelSkinAttempted) return _panelSkin;
                _panelSkinAttempted = true;
                try
                {
                    string assemblyFolder = Path.GetDirectoryName(
                        GetType().Assembly.Location);
                    string skinPath = Path.Combine(
                        assemblyFolder ?? string.Empty,
                        SkinFileName);
                    if (File.Exists(skinPath))
                        using (Image source = Image.FromFile(skinPath))
                            _panelSkin = new Bitmap(source);
                }
                catch
                {
                    _panelSkin = null;
                }
                return _panelSkin;
            }
        }

#if EMBER_GLOW_WIDGET
        private Image ReflectiveRim
        {
            get
            {
                if (_reflectiveRimAttempted) return _reflectiveRim;
                _reflectiveRimAttempted = true;
                try
                {
                    string folder = Path.GetDirectoryName(
                        GetType().Assembly.Location) ?? string.Empty;
                    string path = Path.Combine(folder,
                        "ember-glow-system-info-skin.png");
                    if (File.Exists(path))
                        using (Image source = Image.FromFile(path))
                        {
                            var frame = new Bitmap(
                                AmountPanelWidth, AmountPanelHeight);
                            using (Graphics frameGraphics =
                                Graphics.FromImage(frame))
                                DrawReflectiveGlassRim(frameGraphics, source,
                                    new Rectangle(0, 0,
                                        AmountPanelWidth, AmountPanelHeight));
                            _reflectiveRim = frame;
                        }
                }
                catch { _reflectiveRim = null; }
                return _reflectiveRim;
            }
        }

        private static void DrawReflectiveGlassRim(
            Graphics graphics, Image source, Rectangle target)
        {
            const int left = 86, top = 125, right = 1398, bottom = 926;
            const int sourceCorner = 70;
            const int targetCorner = 14;
            int[] sx = { left, left + sourceCorner,
                right - sourceCorner, right };
            int[] sy = { top, top + sourceCorner,
                bottom - sourceCorner, bottom };
            int[] dx = { target.Left, target.Left + targetCorner,
                target.Right - targetCorner, target.Right };
            int[] dy = { target.Top, target.Top + targetCorner,
                target.Bottom - targetCorner, target.Bottom };
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                using (GraphicsPath clip = RoundedRectangle(target, 14))
                    graphics.SetClip(clip, CombineMode.Intersect);
                for (int row = 0; row < 3; row++)
                    for (int column = 0; column < 3; column++)
                    {
                        if (row == 1 && column == 1) continue;
                        graphics.DrawImage(source,
                            new Rectangle(dx[column], dy[row],
                                dx[column + 1] - dx[column],
                                dy[row + 1] - dy[row]),
                            sx[column], sy[row],
                            sx[column + 1] - sx[column],
                            sy[row + 1] - sy[row], GraphicsUnit.Pixel);
                    }
                // Preserve narrow photographic hotspots after downscaling.
                graphics.DrawImage(source,
                    new Rectangle(target.Left + 14, target.Top + 2,
                        target.Width - 28, 2),
                    left + sourceCorner, 130,
                    right - left - sourceCorner * 2, 7,
                    GraphicsUnit.Pixel);
                graphics.DrawImage(source,
                    new Rectangle(target.Left + 14, target.Bottom - 4,
                        target.Width - 28, 2),
                    left + sourceCorner, 901,
                    right - left - sourceCorner * 2, 7,
                    GraphicsUnit.Pixel);
                graphics.DrawImage(source,
                    new Rectangle(target.Left + 2, target.Top + 14,
                        2, target.Height - 28),
                    96, top + sourceCorner, 7,
                    bottom - top - sourceCorner * 2,
                    GraphicsUnit.Pixel);
                graphics.DrawImage(source,
                    new Rectangle(target.Right - 4, target.Top + 14,
                        2, target.Height - 28),
                    1380, top + sourceCorner, 7,
                    bottom - top - sourceCorner * 2,
                    GraphicsUnit.Pixel);
            }
            finally { graphics.Restore(state); }
        }
#endif

        private Image SwapButton
        {
            get
            {
                if (_swapButtonAttempted) return _swapButton;
                _swapButtonAttempted = true;
                try
                {
                    string folder = Path.GetDirectoryName(
                        GetType().Assembly.Location) ?? string.Empty;
                    string path = Path.Combine(folder,
                        "ember-glow-swap-button.png");
                    if (File.Exists(path))
                        using (Image source = Image.FromFile(path))
                            _swapButton = new Bitmap(source);
                }
                catch { _swapButton = null; }
                return _swapButton;
            }
        }

        public Rectangle GetSnapBounds(Size renderedSurface)
        {
            double scaleX = renderedSurface.Width / (double)BaseWidth;
            double scaleY = renderedSurface.Height / (double)BaseHeight;
#if EMBER_GLOW_WIDGET
            // Alpha > 10 bounds of the current 1545x1018 Bakelite skin,
            // scaled onto the 550x363 widget surface.
            int left = (int)Math.Round(9D * scaleX);
            int top = (int)Math.Round(21D * scaleY);
            int right = (int)Math.Round(542D * scaleX);
            int bottom = (int)Math.Round(330D * scaleY);
#elif INDUSTRIAL_WIDGET || WOODLAND_WIDGET || BOTANICAL_WIDGET
            int left = (int)Math.Round(4D * scaleX);
            int top = (int)Math.Round(4D * scaleY);
            int right = (int)Math.Round(546D * scaleX);
            int bottom = (int)Math.Round(359D * scaleY);
#else
            int left = (int)Math.Round(2D * scaleX);
            int top = (int)Math.Round(15D * scaleY);
            int right = (int)Math.Round(418D * scaleX);
            int bottom = (int)Math.Round(370D * scaleY);
#endif
            return Rectangle.FromLTRB(
                Math.Max(0, left),
                Math.Max(0, top),
                Math.Min(renderedSurface.Width, Math.Max(left + 1, right)),
                Math.Min(renderedSurface.Height, Math.Max(top + 1, bottom)));
        }

        private static void DrawFallbackPanel(Graphics graphics)
        {
            Rectangle full = new Rectangle(2, 2,
                BaseWidth - 4, BaseHeight - 4);
            using (GraphicsPath path = RoundedRectangle(full, 20))
            using (var brush = new LinearGradientBrush(
                full,
                FallbackTop,
                FallbackBottom,
                90F))
            using (var border = new Pen(
                FallbackBorder, 3F))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
            }
        }

#if ART_DECO_WIDGET
        private static void DrawArtDecoTitlePlate(
            Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 7))
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(111, 68, 18),
                Color.FromArgb(255, 238, 174), 90F))
            using (var border = new Pen(
                Color.FromArgb(244, 198, 92), 1.5F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(111, 68, 18),
                        Color.FromArgb(255, 238, 174),
                        Color.FromArgb(205, 145, 52),
                        Color.FromArgb(96, 56, 16)
                    },
                    Positions = new[] { 0F, .3F, .67F, 1F }
                };
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                using (var shine = new Pen(
                    Color.FromArgb(185, 255, 250, 214), 1F))
                    graphics.DrawLine(shine, bounds.Left + 14,
                        bounds.Top + 7, bounds.Right - 14,
                        bounds.Top + 7);
            }
        }
#elif INDUSTRIAL_WIDGET
        private static void DrawIndustrialTitlePlate(
            Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 7))
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(70, 74, 76),
                Color.FromArgb(205, 208, 208), 90F))
            using (var border = new Pen(Color.FromArgb(37, 40, 42), 1.5F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(57, 61, 63),
                        Color.FromArgb(226, 228, 228),
                        Color.FromArgb(132, 136, 137),
                        Color.FromArgb(48, 52, 54)
                    },
                    Positions = new[] { 0F, .3F, .67F, 1F }
                };
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                using (var shine = new Pen(
                    Color.FromArgb(165, 255, 255, 255), 1F))
                    graphics.DrawLine(shine, bounds.Left + 14,
                        bounds.Top + 7, bounds.Right - 14,
                        bounds.Top + 7);
            }
        }
#elif WOODLAND_WIDGET
        private static void DrawWoodlandTitlePlate(
            Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 7))
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(60, 35, 20),
                Color.FromArgb(174, 119, 64), 90F))
            using (var border = new Pen(Color.FromArgb(195, 153, 83), 1.5F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(48, 29, 18),
                        Color.FromArgb(177, 124, 69),
                        Color.FromArgb(95, 55, 31),
                        Color.FromArgb(38, 24, 16)
                    },
                    Positions = new[] { 0F, .3F, .67F, 1F }
                };
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                using (var shine = new Pen(
                    Color.FromArgb(125, 255, 231, 180), 1F))
                    graphics.DrawLine(shine, bounds.Left + 14,
                        bounds.Top + 7, bounds.Right - 14,
                        bounds.Top + 7);
            }
        }
#elif BOTANICAL_WIDGET
        private static void DrawBotanicalTitlePlate(
            Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 7))
            using (var brush = new LinearGradientBrush(bounds,
                Color.FromArgb(249, 231, 226),
                Color.FromArgb(217, 146, 142), 90F))
            using (var border = new Pen(Color.FromArgb(102, 116, 85), 1.5F))
            {
                brush.InterpolationColors = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(233, 188, 183),
                        Color.FromArgb(255, 244, 240),
                        Color.FromArgb(223, 164, 160),
                        Color.FromArgb(207, 132, 130)
                    },
                    Positions = new[] { 0F, .3F, .67F, 1F }
                };
                graphics.FillPath(brush, path);
                graphics.DrawPath(border, path);
                using (var shine = new Pen(
                    Color.FromArgb(185, 255, 255, 255), 1F))
                    graphics.DrawLine(shine, bounds.Left + 14,
                        bounds.Top + 7, bounds.Right - 14,
                        bounds.Top + 7);
            }
        }
#endif

        private static Color TitleInk { get {
#if ART_DECO_WIDGET
            return Color.FromArgb(236, 218, 177);
#elif EMBER_GLOW_WIDGET
            return Color.FromArgb(246, 223, 189);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(31, 34, 35);
#elif WOODLAND_WIDGET
            return Color.FromArgb(245, 226, 184);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(66, 82, 59);
#else
            return Color.FromArgb(73, 38, 18);
#endif
        } }
        private static Color LabelInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(220, 129, 49);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(216, 166, 73);
#elif WOODLAND_WIDGET
            return Color.FromArgb(214, 174, 91);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(169, 95, 97);
#else
            return Color.FromArgb(211, 164, 88);
#endif
        } }
        private static Color ValueInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(246, 223, 189);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(239, 232, 211);
#elif WOODLAND_WIDGET
            return Color.FromArgb(245, 238, 210);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(66, 82, 59);
#else
            return Color.FromArgb(255, 239, 201);
#endif
        } }
        private static Color AccentInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(255, 155, 40);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(245, 177, 63);
#elif WOODLAND_WIDGET
            return Color.FromArgb(235, 188, 82);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(66, 82, 59);
#else
            return Color.FromArgb(255, 205, 112);
#endif
        } }
        private static Color MutedInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(181, 139, 108);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(161, 166, 166);
#elif WOODLAND_WIDGET
            return Color.FromArgb(174, 181, 149);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(122, 117, 105);
#else
            return Color.FromArgb(183, 137, 82);
#endif
        } }
        private static Color DividerInk { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(173, 82, 27);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(145, 126, 131, 133);
#elif WOODLAND_WIDGET
            return Color.FromArgb(150, 168, 132, 66);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(170, 217, 146, 142);
#else
            return Color.FromArgb(135, 151, 94, 45);
#endif
        } }
        private static Color PanelGlow { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(31, 176, 114, 72);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(34, 245, 177, 63);
#elif WOODLAND_WIDGET
            return Color.FromArgb(38, 220, 171, 72);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(42, 217, 146, 142);
#else
            return Color.FromArgb(42, 255, 166, 58);
#endif
        } }
        private static Color PanelTop { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(38, 22, 15);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(45, 49, 50);
#elif WOODLAND_WIDGET
            return Color.FromArgb(27, 47, 36);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(255, 253, 248);
#else
            return Color.FromArgb(58, 35, 23);
#endif
        } }
        private static Color PanelBottom { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(13, 8, 7);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(11, 13, 14);
#elif WOODLAND_WIDGET
            return Color.FromArgb(9, 22, 17);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(240, 235, 224);
#else
            return Color.FromArgb(19, 13, 11);
#endif
        } }
        private static Color PanelBorder { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(113, 139, 94, 64);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(133, 139, 141);
#elif WOODLAND_WIDGET
            return Color.FromArgb(151, 125, 69);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(102, 116, 85);
#else
            return Color.FromArgb(205, 142, 54);
#endif
        } }
        private static Color BadgeTop { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(157, 75, 25);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(108, 79, 35);
#elif WOODLAND_WIDGET
            return Color.FromArgb(124, 91, 42);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(152, 167, 128);
#else
            return Color.FromArgb(170, 101, 35);
#endif
        } }
        private static Color BadgeBottom { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(66, 26, 11);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(43, 36, 27);
#elif WOODLAND_WIDGET
            return Color.FromArgb(36, 47, 30);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(102, 116, 85);
#else
            return Color.FromArgb(71, 36, 20);
#endif
        } }
        private static Color FallbackTop { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(79, 40, 22);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(109, 113, 115);
#elif WOODLAND_WIDGET
            return Color.FromArgb(55, 83, 60);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(255, 250, 240);
#else
            return Color.FromArgb(65, 36, 24);
#endif
        } }
        private static Color FallbackBottom { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(18, 9, 7);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(25, 28, 29);
#elif WOODLAND_WIDGET
            return Color.FromArgb(12, 27, 20);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(225, 218, 199);
#else
            return Color.FromArgb(20, 13, 11);
#endif
        } }
        private static Color FallbackBorder { get {
#if EMBER_GLOW_WIDGET
            return Color.FromArgb(244, 124, 24);
#elif INDUSTRIAL_WIDGET
            return Color.FromArgb(202, 205, 205);
#elif WOODLAND_WIDGET
            return Color.FromArgb(144, 104, 58);
#elif BOTANICAL_WIDGET
            return Color.FromArgb(102, 116, 85);
#else
            return Color.FromArgb(211, 151, 65);
#endif
        } }

        private static void DrawFittedText(
            Graphics graphics,
            string text,
            Font font,
            Color color,
            Rectangle bounds,
            StringAlignment alignment,
            float minimumPointSize)
        {
            Font fitted = font;
            Font replacement = null;
            try
            {
                float pointSize = font.Size;
                while (pointSize > minimumPointSize &&
                    graphics.MeasureString(text, fitted).Width >
                        bounds.Width - 2)
                {
                    pointSize = Math.Max(minimumPointSize,
                        pointSize - 1F);
                    if (replacement != null) replacement.Dispose();
                    replacement = new Font(font.FontFamily,
                        pointSize, font.Style);
                    fitted = replacement;
                }
                DrawText(graphics, text, fitted, color, bounds, alignment);
            }
            finally
            {
                if (replacement != null) replacement.Dispose();
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void DrawText(
            Graphics graphics,
            string text,
            Font font,
            Color color,
            Rectangle bounds,
            StringAlignment alignment)
        {
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat())
            {
                format.Alignment = alignment;
                format.LineAlignment = StringAlignment.Center;
                format.Trimming = StringTrimming.EllipsisCharacter;
                graphics.DrawString(text, font, brush, bounds, format);
            }
        }

        private void RequestInvalidate()
        {
            if (_disposed) return;
            if (_invalidate != null) _invalidate();
            if (_host != null) _host.Invalidate();
        }

        public void Dispose()
        {
            if (_runtime != null)
                _runtime.Events.Published -= DesignerSaved;
            _runtime = null;
            lock (_sync)
            {
                _disposed = true;
                if (_panelSkin != null)
                {
                    _panelSkin.Dispose();
                    _panelSkin = null;
                }
                if (_swapButton != null)
                {
                    _swapButton.Dispose();
                    _swapButton = null;
                }
#if EMBER_GLOW_WIDGET
                if (_reflectiveRim != null)
                {
                    _reflectiveRim.Dispose();
                    _reflectiveRim = null;
                }
#endif
                _invalidate = null;
                _host = null;
            }
        }
    }
}
