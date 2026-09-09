using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Projections;
using Xunit;

namespace Nsi.Geospatial.Core.Tests;

/// <summary>
/// Evaluation of the spherical metrics added on feature/spherical:
/// GeometryMath.SphericalArea / SphericalDistance / SphericalPerimeter /
/// SphericalPointToSegmentDistance, and the CrsKind-dispatched
/// Part/Feature.AreaSquareMeters and LengthMeters.
///
/// Expected values are derived independently of the implementation:
///   - graticule cells from the closed form R^2 * dLon * (sin lat2 - sin lat1)
///   - triangles from l'Huilier's spherical excess on the unit sphere
///   - ellipsoid truth from authalic latitude (Snyder), cross-checked by
///     numerical integration of N*cos(phi)*M dphi dLambda
///   - point-to-segment truth by dense sampling along the great-circle arc
///
/// Tests marked Skip are failing-by-design: they assert the behaviour the
/// docstrings promise. See the Skip reasons.
/// </summary>
public class SphericalMetricsTests
{
  // ---------------------------------------------------------------- goldens

  /// <summary>1x1 degree graticule cell at the equator, square metres (authalic).</summary>
  private const double CellAtEquator = 1.2363711861448e10;

  /// <summary>1x1 degree cell spanning 44N..45N.</summary>
  private const double CellAt44N = 8.8187588297044e9;

  /// <summary>1x1 degree cell spanning 65N..66N.</summary>
  private const double CellAt65N = 5.1273429966014e9;

  /// <summary>Half the sphere: 2*pi*Ra^2.</summary>
  private const double Hemisphere = 2.55032810868571e14;

  /// <summary>WGS84 area of the 44N..45N cell, authalic-latitude formula.</summary>
  private const double EllipsoidCellAt44N = 8.8373695264e9;

  /// <summary>WGS84 area of the 65N..66N cell.</summary>
  private const double EllipsoidCellAt65N = 5.1614833020e9;

  /// <summary>True spherical area of the 1x1 right triangle at 44N, l'Huilier.</summary>
  private const double TrueTriangleAt44N = 4.4471416554e9;

  private const double QuarterMeridian = 10007557.1760931872; // pi/2 * Rmean
  private const double OneDegree = 111195.0797343687; // pi/180 * Rmean

  // ---------------------------------------------------------------- helpers

  private static List<(double X, double Y)> Cell(
    double lon,
    double lat,
    double w = 1,
    double h = 1
  ) => new() { (lon, lat), (lon + w, lat), (lon + w, lat + h), (lon, lat + h) };

  /// <summary>Closed-form sphere area of a graticule cell — the reference the code is checked against.</summary>
  private static double AnalyticCell(double lonSpan, double lat1, double lat2) =>
    GeometryMath.EarthRadiusAuthalicMeters
    * GeometryMath.EarthRadiusAuthalicMeters
    * (lonSpan * Math.PI / 180.0)
    * (Math.Sin(lat2 * Math.PI / 180.0) - Math.Sin(lat1 * Math.PI / 180.0));

  /// <summary>Relative-tolerance compare. A null actual is a failure, not a skip —
  /// null means "this geometry kind has no such measure", which is exactly what
  /// these tests are asserting against.</summary>
  private static void Rel(double expected, double? actual, double relTol, string what)
  {
    Assert.True(
      actual.HasValue,
      $"{what}: expected {expected:R}, got null (no such measure for this PartType)"
    );

    RelD(expected, actual.Value, relTol);
  }

  private static void RelD(double expected, double actual, double relTol, double absTol = 1e-9)
  {
    double diff = Math.Abs(expected - actual);
    // Relative tolerance is meaningless at expected == 0 (and prints rel diff ∞),
    // so fall back to an absolute floor. Callers comparing metres should pass an
    // absTol in metres; comparing degrees needs a much smaller one.
    Assert.True(
      diff <= Math.Max(Math.Abs(expected) * relTol, absTol),
      $"expected {expected:R}, actual {actual:R}, diff {diff:E}, rel {diff / Math.Abs(expected):E}"
    );
  }

  private static Part Ring(IEnumerable<(double X, double Y)> ring, bool exterior)
  {
    var part = new Part(PartType.Ring) { IsHole = !exterior };
    foreach (var (x, y) in ring)
      part.AddVertex(new Vertex(x, y));
    part.Seal();
    return part;
  }

  private static Feature Polygon(FeatureCollection owner, Part exterior, params Part[] holes)
  {
    var f = new Feature();
    f.AddPart(exterior);
    foreach (var h in holes)
      f.AddPart(h);
    owner.AddFeature(f);
    return f;
  }

