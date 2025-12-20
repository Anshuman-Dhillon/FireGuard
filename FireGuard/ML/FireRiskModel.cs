using FireGuard.Models;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Globalization;
using System.Text;

namespace FireGuard.ML;

public class FireRiskModel
{
    private readonly MLContext _mlContext;
    private ITransformer _model = null!;
    private PredictionEngine<FireData, FirePrediction> _engine = null!;
    private const string CsvPath = "Data/firms.csv";
    private const string ModelPath = "Data/fire_risk_model.zip";

    // Store fire density map for location-based risk
    private Dictionary<(int, int), float> _fireHotspots = new();

    public FireRiskModel()
    {
        _mlContext = new MLContext(seed: 42);

        if (File.Exists(ModelPath))
        {
            LoadModel();
        }
        else
        {
            TrainModel();
            SaveModel();
        }
    }

    private void TrainModel()
    {
        Console.WriteLine("Training enhanced fire risk model...");

        var trainingData = LoadFireDataFromCsv();

        if (trainingData.Count == 0)
        {
            Console.WriteLine("WARNING: No training data loaded, using dummy data");
            trainingData = GetDummyTrainingData();
        }
        else
        {
            Console.WriteLine($"Loaded {trainingData.Count} training samples");
            var fireCount = trainingData.Count(d => d.Label);
            Console.WriteLine($"  - Fire samples: {fireCount}");
            Console.WriteLine($"  - No-fire samples: {trainingData.Count - fireCount}");
        }

        var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

        // Split for evaluation
        var split = _mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2);

        // Enhanced pipeline with more sophisticated features
        var pipeline = _mlContext.Transforms
            .Concatenate("Features",
                nameof(FireData.Latitude),
                nameof(FireData.Longitude),
                nameof(FireData.Temperature),
                nameof(FireData.WindSpeed),
                nameof(FireData.Humidity),
                nameof(FireData.Precipitation),
                nameof(FireData.DayOfYear),
                nameof(FireData.NDVI),
                nameof(FireData.DroughtIndex),
                nameof(FireData.Elevation),
                nameof(FireData.HistoricalFireDensity))
            .Append(_mlContext.Transforms.NormalizeMinMax("Features"))
            .Append(_mlContext.BinaryClassification.Trainers.LightGbm(
                numberOfLeaves: 31,
                numberOfIterations: 200,
                minimumExampleCountPerLeaf: 20,
                learningRate: 0.15));

        Console.WriteLine("Training model with enhanced features...");
        _model = pipeline.Fit(split.TrainSet);
        _engine = _mlContext.Model.CreatePredictionEngine<FireData, FirePrediction>(_model);

        // Evaluate
        var predictions = _model.Transform(split.TestSet);
        var metrics = _mlContext.BinaryClassification.Evaluate(predictions);

