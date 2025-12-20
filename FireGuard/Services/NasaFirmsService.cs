using System.Globalization;
using System.Text;

namespace FireGuard.Services;

public class NasaFirmsService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<NasaFirmsService> _logger;

    public NasaFirmsService(HttpClient http, IConfiguration config, ILogger<NasaFirmsService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<List<FireDetection>> GetActiveFiresAsync()
    {
        var fires = new List<FireDetection>();

        try
        {
            var apiKey = _config["NASA_FIRMS_API_KEY"];

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("NASA FIRMS API key not configured");
                return fires;
            }

            // Get fires for Canada in the last 1 day
            // Using CSV endpoint as it's more reliable than JSON
            var url = $"https://firms.modaps.eosdis.nasa.gov/api/area/csv/{apiKey}/VIIRS_SNPP_NRT/world/1";

            _logger.LogInformation("Fetching active fires from NASA FIRMS...");
            var response = await _http.GetStringAsync(url);

            // Parse CSV response
            var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            if (lines.Length < 2)
            {
                _logger.LogWarning("No fire data returned from NASA FIRMS");
                return fires;
            }

            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');

                if (parts.Length < 13)
                    continue;

                try
                {
                    var latitude = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    var longitude = float.Parse(parts[1], CultureInfo.InvariantCulture);

                    // Filter for Canada only
                    if (latitude < 41.0f || latitude > 83.0f || longitude < -141.0f || longitude > -52.0f)
                        continue;

                    var fire = new FireDetection
                    {
                        Latitude = latitude,
                        Longitude = longitude,
                        BrightTi4 = float.TryParse(parts[2], CultureInfo.InvariantCulture, out var bt4) ? bt4 : 0,
                        Scan = float.TryParse(parts[3], CultureInfo.InvariantCulture, out var scan) ? scan : 0,
                        Track = float.TryParse(parts[4], CultureInfo.InvariantCulture, out var track) ? track : 0,
                        AcqDate = parts.Length > 5 ? parts[5] : "",
                        AcqTime = parts.Length > 6 ? parts[6] : "",
                        Satellite = parts.Length > 7 ? parts[7] : "",
                        Confidence = parts.Length > 9 ? parts[9] : "",
                        Version = parts.Length > 10 ? parts[10] : "",
                        BrightTi5 = parts.Length > 11 && float.TryParse(parts[11], CultureInfo.InvariantCulture, out var bt5) ? bt5 : 0,
                        Frp = parts.Length > 12 && float.TryParse(parts[12], CultureInfo.InvariantCulture, out var frp) ? frp : 0
                    };

                    fires.Add(fire);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to parse fire detection line: {Line}", lines[i]);
                    continue;
                }
            }

            _logger.LogInformation("Retrieved {Count} active fires in Canada", fires.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching active fires from NASA FIRMS");
        }

        return fires;
    }
}

public class FireDetection
{
    public float Latitude { get; set; }
    public float Longitude { get; set; }
    public float BrightTi4 { get; set; }
    public float Scan { get; set; }
    public float Track { get; set; }
    public string AcqDate { get; set; } = "";
    public string AcqTime { get; set; } = "";
    public string Satellite { get; set; } = "";
    public string Confidence { get; set; } = "";
    public string Version { get; set; } = "";
    public float BrightTi5 { get; set; }
    public float Frp { get; set; }
}