  private static FeatureCollection Geographic(ShapeType shape = ShapeType.Polygon) =>
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

  private static FeatureCollection Projected(
    double unitToMeters,
    LinearUnit unit = LinearUnit.Meter
  ) =>
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

  // ============================================================ SphericalArea

  [Fact]
  public void SphericalAreaOfGraticuleCellMatchesClosedFormExactly()
  {
    // The formula is the shoelace applied to (lon, sin lat): exact whenever every
    // edge follows a meridian or a parallel, which is what graticule cells do.
    foreach (double lat in new[] { 0.0, 30.0, 44.0, 60.0, 65.0 })
    {
      double got = GeometryMath.SphericalArea(Cell(0, lat));
      Rel(AnalyticCell(1, lat, lat + 1), got, 1e-12, $"1x1 cell at {lat}N");
    }
  }

  [Theory]
  [InlineData(0.0, CellAtEquator)]
  [InlineData(44.0, CellAt44N)]
  [InlineData(65.0, CellAt65N)]
  public void SphericalAreaMatchesPinnedValues(double lat, double expected) =>
    Rel(expected, GeometryMath.SphericalArea(Cell(0, lat)), 1e-12, $"1x1 cell at {lat}N");

  [Fact]
  public void SphericalAreaShrinksWithLatitudeAsCosineOfMidLatitude()
  {
    // A degree of longitude halves by 60N, so a 1x1 cell must halve (to first order).
    double ratio = GeometryMath.SphericalArea(Cell(0, 0)) / GeometryMath.SphericalArea(Cell(0, 60));
    double expected = 1.0 / Math.Cos(60 * Math.PI / 180.0);
    Rel(expected, ratio, 2e-2, "equatorial/60N area ratio");

    // Second-order effect: the exact ratio is the ratio of d(sin lat).
    double exact =
      (Math.Sin(1 * Math.PI / 180.0) - 0.0)
      / (Math.Sin(61 * Math.PI / 180.0) - Math.Sin(60 * Math.PI / 180.0));
    Rel(exact, ratio, 1e-12, "equatorial/60N area ratio, exact");
  }

  [Fact]
  public void SphericalAreaIsAdditiveOverStackedCells()
  {
    // No overlap term: tiling must sum, or the metric cannot be aggregated.
    double whole = GeometryMath.SphericalArea(Cell(0, 44, 1, 2));
    double parts =
      GeometryMath.SphericalArea(Cell(0, 44, 1, 1)) + GeometryMath.SphericalArea(Cell(0, 45, 1, 1));
    Rel(whole, parts, 1e-13, "stacked cells");
  }

  [Fact]
  public void SphericalAreaHemisphereIsHalfTheSphere()
  {
    // Exercises LongDeltaRadians at exactly +/-180 degrees.
    var ring = new List<(double X, double Y)> { (0, -90), (180, -90), (180, 90), (0, 90) };
    Rel(Hemisphere, GeometryMath.SphericalArea(ring), 1e-13, "hemisphere");

    double r = GeometryMath.EarthRadiusAuthalicMeters;
    Rel(2 * Math.PI * r * r, GeometryMath.SphericalArea(ring), 1e-13, "hemisphere vs 2piR^2");
  }

  [Fact]
  public void SphericalAreaIndependentOfLongitudePosition()
  {
    // Depends on the latitude band only, not where the cell sits in longitude.
    Rel(
      GeometryMath.SphericalArea(Cell(0, 44)),
      GeometryMath.SphericalArea(Cell(-93, 44)),
      1e-15,
      "lon 0 vs lon -93"
    );
  }

  [Fact]
  public void SphericalAreaIgnoresRingOrientation()
  {
    var ccw = Cell(0, 44);
    var cw = new List<(double X, double Y)>(ccw);
    cw.Reverse();
    Rel(GeometryMath.SphericalArea(ccw), GeometryMath.SphericalArea(cw), 1e-15, "CW vs CCW");
  }

  [Fact]
  public void SphericalAreaIsInvariantToTheClosingVertex()
  {
    // SpatialReader calls CloseRing(), so every ring arrives with the first
    // vertex repeated. A repeated point must contribute nothing.
    var open = Cell(0, 44);
    var closed = new List<(double X, double Y)>(open) { open[0] };
    Assert.Equal(GeometryMath.SphericalArea(open), GeometryMath.SphericalArea(closed));
  }

