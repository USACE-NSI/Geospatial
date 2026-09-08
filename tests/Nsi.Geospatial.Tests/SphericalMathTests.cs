using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Projections;
using Xunit;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// Spherical (geographic) geometry on the unit sphere. Pure math, no GDAL.
/// All expectations are derived from closed-form sphere expressions rather than
/// literals, so the test validates the implementation against independent maths.
/// </summary>
public class SphericalMathTests
{
  private const double Deg2Rad = Math.PI / 180.0;

  /// <summary>Closed-form spherical zone area: R^2 * dLon * (sin lat2 - sin lat1).</summary>
  private static double ZoneArea(double radius, double dLonDeg, double lat1Deg, double lat2Deg) =>
    radius
    * radius
    * (dLonDeg * Deg2Rad)
    * (Math.Sin(lat2Deg * Deg2Rad) - Math.Sin(lat1Deg * Deg2Rad));

  /// <summary>Axis-aligned lon/lat rectangle, CCW from (lonMin,latMin).</summary>
  private static List<(double X, double Y)> LonLatCell(
    double lonMin,
    double lonMax,
    double latMin,
    double latMax
  ) => [(lonMin, latMin), (lonMax, latMin), (lonMax, latMax), (lonMin, latMax)];

  private static void Rel(double expected, double actual, double tol = 1e-9)
  {
    double diff = Math.Abs(expected - actual);
    Assert.True(
      diff <= Math.Abs(expected) * tol,
      $"expected {expected:R}, actual {actual:R}, rel diff {diff / Math.Abs(expected):E}"
    );
  }

  // ------------------------------------------------------------------ area

  [Fact]
  public void SphericalAreaOneDegreeCellAtEquatorMatchesZoneFormula()
  {
    // For an axis-aligned cell the trapezoid-in-sine sum is exact, not an
    // approximation: dLon is non-zero only on the two horizontal edges, where
    // latitude is constant, so (sin1+sin2)/2 == sin(lat) exactly.
    double expected = ZoneArea(GeometryMath.EarthRadiusAuthalicMeters, 1.0, 0.0, 1.0);

    Rel(expected, GeometryMath.SphericalArea(LonLatCell(0, 1, 0, 1)));
  }

  [Fact]
  public void SphericalAreaOneDegreeCellAt44NIsSmallerThanAtEquator()
  {
    double at44 = ZoneArea(GeometryMath.EarthRadiusAuthalicMeters, 1.0, 44.0, 45.0);

    Rel(at44, GeometryMath.SphericalArea(LonLatCell(0, 1, 44, 45)));

    // A degree of longitude shrinks with cos(latitude). This also pins the axis
    // order: if the sine term were applied to X instead of Y the cell would
    // evaluate to the equator value and this would fail.
    double atEquator = ZoneArea(GeometryMath.EarthRadiusAuthalicMeters, 1.0, 0.0, 1.0);
    Assert.True(at44 < atEquator);
    Rel(
      atEquator * Math.Cos(44.5 * Deg2Rad),
      GeometryMath.SphericalArea(LonLatCell(0, 1, 44, 45)),
      2e-3
    );
  }

  [Fact]
  public void SphericalAreaCrossesAntimeridian()
  {
    // Straddles 180 degrees: 1 degree wide, 1 degree tall at the equator. Without
    // wrapping dLon into (-pi,pi] the first edge reads as -359 degrees and the
    // result is off by three orders of magnitude.
    var ring = LonLatCell(179.5, -179.5, 0, 1);
    double expected = ZoneArea(GeometryMath.EarthRadiusAuthalicMeters, 1.0, 0.0, 1.0);

    Rel(expected, GeometryMath.SphericalArea(ring), 1e-9);
  }

  [Fact]
  public void SphericalAreaIsWindingIndependent()
  {
    var cw = LonLatCell(0, 1, 44, 45);
    var ccw = cw.AsEnumerable().Reverse().ToList();

    Rel(GeometryMath.SphericalArea(cw), GeometryMath.SphericalArea(ccw), 1e-12);
  }

  [Theory]
  [InlineData(0)]
  [InlineData(1)]
  [InlineData(2)]
  public void SphericalAreaDegenerateRingReturnsZero(int count)
  {
    var ring = LonLatCell(0, 1, 44, 45).Take(count).ToList();
    Assert.Equal(0.0, GeometryMath.SphericalArea(ring));
  }

  [Fact]
  public void SphericalAreaFeetRadiusScalesByFeetPerMetreSquared()
  {
    var ring = LonLatCell(0, 1, 44, 45);
    double sqMeters = GeometryMath.SphericalArea(ring);
    double sqFeet = GeometryMath.SphericalArea(
      ring,
      GeometryMath.EarthRadiusAuthalicMeters / 0.3048
    );
    // A radius expressed in feet produces an area in square feet, so the
    // conversion is by feetPerMetre^2 -- i.e. divide the square-metre figure by
    // 0.3048^2. Asserting this direction matters: the inverse error is a factor
    // of 115.86 and would otherwise look like a plausible-sized polygon.
    Rel(sqMeters / (0.3048 * 0.3048), sqFeet, 1e-12);
  }

  // -------------------------------------------------------------- distance

