using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Script.Serialization;
using XWidgetReborn.Shared;

namespace XWidgetWeatherBridgeV2
{
    public sealed class BridgeService : ServiceBase
    {
        private BridgeHost _host;

        public BridgeService()
        {
            ServiceName = AppInfo.ServiceName;
            CanStop = true;
            CanShutdown = true;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            _host = new BridgeHost();
            _host.Start();
        }

        protected override void OnStop()
        {
            if (_host != null)
            {
                _host.Stop();
                _host.Dispose();
                _host = null;
            }
        }

        protected override void OnShutdown()
        {
            OnStop();
            base.OnShutdown();
        }
    }

    internal static class AppInfo
    {
        public const string ServiceName = AppConstants.ServiceName;
        public const string Version = AppConstants.Version;
        public const string ListenerPrefix = AppConstants.ListenerPrefix;
        public const string ProviderName = "Provider adapter";
    }

    internal sealed class LocationRecord
    {
        public string name { get; set; }
        public string admin1 { get; set; }
        public string country { get; set; }
        public string country_code { get; set; }
        public double latitude { get; set; }
        public double longitude { get; set; }
        public string timezone { get; set; }
    }

    public sealed class BridgeHost : IDisposable
    {
        private readonly string _dataDir;
        private readonly string _logFile;
        private readonly string _locationsFile;
        private readonly string _cacheDir;
        private readonly HttpListener _listener;
        private readonly JavaScriptSerializer _json;
        private readonly object _locationLock = new object();
        private readonly object _logLock = new object();
        private readonly ProviderManager _providerManager;
        private readonly DateTime _startedUtc = DateTime.UtcNow;

        private Dictionary<string, LocationRecord> _locations;
        private Thread _thread;
        private volatile bool _running;

        public BridgeHost()
        {
            _dataDir = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "EmilyDesk", "WeatherService");

            _logFile = Path.Combine(_dataDir, "bridge.log");
            _locationsFile = Path.Combine(_dataDir, "locations.json");
            _cacheDir = Path.Combine(_dataDir, "cache");

            Directory.CreateDirectory(_dataDir);
            Directory.CreateDirectory(_cacheDir);
            MigrateLegacyUserData();

            _json = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue,
                RecursionLimit = 100
            };

            _providerManager = new ProviderManager(_dataDir, _json);

            _listener = new HttpListener();
            _listener.Prefixes.Add(AppInfo.ListenerPrefix);

