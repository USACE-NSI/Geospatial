using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Io;
using Nsi.Geospatial.Projections;
using OSGeo.OSR;
using Xunit;

namespace Nsi.Geospatial.Io.Tests;

/// <summary>
/// T-5, P-05. Same geometry, through the driver. Three tests because they fail for unrelated
/// reasons: the area is our claim, the hole flags are our claim, and the ring ORDER is the
/// driver's -- only the first two should be able to fail on a library change.
///
/// Rings are rebuilt here rather than reused from TestFeatures, which is internal to the
/// core test assembly. Areas and derivation are documented in FeatureAreaTests.
/// </summary>
public class HoleRoundTripTests
{
  static readonly (double X, double Y)[] Exterior =
  [
    (0, 0),
    (100, 0),
    (100, 40),
    (40, 40),
    (40, 100),
    (0, 100),
  ];
  static readonly (double X, double Y)[] Triangle = [(5, 5), (25, 5), (5, 25)];
  static readonly (double X, double Y)[] Square = [(5, 60), (15, 60), (15, 70), (5, 70)];

  [Fact]
  public void PolygonHoleIsSubtractedAfterRead()
  {
    var read = RoundTrip(exteriorOnly: false);
    var feature = Assert.Single(read.FeatureSet); // one feature came back

    Assert.Equal(CrsKind.Projected, read.Crs.Kind); // premises: if these fail the areas
    Assert.Equal(1.0, read.Crs.UnitToMeters, 9); // below are vacuous, not merely wrong
    Assert.Equal(2, feature.Parts.Count(p => p.IsHole)); // order-insensitive
    Assert.Equal(1, feature.Parts.Count(p => !p.IsHole));
    Assert.Equal(6100d, feature.AreaSquareMeters!.Value, 6);
  }

  /// <summary>The control through the driver. If the exterior comes back as anything other
  /// than 6400, the subtraction test above is measuring a ring-reading bug and not a hole
  /// bug -- which is a different row, and would make this file's headline claim false.</summary>
  [Fact]
  public void ExteriorOnlyFeatureSurvivesRoundTrip()
  {
    var read = RoundTrip(exteriorOnly: true);

    var feature = Assert.Single(read.FeatureSet);
    var part = Assert.Single(feature.Parts);
    Assert.False(part.IsHole);
    Assert.Equal(6400d, feature.AreaSquareMeters!.Value, 6);
  }

  /// <summary>
  /// T-5's literal "IsHole == true on ring 1" claim, kept separate because it is the FRAGILE
  /// one. SpatialReader derives the flag positionally (`IsHole = r > 0`) and SpatialWriter
  /// emits Parts order without consulting IsHole, so nothing in this repository decides ring
  /// order -- OGR does, from winding. If a GDAL upgrade reorders rings THIS is the test that
  /// breaks, and the two above keep guarding the arithmetic. Fix the expectation here; do not
  /// touch the area tests.
  /// </summary>
  [Fact]
  public void ExteriorIsRingZeroAndHolesFollowAfterRead()
  {
    var read = RoundTrip(exteriorOnly: false);
    var parts = read.FeatureSet[0].Parts;

    Assert.False(parts[0].IsHole);
    Assert.True(parts[1].IsHole);
    Assert.True(parts[2].IsHole);
  }

  static Features RoundTrip(bool exteriorOnly)
  {
    // .prj comes from Crs.Wkt and nothing else. Without WKT the writer emits no .prj,
    // CrsInspector reports Unknown, AreaSquareMeters returns null, and every assertion in
    // this file passes while proving nothing. Resolved through OSR rather than hand-authored
    // so the WKT is authoritative; metre units, so UnitToMeters is 1 and 6400 planar units
    // are exactly 6400 square metres.
    var srs = new SpatialReference(null);
    try
    {
      Assert.Equal(0, srs.SetFromUserInput("EPSG:32616"));
      srs.ExportToWkt(out string wkt, Array.Empty<string>());

      var write = new Features
      {
        Name = "holes",
        ShapeType = ShapeType.Polygon,
        Crs = new CrsInfo { Kind = CrsKind.Projected, Wkt = wkt },
      };
      var feature = new Feature();
      feature.AddPart(Ring(Exterior, isHole: false));
      if (!exteriorOnly)
      {
        feature.AddPart(Ring(Triangle, isHole: true));
        feature.AddPart(Ring(Square, isHole: true));
      }
      write.AddFeature(feature);

      var path = Path.Combine(Path.GetTempPath(), $"t5-{Guid.NewGuid():N}.shp");
      new SpatialWriter().Write(write, path);
      return new SpatialReader().Read(path);
    }
    finally
    {
      srs.Dispose();
    }
  }

  /// <summary>Sealed and CCW, matching TestFeatures.Ring. The writer closes rings itself, so
  /// 6 vertices go out and 7 come back; no test here asserts counts -- that is T-3.</summary>
  static Part Ring((double X, double Y)[] points, bool isHole)
  {
    var part = new Part(PartType.Ring) { IsHole = !isHole };
    foreach (var (x, y) in points)
      part.AddVertex(new Vertex(x, y));
    part.Seal();
    return part;
  }
}

