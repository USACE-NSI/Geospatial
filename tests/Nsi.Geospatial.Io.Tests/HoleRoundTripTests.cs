using System.Globalization;
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

  /// Three tests because they fail for unrelated reasons, plus a fourth that asks a question
  /// the first three cannot: the AREA is our claim, the ring ORDER is the driver's, and the
  /// hole FLAGS are neither -- SpatialReader assigns them from ring position, so a file whose
  /// order was already conventional round-trips its flags for a reason that has nothing to
  /// do with the write. HoleFlagsSurviveRoundTripWhenTheShellIsNotFirst authors the shell at
  /// Parts[1] so position and flag disagree, and says which rule the reader implements.
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
  /// T-5, P-05. The ring ORDER claim, and the FRAGILE one. What this fixture actually
  /// demonstrates is narrower than it looks: all three rings are authored CCW, so winding
  /// could not have told OGR which was the shell -- the ESRI spec's rule is exterior
  /// clockwise, hole counter-clockwise, and this file violates it on all three rings. OGR
  /// preserved our order (or used containment), repaired the winding, and the reader then
  /// assigned IsHole from position. So this pins that this GDAL build preserves order for
  /// input whose order was already conventional. It is NOT evidence that positional
  /// assignment is sound -- see HoleFlagsSurviveRoundTripWhenTheShellIsNotFirst.
  /// If a GDAL upgrade reorders rings THIS is the test that breaks; fix the expectation here
  /// and leave the area tests alone.
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

  static Features RoundTrip(bool exteriorOnly, bool shellFirst = true)
  {
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
      if (exteriorOnly)
      {
        feature.AddPart(Ring(Exterior, isHole: false));
      }
      else if (shellFirst)
      {
        feature.AddPart(Ring(Exterior, isHole: false));
        feature.AddPart(Ring(Triangle, isHole: true));
        feature.AddPart(Ring(Square, isHole: true));
      }
      else
      {
        // The state P-05 makes reachable in memory and the reader cannot represent:
        // position and flag disagree.
        feature.AddPart(Ring(Square, isHole: true)); // Parts[0] is a hole
        feature.AddPart(Ring(Exterior, isHole: false)); // Parts[1] is the shell
        feature.AddPart(Ring(Triangle, isHole: true));
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

  /// <summary>
  /// P-05, P-21. What PolygonHoleIsSubtractedAfterRead cannot assert: that the flag we wrote
  /// is the flag we read back. SpatialReader assigns IsHole from ring position, so a
  /// positionally-conforming file round-trips its flags for a reason that has nothing to do
  /// with the write -- the three tests above pass whether or not IsHole survives anything.
  /// This one authors the shell at Parts[1], so position and flag disagree.
  /// </summary>
  [Fact]
  public void HoleFlagsSurviveRoundTripWhenTheShellIsNotFirst()
  {
    var feature = Assert.Single(RoundTrip(exteriorOnly: false, shellFirst: false).FeatureSet);
    var parts = feature.Parts;
    // Each ring is identified by its own area (6400 exterior, 200 triangle, 100 square)
    // rather than by the slot we wrote it into, and the two holes are unordered. Asserting
    // flags by index before knowing which ring landed where is what made the last run
    // uninformative: positional assignment and driver reordering both answer False.
    Assert.Equal(
      "shell=6400 hole=200 hole=100",
      string.Join(
        " ",
        feature
          .Parts.OrderByDescending(p => p.AreaSquareMeters ?? 0)
          .Select(p =>
            $"{(p.IsHole ? "hole" : "shell")}={p.AreaSquareMeters?.ToString(
                "0",
                CultureInfo.InvariantCulture
              ) ?? "null"}"
          )
      )
    );
    Assert.Equal(6100d, feature.AreaSquareMeters!.Value, 6);
    Assert.Equal(3, parts.Count); // else the two below throw instead of reporting
    Assert.True(parts[0].IsHole, $"Parts[0].IsHole came back {parts[0].IsHole}");
    Assert.False(parts[1].IsHole, $"Parts[1].IsHole came back {parts[1].IsHole}");
    Assert.True(parts[2].IsHole, $"Parts[2].IsHole came back {parts[2].IsHole}");
    Assert.Equal(6100d, feature.AreaSquareMeters!.Value, 6);
  }

  /// <summary>
  /// Sealed and CCW, matching TestFeatures.Ring. No test here asserts vertex counts -- that
  /// is T-3. IsHole is authored faithfully; whether it SURVIVES the round trip is a separate
  /// question, because SpatialReader assigns the flag from ring position.
  /// </summary>
  static Part Ring((double X, double Y)[] points, bool isHole)
  {
    var part = new Part(PartType.Ring) { IsHole = isHole };
    foreach (var (x, y) in points)
      part.AddVertex(new Vertex(x, y));
    part.Seal();
    return part;
  }
}