        Console.WriteLine("\n=== Enhanced Model Evaluation ===");
        Console.WriteLine($"Accuracy: {metrics.Accuracy:P2}");
        Console.WriteLine($"AUC: {metrics.AreaUnderRocCurve:F3}");
        Console.WriteLine($"F1 Score: {metrics.F1Score:F3}");
        Console.WriteLine($"Precision: {metrics.PositivePrecision:F3}");
        Console.WriteLine($"Recall: {metrics.PositiveRecall:F3}");
        Console.WriteLine($"Log-Loss: {metrics.LogLoss:F3}");
        Console.WriteLine("=================================\n");
    }

    private List<FireData> LoadFireDataFromCsv()
    {
        var fireData = new List<FireData>();
        var noFireData = new List<FireData>();
        var random = new Random(42);

        try
        {
            if (!File.Exists(CsvPath))
            {
                Console.WriteLine($"CSV file not found: {CsvPath}");
                return new List<FireData>();
            }

            // First pass: build fire hotspot map
            var fireLocations = new List<(float lat, float lon, DateTime date)>();

            using (var reader = new StreamReader(CsvPath, Encoding.UTF8))
            {
                string? line;
                int lineNum = 0;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNum++;
                    if (lineNum == 1) continue;

                    var parts = line.Split(',');
                    if (parts.Length < 13) continue;

                    if (!float.TryParse(parts[0], NumberStyles.Any, CultureInfo.InvariantCulture, out var latitude) ||
                        !float.TryParse(parts[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var longitude) ||
                        !float.TryParse(parts[12], NumberStyles.Any, CultureInfo.InvariantCulture, out var frp))
                    {
                        continue;
                    }

                    if (latitude < 41.0f || latitude > 83.0f || longitude < -141.0f || longitude > -52.0f)
                        continue;

                    // Parse date
                    DateTime date = DateTime.Now;
                    if (parts.Length > 5 && DateTime.TryParseExact(parts[5], "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                    {
                        date = parsedDate;
                    }

                    fireLocations.Add((latitude, longitude, date));

                    // Update hotspot map (grid cells of 0.5 degrees)
                    var gridLat = (int)(latitude * 2);
                    var gridLon = (int)(longitude * 2);
                    var key = (gridLat, gridLon);
                    _fireHotspots[key] = _fireHotspots.GetValueOrDefault(key, 0) + 1;
                }
            }

            Console.WriteLine($"Found {fireLocations.Count} fire detections");
            Console.WriteLine($"Identified {_fireHotspots.Count} fire hotspot zones");

            // Normalize fire density
            var maxDensity = _fireHotspots.Values.Max();
            foreach (var key in _fireHotspots.Keys.ToList())
            {
                _fireHotspots[key] = _fireHotspots[key] / maxDensity;
            }

            // Second pass: create training data with enhanced features
            foreach (var (lat, lon, date) in fireLocations)
            {
                var dayOfYear = date.DayOfYear;

                // Enhanced weather features based on FRP and season
                var baseTemp = 15 + (dayOfYear - 180) / 15.0f; // Warmer in summer
                var temp = Math.Clamp(baseTemp + random.Next(-10, 20), -20, 45);

                var windSpeed = Math.Clamp(5 + random.Next(0, 25), 0, 50);
                var humidity = Math.Clamp(60 - (temp - 15) * 2 + random.Next(-20, 10), 10, 95);
                var precipitation = dayOfYear is > 150 and < 250 ? random.Next(0, 3) : random.Next(0, 10);

                // Vegetation index (NDVI): higher in summer, lower in winter
                var ndvi = CalculateNDVI(lat, lon, dayOfYear);

                // Drought index: higher in fire season
                var droughtIndex = CalculateDroughtIndex(dayOfYear, precipitation, temp);

                // Elevation approximation
                var elevation = CalculateElevation(lat, lon);

                // Historical fire density
                var historicalDensity = GetFireDensity(lat, lon);

                fireData.Add(new FireData
                {
                    Latitude = lat,
                    Longitude = lon,
                    Temperature = temp,
                    WindSpeed = windSpeed,
                    Humidity = humidity,
                    Precipitation = precipitation,
                    DayOfYear = dayOfYear,
                    NDVI = ndvi,
                    DroughtIndex = droughtIndex,
                    Elevation = elevation,
                    HistoricalFireDensity = historicalDensity,
                    Label = true
                });
            }

            Console.WriteLine($"Created {fireData.Count} fire training samples with enhanced features");

            // Generate negative examples with realistic seasonal distribution
            int negativeCount = fireData.Count;

            for (int i = 0; i < negativeCount; i++)
            {
                // Random location in Canada
                var lat = (float)(41.0 + random.NextDouble() * 42.0);
                var lon = (float)(-141.0 + random.NextDouble() * 89.0);

                // Random time of year
                var dayOfYear = random.Next(1, 366);

                // Cooler, wetter conditions (less fire-prone)
                var baseTemp = 5 + (dayOfYear - 180) / 20.0f;
                var temp = Math.Clamp(baseTemp + random.Next(-15, 10), -30, 35);

                var windSpeed = (float)(random.NextDouble() * 20);
                var humidity = Math.Clamp(60 + random.Next(0, 30), 40, 100);
                var precipitation = random.Next(0, 15);

                var ndvi = CalculateNDVI(lat, lon, dayOfYear);
                var droughtIndex = CalculateDroughtIndex(dayOfYear, precipitation, temp);
                var elevation = CalculateElevation(lat, lon);
                var historicalDensity = GetFireDensity(lat, lon) * 0.5f; // Lower density areas

                noFireData.Add(new FireData
                {
                    Latitude = lat,
                    Longitude = lon,
                    Temperature = temp,
                    WindSpeed = windSpeed,
                    Humidity = humidity,
                    Precipitation = precipitation,
                    DayOfYear = dayOfYear,
                    NDVI = ndvi,
                    DroughtIndex = droughtIndex,
                    Elevation = elevation,
                    HistoricalFireDensity = historicalDensity,
                    Label = false
                });
            }

            Console.WriteLine($"Generated {noFireData.Count} no-fire samples");

            // Combine and shuffle
            var allData = fireData.Concat(noFireData).OrderBy(x => random.Next()).ToList();
            return allData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading CSV: {ex.Message}");
            return new List<FireData>();
        }
    }

    private float CalculateNDVI(float lat, float lon, int dayOfYear)
    {
        // Normalized Difference Vegetation Index
        // Higher values (0.6-0.8) = dense vegetation (fuel)
        // Lower values (0.2-0.4) = sparse vegetation

        // Seasonal variation: peak in summer
        float seasonalFactor = (float)(0.6 + 0.2 * Math.Sin((dayOfYear - 100) * Math.PI / 180));

        // Latitude effect: northern areas less dense
        float latitudeFactor = 1.0f - (lat - 41.0f) / 50.0f * 0.3f;

        // Coastal areas tend to be more vegetated
        float coastalFactor = lon < -120 || lon > -70 ? 1.1f : 1.0f;

        return Math.Clamp(seasonalFactor * latitudeFactor * coastalFactor, 0.1f, 0.9f);
    }

    private float CalculateDroughtIndex(int dayOfYear, float precipitation, float temp)
    {
        // Simplified drought index
        // Higher values = more drought = higher fire risk

        // Summer months (May-Sept) have higher drought potential
        float seasonalDrought = dayOfYear is > 120 and < 270 ? 0.7f : 0.3f;

        // Low precipitation increases drought
        float precipEffect = precipitation < 2 ? 0.8f : precipitation < 5 ? 0.5f : 0.2f;

        // High temperature increases drought
        float tempEffect = temp > 25 ? 0.8f : temp > 15 ? 0.5f : 0.2f;

        return Math.Clamp((seasonalDrought + precipEffect + tempEffect) / 3.0f, 0.0f, 1.0f);
    }

    private float CalculateElevation(float lat, float lon)
    {
        // Approximate elevation based on location
        // Rocky Mountains: high elevation
        // Prairies: low elevation
        // Canadian Shield: medium elevation

        if (lon is > -120 and < -110 && lat is > 49 and < 55)
            return 1500; // Rocky Mountains
        else if (lon is > -110 and < -95 && lat is > 49 and < 55)
            return 600; // Prairies
        else if (lon is > -95 and < -75 && lat is > 45 and < 55)
            return 300; // Canadian Shield (lower)
        else if (lon < -120)
            return 800; // BC mountains
        else
            return 200; // Default low elevation
    }

    private float GetFireDensity(float lat, float lon)
    {
        var gridLat = (int)(lat * 2);
        var gridLon = (int)(lon * 2);
        return _fireHotspots.GetValueOrDefault((gridLat, gridLon), 0.0f);
    }

    private List<FireData> GetDummyTrainingData()
    {
        return new List<FireData>
        {
            // High risk summer scenarios
            new() { Latitude = 50, Longitude = -100, Temperature = 35, WindSpeed = 25, Humidity = 15,
                    Precipitation = 0, DayOfYear = 200, NDVI = 0.7f, DroughtIndex = 0.8f,
                    Elevation = 500, HistoricalFireDensity = 0.8f, Label = true },
            new() { Latitude = 52, Longitude = -110, Temperature = 32, WindSpeed = 20, Humidity = 20,
                    Precipitation = 0, DayOfYear = 210, NDVI = 0.65f, DroughtIndex = 0.75f,
                    Elevation = 600, HistoricalFireDensity = 0.7f, Label = true },
            
            // Low risk winter scenarios
            new() { Latitude = 50, Longitude = -100, Temperature = -15, WindSpeed = 10, Humidity = 70,
                    Precipitation = 5, DayOfYear = 30, NDVI = 0.3f, DroughtIndex = 0.1f,
                    Elevation = 500, HistoricalFireDensity = 0.8f, Label = false },
            new() { Latitude = 52, Longitude = -110, Temperature = -20, WindSpeed = 5, Humidity = 80,
                    Precipitation = 8, DayOfYear = 350, NDVI = 0.2f, DroughtIndex = 0.05f,
                    Elevation = 600, HistoricalFireDensity = 0.7f, Label = false }
        };
    }

    private void SaveModel()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ModelPath)!);
            _mlContext.Model.Save(_model, null, ModelPath);
            Console.WriteLine($"Enhanced model saved to {ModelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving model: {ex.Message}");
        }
    }

    private void LoadModel()
    {
        try
        {
            _model = _mlContext.Model.Load(ModelPath, out _);
            _engine = _mlContext.Model.CreatePredictionEngine<FireData, FirePrediction>(_model);
            Console.WriteLine($"Model loaded from {ModelPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading model: {ex.Message}, training new model...");
            TrainModel();
            SaveModel();
        }
    }

    public FirePrediction Predict(FireData input)
    {
        return _engine.Predict(input);
    }

    public float GetHistoricalDensity(float lat, float lon)
    {
        return GetFireDensity(lat, lon);
    }
}