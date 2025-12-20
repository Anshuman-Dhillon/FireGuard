namespace FireGuard.Models;

public class RiskResponse
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string RiskLevel { get; set; } = "Low";
    public float Probability { get; set; }
    public object? Weather { get; set; }
    public object? EnhancedFeatures { get; set; }
}

public class GridPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public float Probability { get; set; }
    public string RiskLevel { get; set; } = "Low";
}