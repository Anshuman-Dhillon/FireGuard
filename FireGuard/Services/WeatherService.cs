using System.Text.Json;

namespace FireGuard.Services;

public class WeatherService
{
    private readonly HttpClient _http;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient http, ILogger<WeatherService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<object> GetWeatherAsync(double lat, double lon)
    {
        try
        {
            var url =
                $"https://api.open-meteo.com/v1/forecast" +
                $"?latitude={lat}&longitude={lon}" +
                $"&current=temperature_2m,relative_humidity_2m,wind_speed_10m,precipitation";

            _logger.LogDebug("Fetching weather from: {Url}", url);

            var response = await _http.GetStringAsync(url);

            _logger.LogDebug("Weather response: {Response}", response.Substring(0, Math.Min(200, response.Length)));

            var weatherData = JsonSerializer.Deserialize<object>(response);

            if (weatherData == null)
            {
                _logger.LogWarning("Weather API returned null for {Lat}, {Lon}", lat, lon);
                throw new Exception("Weather API returned null");
            }

            return weatherData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather for {Lat}, {Lon}", lat, lon);
            throw;
        }
    }
}