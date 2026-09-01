namespace Nsi.Geospatial.Projections;

/// <summary>A coordinate reference system, identified by WKT and optional EPSG code.</summary>
public sealed class Projection
{
    public string Wkt { get; }
    public string? EpsgCode { get; }

    public Projection(string wkt, string? epsgCode = null)
    {
        Wkt = wkt;
        EpsgCode = epsgCode;
    }

    public static readonly Projection Wgs84 = new(
        "GEOGCS[\"WGS84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]]," +
        "AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]]," +
        "UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4326\"]]",
        "EPSG:4326");

    public static readonly Projection Nad83 = new(
        "GEOGCS[\"NAD83\",DATUM[\"North_American_Datum_1983\",SPHEROID[\"GRS 1980\",6378137,298.257222101,AUTHORITY[\"EPSG\",\"7019\"]]," +
        "AUTHORITY[\"EPSG\",\"6269\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]]," +
        "UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4269\"]]",
        "EPSG:4269");

    public static readonly Projection AlbersUsa = new(
        "PROJCS[\"USA_Contiguous_Albers_Equal_Area_Conic\",GEOGCS[\"GCS_North_American_1983\"," +
        "DATUM[\"North_American_Datum_1983\",SPHEROID[\"GRS 1980\",6378137,298.257222101]]," +
        "PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]]," +
        "PROJECTION[\"Albers_Conic_Equal_Area\"],PARAMETER[\"False_Easting\",0]," +
        "PARAMETER[\"False_Northing\",0],PARAMETER[\"longitude_of_center\",-96]," +
        "PARAMETER[\"Standard_Parallel_1\",29.5],PARAMETER[\"Standard_Parallel_2\",45.5]," +
        "PARAMETER[\"latitude_of_center\",37.5],UNIT[\"Meter\",1],AUTHORITY[\"EPSG\",\"102003\"]]",
        "EPSG:102003");

    public override bool Equals(object? obj) => obj is Projection p && p.Wkt == Wkt;
    public override int GetHashCode() => HashCode.Combine(Wkt);
}