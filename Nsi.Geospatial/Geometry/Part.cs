using Nsi.Geospatial.Enums;

namespace Nsi.Geospatial.Geometry;

/// <summary>A ring (polygon part, line, or single point) with cached MBR/centroid/area/perimeter.</summary>
public sealed class Part
{
    public List<Vertex> Vertices { get; } = new();
    public BoundingBox Mbr { get; private set; } = BoundingBox.Empty;
    public bool IsHole { get; set; }
    public bool Direction { get; set; }
    public int BeginIndex { get; set; }
    public int EndIndex { get; set; }
    public double CentroidX { get; private set; }
    public double CentroidY { get; private set; }
    public double Area { get; private set; }
    public double Perimeter { get; private set; }

    public string? Wkt { get; set; }

    public Part(string? wkt = null) => Wkt = wkt;

    public void AddVertex(Vertex vertex, bool updateMbr = true)
    {
        if (Vertices.Count > 0)
        {
            var last = Vertices[^1];
            Perimeter += GeometryMath.Distance((last.X, last.Y), (vertex.X, vertex.Y));
        }
        else
        {
            // fix: the original set BeginIndex/EndIndex to 0 and derived IsHole from
            // Direction *before* any real geometry existed; keep the intent but make it explicit.
            BeginIndex = 0;
            EndIndex = 0;
            IsHole = !Direction;
        }

        Vertices.Add(vertex);

        if (updateMbr)
        {
            Mbr = Vertices.Count == 1
                ? BoundingBox.Point(vertex.X, vertex.Y)
                : Mbr.Union(BoundingBox.Point(vertex.X, vertex.Y));
        }
    }

    public void CloseRing()
    {
        EndIndex = IsHole ? 0 : Vertices.Count - 1;
        if (Vertices.Count > 0)
        {
            var first = Vertices[0];
            AddVertex(new Vertex(first.X, first.Y), updateMbr: false);
            (CentroidX, CentroidY) = GeometryMath.Centroid(Vertices.Select(v => (v.X, v.Y)));
            Area = GeometryMath.Area(Vertices.Select(v => (v.X, v.Y)));
        }
    }
}