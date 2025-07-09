using System.Xml.Linq;
using System.Globalization;
using AgroSpace.Models;

namespace AgroSpace.Services;

public interface IKmlService
{
    List<Field> GetAllFields();
    double? GetFieldSize(int fieldId);
    double CalculateDistance(int fieldId, double lat, double lng);
    PointLocationResponse? IsPointInField(double lat, double lng);
}

public class KmlService : IKmlService
{
    private readonly IWebHostEnvironment _environment;
    private readonly Dictionary<int, Field> _fieldsCache = new();
    private readonly object _cacheLock = new();
    private readonly XNamespace kml = "http://www.opengis.net/kml/2.2";

    public KmlService(IWebHostEnvironment environment)
    {
        _environment = environment;
        LoadFields();
    }

    private void LoadFields()
    {
        lock (_cacheLock)
        {
            if (_fieldsCache.Count > 0) return;

            try
            {
                var fieldsPath = Path.Combine(_environment.WebRootPath, "coord", "fields.kml");
                var centroidsPath = Path.Combine(_environment.WebRootPath, "coord", "centroids.kml");

                if (!File.Exists(fieldsPath))
                {
                    throw new FileNotFoundException($"KML файл полей не найден: {fieldsPath}");
                }

                if (!File.Exists(centroidsPath))
                {
                    throw new FileNotFoundException($"KML файл центроидов не найден: {centroidsPath}");
                }

                var fields = ParseFieldsKml(fieldsPath);
                var centroids = ParseCentroidsKml(centroidsPath);

                foreach (var field in fields)
                {
                    if (centroids.TryGetValue(field.Id, out var center))
                    {
                        field.Center = center;
                        _fieldsCache[field.Id] = field;
                    }
                }

                Console.WriteLine($"Загружено {_fieldsCache.Count} полей");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки полей: {ex.Message}");
                throw;
            }
        }
    }

    private List<Field> ParseFieldsKml(string filePath)
    {
        var fields = new List<Field>();
        var doc = XDocument.Load(filePath);
        
        var placemarks = doc.Descendants(kml + "Placemark");
        Console.WriteLine($"Found {placemarks.Count()} placemarks");

        foreach (var placemark in placemarks)
        {
            var field = ParseFieldFromPlacemark(placemark);
            if (field != null)
            {
                fields.Add(field);
                Console.WriteLine($"Added field: {field.Id} - {field.Name} - {field.Size}");
            }
        }

        return fields;
    }

    private Field? ParseFieldFromPlacemark(XElement placemark)
    {
        var field = new Field();

        // Parse name
        field.Name = placemark.Element(kml + "name")?.Value ?? string.Empty;

        // Parse ExtendedData (ID and size)
        ParseExtendedData(placemark, field);

        // Parse polygon coordinates
        if (!ParsePolygonCoordinates(placemark, field))
        {
            Console.WriteLine($"Failed to parse polygon for field {field.Id}");
            return null;
        }

        // Validate field
        if (field.Id <= 0)
        {
            Console.WriteLine($"Skipped field with invalid ID: {field.Name}");
            return null;
        }

        return field;
    }

    private void ParseExtendedData(XElement placemark, Field field)
    {
        var simpleDataElements = placemark
            .Element(kml + "ExtendedData")?
            .Element(kml + "SchemaData")?
            .Elements(kml + "SimpleData") ?? Enumerable.Empty<XElement>();

        foreach (var simpleData in simpleDataElements)
        {
            var name = simpleData.Attribute("name")?.Value;
            var value = simpleData.Value;
            
            switch (name)
            {
                case "fid" when int.TryParse(value, out var id):
                    field.Id = id;
                    break;
                case "size" when double.TryParse(value, out var size):
                    field.Size = size;
                    break;
            }
        }
    }

