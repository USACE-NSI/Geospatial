using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Projections;

namespace Nsi.Geospatial.Geometry;

/// <summary>
/// A ring (polygon part, line, or single point) with cached MBR/centroid/area/perimeter.
/// All cached metrics are planar and expressed in the linear units of the owning
/// collection's CRS — see Crs. Ask for AreaSquareMeters / LengthMeters when you need
/// a named unit.
/// </summary>
public sealed class Part
{
  public List<Vertex> Vertices { get; } = new();
  public BoundingBox BoundingBox { get; private set; } = BoundingBox.Empty;
  public bool IsHole { get; set; }
  public bool Direction { get; set; }
  public int BeginIndex { get; set; }
  public int EndIndex { get; set; }
  public double CentroidX { get; private set; }
  public double CentroidY { get; private set; }

  /// <summary>Planar area in the square linear units of Crs. Null-safe interpretation
  /// via AreaSquareMeters.</summary>
  public double Area { get; private set; }

  /// <summary>Planar perimeter in the linear units of Crs.</summary>
  public double Perimeter { get; private set; }

  /// <summary>Set by Feature.AddPart. The single route to a CRS.</summary>
  internal Feature? Owner { get; set; }

  /// <summary>The dependable CRS source: walks Part -> Feature -> FeatureCollection.
  /// Unknown for a standalone Part, which is honest — synthetic geometry has no CRS.</summary>
  public CrsInfo Crs => Owner?.Crs ?? Projections.CrsInfo.Unknown;

  /// <summary>
  /// Area in square metres, or null when the CRS is unknown. Geographic rings are
  /// measured spherically (planar shoelace on degrees is square degrees); projected
  /// rings use the declared unit, no transform needed.
  /// </summary>
  public double? AreaSquareMeters =>
    Crs.Kind switch
    {
      Projections.CrsKind.Projected => Area * Crs.UnitToMetersOrMeter * Crs.UnitToMetersOrMeter,
      Projections.CrsKind.Geographic => GeometryMath.SphericalArea(
        Vertices.Select(v => (v.X, v.Y))
      ),
      _ => null,
    };

  public double? LengthMeters =>
    Crs.Kind switch
    {
      Projections.CrsKind.Projected => Perimeter * Crs.UnitToMetersOrMeter,
      Projections.CrsKind.Geographic => GeometryMath.SphericalPerimeter(
        Vertices.Select(v => (v.X, v.Y))
      ),
      _ => null,
    };

  public Part() { }

  public void AddVertex(Vertex vertex, bool updateBoundingBox = true)
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

    if (updateBoundingBox)
    {
      BoundingBox =
        Vertices.Count == 1
          ? BoundingBox.Point(vertex.X, vertex.Y)
          : BoundingBox.Union(BoundingBox.Point(vertex.X, vertex.Y));
    }
  }

  public void CloseRing()
  {
    EndIndex = IsHole ? 0 : Vertices.Count - 1;
    if (Vertices.Count > 0)
    {
      var first = Vertices[0];
      AddVertex(new Vertex(first.X, first.Y), updateBoundingBox: false);
      (CentroidX, CentroidY) = GeometryMath.Centroid(Vertices.Select(v => (v.X, v.Y)));
      Area = GeometryMath.Area(Vertices.Select(v => (v.X, v.Y)));
    }
  }
}

