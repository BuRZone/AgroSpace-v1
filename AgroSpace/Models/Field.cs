namespace AgroSpace.Models;

public class Field
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Size { get; set; }
    public Location Center { get; set; } = new();
    public List<Location> Polygon { get; set; } = new();
}

public class Location
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class FieldResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Size { get; set; }
    public FieldLocations Locations { get; set; } = new();
}

public class FieldLocations
{
    public Location Center { get; set; } = new();
    public List<Location> Polygon { get; set; } = new();
}

public class DistanceRequest
{
    public int FieldId { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class PointLocationRequest
{
    public double Lat { get; set; }
    public double Lng { get; set; }
}

public class PointLocationResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
} 