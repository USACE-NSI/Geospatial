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
  public PartType Kind { get; set; }

  /// <summary>Rings close for length and area; polylines and points never do.</summary>
  public bool IsRing => Kind == PartType.Ring;

  public bool Direction { get; set; }
  public int BeginIndex { get; set; }
  public int EndIndex { get; set; }
  public double CentroidX { get; private set; }
  public double CentroidY { get; private set; }

  /// <summary>Planar area in the square linear units of Crs. Null-safe interpretation
  /// via AreaSquareMeters.</summary>
  public double? Area { get; private set; }

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

  public Part(PartType kind) => Kind = kind;

  /// <summary>
  /// Append a vertex, maintaining the incremental open-walk Perimeter and the MBR.
  /// Ring closure is deliberately NOT handled here — GeometryMath closes implicitly,
  /// and the closing edge is added once by Seal().
  /// </summary>
  public void AddVertex(Vertex vertex)
  {
    if (Vertices.Count > 0)
    {
      var last = Vertices[^1];
      Perimeter += GeometryMath.Distance((last.X, last.Y), (vertex.X, vertex.Y));
    }

    Vertices.Add(vertex);
    BoundingBox = BoundingBox.Union(BoundingBox.Point(vertex.X, vertex.Y));
  }

  /// Finalise after the last vertex. Never mutates the vertex list: GeometryMath
  /// closes implicitly. Idempotent with respect to authored closure.
  public void Seal()
  {
    if (Vertices.Count == 0)
      return;

    // Canonical form: strip a trailing duplicate so Vertices.Count is the
    // unique-vertex count, whether the source arrived open or closed.
    if (IsRing && (Vertices.Count > 1) && (Vertices[^1].Coordinates == Vertices[0].Coordinates))
      Vertices.RemoveAt(Vertices.Count - 1);

    if (IsRing)
    {
      if (Vertices.Count > 1) // the one legitimate use of today's behaviour
        Perimeter += GeometryMath.Distance(Vertices[^1].XY, Vertices[0].XY);
      if (Vertices.Count >= 3)
      {
        Area = GeometryMath.Area(Vertices.Select(v => (v.X, v.Y)));
        (CentroidX, CentroidY) = GeometryMath.Centroid(Vertices.Select(v => (v.X, v.Y)));
      }
    }
    else
    {
      Area = null; // a polyline has no area; 0 is a lie
    }
  }
}