    private bool ParsePolygonCoordinates(XElement placemark, Field field)
    {
        var coordinatesElement = placemark
            .Element(kml + "Polygon")?
            .Element(kml + "outerBoundaryIs")?
            .Element(kml + "LinearRing")?
            .Element(kml + "coordinates");

        if (coordinatesElement == null)
        {
            Console.WriteLine($"No coordinates found for field {field.Id}");
            return false;
        }

        Console.WriteLine($"Found coordinates for field {field.Id}: {coordinatesElement.Value.Substring(0, Math.Min(100, coordinatesElement.Value.Length))}...");
        
        var coordinates = ParseCoordinates(coordinatesElement.Value);
        field.Polygon = coordinates;
        
        Console.WriteLine($"Field {field.Id} ({field.Name}): {coordinates.Count} coordinates");
        return coordinates.Count > 0;
    }

    private Dictionary<int, Location> ParseCentroidsKml(string filePath)
    {
        var centroids = new Dictionary<int, Location>();
        var doc = XDocument.Load(filePath);
        
        var placemarks = doc.Descendants(kml + "Placemark");

        foreach (var placemark in placemarks)
        {
            var centroid = ParseCentroidFromPlacemark(placemark);
            if (centroid.HasValue)
            {
                centroids[centroid.Value.Key] = centroid.Value.Value;
            }
        }

        return centroids;
    }

    private (int Key, Location Value)? ParseCentroidFromPlacemark(XElement placemark)
    {
        // Parse ID from ExtendedData
        var id = ParseIdFromExtendedData(placemark);
        if (id == 0) return null;

        // Parse coordinates
        var coordinatesElement = placemark
            .Element(kml + "Point")?
            .Element(kml + "coordinates");

        if (coordinatesElement == null) return null;

        var coordinates = ParseCoordinates(coordinatesElement.Value);
        if (coordinates.Count == 0) return null;

        return (id, coordinates[0]);
    }

    private int ParseIdFromExtendedData(XElement placemark)
    {
        var simpleDataElements = placemark
            .Element(kml + "ExtendedData")?
            .Element(kml + "SchemaData")?
            .Elements(kml + "SimpleData") ?? Enumerable.Empty<XElement>();

        foreach (var simpleData in simpleDataElements)
        {
            if (simpleData.Attribute("name")?.Value == "fid" && 
                int.TryParse(simpleData.Value, out var id))
            {
                return id;
            }
        }

        return 0;
    }

    private List<Location> ParseCoordinates(string coordinatesText)
    {
        var locations = new List<Location>();
        var coordinatePairs = coordinatesText.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var pair in coordinatePairs)
        {
            var parts = pair.Split(',');
            if (parts.Length >= 2)
            {
                // В KML координаты идут в формате: longitude,latitude
                if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                {
                    locations.Add(new Location { Lng = lng, Lat = lat });
                }
            }
        }

        return locations;
    }

    public List<Field> GetAllFields()
    {
        return _fieldsCache.Values.ToList();
    }

    public double? GetFieldSize(int fieldId)
    {
        return _fieldsCache.TryGetValue(fieldId, out var field) ? field.Size : null;
    }

    public double CalculateDistance(int fieldId, double lat, double lng)
    {
        if (!_fieldsCache.TryGetValue(fieldId, out var field))
            throw new ArgumentException($"Field with id {fieldId} not found");

        return CalculateHaversineDistance(field.Center.Lat, field.Center.Lng, lat, lng);
    }

    public PointLocationResponse? IsPointInField(double lat, double lng)
    {
        foreach (var field in _fieldsCache.Values)
        {
            if (IsPointInPolygon(lat, lng, field.Polygon))
            {
                return new PointLocationResponse
                {
                    Id = field.Id,
                    Name = field.Name
                };
            }
        }

        return null;
    }

    private double CalculateHaversineDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double earthRadius = 6371000; // Earth radius in meters

        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadius * c;
    }

    private double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180;
    }

    private bool IsPointInPolygon(double lat, double lng, List<Location> polygon)
    {
        if (polygon.Count < 3) return false;

        var inside = false;
        var j = polygon.Count - 1;

        for (var i = 0; i < polygon.Count; i++)
        {
            if (((polygon[i].Lat > lat) != (polygon[j].Lat > lat)) &&
                (lng < (polygon[j].Lng - polygon[i].Lng) * (lat - polygon[i].Lat) / (polygon[j].Lat - polygon[i].Lat) + polygon[i].Lng))
            {
                inside = !inside;
            }
            j = i;
        }

        return inside;
    }
} 