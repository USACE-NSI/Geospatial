using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Projections;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// Fixture builders for the core test assembly (P-36). Rectangle/PointAt lived in
/// SpatialJoinTests, Cell/Ring/Polygon/Geographic/Projected in SphericalMetricsTests, and
/// Ring/Hole/Rect/FeatureOf/CollectionOf/Projected/Geographic in CrsInfoAndAreaTests -- three
/// independent ways to make a ring, in files that are not about rings. That is how a duplicate
/// suite survived: two namespaces meant two classes could not collide.
///
/// VERBATIM, on purpose. An earlier pass renamed Polygon to PolygonOf and moved Ring's bool to
/// the front, which broke 31 call sites. These signatures are the contract the tests were written
/// against; a relocation that needs call-site edits is not a relocation.
///
/// Geographic/Projected appear twice with DIFFERENT RETURN TYPES -- Features in
/// SphericalMetricsTests, CrsInfo in CrsInfoAndAreaTests. Same name, two meanings, which is the
/// sharpest evidence in this row. They are therefore Geographic()/Projected() for the collection
/// form and GeographicCrs/ProjectedCrs() for the CrsInfo form. Do not "unify" them.
///
/// These build GEOMETRY. They deliberately do not absorb the R-tree's Covers/ContainsPoint/
/// AssertCovers/AssertIsExactUnion helpers: those reimplement BoundingBox members and should be
/// deleted in favour of the members, which is P-39's work, not a relocation.
/// </summary>
internal static class TestFeatures
{
  // ---- from SpatialJoinTests -------------------------------------------------------------

  /// <summary>
  /// Axis-aligned polygon with an "ID" attribute, authored open or closed. Both are legal --
  /// Part permits either and the metric primitives close implicitly -- so a test that authors
  /// only one shape cannot tell a closed-walk index bug from correct code.
  /// </summary>
  internal static Feature Rectangle(
    double minX,
    double minY,
    double maxX,
    double maxY,
    int id,
    bool closeAuthoredRing
  )
  {
    var ring = new Part(PartType.Ring);
    ring.AddVertex(new Vertex(minX, minY));
    ring.AddVertex(new Vertex(maxX, minY));
    ring.AddVertex(new Vertex(maxX, maxY));
    ring.AddVertex(new Vertex(minX, maxY));
    if (closeAuthoredRing)
    {
      ring.AddVertex(new Vertex(minX, minY));
    }
    ring.Seal();

    var feature = new Feature { ShapeType = ShapeType.Polygon };
    feature.AddPart(ring);
    feature.ComputeBoundingBox();
    feature.Attributes["ID"] = id;
    return feature;
  }

  /// <summary>ComputeBoundingBox is not optional: DistanceFeatureToFeature reads BoundingBox.MinX/MinY.</summary>
  internal static Feature PointAt(double x, double y)
  {
    var point = new Part(PartType.Point);
    point.AddVertex(new Vertex(x, y));
    point.Seal();

    var feature = new Feature { ShapeType = ShapeType.Point };
    feature.AddPart(point);
    feature.ComputeBoundingBox();
    return feature;
  }

  // ---- from SphericalMetricsTests --------------------------------------------------------

  /// <summary>1x1 (or w x h) graticule cell in degrees -- the shape the spherical metrics are exact for.</summary>
  internal static List<(double X, double Y)> Cell(
    double lon,
    double lat,
    double w = 1,
    double h = 1
  ) => new() { (lon, lat), (lon + w, lat), (lon + w, lat + h), (lon, lat + h) };

  /// <summary>Order preserved, no Seal(): some tests author a ring without sealing it.</summary>
  internal static Part Ring(IEnumerable<(double X, double Y)> ring, bool exterior)
  {
    var part = new Part(PartType.Ring) { IsHole = !exterior };
    foreach (var (x, y) in ring)
    {
      part.AddVertex(new Vertex(x, y));
    }
    part.Seal();
    return part;
  }

  /// <summary>Adds to the owner as a side effect -- that is what wires the CRS chain, so keep it.</summary>
  internal static Feature Polygon(Features owner, Part exterior, params Part[] holes)
  {
    var f = new Feature();
    f.AddPart(exterior);
    foreach (var h in holes)
    {
      f.AddPart(h);
    }
    owner.AddFeature(f);
    return f;
  }

  /// <summary>Returns a FEATURES collection. The CrsInfo form is GeographicCrs.</summary>
  internal static Features Geographic(ShapeType shape = ShapeType.Polygon) =>
    new()
    {
      ShapeType = shape,
      Crs = new CrsInfo
      {
        Kind = CrsKind.Geographic,
        Wkt = Projection.Wgs84.Wkt,
        EpsgCode = 4326,
      },
    };

  /// <summary>Returns a FEATURES collection. The CrsInfo form is ProjectedCrs.</summary>
  internal static Features Projected(double unitToMeters, LinearUnit unit = LinearUnit.Meter) =>
    new()
    {
      ShapeType = ShapeType.Polygon,
      Crs = new CrsInfo
      {
        Kind = CrsKind.Projected,
        Unit = unit,
        UnitToMeters = unitToMeters,
      },
    };

  // ---- from CrsInfoAndAreaTests ----------------------------------------------------------

  /// <summary>
  /// Direction is set before the first AddVertex on purpose: AddVertex derives
  /// IsHole = !Direction only while the ring is still empty, so setting it later silently
  /// leaves IsHole wrong.
  /// </summary>
  internal static Part Ring(params (double X, double Y)[] points) => Build(true, points);

  internal static Part Hole(params (double X, double Y)[] points) => Build(false, points);

  internal static Feature FeatureOf(params Part[] parts)
  {
    var feature = new Feature();
    foreach (Part part in parts)
    {
      feature.AddPart(part);
    }
    return feature;
  }

  internal static Features CollectionOf(CrsInfo crs, params Feature[] features)
  {
    var fc = new Features { Crs = crs };
    foreach (Feature f in features)
    {
      fc.AddFeature(f);
    }
    return fc;
  }

  /// <summary>CCW rectangle in CRS units, default 100 x 50.</summary>
  internal static (double X, double Y)[] Rect(double w = 100, double h = 50) =>
    [(0, 0), (w, 0), (w, h), (0, h)];

  /// <summary>Returns a CRSINFO. The Features form is Projected().</summary>
  internal static CrsInfo ProjectedCrs(double unitToMeters) =>
    new() { Kind = CrsKind.Projected, UnitToMeters = unitToMeters };

  /// <summary>Returns a CRSINFO. The Features form is Geographic().</summary>
  internal static CrsInfo GeographicCrs { get; } = new() { Kind = CrsKind.Geographic };

  private static Part Build(bool exterior, params (double X, double Y)[] points)
  {
    var part = new Part(PartType.Ring) { IsHole = !exterior };
    foreach ((double x, double y) in points)
    {
      part.AddVertex(new Vertex(x, y));
    }
    part.Seal();
    return part;
  }
}

