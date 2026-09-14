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
///   Square   (5,60)(15,60)(15,70)(5,70) = 100. A SECOND hole: P-05 subtracts inside a loop
///     over Parts[1..], and with one hole "subtracts holes" is indistinguishable from
///     "subtracts Parts[1]".
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
  /// The three parts are always present; only the IsHole flags vary, so each row isolates
  /// one subtraction and row 1 proves the flags are load-bearing -- an unflagged interior
  /// must change nothing. Row 4 needs the loop. All four are green today and none of them
  /// says anything about ORDER, which is the next test's job.
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
  /// P-05, T-5. CHARACTERISATION -- this is the bug, not the spec. Area is a property of the
  /// geometry, so the order rings were appended in cannot change it: the answer is 6100.
  /// Feature.AreaSquareMeters seeds `total` from Parts[0] unconditionally, so with a hole
  /// first it computes 100 - 200 = -100: the exterior is skipped as a "hole" and one hole is
  /// subtracted from another. NEGATIVE, not merely too small -- which is what makes this
  /// worth a guard, since a negative area is measurable, compares normally, and escapes.
  ///
  /// Change the expectation to 6100 in the SAME commit as the fix (T-24's rule); do not
  /// delete this test first, or the fix ships unguarded and P-05 reopens green.
  /// </summary>
  [Fact]
  public void HoleFirstFeatureSeedsFromPartsZeroSoTheExteriorIsNeverCounted()
  {
    var features = TestFeatures.Projected(1.0);
    var feature = TestFeatures.Polygon(
      features,
      TestFeatures.Hole(Square), // Parts[0] is a hole
      TestFeatures.Ring(Exterior),
      TestFeatures.Hole(Triangle)
    );

    Assert.True(feature.Parts[0].IsHole); // the premise, stated rather than assumed
    Assert.Equal(6100d, feature.AreaSquareMeters!.Value, 6); // 6100 after P-05
  }
}

