import React, { useState, useEffect } from 'react';
import { MapContainer, TileLayer, Marker, Popup, Circle, useMap } from 'react-leaflet';
import { AlertCircle, Flame, Cloud, Wind, Droplets, RefreshCw, Loader2, MapPin } from 'lucide-react';
import 'leaflet/dist/leaflet.css';
import L from 'leaflet';

// Fix Leaflet default marker icon
delete L.Icon.Default.prototype._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon-2x.png',
  iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon.png',
  shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
});

// Auto-detect backend URL (works with any port)
const API_BASE = window.location.hostname === 'localhost' 
  ? 'http://localhost:5014/api'
  : 'https://fireguard-awckf3gff8c0ehbe.canadacentral-01.azurewebsites.net/api';

const FireGuardApp = () => {
  const [gridData, setGridData] = useState([]);
  const [activeFires, setActiveFires] = useState([]);
  const [locationRisk, setLocationRisk] = useState(null);
  const [loading, setLoading] = useState(false);
  const [selectedPoint, setSelectedPoint] = useState(null);
  const [error, setError] = useState(null);

  const canadaCenter = [56.1304, -106.3468];

  useEffect(() => {
    loadGridData();
    loadActiveFires();
  }, []);

  const loadGridData = async () => {
    setLoading(true);
    setError(null);
    try {
      const response = await fetch(`${API_BASE}/risk/grid?gridSize=15`);
      if (!response.ok) throw new Error('Failed to fetch grid data');
      const data = await response.json();
      setGridData(data.points || []);
    } catch (err) {
      setError('Failed to load risk grid: ' + err.message);
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const loadActiveFires = async () => {
    try {
      const response = await fetch(`${API_BASE}/risk/active-fires`);
      if (!response.ok) throw new Error('Failed to fetch active fires');
      const data = await response.json();
      if (Array.isArray(data)) {
        setActiveFires(data.slice(0, 100));
      }
    } catch (err) {
      console.error('Failed to load active fires:', err);
    }
  };

  const checkLocationRisk = async (lat, lon) => {
    try {
      const response = await fetch(`${API_BASE}/risk/location?lat=${lat}&lon=${lon}`);
      if (!response.ok) {
        const errorText = await response.text();
        console.error('Backend error:', errorText);
        throw new Error(`Failed to fetch location risk (${response.status})`);
      }
      const data = await response.json();
      setLocationRisk(data);
      setSelectedPoint({ lat, lon });
    } catch (err) {
      console.error('Failed to check location risk:', err);
      setError(`Failed to get risk for this location: ${err.message}`);
    }
  };

  const getRiskColor = (probability) => {
    if (probability > 0.7) return '#dc2626';
    if (probability > 0.4) return '#ea580c';
    return '#16a34a';
  };

  const getRiskOpacity = (probability) => {
    return Math.min(0.3 + (probability * 0.5), 0.8);
  };

  const MapClickHandler = () => {
    const map = useMap();
    
    useEffect(() => {
      const handleClick = (e) => {
        checkLocationRisk(e.latlng.lat, e.latlng.lng);
      };
      
      map.on('click', handleClick);
      return () => {
        map.off('click', handleClick);
      };
    }, [map]);
    
    return null;
  };

  return (
    <div className="min-h-screen relative">
      {/* Background with overlay */}
      <div 
        className="fixed inset-0 z-0"
        style={{
          backgroundImage: 'url(/wildfire.jpg)',
          backgroundSize: 'cover',
          backgroundPosition: 'center',
          backgroundAttachment: 'fixed'
        }}
      >
        <div className="absolute inset-0 bg-gradient-to-b from-white/95 via-white/85 to-white/75"></div>
      </div>

      {/* Content */}
      <div className="relative z-10">
        {/* Header */}
        <div className="bg-red-25/90 backdrop-blur-md border-b border-red-0 shadow-sm">
          <div className="max-w-7xl mx-auto px-8 py-7">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-5">
                <div className="w-14 h-14 bg-gradient-to-br from-red-600 to-orange-600 flex items-center justify-center">
                  <Flame className="w-8 h-8 text-white" />
                </div>
                <div>
                  <div className="flex items-center gap-3">
                    <h1 className="text-3xl font-bold text-gray-900 tracking-tight">
                      FireGuard
                    </h1>
                    <span className="px-2 py-0.5 bg-orange-100 text-orange-700 text-xs font-semibold uppercase tracking-wider border border-orange-300">
                      Beta
                    </span>
                  </div>
                  <p className="text-sm text-gray-700 mt-1 font-medium">Real-time Wildfire Risk Assessment</p>
                  <p className="text-xs text-gray-600 mt-1 italic">Experimental tool for predicting wilfires based on local weather conditions and other geological data</p>
                </div>
              </div>
              
              <button
                onClick={() => { loadGridData(); loadActiveFires(); }}
                disabled={loading}
                className="flex items-center gap-2.5 px-6 py-3 bg-gray-900 hover:bg-gray-800 text-white transition-all disabled:opacity-50 disabled:cursor-not-allowed text-sm font-semibold shadow-md hover:shadow-lg transform hover:scale-105"
              >
                {loading ? (
                  <Loader2 className="w-4 h-4 animate-spin" />
                ) : (
                  <RefreshCw className="w-4 h-4" />
                )}
                Refresh Data
              </button>
            </div>
          </div>
        </div>

        {/* Main Content */}
        <div className="max-w-7xl mx-auto px-8 py-12">
          {error && (
            <div className="mb-10 p-5 bg-red-100/90 backdrop-blur-sm border-l-4 border-red-600 flex items-start gap-4 shadow-md">
              <AlertCircle className="w-6 h-6 text-red-700 flex-shrink-0 mt-0.5" />
              <p className="text-red-900 text-sm leading-relaxed font-medium">{error}</p>
            </div>
          )}

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-10">
            {/* Map Section */}
            <div className="lg:col-span-2">
              <div className="bg-white/90 backdrop-blur-sm border-2 border-gray-300 overflow-hidden shadow-xl hover:shadow-2xl transition-shadow cursor-crosshair">
                <div className="px-7 py-5 border-b-2 border-gray-300 bg-gradient-to-r from-gray-50 to-gray-100">
                  <div className="flex items-center justify-between">
                    <div>
                      <h2 className="text-xl font-bold text-gray-900 flex items-center gap-3">
                        <Flame className="w-6 h-6 text-red-600" />
                        Canada Fire Risk Map
                      </h2>
                      <p className="text-gray-700 text-sm mt-2 flex items-center gap-2">
                        <MapPin className="w-4 h-4 text-orange-600" />
                        Click anywhere on the map to assess fire risk
                      </p>
                    </div>
                  </div>
                </div>
                
                <div className="relative" style={{ height: '700px' }}>
                  <MapContainer
                    center={canadaCenter}
                    zoom={4}
                    style={{ height: '100%', width: '100%' }}
                    className="z-0"
                  >
                    <TileLayer
                      attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
                      url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
                    />
                    
                    <MapClickHandler />
                    
                    {/* Risk Grid Circles */}
                    {gridData.map((point, idx) => (
                      <Circle
                        key={idx}
                        center={[point.latitude, point.longitude]}
                        radius={50000}
                        pathOptions={{
                          fillColor: getRiskColor(point.probability),
                          fillOpacity: getRiskOpacity(point.probability),
                          color: getRiskColor(point.probability),
                          weight: 1,
                          opacity: 0.5
                        }}
                      >
                        <Popup>
                          <div className="text-sm">
                            <div className="font-semibold mb-1">{point.riskLevel} Risk</div>
                            <div>Probability: {(point.probability * 100).toFixed(1)}%</div>
                            <div className="text-xs text-gray-600 mt-1">
                              {point.latitude.toFixed(2)}, {point.longitude.toFixed(2)}
                            </div>
                          </div>
                        </Popup>
                      </Circle>
                    ))}
                    
                    {/* Active Fire Markers */}
                    {activeFires.map((fire, idx) => (
                      <Marker
                        key={`fire-${idx}`}
                        position={[fire.latitude, fire.longitude]}
                        icon={L.divIcon({
                          className: 'custom-fire-icon',
                          html: `<div style="background: #dc2626; width: 12px; height: 12px; border-radius: 50%; border: 2px solid white; box-shadow: 0 0 10px rgba(220,38,38,0.5);"></div>`,
                          iconSize: [12, 12]
                        })}
                      >
                        <Popup>
                          <div className="text-sm">
                            <div className="font-semibold text-red-600 mb-1">Active Fire</div>
                            <div>Confidence: {fire.confidence || 'N/A'}</div>
                            <div>FRP: {fire.frp || 'N/A'} MW</div>
                            <div className="text-xs text-gray-600 mt-1">
                              {fire.latitude.toFixed(2)}, {fire.longitude.toFixed(2)}
                            </div>
                          </div>
                        </Popup>
                      </Marker>
                    ))}
                    
                    {/* Selected Location Marker */}
                    {selectedPoint && (
                      <Marker position={[selectedPoint.lat, selectedPoint.lon]}>
                        <Popup>
                          <div className="text-sm font-semibold">Selected Location</div>
                        </Popup>
                      </Marker>
                    )}
                  </MapContainer>
                </div>
              </div>
            </div>

            {/* Info Panel */}
            <div className="space-y-8">
              {/* Legend */}
              <div className="bg-white/90 backdrop-blur-sm border-2 border-gray-300 p-7 shadow-xl">
                <h3 className="text-lg font-bold text-gray-900 mb-6 pb-3 border-b-2 border-orange-200">Risk Levels</h3>
                <div className="space-y-5">
                  <div className="flex items-center gap-4 group hover:bg-red-50 p-2 -m-2 transition-colors">
                    <div className="w-6 h-6 bg-red-600 flex-shrink-0 shadow-sm"></div>
                    <span className="text-gray-800 text-sm font-semibold">High Risk (&gt;70%)</span>
                  </div>
                  <div className="flex items-center gap-4 group hover:bg-orange-50 p-2 -m-2 transition-colors">
                    <div className="w-6 h-6 bg-orange-600 flex-shrink-0 shadow-sm"></div>
                    <span className="text-gray-800 text-sm font-semibold">Medium Risk (40-70%)</span>
                  </div>
                  <div className="flex items-center gap-4 group hover:bg-green-50 p-2 -m-2 transition-colors">
                    <div className="w-6 h-6 bg-green-600 flex-shrink-0 shadow-sm"></div>
                    <span className="text-gray-800 text-sm font-semibold">Low Risk (&lt;40%)</span>
                  </div>
                  <div className="pt-4 border-t-2 border-gray-300 mt-4">
                    <div className="flex items-center gap-4 group hover:bg-red-50 p-2 -m-2 transition-colors">
                      <div className="w-4 h-4 bg-red-600 flex-shrink-0 ring-2 ring-white shadow-md"></div>
                      <span className="text-gray-800 text-sm font-semibold">Active Fire Detection</span>
                    </div>
                  </div>
                </div>
              </div>

              {/* Location Risk Info */}
              {locationRisk && (
                <div className="bg-white/90 backdrop-blur-sm border-2 border-gray-300 p-7 shadow-xl">
                  <h3 className="text-lg font-bold text-gray-900 mb-6 pb-3 border-b-2 border-orange-200">Selected Location</h3>
                  
                  <div className="space-y-7">
                    <div className="p-6 bg-gradient-to-br from-gray-50 to-gray-100 border-l-4 shadow-sm" 
                         style={{ borderColor: getRiskColor(locationRisk.probability) }}>
                      <div className="text-3xl font-bold" style={{ color: getRiskColor(locationRisk.probability) }}>
                        {locationRisk.riskLevel} Risk
                      </div>
                      <div className="text-gray-700 text-base mt-2 font-semibold">
                        {(locationRisk.probability * 100).toFixed(1)}% probability
                      </div>
                    </div>

                    {locationRisk.weather && (
                      <div className="space-y-5">
                        <h4 className="text-sm font-bold text-gray-900 uppercase tracking-wider pb-2 border-b border-orange-200">Current Conditions</h4>
                        
                        <div className="flex items-center gap-4 hover:bg-sky-50 p-3 -m-3 transition-colors">
                          <div className="w-12 h-12 bg-gradient-to-br from-sky-100 to-sky-200 flex items-center justify-center flex-shrink-0 border border-sky-300">
                            <Cloud className="w-6 h-6 text-sky-700" />
                          </div>
                          <div>
                            <div className="text-xs text-gray-600 uppercase tracking-wide font-semibold">Temperature</div>
                            <div className="font-bold text-gray-900 text-xl">{locationRisk.weather.temperature_2m}°C</div>
                          </div>
                        </div>

                        <div className="flex items-center gap-4 hover:bg-cyan-50 p-3 -m-3 transition-colors">
                          <div className="w-12 h-12 bg-gradient-to-br from-cyan-100 to-cyan-200 flex items-center justify-center flex-shrink-0 border border-cyan-300">
                            <Wind className="w-6 h-6 text-cyan-700" />
                          </div>
                          <div>
                            <div className="text-xs text-gray-600 uppercase tracking-wide font-semibold">Wind Speed</div>
                            <div className="font-bold text-gray-900 text-xl">{locationRisk.weather.wind_speed_10m} km/h</div>
                          </div>
                        </div>

                        <div className="flex items-center gap-4 hover:bg-blue-50 p-3 -m-3 transition-colors">
                          <div className="w-12 h-12 bg-gradient-to-br from-blue-100 to-blue-200 flex items-center justify-center flex-shrink-0 border border-blue-300">
                            <Droplets className="w-6 h-6 text-blue-700" />
                          </div>
                          <div>
                            <div className="text-xs text-gray-600 uppercase tracking-wide font-semibold">Humidity</div>
                            <div className="font-bold text-gray-900 text-xl">{locationRisk.weather.relative_humidity_2m}%</div>
                          </div>
                        </div>

                        <div className="flex items-center gap-4 hover:bg-indigo-50 p-3 -m-3 transition-colors">
                          <div className="w-12 h-12 bg-gradient-to-br from-indigo-100 to-indigo-200 flex items-center justify-center flex-shrink-0 border border-indigo-300">
                            <Droplets className="w-6 h-6 text-indigo-700" />
                          </div>
                          <div>
                            <div className="text-xs text-gray-600 uppercase tracking-wide font-semibold">Precipitation</div>
                            <div className="font-bold text-gray-900 text-xl">{locationRisk.weather.precipitation} mm</div>
                          </div>
                        </div>
                      </div>
                    )}

                    {locationRisk.enhancedFeatures && (
                      <div className="pt-6 border-t-2 border-gray-300 space-y-6">
                        <h4 className="text-sm font-bold text-gray-900 uppercase tracking-wider">Risk Factors</h4>
                        
                        <div className="grid grid-cols-2 gap-4">
                          <div className="bg-gradient-to-br from-gray-50 to-gray-100 p-4 border-2 border-gray-300 hover:border-orange-400 transition-colors">
                            <div className="text-xs text-gray-600 uppercase tracking-wide mb-2 font-bold">Season</div>
                            <div className="text-gray-900 font-bold text-lg">{locationRisk.enhancedFeatures.season}</div>
                          </div>
                          <div className="bg-gradient-to-br from-gray-50 to-gray-100 p-4 border-2 border-gray-300 hover:border-orange-400 transition-colors">
                            <div className="text-xs text-gray-600 uppercase tracking-wide mb-2 font-bold">Vegetation</div>
                            <div className="text-gray-900 font-bold text-lg">
                              {(locationRisk.enhancedFeatures.ndvi * 100).toFixed(0)}%
                            </div>
                          </div>
                          <div className="bg-gradient-to-br from-gray-50 to-gray-100 p-4 border-2 border-gray-300 hover:border-orange-400 transition-colors">
                            <div className="text-xs text-gray-600 uppercase tracking-wide mb-2 font-bold">Drought Index</div>
                            <div className="text-gray-900 font-bold text-lg">
                              {(locationRisk.enhancedFeatures.droughtIndex * 100).toFixed(0)}%
                            </div>
                          </div>
                          <div className="bg-gradient-to-br from-gray-50 to-gray-100 p-4 border-2 border-gray-300 hover:border-orange-400 transition-colors">
                            <div className="text-xs text-gray-600 uppercase tracking-wide mb-2 font-bold">Fire History</div>
                            <div className="text-gray-900 font-bold text-lg">
                              {(locationRisk.enhancedFeatures.historicalFireDensity * 100).toFixed(0)}%
                            </div>
                          </div>
                          <div className="bg-gradient-to-br from-gray-50 to-gray-100 p-4 border-2 border-gray-300 hover:border-orange-400 transition-colors col-span-2">
                            <div className="text-xs text-gray-600 uppercase tracking-wide mb-2 font-bold">Elevation</div>
                            <div className="text-gray-900 font-bold text-lg">
                              {locationRisk.enhancedFeatures.elevation.toFixed(0)}m
                            </div>
                          </div>
                        </div>

                        <div className="p-4 bg-blue-50/80 border-l-4 border-blue-600">
                          <p className="text-xs text-gray-800 leading-relaxed">
                            <span className="font-bold">Note:</span> Winter conditions show lower risk. Model accounts for seasonal patterns, vegetation, drought, and historical fire data for year-round accuracy.
                          </p>
                        </div>
                      </div>
                    )}

                    <div className="text-xs text-gray-600 pt-5 border-t border-gray-300 font-mono bg-gray-50 p-3">
                      Lat: {locationRisk.latitude.toFixed(4)}, Lon: {locationRisk.longitude.toFixed(4)}
                    </div>
                  </div>
                </div>
              )}

              {/* Stats */}
              <div className="bg-white/90 backdrop-blur-sm border-2 border-gray-300 p-7 shadow-xl">
                <h3 className="text-lg font-bold text-gray-900 mb-6 pb-3 border-b-2 border-orange-200">Statistics</h3>
                <div className="space-y-6">
                  <div 
                    className="pb-6 border-b-2 border-gray-300 hover:bg-gray-50 p-4 -m-4 transition-colors cursor-default"
                  >
                    <div className="text-xs text-gray-700 uppercase tracking-wider mb-3 font-bold">Grid Points Analyzed</div>
                    <div className="flex items-baseline gap-2">
                      <div className="text-5xl font-bold text-gray-900">
                        {gridData.length}
                      </div>
                      <div className="text-sm text-gray-600 font-medium">locations</div>
                    </div>
                  </div>
                  <div 
                    className="hover:bg-red-50 p-4 -m-4 transition-colors cursor-default"
                  >
                    <div className="text-xs text-gray-700 uppercase tracking-wider mb-3 font-bold">Active Fires Detected</div>
                    <div className="flex items-baseline gap-2">
                      <div className="text-5xl font-bold text-red-600">
                        {activeFires.length}
                      </div>
                      <div className="text-sm text-gray-600 font-medium">fires</div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div className="bg-white/80 backdrop-blur-md border-t-2 border-gray-300 mt-16">
          <div className="max-w-7xl mx-auto px-8 py-6">
            <div className="flex items-center justify-between text-sm">
              <div className="text-gray-700">
                <span className="font-semibold">FireGuard</span> v1
              </div>
              <div className="text-gray-600 text-xs">
                Credits for Background: Thibaud Moritz/AFP via Getty Images
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default FireGuardApp;