            _locations = LoadLocations();
            EnsureBuiltInLocations();
            SaveLocations();
        }

        private void MigrateLegacyUserData()
        {
            string[] legacyDirectories =
            {
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "EmilyDesk"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                    "XWidgetWeatherBridge"),
                Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                    "EmilyDesk")
            };
            foreach (string legacyDirectory in legacyDirectories)
            {
                if (!Directory.Exists(legacyDirectory) || string.Equals(
                    Path.GetFullPath(legacyDirectory).TrimEnd('\\'),
                    Path.GetFullPath(_dataDir).TrimEnd('\\'),
                    StringComparison.OrdinalIgnoreCase)) continue;
                TryMigrateFile(Path.Combine(legacyDirectory,
                    "locations.json"), _locationsFile);
                TryMigrateFile(Path.Combine(legacyDirectory,
                    "provider-settings.json"), Path.Combine(_dataDir,
                    "provider-settings.json"));
                string legacyCache = Path.Combine(legacyDirectory, "cache");
                if (!Directory.Exists(legacyCache)) continue;
                try
                {
                    foreach (string source in Directory.GetFiles(
                        legacyCache, "*.json"))
                        TryMigrateFile(source, Path.Combine(_cacheDir,
                            Path.GetFileName(source)));
                }
                catch { }
            }
        }

        private static void TryMigrateFile(
            string source,
            string destination)
        {
            if (File.Exists(destination) || !File.Exists(source))
                return;

            try
            {
                string parent = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                File.Copy(source, destination, false);
            }
            catch
            {
                // Migration is best-effort. An inaccessible legacy file must
                // never prevent the user-mode Weather Engine from starting.
            }
        }

        public void Start()
        {
            if (_running) return;

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            _listener.Start();
            _running = true;

            _thread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "XWidget Weather Bridge v2"
            };
            _thread.Start();

            Log("INFO", "SERVICE_START",
                "Bridge v" + AppInfo.Version + " started on " + AppInfo.ListenerPrefix +
                " PID=" + System.Diagnostics.Process.GetCurrentProcess().Id);
        }

        public void Stop()
        {
            if (!_running) return;

            _running = false;
            try { _listener.Stop(); } catch { }
            try { _listener.Close(); } catch { }

            if (_thread != null && _thread.IsAlive)
                _thread.Join(3000);

            Log("INFO", "SERVICE_STOP", "Bridge stopped");
        }

        public void Dispose()
        {
            Stop();
        }

        private void ListenLoop()
        {
            while (_running)
            {
                HttpListenerContext ctx = null;
                try
                {
                    ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
                }
                catch (HttpListenerException)
                {
                    if (!_running) break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Log("ERROR", "LISTENER", ex.ToString());
                    SafeError(ctx, 500, ex.Message);
                }
            }
        }

        private void Handle(HttpListenerContext ctx)
        {
            try
            {
                string path = ctx.Request.Url.AbsolutePath;
                Log("INFO", "REQUEST", ctx.Request.HttpMethod + " " + ctx.Request.RawUrl);

                if (string.Equals(path, "/xwidgetbridge/status.json", StringComparison.OrdinalIgnoreCase))
                {
                    ProviderManager.ProviderHealth providerHealth =
                        _providerManager.GetHealth(
                            _providerManager.ActiveProvider.Id);
                    WriteJson(ctx, Obj(
                        "status", "running",
                        "version", AppInfo.Version,
                        "provider", _providerManager.ActiveProvider.DisplayName,
                        "providerId", _providerManager.ActiveProvider.Id,
                        "providerRequiresApiKey", _providerManager.ActiveProvider.RequiresApiKey,
                        "providerProfileSource", _providerManager.ActiveProvider.ProfileSource,
                        "loadedProviderProfiles", _providerManager.LoadedProfileCount,
                        "serviceStartedUtc", _startedUtc.ToString("o", CultureInfo.InvariantCulture),
                        "serviceUptimeSeconds", Math.Max(0, (long)(DateTime.UtcNow - _startedUtc).TotalSeconds),
                        "providerLastLiveFetchUtc", _providerManager.LastLiveFetchUtc.HasValue
                            ? _providerManager.LastLiveFetchUtc.Value.ToString("o", CultureInfo.InvariantCulture)
                            : null,
                        "providerLastSuccessUtc", _providerManager.LastSuccessUtc.HasValue
                            ? _providerManager.LastSuccessUtc.Value.ToString("o", CultureInfo.InvariantCulture)
                            : null,
                        "providerLastFailureUtc", _providerManager.LastFailureUtc.HasValue
                            ? _providerManager.LastFailureUtc.Value.ToString("o", CultureInfo.InvariantCulture)
                            : null,
                        "providerLastError", _providerManager.LastError,
                        "providerConsecutiveFailures", providerHealth.ConsecutiveFailures,
                        "providerNextRetryUtc", providerHealth.NextRetryUtc > DateTime.MinValue
                            ? providerHealth.NextRetryUtc.ToString("o", CultureInfo.InvariantCulture)
                            : null,
                        "usingStaleCache", _providerManager.LastResponseUsedStaleCache,
                        "usingCachedData", _providerManager.LastResponseUsedCache,
                        "staleCacheHours", _providerManager.Settings.staleCacheHours,
                        "locations", _locations.Count,
                        "cacheFiles", Directory.Exists(_cacheDir) ? Directory.GetFiles(_cacheDir, "*.json").Length : 0,
                        "dataDirectory", _dataDir,
                        "utc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));
                    return;
                }


                if (string.Equals(path, "/xwidgetbridge/providers.json", StringComparison.OrdinalIgnoreCase))
                {
                    var providers = new List<object>();
                    foreach (IWeatherProvider provider in _providerManager.Providers)
                    {
                        providers.Add(Obj(
                            "id", provider.Id,
                            "name", provider.DisplayName,
                            "requiresApiKey", provider.RequiresApiKey,
                            "hasApiKey", _providerManager.HasApiKey(provider.Id),
                            "profileSource", provider.ProfileSource,
                            "active", string.Equals(
                                provider.Id,
                                _providerManager.ActiveProvider.Id,
                                StringComparison.OrdinalIgnoreCase)));
                    }

                    WriteJson(ctx, Obj(
                        "activeProviderId", _providerManager.ActiveProvider.Id,
                        "activeProvider", _providerManager.ActiveProvider.DisplayName,
                        "allowStaleCache", _providerManager.Settings.allowStaleCache,
                        "staleCacheHours", _providerManager.Settings.staleCacheHours,
                        "providers", providers));
                    return;
                }


                if (string.Equals(path, "/xwidgetbridge/provider/reload", StringComparison.OrdinalIgnoreCase))
                {
                    _providerManager.ReloadProfiles();
                    WriteJson(ctx, Obj(
                        "status", "ok",
                        "loadedProviderProfiles", _providerManager.LoadedProfileCount,
                        "activeProviderId", _providerManager.ActiveProvider.Id,
                        "activeProvider", _providerManager.ActiveProvider.DisplayName));
                    return;
                }

                if (string.Equals(path, "/xwidgetbridge/provider/select", StringComparison.OrdinalIgnoreCase))
                {
                    string id = ctx.Request.QueryString["id"] ?? "";
                    bool selected = _providerManager.Select(id);
                    WriteJson(ctx, Obj(
                        "status", selected ? "ok" : "api-key-required",
                        "activeProviderId", _providerManager.ActiveProvider.Id,
                        "activeProvider", _providerManager.ActiveProvider.DisplayName),
                        selected ? 200 : 409);
                    return;
                }

                if (string.Equals(path, "/xwidgetbridge/provider/key", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(ctx.Request.HttpMethod, "POST",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        WriteJson(ctx, Obj("status", "method-not-allowed"), 405);
                        return;
                    }

                    string body;
                    using (var reader = new StreamReader(ctx.Request.InputStream,
                        ctx.Request.ContentEncoding ?? Encoding.UTF8))
                        body = reader.ReadToEnd();
                    System.Collections.Specialized.NameValueCollection form =
                        HttpUtility.ParseQueryString(body);
                    string id = form["id"] ?? string.Empty;
                    string apiKey = form["key"] ?? string.Empty;
                    bool saved = _providerManager.SetApiKey(id, apiKey);
                    WriteJson(ctx, Obj(
                        "status", saved ? "ok" : "invalid-provider",
                        "providerId", id,
                        "hasApiKey", saved && _providerManager.HasApiKey(id)),
                        saved ? 200 : 400);
                    return;
                }

                if (string.Equals(path, "/xwidgetbridge/clear-cache", StringComparison.OrdinalIgnoreCase))
                {
                    int removed = 0;
                    if (Directory.Exists(_cacheDir))
                    {
                        foreach (string file in Directory.GetFiles(_cacheDir, "*.json"))
                        {
                            try { File.Delete(file); removed++; } catch { }
                        }
                    }

                    Log("INFO", "CACHE_CLEAR", "Removed " + removed + " cache files");
                    WriteJson(ctx, Obj("status", "ok", "removed", removed));
                    return;
                }

                if (string.Equals(path, "/locations/v1/cities/autocomplete.json", StringComparison.OrdinalIgnoreCase))
                {
                    HandleAutocomplete(ctx);
                    return;
                }

                const string currentPrefix = "/currentconditions/v1/";
                if (path.StartsWith(currentPrefix, StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    string key = path.Substring(currentPrefix.Length, path.Length - currentPrefix.Length - 5);
                    HandleCurrent(ctx, key);
                    return;
                }

                const string forecastPrefix = "/forecasts/v1/daily/15day/";
                if (path.StartsWith(forecastPrefix, StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    string key = path.Substring(forecastPrefix.Length, path.Length - forecastPrefix.Length - 5);
                    HandleForecast(ctx, key);
                    return;
                }

                WriteJson(ctx, Obj("Code", "NotFound", "Message", "Endpoint not supported."), 404);
            }
            catch (Exception ex)
            {
                Log("ERROR", "REQUEST", ex.ToString());
                SafeError(ctx, 500, ex.Message);
            }
        }

        private void HandleAutocomplete(HttpListenerContext ctx)
        {
            string q = WebUtility.UrlDecode(ctx.Request.QueryString["q"] ?? "");
            if (string.IsNullOrWhiteSpace(q))
            {
                WriteJson(ctx, new object[0]);
                return;
            }

            string url =
                "https://geocoding-api.open-meteo.com/v1/search?count=10&language=en&format=json&name=" +
                Uri.EscapeDataString(q);

            var geo = GetJsonWithCache(
                _providerManager.ActiveProvider,
                "geocode_" + SafeFileName(q),
                url,
                TimeSpan.FromHours(24),
                true);
            var output = new List<object>();

            object resultsObj;
            if (geo.TryGetValue("results", out resultsObj))
            {
                foreach (var item in AsObjectList(resultsObj))
                {
                    var r = AsDictionary(item);
                    double lat = ToDouble(r, "latitude");
                    double lon = ToDouble(r, "longitude");
                    string key = NewLocationKey(lat, lon);

                    var loc = new LocationRecord
                    {
                        name = ToStringValue(r, "name"),
                        admin1 = ToStringValue(r, "admin1"),
                        country = ToStringValue(r, "country"),
                        country_code = ToStringValue(r, "country_code"),
                        latitude = lat,
                        longitude = lon,
                        timezone = ToStringValue(r, "timezone")
                    };

                    lock (_locationLock)
                    {
                        _locations[key] = loc;
                    }

                    output.Add(Obj(
                        "Version", 1,
                        "Key", key,
                        "Type", "City",
                        "Rank", 50,
                        "LocalizedName", loc.name,
                        "EnglishName", loc.name,
                        "PrimaryPostalCode", "",
                        "Region", Obj(
                            "ID", loc.country_code,
                            "LocalizedName", loc.country,
                            "EnglishName", loc.country),
                        "Country", Obj(
                            "ID", loc.country_code,
                            "LocalizedName", loc.country,
                            "EnglishName", loc.country),
                        "AdministrativeArea", Obj(
                            "ID", "",
                            "LocalizedName", loc.admin1,
                            "EnglishName", loc.admin1,
                            "Level", 1,
                            "LocalizedType", "Province",
                            "EnglishType", "Province",
                            "CountryID", loc.country_code)
                    ));
                }
            }

            SaveLocations();
            WriteJson(ctx, output);
        }

        private void HandleCurrent(HttpListenerContext ctx, string key)
        {
            LocationRecord loc = GetLocation(key);
            if (loc == null)
            {
                WriteJson(ctx, Obj(
                    "Code", "ServiceError",
                    "Message", "Unknown location key. Search and select the city again."), 404);
                return;
            }

            var d = GetWeatherData(loc, key);
            var current = AsDictionary(d["current"]);
            var hourly = AsDictionary(d["hourly"]);
            int hourIndex = FindHourIndex(current, hourly);

            double visKm = GetArrayDouble(hourly, "visibility", hourIndex, 10000.0) / 1000.0;
            double uv = GetArrayDouble(hourly, "uv_index", hourIndex, 0.0);
            int weatherCode = ToInt(current, "weather_code");
            bool isDay = ToInt(current, "is_day") == 1;
            WeatherInfo wi = MapWeather(weatherCode, isDay);

            double temp = ToDouble(current, "temperature_2m");
            double apparent = ToDouble(current, "apparent_temperature");
            int humidity = ToInt(current, "relative_humidity_2m");
            double windDirection = ToDouble(current, "wind_direction_10m");
            string dir = WindText(windDirection);
            double precip = ToDouble(current, "precipitation");
            double rain = ToDouble(current, "rain");
            double snowfall = ToDouble(current, "snowfall");

            var obj = Obj(
                "LocalObservationDateTime", ToStringValue(current, "time") + ":00",
                "EpochTime", DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                "WeatherText", wi.Text,
                "WeatherIcon", wi.Icon,
                "HasPrecipitation", precip > 0,
                "PrecipitationType", snowfall > 0 ? "Snow" : rain > 0 ? "Rain" : null,
                "IsDayTime", isDay,
                "Temperature", UnitTemperature(temp),
                "RealFeelTemperature", UnitTemperature(apparent),
                "RealFeelTemperatureShade", UnitTemperature(apparent),
                "RelativeHumidity", humidity,
                "IndoorRelativeHumidity", humidity,
                "DewPoint", UnitTemperature(temp - ((100.0 - humidity) / 5.0)),
                "Wind", Obj(
                    "Direction", Obj("Degrees", (int)windDirection, "Localized", dir, "English", dir),
                    "Speed", UnitSpeed(ToDouble(current, "wind_speed_10m"))),
                "WindGust", Obj("Speed", UnitSpeed(ToDouble(current, "wind_gusts_10m"))),
                "UVIndex", (int)Math.Round(uv),
                "UVIndexText", UvText(uv),
                "Visibility", UnitDistance(visKm),
                "ObstructionsToVisibility", "",
                "CloudCover", ToInt(current, "cloud_cover"),
                "Pressure", UnitPressure(ToDouble(current, "pressure_msl")),
                "PressureTendency", Obj("LocalizedText", "Steady", "Code", "S"),
                "ApparentTemperature", UnitTemperature(apparent),
                "WindChillTemperature", UnitTemperature(apparent),
                "WetBulbTemperature", UnitTemperature(temp),
                "Precip1hr", UnitPrecip(precip),
                "MobileLink", _providerManager.ActiveProvider.WebsiteUrl,
                "Link", _providerManager.ActiveProvider.WebsiteUrl,
                "XWidgetBridgeVersion", AppInfo.Version
            );

            Log("INFO", "CURRENT",
                string.Format(CultureInfo.InvariantCulture,
                    "{0}, {1}: temp={2:0.0}C humidity={3}% icon={4}",
                    loc.name, loc.admin1, temp, humidity, wi.Icon));

            WriteJson(ctx, new object[] { obj });
        }

        private void HandleForecast(HttpListenerContext ctx, string key)
        {
            LocationRecord loc = GetLocation(key);
            if (loc == null)
            {
                WriteJson(ctx, Obj(
                    "Code", "ServiceError",
                    "Message", "Unknown location key. Search and select the city again."), 404);
                return;
            }

            var d = GetWeatherData(loc, key);
            var daily = AsDictionary(d["daily"]);
            var times = AsObjectList(daily["time"]);
            var days = new List<object>();

            for (int i = 0; i < times.Count; i++)
            {
                string dateText = Convert.ToString(times[i], CultureInfo.InvariantCulture);
                int code = GetArrayInt(daily, "weather_code", i, 3);
                WeatherInfo wi = MapWeather(code, true);
                double direction = GetArrayDouble(daily, "wind_direction_10m_dominant", i, 0);
                string dir = WindText(direction);
                int pop = GetArrayInt(daily, "precipitation_probability_max", i, 0);
                int thunder = code >= 95 ? pop : 0;
                int snow = code >= 71 && code <= 86 ? pop : 0;
                double maxWind = GetArrayDouble(daily, "wind_speed_10m_max", i, 0);
                double minTemp = GetArrayDouble(daily, "temperature_2m_min", i, 0);
                double maxTemp = GetArrayDouble(daily, "temperature_2m_max", i, 0);
                double uv = GetArrayDouble(daily, "uv_index_max", i, 0);
                DateTimeOffset forecastDate;
                if (!DateTimeOffset.TryParse(
                    dateText + "T07:00:00-03:00",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out forecastDate))
                    forecastDate = new DateTimeOffset(
                        DateTime.Today.AddDays(i));

                var period = Obj(
                    "Icon", wi.Icon,
                    "IconPhrase", wi.Text,
                    "ShortPhrase", wi.Text,
                    "LongPhrase", wi.Text,
                    "PrecipitationProbability", pop,
                    "ThunderstormProbability", thunder,
                    "RainProbability", pop,
                    "SnowProbability", snow,
                    "IceProbability", 0,
                    "Wind", Obj(
                        "Speed", ForecastSpeed(maxWind),
                        "Direction", Obj("Degrees", (int)direction, "Localized", dir, "English", dir)),
                    "WindGust", Obj(
                        "Speed", ForecastSpeed(maxWind),
                        "Direction", Obj("Degrees", (int)direction, "Localized", dir, "English", dir)),
                    "HasPrecipitation", pop > 0,
                    "PrecipitationType", pop > 0 ? "Rain" : null,
                    "PrecipitationIntensity", pop > 60 ? "Moderate" : pop > 0 ? "Light" : null
                );

                days.Add(Obj(
                    "Date", dateText + "T07:00:00-03:00",
                    "EpochDate", forecastDate.ToUnixTimeSeconds(),
                    "Sun", Obj(
                        "Rise", GetArrayString(daily, "sunrise", i),
                        "EpochRise", 0,
                        "Set", GetArrayString(daily, "sunset", i),
                        "EpochSet", 0),
                    "Moon", Obj("Rise", null, "EpochRise", 0, "Set", null, "EpochSet", 0, "Phase", "", "Age", 0),
                    "Temperature", Obj(
                        "Minimum", ForecastTemperature(minTemp),
                        "Maximum", ForecastTemperature(maxTemp)),
                    "RealFeelTemperature", Obj(
                        "Minimum", ForecastTemperature(minTemp),
                        "Maximum", ForecastTemperature(maxTemp)),
                    "HoursOfSun", 0,
                    "DegreeDaySummary", Obj(),
                    "AirAndPollen", new object[]
                    {
                        Obj("Name","AirQuality","Value",0,"Category","Good","CategoryValue",1,"Type","AirQuality"),
                        Obj("Name","Grass","Value",0,"Category","Low","CategoryValue",1,"Type","Grass"),
                        Obj("Name","Mold","Value",0,"Category","Low","CategoryValue",1,"Type","Mold"),
                        Obj("Name","Ragweed","Value",0,"Category","Low","CategoryValue",1,"Type","Ragweed"),
                        Obj("Name","Tree","Value",0,"Category","Low","CategoryValue",1,"Type","Tree"),
                        Obj("Name","UVIndex","Value",(int)Math.Round(uv),"Category",UvText(uv),"CategoryValue",1,"Type","UVIndex")
                    },
                    "Day", period,
                    "Night", period,
                    "Sources", new object[] { _providerManager.ActiveProvider.DisplayName },
                    "MobileLink", _providerManager.ActiveProvider.WebsiteUrl,
                    "Link", _providerManager.ActiveProvider.WebsiteUrl
                ));
            }

            var obj = Obj(
                "Headline", Obj(
                    "EffectiveDate", DateTimeOffset.Now.ToString("o"),
                    "EffectiveEpochDate", DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    "Severity", 7,
                    "Text", "Forecast supplied by " + _providerManager.ActiveProvider.DisplayName,
                    "Category", "weather",
                    "EndDate", null,
                    "EndEpochDate", null,
                    "MobileLink", _providerManager.ActiveProvider.WebsiteUrl,
                    "Link", _providerManager.ActiveProvider.WebsiteUrl,
                    "XWidgetBridgeVersion", AppInfo.Version),
                "DailyForecasts", days
            );

            Log("INFO", "FORECAST",
                string.Format(CultureInfo.InvariantCulture,
                    "{0}, {1}: days={2} low={3:0.0}C high={4:0.0}C",
                    loc.name, loc.admin1, days.Count,
                    GetArrayDouble(daily, "temperature_2m_min", 0, 0),
                    GetArrayDouble(daily, "temperature_2m_max", 0, 0)));

            WriteJson(ctx, obj);
        }

        private LocationRecord GetLocation(string key)
        {
            lock (_locationLock)
            {
                LocationRecord loc;
                return _locations.TryGetValue(key, out loc) ? loc : null;
            }
        }

        private Dictionary<string, object> GetWeatherData(LocationRecord loc, string key)
        {
            Exception lastError = null;
            IWeatherProvider active = _providerManager.ActiveProvider;
            foreach (IWeatherProvider provider in
                _providerManager.GetFallbackOrder())
            {
                if (!_providerManager.CanAttempt(provider.Id))
                    continue;
                try
                {
                    Dictionary<string, object> raw = GetJsonWithCache(
                        provider,
                        "weather_" + provider.Id + "_" + key,
                        provider.BuildForecastUrl(loc),
                        TimeSpan.FromMinutes(10),
                        false);
                    Dictionary<string, object> normalized =
                        provider.NormalizeWeather(raw, loc);
                    EnsureCompleteForecast(normalized, provider);
                    return normalized;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    Log("WARN", "PROVIDER_FALLBACK",
                        provider.DisplayName + " failed; trying the next provider.");
                }
            }
            try
            {
                Dictionary<string, object> raw = GetJsonWithCache(
                    active,
                    "weather_" + active.Id + "_" + key,
                    active.BuildForecastUrl(loc),
                    TimeSpan.FromMinutes(10),
                    true);
                Dictionary<string, object> normalized =
                    active.NormalizeWeather(raw, loc);
                EnsureCompleteForecast(normalized, active);
                return normalized;
            }
            catch
            {
                throw lastError ?? new InvalidOperationException(
                    "Weather providers are waiting before retrying.");
            }
        }

        private static void EnsureCompleteForecast(
            Dictionary<string, object> weather,
            IWeatherProvider provider)
        {
            object dailyObject;
            Dictionary<string, object> daily = weather != null &&
                weather.TryGetValue("daily", out dailyObject)
                ? dailyObject as Dictionary<string, object> : null;
            object timeObject;
            IList times = daily != null &&
                daily.TryGetValue("time", out timeObject)
                ? timeObject as IList : null;
            if (times == null || times.Count < 4)
                throw new InvalidDataException(
                    provider.DisplayName +
                    " returned fewer than four forecast days.");
        }

        private Dictionary<string, object> GetJsonWithCache(
            IWeatherProvider provider,
            string cacheName,
            string url,
            TimeSpan maxAge,
            bool allowCache)
        {
            string cacheFile = Path.Combine(_cacheDir, cacheName + ".json");

            try
            {
                using (var wc = new WebClient())
                {
                    wc.Encoding = Encoding.UTF8;
                    wc.Headers[HttpRequestHeader.UserAgent] = provider.UserAgent;
                    string content = wc.DownloadString(url);
                    File.WriteAllText(cacheFile, content, new UTF8Encoding(false));
                    _providerManager.RecordSuccess(provider.Id, false);
                    return _json.Deserialize<Dictionary<string, object>>(content);
                }
            }
            catch (Exception ex)
            {
                _providerManager.RecordFailure(provider.Id, ex);

                if (allowCache && File.Exists(cacheFile))
                {
                    TimeSpan age =
                        DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile);

                    bool freshEnough = age <= maxAge;
                    bool staleAllowed =
                        _providerManager.Settings.allowStaleCache &&
                        age <= TimeSpan.FromHours(
                            _providerManager.Settings.staleCacheHours);

                    if (freshEnough || staleAllowed)
                    {
                        bool stale = !freshEnough;
                        Log(
                            "WARN",
                            stale ? "STALE_CACHE_FALLBACK" : "CACHE_FALLBACK",
                            cacheName + " age=" + age.ToString() + ": " + ex.Message);

                        string cached =
                            File.ReadAllText(cacheFile, Encoding.UTF8);
                        _providerManager.RecordCacheFallback(provider.Id, stale);

                        return _json.Deserialize<Dictionary<string, object>>(
                            cached);
                    }
                }

                throw;
            }
        }

        private int FindHourIndex(Dictionary<string, object> current, Dictionary<string, object> hourly)
        {
            string target = ToStringValue(current, "time");
            var times = AsObjectList(hourly["time"]);

            for (int i = 0; i < times.Count; i++)
            {
                if (string.Equals(
                    Convert.ToString(times[i], CultureInfo.InvariantCulture),
                    target,
                    StringComparison.Ordinal))
                    return i;
            }
            return 0;
        }

        private Dictionary<string, LocationRecord> LoadLocations()
        {
            var map = new Dictionary<string, LocationRecord>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(_locationsFile))
                {
                    string text = File.ReadAllText(_locationsFile, Encoding.UTF8);
                    var loaded = _json.Deserialize<Dictionary<string, LocationRecord>>(text);
                    if (loaded != null)
                    {
                        foreach (var pair in loaded)
                            map[pair.Key] = pair.Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("WARN", "LOCATIONS_LOAD", ex.Message);
            }
            return map;
        }

        private void EnsureBuiltInLocations()
        {
            if (!_locations.ContainsKey("54704"))
            {
                _locations["54704"] = new LocationRecord
                {
                    name = "Kentville",
                    admin1 = "Nova Scotia",
                    country = "Canada",
                    country_code = "CA",
                    latitude = 45.0771,
                    longitude = -64.4960,
                    timezone = "America/Halifax"
                };
            }
        }

        private void SaveLocations()
        {
            lock (_locationLock)
            {
                File.WriteAllText(
                    _locationsFile,
                    _json.Serialize(_locations),
                    new UTF8Encoding(false));
            }
        }

        private void WriteJson(HttpListenerContext ctx, object value, int status = 200)
        {
            string json = _json.Serialize(value);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            ctx.Response.ContentEncoding = Encoding.UTF8;
            ctx.Response.ContentLength64 = bytes.Length;
            ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
            ctx.Response.OutputStream.Close();
        }

        private void SafeError(HttpListenerContext ctx, int status, string message)
        {
            try
            {
                if (ctx != null)
                    WriteJson(ctx, Obj("Code", "ServiceError", "Message", message), status);
            }
            catch { }
        }

        private void Log(string level, string eventName, string message)
        {
            lock (_logLock)
            {
                Directory.CreateDirectory(_dataDir);
                string line =
                    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture) +
                    "\t" + level +
                    "\t" + eventName +
                    "\t" + message +
                    Environment.NewLine;

                File.AppendAllText(_logFile, line, Encoding.UTF8);

                var info = new FileInfo(_logFile);
                if (info.Exists && info.Length > 5 * 1024 * 1024)
                {
                    string old = Path.Combine(_dataDir, "bridge.old.log");
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(_logFile, old);
                }
            }
        }

        private static string SafeFileName(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                value = value.Replace(c, '_');

            using (var sha = SHA1.Create())
            {
                string hash = BitConverter.ToString(
                    sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").Substring(0, 12);
                return hash;
            }
        }

        private static Dictionary<string, object> Obj(params object[] items)
        {
            var d = new Dictionary<string, object>(StringComparer.Ordinal);
            for (int i = 0; i + 1 < items.Length; i += 2)
                d[Convert.ToString(items[i], CultureInfo.InvariantCulture)] = items[i + 1];
            return d;
        }

        private static List<object> AsObjectList(object value)
        {
            if (value is object[]) return ((object[])value).ToList();

            var enumerable = value as IEnumerable;
            if (enumerable != null && !(value is string))
            {
                var result = new List<object>();
                foreach (object item in enumerable) result.Add(item);
                return result;
            }
            return new List<object>();
        }

        private static Dictionary<string, object> AsDictionary(object value)
        {
            return value as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static string ToStringValue(Dictionary<string, object> d, string key)
        {
            object value;
            return d.TryGetValue(key, out value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : "";
        }

        private static double ToDouble(Dictionary<string, object> d, string key)
        {
            object value;
            return d.TryGetValue(key, out value) && value != null
                ? Convert.ToDouble(value, CultureInfo.InvariantCulture)
                : 0.0;
        }

        private static int ToInt(Dictionary<string, object> d, string key)
        {
            return (int)Math.Round(ToDouble(d, key));
        }

        private static double GetArrayDouble(
            Dictionary<string, object> d, string key, int index, double fallback)
        {
            object value;
            if (!d.TryGetValue(key, out value)) return fallback;

            var list = AsObjectList(value);
            if (index < 0 || index >= list.Count || list[index] == null) return fallback;

            return Convert.ToDouble(list[index], CultureInfo.InvariantCulture);
        }

        private static int GetArrayInt(
            Dictionary<string, object> d, string key, int index, int fallback)
        {
            return (int)Math.Round(GetArrayDouble(d, key, index, fallback));
        }

        private static string GetArrayString(
            Dictionary<string, object> d, string key, int index)
        {
            object value;
            if (!d.TryGetValue(key, out value)) return null;

            var list = AsObjectList(value);
            if (index < 0 || index >= list.Count || list[index] == null) return null;

            return Convert.ToString(list[index], CultureInfo.InvariantCulture);
        }

        private static string NewLocationKey(double lat, double lon)
        {
            string input =
                lat.ToString("F4", CultureInfo.InvariantCulture) + "," +
                lon.ToString("F4", CultureInfo.InvariantCulture);

            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                uint n = BitConverter.ToUInt32(hash, 0) % 90000000U + 10000000U;
                return n.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static Dictionary<string, object> UnitTemperature(double c)
        {
            return Obj(
                "Metric", Obj("Value", Math.Round(c, 1), "Unit", "C", "UnitType", 17),
                "Imperial", Obj(
                    "Value", Math.Round(c * 9.0 / 5.0 + 32.0, 1),
                    "Unit", "F",
                    "UnitType", 18));
        }

        private static Dictionary<string, object> UnitSpeed(double kmh)
        {
            return Obj(
                "Metric", Obj("Value", Math.Round(kmh, 1), "Unit", "km/h", "UnitType", 7),
                "Imperial", Obj(
                    "Value", Math.Round(kmh * 0.621371, 1),
                    "Unit", "mi/h",
                    "UnitType", 9));
        }

        private static Dictionary<string, object> UnitDistance(double km)
        {
            return Obj(
                "Metric", Obj("Value", Math.Round(km, 1), "Unit", "km", "UnitType", 6),
                "Imperial", Obj(
                    "Value", Math.Round(km * 0.621371, 1),
                    "Unit", "mi",
                    "UnitType", 2));
        }

        private static Dictionary<string, object> UnitPressure(double hpa)
        {
            return Obj(
                "Metric", Obj("Value", Math.Round(hpa, 1), "Unit", "mb", "UnitType", 14),
                "Imperial", Obj(
                    "Value", Math.Round(hpa * 0.0295299831, 2),
                    "Unit", "inHg",
                    "UnitType", 12));
        }

        private static Dictionary<string, object> UnitPrecip(double mm)
        {
            return Obj(
                "Metric", Obj("Value", Math.Round(mm, 1), "Unit", "mm", "UnitType", 3),
                "Imperial", Obj(
                    "Value", Math.Round(mm / 25.4, 2),
                    "Unit", "in",
                    "UnitType", 1));
        }

        private static Dictionary<string, object> ForecastTemperature(double c)
        {
            return Obj("Value", Math.Round(c, 1), "Unit", "C", "UnitType", 17);
        }

        private static Dictionary<string, object> ForecastSpeed(double kmh)
        {
            return Obj("Value", Math.Round(kmh, 1), "Unit", "km/h", "UnitType", 7);
        }

        private static string WindText(double degrees)
        {
            string[] dirs =
            {
                "N","NNE","NE","ENE","E","ESE","SE","SSE",
                "S","SSW","SW","WSW","W","WNW","NW","NNW"
            };
            int index = (int)Math.Floor(((degrees + 11.25) % 360.0) / 22.5);
            return dirs[index];
        }

        private static string UvText(double uv)
        {
            if (uv < 3) return "Low";
            if (uv < 6) return "Moderate";
            if (uv < 8) return "High";
            if (uv < 11) return "Very High";
            return "Extreme";
        }

        private static WeatherInfo MapWeather(int code, bool day)
        {
            switch (code)
            {
                case 0: return new WeatherInfo(day ? 1 : 33, "Clear");
                case 1: return new WeatherInfo(day ? 2 : 34, "Mostly sunny");
                case 2: return new WeatherInfo(day ? 3 : 35, "Partly cloudy");
                case 3: return new WeatherInfo(7, "Cloudy");
                case 45:
                case 48: return new WeatherInfo(11, code == 48 ? "Freezing fog" : "Fog");
                case 51: return new WeatherInfo(12, "Light drizzle");
                case 53: return new WeatherInfo(12, "Drizzle");
                case 55: return new WeatherInfo(18, "Heavy drizzle");
                case 56:
                case 57: return new WeatherInfo(24, "Freezing drizzle");
                case 61: return new WeatherInfo(12, "Light rain");
                case 63: return new WeatherInfo(18, "Rain");
                case 65: return new WeatherInfo(18, "Heavy rain");
                case 66:
                case 67: return new WeatherInfo(24, "Freezing rain");
                case 71: return new WeatherInfo(19, "Light snow");
                case 73: return new WeatherInfo(22, "Snow");
                case 75: return new WeatherInfo(22, "Heavy snow");
                case 77: return new WeatherInfo(25, "Snow grains");
                case 80: return new WeatherInfo(13, "Light rain showers");
                case 81: return new WeatherInfo(14, "Rain showers");
                case 82: return new WeatherInfo(18, "Heavy rain showers");
                case 85: return new WeatherInfo(20, "Snow showers");
                case 86: return new WeatherInfo(22, "Heavy snow showers");
                case 95: return new WeatherInfo(15, "Thunderstorms");
                case 96: return new WeatherInfo(16, "Thunderstorms with hail");
                case 99: return new WeatherInfo(17, "Severe thunderstorms with hail");
                default: return new WeatherInfo(7, "Cloudy");
            }
        }

        private sealed class WeatherInfo
        {
            public int Icon { get; private set; }
            public string Text { get; private set; }

            public WeatherInfo(int icon, string text)
            {
                Icon = icon;
                Text = text;
            }
        }
    }
}
