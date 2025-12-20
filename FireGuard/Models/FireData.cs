namespace FireGuard.Models;

public class FireData
{
    // Basic weather features
    public float Temperature { get; set; }
    public float WindSpeed { get; set; }
    public float Humidity { get; set; }
    public float Precipitation { get; set; }

    // Location features
    public float Latitude { get; set; }
    public float Longitude { get; set; }

    // Enhanced features for better accuracy
    public float DayOfYear { get; set; }           // Seasonal patterns (1-365)
    public float NDVI { get; set; }               // Vegetation index (0-1)
    public float DroughtIndex { get; set; }       // Drought severity (0-1)
    public float Elevation { get; set; }          // Meters above sea level
    public float HistoricalFireDensity { get; set; } // Historical fire frequency (0-1)

    // Label (used only during training)
    public bool Label { get; set; }
}