  [Fact]
  public void SphericalAreaCrossesTheAntimeridianWithoutExploding()
  {
    // The classic lon-wrap failure is a dateline cell reporting a huge area.
    var wrapped = new List<(double X, double Y)>
    {
      (179.5, 44),
      (-179.5, 44),
      (-179.5, 45),
      (179.5, 45),
    };
    Rel(CellAt44N, GeometryMath.SphericalArea(wrapped), 1e-10, "dateline cell");

    var raw = new List<(double X, double Y)> { (179.5, 44), (180.5, 44), (180.5, 45), (179.5, 45) };
    Rel(CellAt44N, GeometryMath.SphericalArea(raw), 1e-10, "cell using lon 180.5");
  }

  [Theory]
  [InlineData(2)]
  [InlineData(1)]
  [InlineData(0)]
  public void SphericalAreaOfDegenerateRingIsZero(int vertexCount)
  {
    var ring = Cell(0, 44).Take(vertexCount).ToList();
    Assert.Equal(0.0, GeometryMath.SphericalArea(ring));
  }

  [Fact]
  public void SphericalAreaUsesSuppliedRadius()
  {
    // Passing a radius in feet yields square feet; scaling must be R^2.
    double metres = GeometryMath.SphericalArea(Cell(0, 44));
    double feet = GeometryMath.SphericalArea(
      Cell(0, 44),
      GeometryMath.EarthRadiusAuthalicMeters / CrsInfo.MetersPerFoot
    );
    Rel(metres / (CrsInfo.MetersPerFoot * CrsInfo.MetersPerFoot), feet, 1e-12, "square feet");
  }

  [Fact]
  public void SphericalAreaIsNotPlanarShoelaceOnDegrees()
  {
    // The whole point of the addition: planar shoelace on (lon, lat) returns
    // square degrees, which is 1.0 for a 1x1 cell and useless downstream.
    var ring = Cell(0, 44);
    Assert.Equal(1.0, GeometryMath.Area(ring), 9);
    Assert.True(GeometryMath.SphericalArea(ring) > 1e9, "spherical area must be square metres");
  }

  // ------------------------------- accuracy claims made in the doc comments

  [Fact]
  public void SphericalAreaSphereVsWgs84EllipsoidWithinDocumentedError()
  {
    // Docstring: "the only error is sphere-vs-ellipsoid, about 0.20% low at 44N
    // and 0.63% low at 65N."
    double at44 =
      (GeometryMath.SphericalArea(Cell(0, 44)) - EllipsoidCellAt44N) / EllipsoidCellAt44N;
    double at65 =
      (GeometryMath.SphericalArea(Cell(0, 65)) - EllipsoidCellAt65N) / EllipsoidCellAt65N;

    Assert.InRange(at44 * 100, -0.30, -0.10); // measured -0.211%
    Assert.InRange(at65 * 100, -0.75, -0.55); // measured -0.661%, doc says 0.63%
  }

  [Fact]
  public void SphericalAreaExactOnTheSphereOnlyForMeridianAndParallelEdges()
  {
    // "Exact on a sphere" needs a qualifier: it holds for graticule cells. The
    // 1x1 triangle at 44N is exactly half the cell (the shared diagonal cancels,
    // so the result is additive), yet its true great-circle-edged area is larger,
    // because the code's straight edges are rhumb-ish, not great circles.
    var tri = new List<(double X, double Y)> { (0, 44), (1, 44), (0, 45) };
    double got = GeometryMath.SphericalArea(tri);

    // Compare against the computed cell, not the pinned literal: the literal is
    // truncated at 14 significant digits (3.2e-15), and the two rings sum their
    // terms in different orders (~8e-15). Both are float noise, not error.
    Rel(GeometryMath.SphericalArea(Cell(0, 44)) / 2.0, got, 1e-13, "triangle is half the cell");

    double errorPct = (got - TrueTriangleAt44N) / TrueTriangleAt44N * 100.0;
    Assert.InRange(errorPct, -0.90, -0.80); // ~-0.85% at 1 degree of edge length
  }

