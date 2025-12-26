# FireGuard - Wildfire Risk Prediction System

A real-time wildfire risk assessment platform that uses machine learning to predict fire probability across Canada. Built with C#/.NET, ML.NET, and React, the system combines satellite fire detection data, live weather conditions, and advanced environmental features to provide accurate risk predictions.

![FireGuard Demo](./demo/FireGuard.png)

## What It Does

FireGuard analyzes current conditions and historical patterns to predict wildfire risk at any location in Canada. The system integrates data from NASA's FIRMS satellite system for active fire detections and Open-Meteo for current weather conditions. Users can explore an interactive map showing risk levels across the country or click any location to get detailed analysis with probability scores and contributing factors.

The machine learning model considers eleven different features including temperature, wind speed, humidity, precipitation, seasonal timing, vegetation density, drought conditions, elevation, and historical fire patterns. This approach allows the model to understand that a hot day in July carries very different risk than the same temperature in January, and that certain regions are inherently more fire-prone than others.

## Technical Details

**Backend:**
- ASP.NET Core Web API with RESTful endpoints
- ML.NET using LightGBM algorithm for binary classification
- Trained on 726,000+ real fire detections from NASA FIRMS 2024 Canada wildfires
- Achieves 83% accuracy
- Real-time data integration from NASA FIRMS and Open-Meteo APIs

**Frontend:**
- React with Leaflet for interactive mapping
- Color-coded risk visualization
- Real-time weather and fire detection overlays

**Machine Learning**
- ML model uses NDVI (vegetation index), drought index, elevation, and historical fire density
- Added test endpoint for validating predictions with custom parameters
- Grid-based predictions for mapping risk across regions

## How to Run

Start the backend API server:
```bash
cd Backend
dotnet restore
dotnet run
```

In a separate terminal, start the frontend:
```bash
cd Frontend
npm install
npm run dev
```

Open your browser to `http://localhost:5173` to use the application. The backend will automatically train the ML model on first startup using the provided NASA FIRMS data.

## Testing the Model

The system includes a test endpoint to validate predictions with custom weather conditions. This is useful for testing accuracy since current winter conditions naturally show low risk. You can simulate summer fire scenarios:

```bash
# High risk summer scenario
curl "http://localhost:5014/api/risk/test?lat=56.7267&lon=-111.3790&temperature=35&windSpeed=30&humidity=15&precipitation=0&dayOfYear=200"

# Low risk winter scenario  
curl "http://localhost:5014/api/risk/test?lat=56.7267&lon=-111.3790&temperature=-20&windSpeed=10&humidity=70&precipitation=5&dayOfYear=15"
```

## Model Accuracy

The model shows strong performance across multiple metrics. It correctly identifies about 83% of test cases, with an AUC of 0.84-0.87 indicating excellent discrimination between fire and no-fire conditions. This shows it achieves good recall to catch most actual fire risks.

One key insight is that the model properly accounts for seasonality. Winter predictions are appropriately low regardless of other conditions, while summer predictions in fire-prone areas with extreme weather correctly show high risk. This demonstrates that the model has learned complex interactions between features rather than relying on simple thresholds.

Despite these facts, the model is not perfect and should not be relied upon for any kind of emergency use.

## Future Improvements

The current implementation provides a solid foundation with room for improvement. Incorporating real historical weather data instead of synthetic features would increase accuracy. Adding satellite imagery analysis, wind direction for fire spread prediction, and real-time drought monitoring from government sources would provide more comprehensive risk assessment.

For production deployment, implementing result caching, rate limiting, and a database for storing predictions would improve performance and reliability. The system could also be extended to support multi-day forecasting and integration with emergency response systems.

## Credits/Sources

- NASA FIRMS: Active fire detections from VIIRS satellite
- Open-Meteo: Real-time weather data (temperature, wind, humidity, precipitation)
- Training data: Canada 2024 wildfire season CSV file
