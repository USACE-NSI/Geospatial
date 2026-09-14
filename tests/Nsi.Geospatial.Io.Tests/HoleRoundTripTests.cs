using System.Globalization;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Io;
using Nsi.Geospatial.Projections;
using OSGeo.OSR;
using Xunit;

namespace Nsi.Geospatial.Io.Tests;

/// <summary>
/// T-5, P-05. Same geometry, through the driver. Four tests, split by WHOSE claim each pins:
/// the AREA is ours, the ring ORDER is the driver's, and the hole FLAGS are neither --
/// SpatialReader assigns them from ring position, and the driver decides the position.
/// HoleFirstInputIsNormalisedByTheDriverSoTheShellIsAlwaysFirst found that, and it is the one
/// that breaks if GDAL ever stops normalising.
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
  /// T-5. Shell first, holes after -- and the reason is now known rather than assumed. All
  /// three rings here are authored CCW, violating the ESRI rule (exterior clockwise, holes
  /// counter-clockwise), so winding could not have identified the shell; the driver
  /// normalises ring order itself. That makes this STABLE against a GDAL reorder, and also
  /// much weaker than it looks: for an already-conventional file, positional assignment and
  /// winding-based assignment give the same answer, so this cannot distinguish them.
  /// The reader's positional rule is observable by inspection of SpatialReader only --
  /// nothing can reach it through the writer, because the writer cannot emit a file that
  /// contradicts it.
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

  /// <summary>
  /// Write then read one file. .prj comes from Crs.Wkt and nothing else: without WKT the
  /// writer emits no .prj, CrsInspector reports Unknown, AreaSquareMeters returns null, and
  /// every area assertion in this file passes while proving nothing. Hence the Crs premises
  /// in PolygonHoleIsSubtractedAfterRead. Resolved through OSR rather than hand-authored so
  /// the WKT is authoritative; metre units, so UnitToMeters is 1 and 6400 planar units are
  /// exactly 6400 square metres.
  /// </summary>
  static Features RoundTrip(bool exteriorOnly, bool holeFirst = true)
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
  /// P-05. A hole-first feature written and read back. The flags do NOT survive: the driver
  /// emits the exterior ring first, so the file's ring order is never the order Parts was
  /// in, and the reader then derives IsHole from position. The AREA does survive -- 6100 --
  /// because the writer's reorder and the reader's positional rule normalise in the same
  /// direction and cancel.
  ///
  /// Rings are therefore identified by their OWN AREAS (6400 / 200 / 100) rather than by the
  /// slot they were written into. Asserting parts[0].IsHole encoded a wish, could not tell
  /// writer-reorder from positional-reader, and failed twice without saying which.
  /// Assert.Single also pins that the record came back as ONE feature, not split.
  ///
  /// Consequence for P-05: it is unreachable through a read. Its exposure is in-memory --
  /// joins, programmatic build -- which is why four shapefile round-trips passed over it. If
  /// SpatialWriter ever preserves Parts order, this test fails and the read path reopens.
  /// </summary>
  [Fact]
  public void HoleFirstInputIsNormalisedByTheDriverSoTheShellIsAlwaysFirst()
  {
    var feature = Assert.Single(RoundTrip(exteriorOnly: false, holeFirst: true).FeatureSet);

    Assert.Equal(3, feature.Parts.Count); // the premise: three rings came back
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
  }

  /// <summary>
  /// Sealed and CCW, matching TestFeatures.Ring. No test here asserts vertex counts -- that
  /// is T-3. IsHole is authored faithfully and does NOT survive when Parts order and the
  /// flags disagree; see HoleFirstInputIsNormalisedByTheDriverSoTheShellIsAlwaysFirst.
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

