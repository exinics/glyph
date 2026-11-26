using System.Globalization;
using System.Text.Json;

namespace Glyph.Data;

/// Weather from Open-Meteo (free, no account). Only used when a widget or bar module asks for it,
/// and only for the place you set. Refreshed every 30 minutes.
internal static class Weather
{
    public sealed class Day { public DateTime Date; public double High, Low; public int Code; }

    static string place = "";
    static double lat, lon; static bool fahrenheit;
    static volatile string city = "", status = "set a city";
    static double temp, feels, wind, humidity; static int code = -1;
    public static volatile List<Day> Days = new();
    static DateTime next = DateTime.MinValue;
    static volatile bool busy;
    public static volatile int Version;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// Sets where to look (a city name, or latitude/longitude if not 0). Cheap to call every frame.
    public static void Configure(string cityName, double latitude, double longitude, bool useFahrenheit)
    {
        var key = $"{cityName}|{latitude}|{longitude}|{useFahrenheit}";
        if (key != place) { place = key; lat = latitude; lon = longitude; fahrenheit = useFahrenheit; city = cityName; next = DateTime.MinValue; code = -1; status = "loading…"; }
        if (DateTime.UtcNow >= next && !busy) { busy = true; next = DateTime.UtcNow.AddMinutes(30); _ = Fetch(cityName); }
    }

    static async Task Fetch(string cityName)
    {
        try
        {
            double la = lat, lo = lon;
            if (la == 0 && lo == 0)
            {
                if (cityName.Trim().Length == 0) { status = "set a city"; return; }
                var g = await Http.GetStringAsync("https://geocoding-api.open-meteo.com/v1/search?count=1&name=" + Uri.EscapeDataString(cityName.Trim()));
                using var gd = JsonDocument.Parse(g);
                if (!gd.RootElement.TryGetProperty("results", out var rs) || rs.GetArrayLength() == 0) { status = "city not found"; return; }
                la = rs[0].GetProperty("latitude").GetDouble(); lo = rs[0].GetProperty("longitude").GetDouble();
                city = rs[0].GetProperty("name").GetString() ?? cityName;
            }
            var unit = fahrenheit ? "&temperature_unit=fahrenheit&wind_speed_unit=mph" : "";
            var url = string.Format(CultureInfo.InvariantCulture,
                "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=7{2}", la, lo, unit);
            var j = await Http.GetStringAsync(url);
            using var d = JsonDocument.Parse(j);
            var c = d.RootElement.GetProperty("current");
            temp = c.GetProperty("temperature_2m").GetDouble(); feels = c.GetProperty("apparent_temperature").GetDouble();
            humidity = c.GetProperty("relative_humidity_2m").GetDouble(); wind = c.GetProperty("wind_speed_10m").GetDouble();
            code = c.GetProperty("weather_code").GetInt32();
            var daily = d.RootElement.GetProperty("daily");
            var days = new List<Day>();
            var times = daily.GetProperty("time"); var hi = daily.GetProperty("temperature_2m_max"); var lw = daily.GetProperty("temperature_2m_min"); var cd = daily.GetProperty("weather_code");
            for (int i = 0; i < times.GetArrayLength(); i++)
                days.Add(new Day { Date = DateTime.Parse(times[i].GetString()!, CultureInfo.InvariantCulture), High = hi[i].GetDouble(), Low = lw[i].GetDouble(), Code = cd[i].GetInt32() });
            Days = days;
            status = "";
            Version++;
        }
        catch (Exception e) { status = "no connection"; next = DateTime.UtcNow.AddMinutes(5); Log.Write("weather: " + e.Message); }
        finally { busy = false; Version++; }
    }

    public static string Describe(int c) => c switch
    {
        0 => "clear", 1 => "mostly clear", 2 => "partly cloudy", 3 => "cloudy", 45 or 48 => "fog",
        51 or 53 or 55 => "drizzle", 56 or 57 => "freezing drizzle", 61 or 63 => "rain", 65 => "heavy rain", 66 or 67 => "freezing rain",
        71 or 73 => "snow", 75 => "heavy snow", 77 => "snow grains", 80 or 81 => "showers", 82 => "heavy showers", 85 or 86 => "snow showers",
        95 => "thunderstorm", 96 or 99 => "thunderstorm, hail", _ => "",
    };

    public static string Icon(int c) => c switch
    {
        0 or 1 => "☀", 2 => "⛅", 3 or 45 or 48 => "☁", >= 51 and <= 67 => "☂", >= 80 and <= 82 => "☂", >= 71 and <= 77 => "❄", 85 or 86 => "❄", >= 95 => "⚡", _ => "·",
    };

    public static string? Value(string name, string spec)
    {
        if (code < 0) return name == "weather" ? status : "";
        string N(double v) => Fmt.Num(v, spec);
        var today = Days.Count > 0 ? Days[0] : null;
        return name switch
        {
            "temp" => N(temp), "feels" => N(feels), "wind" => N(wind), "humidity" => N(humidity),
            "weather" => Describe(code), "weather_icon" => Icon(code), "city" => city,
            "high" => today != null ? N(today.High) : "", "low" => today != null ? N(today.Low) : "",
            _ => null,
        };
    }
}
