using Nsi.Geospatial.Geometry;
using Xunit;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// T-5, P-05. Rings and areas derived here by shoelace and nowhere else in the repo; all
/// three are authored CCW so they read positive under either a signed or an absolute Area.
///   Exterior (0,0)(100,0)(100,40)(40,40)(40,100)(0,100) = 12800/2 = 6400. Deliberately NOT
///     a rectangle: its bounding box is 10000, so a measurer that worked in boxes cannot
///     land on 6400 by accident.
///   Triangle (5,5)(25,5)(5,25) = 200. THREE vertices and a 400 bounding box, so neither a
///     four-vertex assumption nor a box-shaped hole produces 200.
/// Square (5,60)(15,60)(15,70)(5,70) = 100. A SECOND hole: with one hole, "subtracts every
/// hole" is indistinguishable from "subtracts the part after the exterior", which is what
/// P-05 did. Two holes is what makes the loop observable.
/// Every expected value is distinct (6400/6200/6300/6100), so a row that subtracts the wrong
/// ring, or the wrong count of rings, cannot land on another row's answer.
///
/// Projected with UnitToMeters 1, so planar units ARE square metres and the arithmetic is
/// exact. In a geographic CRS these numbers are wrong by construction, and with no owner at
/// all Feature.AreaSquareMeters returns null -- Kind == Unknown -- which would make every
/// assertion here pass vacuously. Hence AddFeature on every path.
/// </summary>
public class FeatureAreaTests
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

  /// <summary>
  /// The control, and the only test that mentions no hole. Without it 6200 is
  /// unattributable: an error in the exterior's own area and an error in the subtraction can
  /// cancel, and nothing else here would notice.
  /// </summary>
  [Fact]
  public void ExteriorOnlyFeatureReportsItsOwnArea()
  {
    var features = TestFeatures.Projected(1.0);
    var feature = TestFeatures.Polygon(features, TestFeatures.Ring(Exterior));

    Assert.Single(feature.Parts);
    Assert.Equal(6400d, feature.AreaSquareMeters!.Value, 6);
  }

  /// <summary>
  /// P-05. A hole whose area is unknown makes the FEATURE's area unknown. The pre-fix
  /// expression was `total -= Parts[i].AreaSquareMeters ?? 0`, which turned "no area" into
  /// "zero area" -- Part.AreaSquareMeters names exactly that caller mistake in its own
  /// docstring. The hole here has TWO vertices, so its area is null by Part's documented
  /// rule ("fewer than three vertices"), not by a fixture quirk. Restoring `?? 0` makes the
  /// feature answer 6400 and this fail.
  /// </summary>
  [Fact]
  public void AreaOfAFeatureWithAHoleOfUnknownAreaIsUnknown()
  {
    var features = TestFeatures.Projected(1.0);
    var noArea = TestFeatures.Hole((5, 5), (25, 5)); // two vertices: no area, by Part's rule
    var feature = TestFeatures.Polygon(features, TestFeatures.Ring(Exterior), noArea);

    Assert.Null(noArea.AreaSquareMeters); // the premise, stated rather than assumed
    Assert.Null(feature.AreaSquareMeters);
  }

  /// <summary>
  /// P-05. Every part flagged IsHole: there is no exterior to subtract FROM, so the answer
  /// is null and not the negation of a sum. A feature with no shell is authored data, not a
  /// hypothetical -- the reader cannot produce one, joins and hand-built geometry can.
  /// Pre-fix this seeded `total` from Parts[0] regardless of its flag and answered 100: a
  /// plausible, positive, entirely fabricated number, since 200 - 100 is the SQUARE's own
  /// area by coincidence. That is the worst failure this member can have.
  /// </summary>
  [Fact]
  public void AreaOfAFeatureWithNoShellIsUnknown()
  {
    var features = TestFeatures.Projected(1.0);
    var feature = TestFeatures.Polygon(
      features,
      TestFeatures.Hole(Triangle),
      TestFeatures.Hole(Square)
    );

    Assert.Equal(2, feature.Parts.Count(p => p.IsHole)); // premise: no exterior exists
    Assert.Null(feature.AreaSquareMeters);
  }

  /// <summary>
  /// The three parts are always present; only the IsHole flags vary, so each row isolates
  /// one subtraction and row 1 proves the flags are load-bearing -- an unflagged interior
  /// must change nothing. Row 4 needs the loop.
  /// Row 1 (nothing flagged -> 6400) is what constrains the fix: "sum the unflagged parts,
  /// subtract the flagged" would answer 6700 and is therefore wrong. The shell is the first
  /// unflagged part, not the aggregate of unflagged parts.
  /// </summary>
  [Theory]
  [InlineData(false, false, 6400)] // neither flagged: nothing subtracted
  [InlineData(true, false, 6200)] //  6400 - 200, triangle only
  [InlineData(false, true, 6300)] //  6400 - 100, square only
  [InlineData(true, true, 6100)] //   both: Parts[1..] summed, not stopped at the first
  public void AreaSquareMetersSubtractsEveryHole(
    bool triangleIsHole,
    bool squareIsHole,
    double expected
  )
  {
    var features = TestFeatures.Projected(1.0);
    var triangle = TestFeatures.Ring(Triangle);
    var square = TestFeatures.Ring(Square);
    triangle.IsHole = triangleIsHole;
    square.IsHole = squareIsHole;
    var feature = TestFeatures.Polygon(features, TestFeatures.Ring(Exterior), triangle, square);

    Assert.Equal(3, feature.Parts.Count);
    Assert.Equal(expected, feature.AreaSquareMeters!.Value, 6);
  }

  /// <summary>
  /// P-05. The shell is found by flag, not by slot. Area is a property of the geometry, so
  /// the order rings were appended in cannot change it: 6100. Before the fix this answered
  /// -100 -- Parts[0] seeded `total`, so the 6400 exterior was skipped as a "hole" and one
  /// hole was subtracted from another. NEGATIVE, not merely too small: a negative area is
  /// measurable, compares normally, and reaches joins and any area-sorted selection
  /// unchecked. Reverted, this fails; that is the whole point of keeping it.
  /// </summary>
  [Fact]
  public void AreaSquareMetersFindsTheShellByFlagNotByPosition()
  {
    var features = TestFeatures.Projected(1.0);
    var feature = TestFeatures.Polygon(
      features,
      TestFeatures.Hole(Square), // Parts[0] is a hole
      TestFeatures.Ring(Exterior),
      TestFeatures.Hole(Triangle)
    );

    Assert.True(feature.Parts[0].IsHole); // the premise, stated rather than assumed
    Assert.Equal(6100d, feature.AreaSquareMeters!.Value, 6);
  }
}