  [Fact]
  public void SphericalAreaGeodesicEdgeErrorIsNegligibleAtFootprintScale()
  {
    // The -0.85% above is a 1-degree-edge artefact. At NSI footprint scale the
    // same defect is far below the sphere-vs-ellipsoid term, so it is not worth
    // paying for a geodesic polygon. This test locks that reasoning in place.
    const double lat = 44.0;
    double degLat = 1.0 / 111195.0;
    double degLon = 1.0 / (111195.0 * Math.Cos(lat * Math.PI / 180.0));

    double[] metresPerLeg = { 500.0, 100.0, 10.0, 1.0 };
    double[] maxErrorPct = { 5e-3, 1e-3, 1e-4, 1e-5 };

    for (int i = 0; i < metresPerLeg.Length; i++)
    {
      double m = metresPerLeg[i];
      var tri = new List<(double X, double Y)>
      {
        (-76.0, lat),
        (-76.0 + degLon * m, lat),
        (-76.0, lat + degLat * m),
      };
      // Reference: the true spherical excess of the same triangle scales as the
      // measured 1-degree error divided by m^2 (excess error is quadratic in edge
      // length), so bound it rather than recompute l'Huilier here.
      double errorPct = Math.Abs(
        (GeometryMath.SphericalArea(tri) - ExpectedFootprintArea(tri))
          / ExpectedFootprintArea(tri)
          * 100.0
      );
      Assert.True(
        errorPct <= maxErrorPct[i],
        $"{m} m leg: geodesic-edge error {errorPct:E3}% exceeds {maxErrorPct[i]:E3}%"
      );
    }
  }

  /// <summary>
  /// l'Huilier spherical excess, an independent implementation used only as a
  /// reference for the accuracy tests above.
  /// </summary>
  private static double ExpectedFootprintArea(List<(double X, double Y)> ring)
  {
    Assert.Equal(3, ring.Count);
    var v = ring.Select(ToUnitVector).ToList();
    double a = AngleBetween(v[1], v[2]);
    double b = AngleBetween(v[0], v[2]);
    double c = AngleBetween(v[0], v[1]);
    double s = (a + b + c) / 2.0;
    double t =
      Math.Tan(s / 2) * Math.Tan((s - a) / 2) * Math.Tan((s - b) / 2) * Math.Tan((s - c) / 2);
    double excess = 4.0 * Math.Atan(Math.Sqrt(Math.Max(t, 0.0)));
    return excess * GeometryMath.EarthRadiusAuthalicMeters * GeometryMath.EarthRadiusAuthalicMeters;
  }

  private static (double X, double Y, double Z) ToUnitVector((double X, double Y) p)
  {
    double lon = p.X * Math.PI / 180.0;
    double lat = p.Y * Math.PI / 180.0;
    double c = Math.Cos(lat);
    return (c * Math.Cos(lon), c * Math.Sin(lon), Math.Sin(lat));
  }

  private static double AngleBetween(
    (double X, double Y, double Z) u,
    (double X, double Y, double Z) v
  )
  {
    double dot = u.X * v.X + u.Y * v.Y + u.Z * v.Z;
    double cx = u.Y * v.Z - u.Z * v.Y;
    double cy = u.Z * v.X - u.X * v.Z;
    double cz = u.X * v.Y - u.Y * v.X;
    return Math.Atan2(Math.Sqrt(cx * cx + cy * cy + cz * cz), dot);
  }

  [Fact]
  public void SphericalAreaOfPoleEnclosingRingIsWrongDocumentedLimitation()
  {
    // A ring whose vertices all sit on one parallel and which wraps the pole is
    // not a boundary the lon/sin-lat shoelace can represent: it sweeps the whole
    // longitude range and returns an area close to the whole sphere. Pinning the
    // magnitude so a future fix is visible, and so nobody assumes polar data works.
    var cap = new List<(double X, double Y)> { (0, 80), (120, 80), (240, 80) };
    double r = GeometryMath.EarthRadiusAuthalicMeters;
    double trueCap = 2 * Math.PI * r * r * (1 - Math.Sin(80 * Math.PI / 180.0));

    double got = GeometryMath.SphericalArea(cap);
    Assert.True(
      got / trueCap > 60,
      $"expected the known pole-enclosing failure, got ratio {got / trueCap}"
    );
  }

  // ========================================================= SphericalDistance

  [Fact]
  public void SphericalDistanceMatchesGreatCircleGoldens()
  {
    Rel(
      QuarterMeridian,
      GeometryMath.SphericalDistance((0, 0), (0, 90)),
      1e-12,
      "quarter meridian"
    );
    Rel(OneDegree, GeometryMath.SphericalDistance((0, 0), (0, 1)), 1e-12, "one degree of latitude");
    Rel(
      OneDegree,
      GeometryMath.SphericalDistance((0, 0), (1, 0)),
      1e-12,
      "one degree of longitude"
    );
    Rel(QuarterMeridian * 2, GeometryMath.SphericalDistance((0, 0), (0, 180)), 1e-12, "antipodal");
  }

  [Fact]
  public void SphericalDistanceScalesLongitudeByCosLatitude()
  {
    // 1 degree of longitude at 60N is half one at the equator. Not bit-exact:
    // the great circle between two points on a parallel cuts poleward, so the
    // true value is a hair under cos(lat) * dLon.
    double at60 = GeometryMath.SphericalDistance((0, 60), (1, 60));
    double naive = OneDegree * Math.Cos(60 * Math.PI / 180.0);
    Rel(naive, at60, 1e-5, "1 degree of longitude at 60N");
    Rel(55597.0106153172, at60, 1e-12, "1 degree of longitude at 60N, pinned");
  }

