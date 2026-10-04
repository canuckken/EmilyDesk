using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;

namespace XWidgetWeatherBridgeV2
{
    internal interface IWeatherProvider
    {
        string Id { get; }
        string DisplayName { get; }
        bool RequiresApiKey { get; }
        string ProfileSource { get; }
        string WebsiteUrl { get; }
        string UserAgent { get; }
        string BuildForecastUrl(LocationRecord location);
        Dictionary<string, object> NormalizeWeather(
            Dictionary<string, object> raw,
            LocationRecord location);
    }

    internal sealed class ProviderProfile
    {
        public int schemaVersion { get; set; }
        public string id { get; set; }
        public string displayName { get; set; }
        public string adapter { get; set; }
        public bool enabled { get; set; }
        public bool requiresApiKey { get; set; }
        public string baseUrl { get; set; }
        public string websiteUrl { get; set; }
        public Dictionary<string, string> query { get; set; }
    }

    internal sealed class ProfileWeatherProvider : IWeatherProvider
    {
        private readonly ProviderProfile _profile;
        private readonly string _source;
        private readonly string _apiKey;

        public ProfileWeatherProvider(ProviderProfile profile, string source,
            string apiKey)
        {
            _profile = profile;
            _source = source;
            _apiKey = apiKey ?? string.Empty;
        }

