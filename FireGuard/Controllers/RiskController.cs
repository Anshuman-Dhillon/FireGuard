using FireGuard.ML;
using FireGuard.Models;
using FireGuard.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FireGuard.Controllers;

[ApiController]
[Route("api/risk")]
public class RiskController : ControllerBase
{
    private readonly FireRiskModel _model;
    private readonly WeatherService _weather;
    private readonly NasaFirmsService _nasa;
    private readonly ILogger<RiskController> _logger;

    public RiskController(
        FireRiskModel model,
        WeatherService weather,
        NasaFirmsService nasa,
        ILogger<RiskController> logger)
    {
        _model = model;
        _weather = weather;
        _nasa = nasa;
        _logger = logger;
    }

    /// <summary>
    /// Get fire risk prediction for a specific location
    /// </summary>
    [HttpGet("location")]
    public async Task<IActionResult> GetRiskForLocation(
        [FromQuery] double lat,
        [FromQuery] double lon)
    {
        try
        {
            // Validate coordinates are in Canada
            if (lat < 41.0 || lat > 83.0 || lon < -141.0 || lon > -52.0)
            {
                return BadRequest("Coordinates must be within Canada");
            }

            // Get current weather
            var weather = await _weather.GetWeatherAsync(lat, lon);

            // Extract weather features - handle different response formats
            var weatherData = ParseWeatherResponse(weather);

            if (weatherData?.Current == null)
            {
                _logger.LogError("Weather data parsed but Current is null. Input type: {Type}", weather?.GetType().Name);
                return StatusCode(500, "Failed to fetch weather data");
            }

            // Calculate enhanced features
            var dayOfYear = DateTime.Now.DayOfYear;
            var ndvi = CalculateNDVI((float)lat, (float)lon, dayOfYear);
            var droughtIndex = CalculateDroughtIndex(dayOfYear, weatherData.Current.Precipitation, weatherData.Current.Temperature2m);
            var elevation = CalculateElevation((float)lat, (float)lon);
            var historicalDensity = _model.GetHistoricalDensity((float)lat, (float)lon);

            var input = new FireData
            {
                Latitude = (float)lat,
                Longitude = (float)lon,
                Temperature = weatherData.Current.Temperature2m,
                WindSpeed = weatherData.Current.WindSpeed10m,
                Humidity = weatherData.Current.RelativeHumidity2m,
                Precipitation = weatherData.Current.Precipitation,
                DayOfYear = (float)dayOfYear,
                NDVI = ndvi,
                DroughtIndex = droughtIndex,
                Elevation = elevation,
                HistoricalFireDensity = historicalDensity
            };

            var prediction = _model.Predict(input);

            var response = new RiskResponse
            {
                Latitude = lat,
                Longitude = lon,
                RiskLevel = prediction.Probability switch
                {
                    > 0.7f => "High",
                    > 0.4f => "Medium",
                    _ => "Low"
                },
                Probability = prediction.Probability,
                Weather = weatherData.Current,
                EnhancedFeatures = new EnhancedFeatures
                {
                    DayOfYear = dayOfYear,
                    NDVI = ndvi,
                    DroughtIndex = droughtIndex,
                    Elevation = elevation,
                    HistoricalFireDensity = historicalDensity,
                    Season = GetSeason(dayOfYear)
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting risk for location");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get fire risk predictions across a grid covering Canada
    /// </summary>
    [HttpGet("grid")]
    public async Task<IActionResult> GetRiskGrid(
        [FromQuery] double latMin = 42.0,
        [FromQuery] double latMax = 70.0,
        [FromQuery] double lonMin = -140.0,
        [FromQuery] double lonMax = -53.0,
        [FromQuery] int gridSize = 20)
    {
        try
        {
            if (gridSize < 5 || gridSize > 50)
            {
                return BadRequest("Grid size must be between 5 and 50");
            }

            // Fixed warnings by removing .Value since parameters are no longer nullable
            var latStep = (latMax - latMin) / gridSize;
            var lonStep = (lonMax - lonMin) / gridSize;

            var gridPoints = new List<GridPoint>();
            var dayOfYear = DateTime.Now.DayOfYear;

            // Generate grid points
            for (int i = 0; i <= gridSize; i++)
            {
                for (int j = 0; j <= gridSize; j++)
                {
                    var lat = latMin + (i * latStep);
                    var lon = lonMin + (j * lonStep);

                    try
                    {
                        // Get weather for this grid point
                        var weather = await _weather.GetWeatherAsync(lat, lon);
                        var weatherData = ParseWeatherResponse(weather);

                        if (weatherData?.Current != null)
                        {
                            // Calculate enhanced features
                            var ndvi = CalculateNDVI((float)lat, (float)lon, dayOfYear);
                            var droughtIndex = CalculateDroughtIndex(dayOfYear, weatherData.Current.Precipitation, weatherData.Current.Temperature2m);
                            var elevation = CalculateElevation((float)lat, (float)lon);
                            var historicalDensity = _model.GetHistoricalDensity((float)lat, (float)lon);

                            var input = new FireData
                            {
                                Latitude = (float)lat,
                                Longitude = (float)lon,
                                Temperature = weatherData.Current.Temperature2m,
                                WindSpeed = weatherData.Current.WindSpeed10m,
                                Humidity = weatherData.Current.RelativeHumidity2m,
                                Precipitation = weatherData.Current.Precipitation,
                                DayOfYear = (float)dayOfYear,
                                NDVI = ndvi,
                                DroughtIndex = droughtIndex,
                                Elevation = elevation,
                                HistoricalFireDensity = historicalDensity
                            };

                            var prediction = _model.Predict(input);

                            gridPoints.Add(new GridPoint
                            {
                                Latitude = lat,
                                Longitude = lon,
                                Probability = prediction.Probability,
                                RiskLevel = prediction.Probability switch
                                {
                                    > 0.7f => "High",
                                    > 0.4f => "Medium",
                                    _ => "Low"
                                }
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to get weather for {lat}, {lon}");
                    }

                    // Small delay to avoid rate limiting
                    await Task.Delay(50);
                }
            }

            return Ok(new
            {
                gridSize,
                pointCount = gridPoints.Count,
                season = GetSeason(dayOfYear),
                dayOfYear,
                points = gridPoints
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating risk grid");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get active fires from NASA FIRMS
    /// </summary>
    [HttpGet("active-fires")]
    public async Task<IActionResult> GetActiveFires()
    {
        try
        {
            var fires = await _nasa.GetActiveFiresAsync();
            return Ok(fires);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching active fires");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Test endpoint with custom weather parameters (for testing model accuracy)
    /// </summary>
    [HttpGet("test")]
    public IActionResult TestWithCustomWeather(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] float temperature,
        [FromQuery] float windSpeed,
        [FromQuery] float humidity,
        [FromQuery] float precipitation,
        [FromQuery] int? dayOfYear = null)
    {
        try
        {
            // Validate coordinates are in Canada
            if (lat < 41.0 || lat > 83.0 || lon < -141.0 || lon > -52.0)
            {
                return BadRequest("Coordinates must be within Canada");
            }

            // Use provided dayOfYear or current
            var testDayOfYear = dayOfYear ?? DateTime.Now.DayOfYear;

            // Calculate enhanced features
            var ndvi = CalculateNDVI((float)lat, (float)lon, testDayOfYear);
            var droughtIndex = CalculateDroughtIndex(testDayOfYear, precipitation, temperature);
            var elevation = CalculateElevation((float)lat, (float)lon);
            var historicalDensity = _model.GetHistoricalDensity((float)lat, (float)lon);

            var input = new FireData
            {
                Latitude = (float)lat,
                Longitude = (float)lon,
                Temperature = temperature,
                WindSpeed = windSpeed,
                Humidity = humidity,
                Precipitation = precipitation,
                DayOfYear = (float)testDayOfYear,
                NDVI = ndvi,
                DroughtIndex = droughtIndex,
                Elevation = elevation,
                HistoricalFireDensity = historicalDensity
            };

            var prediction = _model.Predict(input);

            var response = new
            {
                latitude = lat,
                longitude = lon,
                riskLevel = prediction.Probability switch
                {
                    > 0.7f => "High",
                    > 0.4f => "Medium",
                    _ => "Low"
                },
                probability = prediction.Probability,
                inputWeather = new
                {
                    temperature,
                    windSpeed,
                    humidity,
                    precipitation
                },
                enhancedFeatures = new
                {
                    dayOfYear = testDayOfYear,
                    season = GetSeason(testDayOfYear),
                    ndvi,
                    droughtIndex,
                    elevation,
                    historicalFireDensity = historicalDensity
                },
                interpretation = prediction.Probability switch
                {
                    > 0.7f => "HIGH RISK: Dangerous fire conditions. Immediate attention needed.",
                    > 0.4f => "MEDIUM RISK: Elevated fire danger. Monitor closely.",
                    _ => "LOW RISK: Conditions not favorable for fires."
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in test endpoint");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            modelLoaded = true,
            season = GetSeason(DateTime.Now.DayOfYear)
        });
    }

    // Helper methods for enhanced features
    private float CalculateNDVI(float lat, float lon, int dayOfYear)
    {
        // Normalized Difference Vegetation Index
        float seasonalFactor = (float)(0.6 + 0.2 * Math.Sin((dayOfYear - 100) * Math.PI / 180));
        float latitudeFactor = 1.0f - (lat - 41.0f) / 50.0f * 0.3f;
        float coastalFactor = lon < -120 || lon > -70 ? 1.1f : 1.0f;

        return Math.Clamp(seasonalFactor * latitudeFactor * coastalFactor, 0.1f, 0.9f);
    }

    private float CalculateDroughtIndex(int dayOfYear, float precipitation, float temp)
    {
        float seasonalDrought = dayOfYear is > 120 and < 270 ? 0.7f : 0.3f;
        float precipEffect = precipitation < 2 ? 0.8f : precipitation < 5 ? 0.5f : 0.2f;
        float tempEffect = temp > 25 ? 0.8f : temp > 15 ? 0.5f : 0.2f;

        return Math.Clamp((seasonalDrought + precipEffect + tempEffect) / 3.0f, 0.0f, 1.0f);
    }

    private float CalculateElevation(float lat, float lon)
    {
        // Approximate elevation based on location
        if (lon is > -120 and < -110 && lat is > 49 and < 55)
            return 1500; // Rocky Mountains
        else if (lon is > -110 and < -95 && lat is > 49 and < 55)
            return 600; // Prairies
        else if (lon is > -95 and < -75 && lat is > 45 and < 55)
            return 300; // Canadian Shield
        else if (lon < -120)
            return 800; // BC mountains
        else
            return 200; // Default
    }

    private string GetSeason(int dayOfYear)
    {
        return dayOfYear switch
        {
            >= 80 and < 172 => "Spring",
            >= 172 and < 266 => "Summer",
            >= 266 and < 355 => "Fall",
            _ => "Winter"
        };
    }

    private WeatherResponse? ParseWeatherResponse(object weather)
    {
        try
        {
            // Serialize and deserialize to get proper type
            var json = JsonSerializer.Serialize(weather);
            _logger.LogDebug("Weather JSON: {Json}", json);

            // Fix: Add Case Insensitive option
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            return JsonSerializer.Deserialize<WeatherResponse>(json, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse weather response");
            return null;
        }
    }
}

// Helper classes for weather deserialization
public class WeatherResponse
{
    public CurrentWeather? Current { get; set; }
}

public class CurrentWeather
{
    [System.Text.Json.Serialization.JsonPropertyName("temperature_2m")]
    public float Temperature2m { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("relative_humidity_2m")]
    public float RelativeHumidity2m { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("wind_speed_10m")]
    public float WindSpeed10m { get; set; }

    [System.Text.Json.Serialization.JsonPropertyName("precipitation")]
    public float Precipitation { get; set; }
}

public class EnhancedFeatures
{
    public int DayOfYear { get; set; }
    public float NDVI { get; set; }
    public float DroughtIndex { get; set; }
    public float Elevation { get; set; }
    public float HistoricalFireDensity { get; set; }
    public string Season { get; set; } = "";
}