  [Fact]
  public void SphericalDistanceIsAccurateAtSubMetreSeparation()
  {
    // Haversine rather than the spherical law of cosines: this is the regime that
    // matters between adjacent footprint vertices, and where acos-based formulas
    // lose all precision.
    Rel(1e-8 * OneDegree, GeometryMath.SphericalDistance((0, 0), (1e-8, 0)), 1e-9, "1.1 mm");
    Rel(1e-7 * OneDegree, GeometryMath.SphericalDistance((0, 0), (1e-7, 0)), 1e-9, "1.1 cm");
    Assert.False(double.IsNaN(GeometryMath.SphericalDistance((0, 0), (1e-12, 0))));
  }

  [Fact]
  public void SphericalDistanceHandlesTheAntimeridianAndIsSymmetric()
  {
    // 0.2 degrees straddling 180, not 359.8 degrees the wrong way round.
    Rel(
      0.2 * OneDegree,
      GeometryMath.SphericalDistance((-179.9, 0), (179.9, 0)),
      1e-10,
      "dateline crossing"
    );
    Assert.Equal(
      GeometryMath.SphericalDistance((5, 44), (6, 45)),
      GeometryMath.SphericalDistance((6, 45), (5, 44))
    );
    Assert.Equal(0.0, GeometryMath.SphericalDistance((5, 44), (5, 44)), 9);
  }

  // ======================================================== SphericalPerimeter

  [Fact]
  public void SphericalPerimeterOfClosedCellSumsItsEdges()
  {
    // 1x1 cell on the equator: three 1-degree edges plus the top edge shortened
    // by one degree of latitude's worth of longitude convergence.
    Rel(444763.3829584258, GeometryMath.SphericalPerimeter(Cell(0, 0)), 1e-12, "perimeter");
  }

  [Fact]
  public void SphericalPerimeterIsInvariantToTheClosingVertex()
  {
    var open = Cell(0, 44);
    var closed = new List<(double X, double Y)>(open) { open[0] };
    Rel(
      GeometryMath.SphericalPerimeter(open),
      GeometryMath.SphericalPerimeter(closed),
      1e-15,
      "perimeter with repeated vertex"
    );
  }

  [Fact]
  public void SphericalPerimeterClosesTheRingEvenForAnOpenPolyline()
  {
    // Characterisation test, and a contradiction to pin: the docstring says
    // "length ... of a (lon, lat) degree polyline", but the implementation
    // indexes with % pts.Count, so a two-vertex line is measured twice round
    // trip. If this ever starts failing, the docstring or the code changed.
    var twoPointLine = new List<(double X, double Y)> { (0, 0), (1, 0) };
    Rel(
      2 * OneDegree,
      GeometryMath.SphericalPerimeter(twoPointLine),
      1e-12,
      "doubled 2-point line"
    );
    Assert.Equal(2, twoPointLine.Count);
  }

  [Fact]
  public void LengthMetersMeansTheSameThingInBothCrsKinds()
  {
    var part2 = new Part(PartType.Polyline);
    part2.AddVertex(new Vertex(0, 0));
    part2.AddVertex(new Vertex(1, 0));
    part2.Seal();
    var geographic = Geographic(ShapeType.Line);
    geographic.AddFeature(IntoFeature(part2));

    var projected = Projected(1.0);
    var planar = new Part(PartType.Polyline);
    planar.AddVertex(new Vertex(0, 0));
    planar.AddVertex(new Vertex(OneDegree, 0));
    projected.AddFeature(IntoFeature(planar));
    planar.Seal();
    Rel(
      projected.Features[0].Parts[0].LengthMeters!.Value,
      geographic.Features[0].Parts[0].LengthMeters!.Value,
      1e-9,
      "same one-edge line, two CRS kinds"
    );
  }

  private static Feature IntoFeature(Part part)
  {
    var f = new Feature();
    f.AddPart(part);
    return f;
  }

  // ============================================ SphericalPointToSegmentDistance

  [Fact]
  public void PointToSegmentWhenFootIsInsideSegmentIsTheCrossTrackDistance()
  {
    var a = ((double, double))(0.0, 40.0);
    var b = ((double, double))(0.012, 40.0);
    var p = ((double, double))(0.006, 40.009); // foot lands mid-segment

    double got = GeometryMath.SphericalPointToSegmentDistance(p, a, b);
    Rel(1000.738516, got, 1e-9, "cross-track distance");

    // Must be strictly nearer than either endpoint.
    Assert.True(got < GeometryMath.SphericalDistance(p, a));
    Assert.True(got < GeometryMath.SphericalDistance(p, b));
  }