        public string Id { get { return _profile.id; } }
        public string DisplayName { get { return _profile.displayName; } }
        public bool RequiresApiKey { get { return _profile.requiresApiKey; } }
        public string ProfileSource { get { return _source; } }
        public string WebsiteUrl
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_profile.websiteUrl))
                    return _profile.websiteUrl;
                Uri uri;
                return Uri.TryCreate(_profile.baseUrl, UriKind.Absolute,
                    out uri)
                    ? uri.GetLeftPart(UriPartial.Authority)
                    : string.Empty;
            }
        }
        public string UserAgent
        {
            get
            {
                return string.Equals(_profile.adapter,
                    "met-no-locationforecast-v2",
                    StringComparison.OrdinalIgnoreCase)
                    ? "EmilyDesk/1.0 (Windows weather widget)"
                    : "XWidgetWeatherBridge/1.0";
            }
        }

        public string BuildForecastUrl(LocationRecord location)
        {
            var query = HttpUtility.ParseQueryString(string.Empty);
            if (string.Equals(_profile.adapter, "open-meteo-v1",
                StringComparison.OrdinalIgnoreCase))
            {
                query["latitude"] = location.latitude.ToString(
                    "0.####", CultureInfo.InvariantCulture);
                query["longitude"] = location.longitude.ToString(
                    "0.####", CultureInfo.InvariantCulture);
            }
            else if (string.Equals(_profile.adapter,
                "met-no-locationforecast-v2",
                StringComparison.OrdinalIgnoreCase))
            {
                query["lat"] = location.latitude.ToString(
                    "0.####", CultureInfo.InvariantCulture);
                query["lon"] = location.longitude.ToString(
                    "0.####", CultureInfo.InvariantCulture);
            }
            else if (string.Equals(_profile.adapter,
                "weatherapi-v1",
                StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(_apiKey))
                    throw new InvalidOperationException(
                        "This weather provider needs an API key.");
                query["key"] = _apiKey;
                query["q"] = location.latitude.ToString(
                    "0.####", CultureInfo.InvariantCulture) + "," +
                    location.longitude.ToString(
                        "0.####", CultureInfo.InvariantCulture);
            }
            else if (string.Equals(_profile.adapter,
                "eccc-geomet-v1",
                StringComparison.OrdinalIgnoreCase))
            {
                // City Page Weather is a Canadian-city collection.  Request a
                // modest area around the saved location and choose its nearest
                // reporting city after receiving the GeoJSON response.
                const double latitudeRange = 1.5;
                const double longitudeRange = 2.0;
                query["bbox"] =
                    (location.longitude - longitudeRange).ToString(
                        "0.####", CultureInfo.InvariantCulture) + "," +
                    (location.latitude - latitudeRange).ToString(
                        "0.####", CultureInfo.InvariantCulture) + "," +
                    (location.longitude + longitudeRange).ToString(
                        "0.####", CultureInfo.InvariantCulture) + "," +
                    (location.latitude + latitudeRange).ToString(
                        "0.####", CultureInfo.InvariantCulture);
            }
            else
                throw new InvalidOperationException(
                    "Unsupported provider adapter: " + _profile.adapter);

            if (_profile.query != null)
            {
                foreach (KeyValuePair<string, string> item in _profile.query)
                    query[item.Key] = item.Value;
            }

            return _profile.baseUrl + "?" + query.ToString();
        }

        public Dictionary<string, object> NormalizeWeather(
            Dictionary<string, object> raw,
            LocationRecord location)
        {
            if (string.Equals(_profile.adapter, "open-meteo-v1",
                StringComparison.OrdinalIgnoreCase))
                return raw;

            if (string.Equals(_profile.adapter,
                "met-no-locationforecast-v2",
                StringComparison.OrdinalIgnoreCase))
                return NormalizeMetNorway(raw);

            if (string.Equals(_profile.adapter, "weatherapi-v1",
                StringComparison.OrdinalIgnoreCase))
                return NormalizeWeatherApi(raw);

            if (string.Equals(_profile.adapter, "eccc-geomet-v1",
                StringComparison.OrdinalIgnoreCase))
                return NormalizeEnvironmentCanada(raw, location);

            throw new InvalidOperationException(
                "Unsupported provider adapter: " + _profile.adapter);
        }

        private static Dictionary<string, object> NormalizeWeatherApi(
            Dictionary<string, object> raw)
        {
            Dictionary<string, object> current = Map(Value(raw, "current"));
            Dictionary<string, object> forecast = Map(Value(raw, "forecast"));
            List<object> sourceDays = Items(Value(forecast, "forecastday"));
            if (sourceDays.Count == 0)
                throw new InvalidDataException(
                    "WeatherAPI.com returned no forecast days.");

            string currentTime = NormalizeWeatherApiTime(
                Text(current, "last_updated"));
            var hourlyTimes = new List<object> { currentTime };
            var visibility = new List<object>
                { Number(current, "vis_km", 10000.0) * 1000.0 };
            var uvIndex = new List<object> { Number(current, "uv", 0) };
            var days = new List<object>();
            var codes = new List<object>();
            var minimums = new List<object>();
            var maximums = new List<object>();
            var windSpeeds = new List<object>();
            var windDirections = new List<object>();
            var precipitation = new List<object>();
            var sunrise = new List<object>();
            var sunset = new List<object>();
            var dailyUv = new List<object>();

            foreach (object sourceDayObject in sourceDays)
            {
                Dictionary<string, object> sourceDay = Map(sourceDayObject);
                Dictionary<string, object> day = Map(Value(sourceDay, "day"));
                Dictionary<string, object> astro = Map(Value(sourceDay, "astro"));
                Dictionary<string, object> condition = Map(Value(day, "condition"));
                days.Add(Text(sourceDay, "date"));
                codes.Add(WeatherApiCodeToWmo(Number(condition, "code", 1000)));
                minimums.Add(Number(day, "mintemp_c", 0));
                maximums.Add(Number(day, "maxtemp_c", 0));
                windSpeeds.Add(Number(day, "maxwind_kph", 0));
                windDirections.Add(0);
                precipitation.Add(Number(day, "daily_chance_of_rain", 0));
                sunrise.Add(Text(astro, "sunrise"));
                sunset.Add(Text(astro, "sunset"));
                dailyUv.Add(Number(day, "uv", 0));
            }

            Dictionary<string, object> currentCondition = Map(
                Value(current, "condition"));
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "current", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", currentTime },
                        { "temperature_2m", Number(current, "temp_c", 0) },
                        { "apparent_temperature", Number(current, "feelslike_c", 0) },
                        { "relative_humidity_2m", Number(current, "humidity", 0) },
                        { "is_day", Number(current, "is_day", 1) },
                        { "precipitation", Number(current, "precip_mm", 0) },
                        { "rain", 0 },
                        { "snowfall", 0 },
                        { "weather_code", WeatherApiCodeToWmo(
                            Number(currentCondition, "code", 1000)) },
                        { "cloud_cover", Number(current, "cloud", 0) },
                        { "pressure_msl", Number(current, "pressure_mb", 0) },
                        { "wind_speed_10m", Number(current, "wind_kph", 0) },
                        { "wind_direction_10m", Number(current, "wind_degree", 0) },
                        { "wind_gusts_10m", Number(current, "gust_kph", 0) }
                    }
                },
                { "hourly", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", hourlyTimes },
                        { "visibility", visibility },
                        { "uv_index", uvIndex }
                    }
                },
                { "daily", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", days },
                        { "weather_code", codes },
                        { "temperature_2m_min", minimums },
                        { "temperature_2m_max", maximums },
                        { "wind_speed_10m_max", windSpeeds },
                        { "wind_direction_10m_dominant", windDirections },
                        { "precipitation_probability_max", precipitation },
                        { "sunrise", sunrise },
                        { "sunset", sunset },
                        { "uv_index_max", dailyUv }
                    }
                }
            };
        }

        private static Dictionary<string, object> NormalizeEnvironmentCanada(
            Dictionary<string, object> raw, LocationRecord location)
        {
            Dictionary<string, object> feature = NearestEcccFeature(raw,
                location);
            Dictionary<string, object> properties = Map(Value(feature,
                "properties"));
            Dictionary<string, object> current = Map(Value(properties,
                "currentConditions"));
            if (current.Count == 0)
                throw new InvalidDataException(
                    "Environment Canada did not return current conditions for this location.");

            double temperature = EcccNumber(current, "temperature", 0);
            string timestamp = NormalizeTime(EcccText(current, "timestamp"));
            if (string.IsNullOrWhiteSpace(timestamp))
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm",
                    CultureInfo.InvariantCulture);

            Dictionary<string, object> forecastGroup = Map(Value(properties,
                "forecastGroup"));
            List<object> forecasts = Items(Value(forecastGroup, "forecasts"));
            List<EcccDay> days = BuildEcccDays(forecasts, timestamp,
                temperature);
            if (days.Count == 0)
                throw new InvalidDataException(
                    "Environment Canada did not return a usable forecast.");

            var times = new List<object>();
            var codes = new List<object>();
            var minimums = new List<object>();
            var maximums = new List<object>();
            var winds = new List<object>();
            var directions = new List<object>();
            var precipitation = new List<object>();
            var sunrise = new List<object>();
            var sunset = new List<object>();
            var dailyUv = new List<object>();
            foreach (EcccDay day in days)
            {
                times.Add(day.Date.ToString("yyyy-MM-dd",
                    CultureInfo.InvariantCulture));
                codes.Add(day.WeatherCode);
                minimums.Add(day.MinimumTemperature);
                maximums.Add(day.MaximumTemperature);
                winds.Add(day.MaximumWindSpeed);
                directions.Add(day.WindDirection);
                precipitation.Add(day.PrecipitationProbability);
                sunrise.Add(string.Empty);
                sunset.Add(string.Empty);
                dailyUv.Add(day.UvIndex);
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "current", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", timestamp },
                        { "temperature_2m", temperature },
                        { "apparent_temperature", temperature },
                        { "relative_humidity_2m", EcccNumber(current,
                            "relativeHumidity", 0) },
                        { "is_day", 1 },
                        { "precipitation", 0 },
                        { "rain", 0 },
                        { "snowfall", 0 },
                        { "weather_code", EcccConditionToWmo(EcccText(
                            current, "condition")) },
                        { "cloud_cover", 0 },
                        { "pressure_msl", EcccNumber(current, "pressure",
                            0) * 10.0 },
                        { "wind_speed_10m", EcccWindSpeed(current) },
                        { "wind_direction_10m", EcccWindBearing(current) },
                        { "wind_gusts_10m", 0 }
                    }
                },
                { "hourly", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", new List<object> { timestamp } },
                        { "visibility", new List<object> { EcccNumber(
                            current, "visibility", 10) * 1000.0 } },
                        { "uv_index", new List<object> { 0 } }
                    }
                },
                { "daily", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", times },
                        { "weather_code", codes },
                        { "temperature_2m_min", minimums },
                        { "temperature_2m_max", maximums },
                        { "wind_speed_10m_max", winds },
                        { "wind_direction_10m_dominant", directions },
                        { "precipitation_probability_max", precipitation },
                        { "sunrise", sunrise },
                        { "sunset", sunset },
                        { "uv_index_max", dailyUv }
                    }
                }
            };
        }

        private static Dictionary<string, object> NearestEcccFeature(
            Dictionary<string, object> raw, LocationRecord location)
        {
            Dictionary<string, object> nearest = null;
            double nearestDistance = double.MaxValue;
            foreach (object featureObject in Items(Value(raw, "features")))
            {
                Dictionary<string, object> feature = Map(featureObject);
                List<object> coordinates = Items(Value(Map(Value(feature,
                    "geometry")), "coordinates"));
                if (coordinates.Count < 2)
                    continue;
                double longitude = ToNumber(coordinates[0], 0);
                double latitude = ToNumber(coordinates[1], 0);
                double distance = Math.Pow(latitude - location.latitude, 2) +
                    Math.Pow(longitude - location.longitude, 2);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = feature;
                }
            }
            if (nearest == null)
                throw new InvalidDataException(
                    "Environment Canada does not publish a city forecast for this location.");
            return nearest;
        }

        private static List<EcccDay> BuildEcccDays(List<object> forecasts,
            string timestamp, double currentTemperature)
        {
            DateTime start;
            if (!DateTime.TryParse(timestamp, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out start))
                start = DateTime.UtcNow;
            var days = new List<EcccDay>();
            EcccDay currentDay = new EcccDay(start.Date);
            days.Add(currentDay);
            foreach (object item in forecasts)
            {
                Dictionary<string, object> forecast = Map(item);
                Dictionary<string, object> period = Map(Value(forecast,
                    "period"));
                string periodName = EcccText(period, "value");
                bool night = periodName.IndexOf("night",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                    periodName.IndexOf("tonight",
                    StringComparison.OrdinalIgnoreCase) >= 0;
                if (!night && currentDay.HasNight)
                {
                    currentDay = new EcccDay(currentDay.Date.AddDays(1));
                    days.Add(currentDay);
                }
                currentDay.Add(forecast, night);
            }
            foreach (EcccDay day in days)
                day.FillMissingTemperatures(currentTemperature);
            return days;
        }

        private sealed class EcccDay
        {
            public readonly DateTime Date;
            public double MinimumTemperature;
            public double MaximumTemperature;
            public double MaximumWindSpeed;
            public double WindDirection;
            public int PrecipitationProbability;
            public double UvIndex;
            public int WeatherCode = 3;
            public bool HasNight;
            private bool _hasMinimum;
            private bool _hasMaximum;

            public EcccDay(DateTime date)
            {
                Date = date;
            }

            public void Add(Dictionary<string, object> forecast, bool night)
            {
                Dictionary<string, object> temperatures = Map(Value(forecast,
                    "temperatures"));
                foreach (object item in Items(Value(temperatures, "temperature")))
                {
                    Dictionary<string, object> value = Map(item);
                    string kind = EcccText(value, "class");
                    double temperature = EcccNumber(value, "value", 0);
                    if (string.Equals(kind, "low",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        MinimumTemperature = temperature;
                        _hasMinimum = true;
                    }
                    else if (string.Equals(kind, "high",
                        StringComparison.OrdinalIgnoreCase))
                    {
                        MaximumTemperature = temperature;
                        _hasMaximum = true;
                    }
                }
                Dictionary<string, object> abbreviated = Map(Value(forecast,
                    "abbreviatedForecast"));
                WeatherCode = EcccConditionToWmo(EcccText(abbreviated,
                    "textSummary"));
                Dictionary<string, object> winds = Map(Value(forecast, "winds"));
                foreach (object item in Items(Value(winds, "periods")))
                {
                    Dictionary<string, object> wind = Map(item);
                    MaximumWindSpeed = Math.Max(MaximumWindSpeed,
                        EcccNumber(Map(Value(wind, "speed")), "value", 0));
                    WindDirection = EcccNumber(Map(Value(wind, "bearing")),
                        "value", WindDirection);
                }
                if (night)
                    HasNight = true;
            }

            public void FillMissingTemperatures(double currentTemperature)
            {
                if (!_hasMinimum)
                    MinimumTemperature = currentTemperature;
                if (!_hasMaximum)
                    MaximumTemperature = currentTemperature;
            }
        }

        private static string EcccText(Dictionary<string, object> map,
            string key)
        {
            object value = Value(map, key);
            Dictionary<string, object> translated = Map(value);
            string english = Text(translated, "en");
            if (string.IsNullOrWhiteSpace(english))
            {
                translated = Map(Value(translated, "value"));
                english = Text(translated, "en");
            }
            return string.IsNullOrWhiteSpace(english)
                ? (value == null ? string.Empty : Convert.ToString(value,
                    CultureInfo.InvariantCulture))
                : english;
        }

        private static double EcccNumber(Dictionary<string, object> map,
            string key, double fallback)
        {
            object value = Value(map, key);
            Dictionary<string, object> nested = Map(value);
            object english = Value(nested, "en");
            if (english == null)
                english = Value(Map(Value(nested, "value")), "en");
            if (english == null)
                english = value;
            return ToNumber(english, fallback);
        }

        private static double ToNumber(object value, double fallback)
        {
            if (value == null)
                return fallback;
            try
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static double EcccWindSpeed(Dictionary<string, object> current)
        {
            return EcccNumber(Map(Value(current, "wind")), "speed", 0);
        }

        private static double EcccWindBearing(Dictionary<string, object> current)
        {
            return EcccNumber(Map(Value(current, "wind")), "bearing", 0);
        }

        private static int EcccConditionToWmo(string condition)
        {
            string text = condition ?? string.Empty;
            if (text.IndexOf("thunder", StringComparison.OrdinalIgnoreCase) >= 0)
                return 95;
            if (text.IndexOf("freezing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("ice", StringComparison.OrdinalIgnoreCase) >= 0)
                return 66;
            if (text.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("flurr", StringComparison.OrdinalIgnoreCase) >= 0)
                return 71;
            if (text.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("shower", StringComparison.OrdinalIgnoreCase) >= 0)
                return 61;
            if (text.IndexOf("drizzle", StringComparison.OrdinalIgnoreCase) >= 0)
                return 51;
            if (text.IndexOf("fog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("mist", StringComparison.OrdinalIgnoreCase) >= 0)
                return 45;
            if (text.IndexOf("cloud", StringComparison.OrdinalIgnoreCase) >= 0)
                return text.IndexOf("mix", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("clearing", StringComparison.OrdinalIgnoreCase) >= 0
                    ? 2 : 3;
            if (text.IndexOf("clear", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0)
                return 0;
            return 3;
        }

        private static string NormalizeWeatherApiTime(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out parsed)
                ? parsed.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)
                : NormalizeTime(value);
        }

        private static int WeatherApiCodeToWmo(double weatherApiCode)
        {
            int code = (int)Math.Round(weatherApiCode);
            switch (code)
            {
                case 1000: return 0;
                case 1003: return 2;
                case 1006: case 1009: return 3;
                case 1030: case 1135: case 1147: return 45;
                case 1063: case 1150: case 1153: case 1168: case 1171: return 51;
                case 1180: case 1183: case 1186: case 1189: return 61;
                case 1192: case 1195: case 1240: case 1243: case 1246: return 65;
                case 1066: case 1210: case 1213: case 1216: case 1219: return 71;
                case 1222: case 1225: case 1255: case 1258: return 75;
                case 1087: case 1273: case 1276: case 1279: case 1282: return 95;
                default: return 3;
            }
        }

        private static Dictionary<string, object> NormalizeMetNorway(
            Dictionary<string, object> raw)
        {
            Dictionary<string, object> properties = Map(
                Value(raw, "properties"));
            List<object> points = Items(Value(properties, "timeseries"));
            if (points.Count == 0)
                throw new InvalidDataException(
                    "MET Norway returned no forecast points.");

            var hourlyTimes = new List<object>();
            var visibility = new List<object>();
            var uvIndex = new List<object>();
            var days = new Dictionary<string, MetDay>(
                StringComparer.Ordinal);
            var dayOrder = new List<string>();
            Dictionary<string, object> first = Map(points[0]);
            Dictionary<string, object> firstData = Map(Value(first, "data"));
            Dictionary<string, object> instant = Map(Value(
                Map(Value(firstData, "instant")), "details"));
            string firstSymbol = Symbol(firstData);

            foreach (object pointObject in points)
            {
                Dictionary<string, object> point = Map(pointObject);
                Dictionary<string, object> data = Map(Value(point, "data"));
                Dictionary<string, object> details = Map(Value(
                    Map(Value(data, "instant")), "details"));
                string time = Text(point, "time");
                if (string.IsNullOrEmpty(time))
                    continue;

                hourlyTimes.Add(NormalizeTime(time));
                visibility.Add(Number(details, "visibility", 10000.0));
                uvIndex.Add(Number(details, "ultraviolet_index_clear_sky", 0));

                string day = time.Length >= 10 ? time.Substring(0, 10) : time;
                MetDay daily;
                if (!days.TryGetValue(day, out daily))
                {
                    daily = new MetDay(day);
                    days.Add(day, daily);
                    dayOrder.Add(day);
                }
                daily.Add(details, Symbol(data), Precipitation(data));
            }

            var dayTimes = new List<object>();
            var codes = new List<object>();
            var minTemps = new List<object>();
            var maxTemps = new List<object>();
            var winds = new List<object>();
            var directions = new List<object>();
            var precipitation = new List<object>();
            var sunrise = new List<object>();
            var sunset = new List<object>();
            var dailyUv = new List<object>();
            foreach (string dayKey in dayOrder)
            {
                MetDay day = days[dayKey];
                dayTimes.Add(day.Date);
                codes.Add(MetSymbolToWmo(day.Symbol));
                minTemps.Add(day.MinimumTemperature);
                maxTemps.Add(day.MaximumTemperature);
                winds.Add(day.MaximumWindSpeed);
                directions.Add(day.WindDirection);
                precipitation.Add(day.MaximumPrecipitationProbability);
                sunrise.Add(string.Empty);
                sunset.Add(string.Empty);
                dailyUv.Add(day.MaximumUvIndex);
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "current", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", NormalizeTime(Text(first, "time")) },
                        { "temperature_2m", Number(instant, "air_temperature", 0) },
                        { "apparent_temperature", Number(instant, "air_temperature", 0) },
                        { "relative_humidity_2m", Number(instant, "relative_humidity", 0) },
                        { "is_day", 1 },
                        { "precipitation", Precipitation(firstData) },
                        { "rain", 0 },
                        { "snowfall", 0 },
                        { "weather_code", MetSymbolToWmo(firstSymbol) },
                        { "cloud_cover", Number(instant, "cloud_area_fraction", 0) },
                        { "pressure_msl", Number(instant, "air_pressure_at_sea_level", 0) },
                        { "wind_speed_10m", Number(instant, "wind_speed", 0) },
                        { "wind_direction_10m", Number(instant, "wind_from_direction", 0) },
                        { "wind_gusts_10m", Number(instant, "wind_speed_of_gust", 0) }
                    }
                },
                { "hourly", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", hourlyTimes },
                        { "visibility", visibility },
                        { "uv_index", uvIndex }
                    }
                },
                { "daily", new Dictionary<string, object>(
                    StringComparer.Ordinal)
                    {
                        { "time", dayTimes },
                        { "weather_code", codes },
                        { "temperature_2m_min", minTemps },
                        { "temperature_2m_max", maxTemps },
                        { "wind_speed_10m_max", winds },
                        { "wind_direction_10m_dominant", directions },
                        { "precipitation_probability_max", precipitation },
                        { "sunrise", sunrise },
                        { "sunset", sunset },
                        { "uv_index_max", dailyUv }
                    }
                }
            };
        }

        private sealed class MetDay
        {
            public readonly string Date;
            public double MinimumTemperature = double.MaxValue;
            public double MaximumTemperature = double.MinValue;
            public double MaximumWindSpeed;
            public double WindDirection;
            public int MaximumPrecipitationProbability;
            public double MaximumUvIndex;
            public string Symbol = "cloudy";

            public MetDay(string date)
            {
                Date = date;
            }

            public void Add(Dictionary<string, object> details,
                string symbol, double precipitation)
            {
                double temperature = Number(details, "air_temperature", 0);
                MinimumTemperature = Math.Min(MinimumTemperature, temperature);
                MaximumTemperature = Math.Max(MaximumTemperature, temperature);
                MaximumWindSpeed = Math.Max(MaximumWindSpeed,
                    Number(details, "wind_speed", 0));
                WindDirection = Number(details, "wind_from_direction",
                    WindDirection);
                MaximumUvIndex = Math.Max(MaximumUvIndex,
                    Number(details, "ultraviolet_index_clear_sky", 0));
                if (precipitation > 0)
                    MaximumPrecipitationProbability = 100;
                if (!string.IsNullOrEmpty(symbol))
                    Symbol = symbol;
            }
        }

        private static Dictionary<string, object> Map(object value)
        {
            return value as Dictionary<string, object> ??
                new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static List<object> Items(object value)
        {
            var result = new List<object>();
            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string)
                return result;
            foreach (object item in enumerable)
                result.Add(item);
            return result;
        }

        private static object Value(Dictionary<string, object> map,
            string key)
        {
            object value;
            return map != null && map.TryGetValue(key, out value)
                ? value
                : null;
        }

        private static string Text(Dictionary<string, object> map,
            string key)
        {
            object value = Value(map, key);
            return value == null ? string.Empty : Convert.ToString(value,
                CultureInfo.InvariantCulture);
        }

        private static string NormalizeTime(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Length >= 16
                ? value.Substring(0, 16)
                : value;
        }

        private static double Number(Dictionary<string, object> map,
            string key, double fallback)
        {
            object value = Value(map, key);
            if (value == null)
                return fallback;
            try
            {
                return Convert.ToDouble(value,
                    CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static string Symbol(Dictionary<string, object> data)
        {
            string[] periods = new[]
            {
                "next_1_hours", "next_6_hours", "next_12_hours"
            };
            foreach (string period in periods)
            {
                Dictionary<string, object> summary = Map(Value(
                    Map(Value(data, period)), "summary"));
                string symbol = Text(summary, "symbol_code");
                if (!string.IsNullOrEmpty(symbol))
                    return symbol;
            }
            return string.Empty;
        }

        private static double Precipitation(
            Dictionary<string, object> data)
        {
            string[] periods = new[]
            {
                "next_1_hours", "next_6_hours", "next_12_hours"
            };
            foreach (string period in periods)
            {
                Dictionary<string, object> details = Map(Value(
                    Map(Value(data, period)), "details"));
                double amount = Number(details, "precipitation_amount", -1);
                if (amount >= 0)
                    return amount;
            }
            return 0;
        }

        private static int MetSymbolToWmo(string symbol)
        {
            string text = symbol ?? string.Empty;
            if (text.IndexOf("thunder", StringComparison.OrdinalIgnoreCase) >= 0)
                return 95;
            if (text.IndexOf("snow", StringComparison.OrdinalIgnoreCase) >= 0)
                return 71;
            if (text.IndexOf("sleet", StringComparison.OrdinalIgnoreCase) >= 0)
                return 66;
            if (text.IndexOf("rain", StringComparison.OrdinalIgnoreCase) >= 0)
                return 61;
            if (text.IndexOf("drizzle", StringComparison.OrdinalIgnoreCase) >= 0)
                return 51;
            if (text.IndexOf("fog", StringComparison.OrdinalIgnoreCase) >= 0)
                return 45;
            if (text.IndexOf("partlycloudy",
                StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            if (text.IndexOf("cloudy", StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;
            if (text.IndexOf("fair", StringComparison.OrdinalIgnoreCase) >= 0)
                return 1;
            return 0;
        }
    }

    internal sealed class ProviderSettings
    {
        public string activeProviderId { get; set; }
        public bool allowStaleCache { get; set; }
        public int staleCacheHours { get; set; }
        public Dictionary<string, string> apiKeys { get; set; }

        public static ProviderSettings CreateDefault()
        {
            return new ProviderSettings
            {
                activeProviderId = "open-meteo",
                allowStaleCache = true,
                staleCacheHours = 24,
                apiKeys = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
            };
        }
    }

    internal sealed class ProviderManager
    {
        internal sealed class ProviderHealth
        {
            public int ConsecutiveFailures;
            public DateTime? LastSuccessUtc;
            public DateTime? LastFailureUtc;
            public DateTime NextRetryUtc;
            public string LastError;
        }

        private readonly Dictionary<string, IWeatherProvider> _providers;
        private readonly Dictionary<string, ProviderHealth> _health;
        private readonly string _settingsFile;
        private readonly string _profileDirectory;
        private readonly JavaScriptSerializer _json;
        private static readonly byte[] ApiKeyEntropy = Encoding.UTF8.GetBytes(
            "EmilyDesk Weather Provider Keys v1");

        public ProviderSettings Settings { get; private set; }
        public DateTime? LastSuccessUtc { get; private set; }
        public DateTime? LastFailureUtc { get; private set; }
        public DateTime? LastLiveFetchUtc { get; private set; }
        public string LastError { get; private set; }
        public bool LastResponseUsedStaleCache { get; private set; }
        public bool LastResponseUsedCache { get; private set; }
        public int LoadedProfileCount { get; private set; }

        public ProviderManager(string dataDirectory, JavaScriptSerializer json)
        {
            _json = json;
            _settingsFile = Path.Combine(dataDirectory, "provider-settings.json");
            _profileDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "ProviderProfiles");

            _providers = new Dictionary<string, IWeatherProvider>(
                StringComparer.OrdinalIgnoreCase);
            _health = new Dictionary<string, ProviderHealth>(
                StringComparer.OrdinalIgnoreCase);

            Settings = LoadSettings();
            ReloadProfiles();
            EnsureValidSelection();
        }

        public IEnumerable<IWeatherProvider> Providers
        {
            get { return _providers.Values; }
        }

        public IWeatherProvider ActiveProvider
        {
            get { return _providers[Settings.activeProviderId]; }
        }

        public IEnumerable<IWeatherProvider> GetFallbackOrder()
        {
            IWeatherProvider active = ActiveProvider;
            yield return active;
            foreach (IWeatherProvider provider in _providers.Values)
                if (!string.Equals(provider.Id, active.Id,
                    StringComparison.OrdinalIgnoreCase))
                    yield return provider;
        }

        public bool CanAttempt(string providerId)
        {
            ProviderHealth health;
            return !_health.TryGetValue(providerId, out health) ||
                DateTime.UtcNow >= health.NextRetryUtc;
        }

        public ProviderHealth GetHealth(string providerId)
        {
            ProviderHealth health;
            if (!_health.TryGetValue(providerId, out health))
            {
                health = new ProviderHealth();
                _health[providerId] = health;
            }
            return health;
        }

        public void ReloadProfiles()
        {
            _providers.Clear();
            _health.Clear();
            LoadedProfileCount = 0;

            if (!Directory.Exists(_profileDirectory))
                Directory.CreateDirectory(_profileDirectory);

            string[] files = Directory.GetFiles(_profileDirectory, "*.json");
            foreach (string file in files)
            {
                try
                {
                    string text = File.ReadAllText(file, Encoding.UTF8);
                    ProviderProfile profile =
                        _json.Deserialize<ProviderProfile>(text);

                    ValidateProfile(profile, file);
                    if (!profile.enabled)
                        continue;

                    _providers[profile.id] = new ProfileWeatherProvider(
                        profile, file, GetApiKey(profile.id));
                    LoadedProfileCount++;
                }
                catch
                {
                    // A damaged optional profile must not stop the service.
                }
            }

            if (!_providers.ContainsKey("open-meteo"))
            {
                ProviderProfile fallback = CreateBuiltInOpenMeteoProfile();
                _providers[fallback.id] =
                    new ProfileWeatherProvider(fallback, "Built-in fallback",
                        string.Empty);
            }

            if (Settings != null)
                EnsureValidSelection();
        }

        public bool Select(string providerId)
        {
            IWeatherProvider provider;
            if (!_providers.TryGetValue(providerId, out provider) ||
                (provider.RequiresApiKey && !HasApiKey(providerId)))
                return false;

            Settings.activeProviderId = providerId;
            SaveSettings();
            return true;
        }

        public bool HasApiKey(string providerId)
        {
            return !string.IsNullOrWhiteSpace(GetApiKey(providerId));
        }

        public bool SetApiKey(string providerId, string apiKey)
        {
            IWeatherProvider provider;
            if (!_providers.TryGetValue(providerId, out provider) ||
                !provider.RequiresApiKey)
                return false;

            if (Settings.apiKeys == null)
                Settings.apiKeys = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(apiKey))
                Settings.apiKeys.Remove(providerId);
            else
                Settings.apiKeys[providerId] = ProtectApiKey(apiKey.Trim());
            SaveSettings();
            ReloadProfiles();
            return true;
        }

        public void RecordSuccess(
            string providerId,
            bool usedStaleCache)
        {
            ProviderHealth health = GetHealth(providerId);
            health.ConsecutiveFailures = 0;
            health.LastSuccessUtc = DateTime.UtcNow;
            health.NextRetryUtc = DateTime.MinValue;
            health.LastError = null;
            LastSuccessUtc = DateTime.UtcNow;
            if (!usedStaleCache)
                LastLiveFetchUtc = DateTime.UtcNow;
            LastError = null;
            LastResponseUsedCache = false;
            LastResponseUsedStaleCache = usedStaleCache;
        }

        public void RecordCacheFallback(string providerId, bool stale)
        {
            LastSuccessUtc = DateTime.UtcNow;
            LastResponseUsedCache = true;
            LastResponseUsedStaleCache = stale;
            // Keep the provider failure and retry deadline visible in health;
            // serving cached data is not a live provider recovery.
        }

        public void RecordFailure(string providerId, Exception error)
        {
            ProviderHealth health = GetHealth(providerId);
            health.ConsecutiveFailures++;
            health.LastFailureUtc = DateTime.UtcNow;
            health.LastError = PlainError(error);
            int delayMinutes = Math.Min(
                30,
                (int)Math.Pow(2, Math.Min(4,
                    health.ConsecutiveFailures - 1)));
            health.NextRetryUtc = DateTime.UtcNow.AddMinutes(delayMinutes);
            LastFailureUtc = DateTime.UtcNow;
            LastError = health.LastError;
        }

        private static string PlainError(Exception error)
        {
            if (error == null) return "The weather provider did not respond.";
            if (error is System.Net.WebException)
                return "The weather provider is temporarily unavailable.";
            if (error is InvalidDataException ||
                error is InvalidOperationException)
                return "The weather provider returned data EmilyDesk could not use.";
            return "EmilyDesk could not update weather right now.";
        }

        private static void ValidateProfile(
            ProviderProfile profile,
            string source)
        {
            if (profile == null)
                throw new InvalidDataException("Empty provider profile: " + source);
            if (profile.schemaVersion != 1)
                throw new InvalidDataException(
                    "Unsupported profile schema in " + source);
            if (string.IsNullOrWhiteSpace(profile.id))
                throw new InvalidDataException("Provider ID is missing.");
            if (string.IsNullOrWhiteSpace(profile.displayName))
                throw new InvalidDataException("Provider name is missing.");
            if (string.IsNullOrWhiteSpace(profile.adapter))
                throw new InvalidDataException("Provider adapter is missing.");
            if (string.IsNullOrWhiteSpace(profile.baseUrl))
                throw new InvalidDataException("Provider URL is missing.");
            Uri uri;
            if (!Uri.TryCreate(profile.baseUrl, UriKind.Absolute, out uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Provider URLs must use HTTPS.");
            }
        }

        private static ProviderProfile CreateBuiltInOpenMeteoProfile()
        {
            return new ProviderProfile
            {
                schemaVersion = 1,
                id = "open-meteo",
                displayName = "Open-Meteo",
                adapter = "open-meteo-v1",
                enabled = true,
                requiresApiKey = false,
                baseUrl = "https://api.open-meteo.com/v1/forecast",
                websiteUrl = "https://open-meteo.com/",
                query = new Dictionary<string, string>
                {
                    { "current", "temperature_2m,relative_humidity_2m,apparent_temperature,is_day,precipitation,rain,snowfall,weather_code,cloud_cover,pressure_msl,wind_speed_10m,wind_direction_10m,wind_gusts_10m" },
                    { "hourly", "visibility,uv_index" },
                    { "daily", "weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset,uv_index_max,precipitation_probability_max,wind_speed_10m_max,wind_direction_10m_dominant" },
                    { "temperature_unit", "celsius" },
                    { "wind_speed_unit", "kmh" },
                    { "precipitation_unit", "mm" },
                    { "timezone", "auto" },
                    { "forecast_days", "15" }
                }
            };
        }

        private void EnsureValidSelection()
        {
            if (Settings == null)
                return;

            IWeatherProvider selected;
            if (string.IsNullOrWhiteSpace(Settings.activeProviderId) ||
                !_providers.TryGetValue(Settings.activeProviderId,
                    out selected) ||
                (selected.RequiresApiKey &&
                    !HasApiKey(Settings.activeProviderId)))
            {
                Settings.activeProviderId = "open-meteo";
                SaveSettings();
            }

            if (Settings.staleCacheHours < 1)
                Settings.staleCacheHours = 24;
            if (Settings.apiKeys == null)
                Settings.apiKeys = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
        }

        private ProviderSettings LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    string text = File.ReadAllText(_settingsFile, Encoding.UTF8);
                    ProviderSettings loaded =
                        _json.Deserialize<ProviderSettings>(text);
                    if (loaded != null)
                    {
                        if (loaded.apiKeys == null)
                            loaded.apiKeys = new Dictionary<string, string>(
                                StringComparer.OrdinalIgnoreCase);
                        return loaded;
                    }
                }
            }
            catch
            {
            }

            ProviderSettings settings = ProviderSettings.CreateDefault();
            Settings = settings;
            SaveSettings();
            return settings;
        }

        private void SaveSettings()
        {
            try
            {
                string text = _json.Serialize(Settings);
                File.WriteAllText(
                    _settingsFile,
                    text,
                    new UTF8Encoding(false));
            }
            catch
            {
            }
        }

        private string GetApiKey(string providerId)
        {
            if (Settings == null || Settings.apiKeys == null ||
                string.IsNullOrWhiteSpace(providerId))
                return string.Empty;

            string protectedValue;
            return Settings.apiKeys.TryGetValue(providerId, out protectedValue)
                ? UnprotectApiKey(protectedValue)
                : string.Empty;
        }

        private static string ProtectApiKey(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            byte[] protectedBytes = ProtectedData.Protect(bytes,
                ApiKeyEntropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private static string UnprotectApiKey(string value)
        {
            try
            {
                byte[] protectedBytes = Convert.FromBase64String(value);
                byte[] bytes = ProtectedData.Unprotect(protectedBytes,
                    ApiKeyEntropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