  [Fact]
  public void SphericalDistanceOneDegreeOfLatitudeIsRadiusTimesOneDegree()
  {
    // On a sphere this is exact at every latitude, which makes it a good anchor.
    double expected = GeometryMath.EarthRadiusMeanMeters * Deg2Rad;

    Rel(expected, GeometryMath.SphericalDistance((0, 0), (0, 1)), 1e-12);
    Rel(expected, GeometryMath.SphericalDistance((-73, 44), (-73, 45)), 1e-12);
  }

  [Fact]
  public void SphericalDistanceOneDegreeOfLongitudeScalesWithCosLatitude()
  {
    double equator = GeometryMath.EarthRadiusMeanMeters * Deg2Rad;

    Rel(equator, GeometryMath.SphericalDistance((0, 0), (1, 0)), 1e-9);
    // Great-circle vs parallel-of-latitude arc differ at O(dLon^3); 1 degree is
    // small enough that 0.1% is a generous bound.
    Rel(
      equator * Math.Cos(44 * Deg2Rad),
      GeometryMath.SphericalDistance((-73, 44), (-72, 44)),
      1e-3
    );
  }

  [Fact]
  public void SphericalDistanceIsSymmetricAndZeroForIdenticalPoints()
  {
    var a = (-73.21, 44.475);
    var b = (-73.2096, 44.4752);

    Rel(GeometryMath.SphericalDistance(a, b), GeometryMath.SphericalDistance(b, a), 1e-12);
    Assert.Equal(0.0, GeometryMath.SphericalDistance(a, a), 12);
  }

  [Fact]
  public void SphericalDistanceStaysAccurateAtSubMetreSeparations()
  {
    // The reason the implementation uses haversine rather than the spherical law
    // of cosines: the law of cosines loses all precision below about a metre.
    var a = (-73.21, 44.475);
    double oneMetreLon =
      1.0 / (GeometryMath.EarthRadiusMeanMeters * Math.Cos(44.475 * Deg2Rad)) / Deg2Rad;
    var b = (a.Item1 + oneMetreLon, a.Item2);

    double d = GeometryMath.SphericalDistance(a, b);
    Assert.True(d > 0.9 && d < 1.1, $"expected ~1 m, got {d:E}");
  }

  [Fact]
  public void SphericalPerimeterIsTheSumOfGreatCircleEdges()
  {
    var ring = LonLatCell(0, 1, 44, 45);
    double expected = 0;
    for (int i = 0; i < ring.Count; i++)
    {
      expected += GeometryMath.SphericalDistance(ring[i], ring[(i + 1) % ring.Count]);
    }

    Rel(expected, GeometryMath.SphericalPerimeter(ring), 1e-12);
  }

  [Fact]
  public void SphericalPerimeterCloseRingDoesNotDoubleTheClosingEdge()
  {
    // CloseRing appends the first vertex, so a ring handed to Perimeter already
    // carries its closing edge. Asserting the closed ring equals the open one
    // pins that contract; if a caller passes an explicitly closed ring to an
    // open-ring API the double count is silent.
    var open = LonLatCell(0, 1, 44, 45);
    var closed = new List<(double X, double Y)>(open) { open[0] };

    Rel(GeometryMath.SphericalPerimeter(open), GeometryMath.SphericalPerimeter(closed), 1e-12);
  }

  // -------------------------------------------------- point-to-segment

  [Fact]
  public void PointToSegmentEastOfNorthSouthSegmentIsOneDegreeOfLongitude()
  {
    // Segment runs north along lon 0 from the equator; the perpendicular from
    // (1E, 0N) is the equator itself, meeting the segment at its endpoint.
    double expected = GeometryMath.EarthRadiusMeanMeters * Deg2Rad;

    Rel(expected, GeometryMath.SphericalPointToSegmentDistance((1, 0), (0, 0), (0, 1)), 1e-9);
  }

  [Fact]
  public void PointToSegmentBeyondSegmentEndFallsBackToNearestEndpoint()
  {
    // (0,2) is collinear with the segment and past its far end, so the answer is
    // the distance to (0,1) -- one degree of latitude.
    double expected = GeometryMath.EarthRadiusMeanMeters * Deg2Rad;

    Rel(expected, GeometryMath.SphericalPointToSegmentDistance((0, 2), (0, 0), (0, 1)), 1e-9);
  }

  [Fact]
  public void PointToSegmentDegenerateSegmentIsDistanceToPoint()
  {
    var a = (-73.21, 44.475);
    var p = (-73.2096, 44.4752);

    Rel(
      GeometryMath.SphericalDistance(p, a),
      GeometryMath.SphericalPointToSegmentDistance(p, a, a),
      1e-12
    );
  }

  [Fact]
  public void PointToSegmentIsNeverGreaterThanDistanceToEitherEnd()
  {
    var p = (1.0, 0.5);
    var a = (0.0, 0.0);
    var b = (0.0, 1.0);

    double cross = GeometryMath.SphericalPointToSegmentDistance(p, a, b);

    Assert.True(cross > 0);
    Assert.True(cross <= GeometryMath.SphericalDistance(p, a) + 1e-9);
    Assert.True(cross <= GeometryMath.SphericalDistance(p, b) + 1e-9);
  }
}