  [Fact]
  public void PointToSegmentWhenFootIsBeyondTheFarEndpointReturnsDistanceToB()
  {
    var a = ((double, double))(0.0, 40.0);
    var b = ((double, double))(0.012, 40.0);
    var p = ((double, double))(0.016, 40.009);

    Rel(
      GeometryMath.SphericalDistance(p, b),
      GeometryMath.SphericalPointToSegmentDistance(p, a, b),
      1e-12,
      "distance to b"
    );
  }

  [Fact(
    Skip = "The outside-the-segment guard only tests the far side. deltaAT = acos(cos(d13)/cos(dXT)) is always >= 0, so a perpendicular foot BEHIND 'a' is never detected and the function returns the cross-track distance instead of the distance to 'a' -- understating it. Fix: reject when cos(bearing(a,p) - bearing(a,b)) < 0 and return R * delta13."
  )]
  public void PointToSegmentWhenFootIsBehindTheNearEndpointReturnsDistanceToA()
  {
    // Segment runs ~1.3 km due east; p sits ~0.4 km west of 'a' and ~1 km north,
    // so the nearest point on the segment is 'a' itself.
    var a = ((double, double))(0.0, 40.0);
    var b = ((double, double))(0.012, 40.0);
    var p = ((double, double))(-0.004, 40.009);

    double got = GeometryMath.SphericalPointToSegmentDistance(p, a, b);
    Rel(GeometryMath.SphericalDistance(p, a), got, 1e-9, "distance to a");

    // Reference by dense sampling along the arc agrees with dist(p, a) = 1057.16 m;
    // the implementation returns the 1000.79 m cross-track value instead.
    Assert.True(got >= GeometryMath.SphericalDistance(p, a) - 1e-6);
  }

  [Fact]
  public void PointToSegmentOfDepenerateSegmentIsTheDistanceToThePoint()
  {
    var a = ((double, double))(0.0, 40.0);
    var p = ((double, double))(0.001, 40.001);
    Rel(
      GeometryMath.SphericalDistance(p, a),
      GeometryMath.SphericalPointToSegmentDistance(p, a, a),
      1e-12,
      "degenerate segment"
    );
  }

  [Fact]
  public void PointToSegmentIsNeverGreaterThanOrZero()
  {
    var a = ((double, double))(-93.0, 44.0);
    var b = ((double, double))(-92.99, 44.002);
    foreach (
      var p in new[]
      {
        ((double, double))(-93.0, 44.0),
        ((double, double))(-92.99, 44.002),
        ((double, double))(-120.0, 60.0),
        ((double, double))(179.99, -33.5),
      }
    )
    {
      double d = GeometryMath.SphericalPointToSegmentDistance(p, a, b);
      Assert.False(double.IsNaN(d), "NaN distance");
      Assert.True(d >= 0.0, "negative distance");
      Assert.True(
        d <= GeometryMath.SphericalDistance(p, a) + 1e-6,
        $"distance {d} exceeds the distance to endpoint a"
      );
    }
  }

  // ============================================================ radius constants

  [Fact]
  public void EarthRadiusConstantsAreTheDocumentedSphereRadii()
  {
    Assert.Equal(6371007.181, GeometryMath.EarthRadiusAuthalicMeters, 3);
    Assert.Equal(6371008.7714, GeometryMath.EarthRadiusMeanMeters, 4);

    // Authalic is the equal-area radius, so it must be the smaller of the two,
    // and both must sit within a few km of the 6371 km nominal mean.
    Assert.True(GeometryMath.EarthRadiusAuthalicMeters < GeometryMath.EarthRadiusMeanMeters);
    Assert.InRange(GeometryMath.EarthRadiusMeanMeters, 6370000.0, 6372000.0);
  }

  [Fact]
  public void EarthRadiusFeetIsNotTheRadiusItsDocstringClaims()
  {
    // Docstring: "AlexRyanUSACE GeospatialTools radius: 6,371,000 m in feet."
    // 6,371,000 m in international feet is 20,902,230.97. The constant is
    // 20,925,524.9, which is 6,378,099.99 m -- within 37 m of the WGS84
    // EQUATORIAL radius, the very radius the SphericalArea docstring warns
    // "inflates them ~0.22%". So callers following the docstring's advice and
    // passing this for square feet get exactly the error the class warns about.
    const double claimed = 20925524.9;
    Assert.Equal(claimed, GeometryMath.EarthRadiusFeet, 1);

    double impliedMetres = GeometryMath.EarthRadiusFeet * CrsInfo.MetersPerFoot;
    Assert.Equal(6378099.99, impliedMetres, 2);

    // Consequence, stated as a number rather than a comment: areas computed with
    // EarthRadiusFeet and converted back to square metres are ~0.22% high.
    double squareFeet = GeometryMath.SphericalArea(Cell(0, 44), GeometryMath.EarthRadiusFeet);
    double backToMetres = squareFeet * CrsInfo.MetersPerFoot * CrsInfo.MetersPerFoot;
    double inflationPct = (backToMetres - CellAt44N) / CellAt44N * 100.0;
    Assert.InRange(inflationPct, 0.20, 0.25);
  }

  [Fact(
    Skip = "EarthRadiusFeet is the equatorial radius expressed in feet, but is documented as 6,371,000 m in feet and is the value the SphericalArea docstring tells callers to pass for square feet. It should be EarthRadiusAuthalicMeters / MetersPerFoot = 20,902,254.53, or be renamed to EarthRadiusEquatorialFeet and excluded from area use."
  )]
  public void EarthRadiusFeetIsTheAuthalicRadiusInFeet()
  {
    Rel(
      GeometryMath.EarthRadiusAuthalicMeters / CrsInfo.MetersPerFoot,
      GeometryMath.EarthRadiusFeet,
      1e-6,
      "authalic radius in feet"
    );
  }

  // ====================================== Part / Feature derived-metric plumbing

  [Fact]
  public void PartAreaSquareMetersUsesTheSphereForGeographicRings()
  {
    var fc = Geographic();
    var part = Ring(Cell(0, 44), exterior: true);
    fc.AddFeature(IntoFeature(part));

    Rel(CellAt44N, part.AreaSquareMeters!.Value, 1e-12, "geographic area");

    // And it must not be the cached planar figure, which is square degrees.
    Assert.Equal(1.0, part.Area!.Value, 9);
    Assert.NotEqual(part.Area, part.AreaSquareMeters!.Value);
  }

  [Fact]
  public void PartAreaSquareMetersScalesByTheDeclaredUnitWhenProjected()
  {
    // A 1000 m square must read 1,000,000 m2, and the same numbers declared in
    // US survey feet must read the survey-foot conversion -- no reprojection.
    var square = new List<(double X, double Y)> { (0, 0), (1000, 0), (1000, 1000), (0, 1000) };

    var metres = Projected(1.0);
    var metrePart = Ring(square, true);
    metres.AddFeature(IntoFeature(metrePart));
    Rel(1_000_000.0, metrePart.AreaSquareMeters!.Value, 1e-12, "metres");

    double feet = 1000.0 / CrsInfo.MetersPerFoot;
    var footSquare = new List<(double X, double Y)> { (0, 0), (feet, 0), (feet, feet), (0, feet) };
    var imperial = Projected(CrsInfo.MetersPerFoot, LinearUnit.Foot);
    var footPart = Ring(footSquare, true);
    imperial.AddFeature(IntoFeature(footPart));
    Rel(1_000_000.0, footPart.AreaSquareMeters!.Value, 1e-9, "international feet");

    double uss = 1000.0 / CrsInfo.MetersPerUsSurveyFoot;
    var usssSquare = new List<(double X, double Y)> { (0, 0), (uss, 0), (uss, uss), (0, uss) };
    var usss = Projected(CrsInfo.MetersPerUsSurveyFoot, LinearUnit.UsSurveyFoot);
    var usssPart = Ring(usssSquare, true);
    usss.AddFeature(IntoFeature(usssPart));
    Rel(1_000_000.0, usssPart.AreaSquareMeters!.Value, 1e-9, "US survey feet");
  }

  [Fact]
  public void PartDerivedMetricsAreNullWhenTheCrsIsUnknown()
  {
    // The point of CrsKind.Unknown: refuse rather than guess a unit.
    var fc = new FeatureCollection { ShapeType = ShapeType.Polygon };
    var part = Ring(Cell(0, 44), exterior: true);
    fc.AddFeature(IntoFeature(part));

    Assert.Null(part.AreaSquareMeters);
    Assert.Null(part.LengthMeters);
    Assert.Equal(CrsKind.Unknown, part.Crs.Kind);
    Assert.True(part.Area > 0, "planar cache is still computed");
  }

  [Fact]
  public void PartCrsResolvesThroughTheOwnerChainAndIsUnknownWhenDetached()
  {
    var detached = Ring(Cell(0, 44), exterior: true);
    Assert.Equal(CrsKind.Unknown, detached.Crs.Kind);

    var fc = Geographic();
    var part = Ring(Cell(0, 44), exterior: true);
    var f = IntoFeature(part);
    fc.AddFeature(f);

    Assert.Same(fc.Crs, f.Crs);
    Assert.Same(fc.Crs, part.Crs);
    Assert.Equal(4326, part.Crs.EpsgCode);
  }

  [Fact]
  public void FeatureAreaSquareMetersSubtractsHoleParts()
  {
    var fc = Geographic();
    var shell = Ring(Cell(0, 44, 1, 1), exterior: true);
    var hole = Ring(Cell(0.25, 44.25, 0.1, 0.1), exterior: false);
    Polygon(fc, shell, hole);

    double expected = CellAt44N - 8.8490668742e7;
    Assert.False(shell.IsHole);
    Assert.True(hole.IsHole); // "IsHole was false" beats "off by 1.01%"
    Rel(expected, fc.Features[0].AreaSquareMeters!.Value, 1e-10, "shell minus hole");
    Rel(8730268160.9622, fc.Features[0].AreaSquareMeters!.Value, 1e-10, "pinned");
  }

  [Fact]
  public void FeatureAreaSquareMetersIsNullWithoutGeometryOrCrs()
  {
    var noGeometry = new FeatureCollection { ShapeType = ShapeType.Polygon };
    noGeometry.Crs = Geographic().Crs;
    var empty = new Feature();
    noGeometry.AddFeature(empty);
    Assert.Null(empty.AreaSquareMeters);

    var noCrs = new FeatureCollection { ShapeType = ShapeType.Polygon };
    var f = IntoFeature(Ring(Cell(0, 44), exterior: true));
    noCrs.AddFeature(f);
    Assert.Null(f.AreaSquareMeters);
  }

  [Fact]
  public void FeatureAreaSquareMetersIsRealisticForAFootprintSizedCell()
  {
    // ~1 km x ~1 km at 44N, in Minnesota's longitude: about 0.89 km2, i.e. the
    // order of magnitude an NSI structure footprint's parent parcel implies.
    var fc = Geographic();
    var part = Ring(Cell(-93.0, 44.0, 0.01, 0.01), exterior: true);
    fc.AddFeature(IntoFeature(part));

    double acres = part.AreaSquareMeters!.Value / 4046.8564224;
    Rel(889341.19916397, part.AreaSquareMeters!.Value, 1e-12, "footprint cell");
    Assert.InRange(acres, 219.0, 220.5);
  }

  // ================================================================== CrsInfo

  [Fact]
  public void CrsInfoUnitToMetersOrMeterDefaultsToMetreForUnlabelledProjectedData()
  {
    var unlabelled = new CrsInfo { Kind = CrsKind.Projected, Unit = LinearUnit.Unknown };
    Assert.Equal(0.0, unlabelled.UnitToMeters);
    Assert.Equal(1.0, unlabelled.UnitToMetersOrMeter);
  }

  [Fact]
  public void CrsInfoUnknownIsNotMetreDefaulting()
  {
    // The guard that keeps unknown-CRS areas null rather than silently metres.
    Assert.Equal(CrsKind.Unknown, CrsInfo.Unknown.Kind);
    Assert.Equal(0.0, CrsInfo.Unknown.UnitToMeters);
  }

  [Fact]
  public void CrsInfoFootUnitsAreExactlyTheDefinedConversions()
  {
    Assert.Equal(0.3048, CrsInfo.MetersPerFoot, 12);
    Assert.Equal(1200.0 / 3937.0, CrsInfo.MetersPerUsSurveyFoot, 15);

    // The two feet differ by ~6.1e-7 m, i.e. ~2 ppm of length and ~4 ppm of area:
    // small, but systematic, and the reason the enum keeps them distinct.
    double delta = CrsInfo.MetersPerUsSurveyFoot - CrsInfo.MetersPerFoot;
    Rel(6.096012191703e-7, delta, 1e-6, "survey foot minus international foot");
  }

  [Fact]
  public void CrsInfoToStringNamesTheEpsgCodeWhenKnown()
  {
    Assert.Equal(
      "EPSG:4326 (Geographic)",
      new CrsInfo { Kind = CrsKind.Geographic, EpsgCode = 4326 }.ToString()
    );
    Assert.Equal("CRS unknown", CrsInfo.Unknown.ToString());
    Assert.Equal(
      "Projected, Foot",
      new CrsInfo { Kind = CrsKind.Projected, Unit = LinearUnit.Foot }.ToString()
    );
  }
}

