using System.Collections.Generic;
using System.Linq;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Spatial;
using Xunit;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// Tests for the Geometry stuff
/// </summary>
public class GeometryTests
{
  [Theory]
  [InlineData(0, 0, 10, 10, 2, 2, 8, 8, 36)] //      this contains other
  [InlineData(2, 2, 8, 8, 0, 0, 10, 10, 36)] //      other contains this
  [InlineData(0, 0, 10, 10, 5, 5, 15, 15, 25)] //    corner overlap
  [InlineData(0, 0, 10, 10, 20, 20, 30, 30, 0)] //   disjoint
  [InlineData(0, 0, 10, 10, 10, 0, 20, 10, 0)] //    flush edge contact
  [InlineData(5, 5, 5, 5, 0, 0, 10, 10, 0)] //       point inside: area 0, Overlaps true
  public void OverlappingAreaIsSymmetricAndHandCorrect(
    double ax,
    double ay,
    double bx,
    double by,
    double cx,
    double cy,
    double dx,
    double dy,
    double expected
  )
  {
    var a = new BoundingBox(ax, ay, bx, by);
    var b = new BoundingBox(cx, cy, dx, dy);

    Assert.Equal(expected, a.OverlappingArea(b));
    Assert.Equal(expected, b.OverlappingArea(a));
  }

  /// <summary>
  /// |A ∪ B| = |A| + |B| − |A ∩ B| only when A ∪ B is itself a rectangle (nested pairs,
  /// or flush-aligned disjoint pairs). EnlargementToContain measures MBR growth -- the
  /// bbox union -- so asserting this on offset or disjoint boxes demands a false equality.
  /// </summary>
  [Theory]
  [InlineData(0, 0, 10, 10, 2, 2, 8, 8)] //  nested
  [InlineData(2, 2, 8, 8, 0, 0, 10, 10)] //  nested, reversed
  [InlineData(5, 5, 5, 5, 0, 0, 10, 10)] //  degenerate nested
  public void OverlappingAreaAgreesWithEnlargementWhenTheUnionIsARectangle(
    double ax,
    double ay,
    double bx,
    double by,
    double cx,
    double cy,
    double dx,
    double dy
  )
  {
    var a = new BoundingBox(ax, ay, bx, by);
    var b = new BoundingBox(cx, cy, dx, dy);

    Assert.Equal(b.Area() - a.OverlappingArea(b), a.EnlargementToContain(b), 9);
  }

  [Fact]
  public void OverlappingAreaAgainstEmptyIsZero()
  {
    Assert.Equal(0, BoundingBox.Empty.OverlappingArea(new BoundingBox(0, 0, 10, 10)));
    Assert.Equal(0, new BoundingBox(0, 0, 10, 10).OverlappingArea(BoundingBox.Empty));
  }

  /// <summary>
  /// T-21. Overlaps is called from three descents in RTreeNode and from the RTreeTests
  /// oracle, and had no direct test. Symmetry is asserted because the implementation is a
  /// symmetric conjunction of four comparisons plus a symmetric Empty guard -- so any
  /// asymmetry is a rewrite, not a rounding artefact.
  /// Rows are (aMinX,aMinY,aMaxX,aMaxY, bMinX,bMinY,bMaxX,bMaxY, expected).
  /// </summary>
  [Theory]
  // ---- the fix(#15) case: one box fully contains the other -------------------------
  // b.Overlaps(a) is the R-tree's call shape (this == node, other == query). The deleted
  // getMBRoverlap returned 0 here, because its gate tested the *query's* corners against
  // the node, and a containing query has no corner inside a contained node.
  [InlineData(0, 0, 10, 10, 2, 2, 8, 8, true)] //   a contains b
  [InlineData(2, 2, 8, 8, 0, 0, 10, 10, true)] //   b contains a
  // ---- partial overlap --------------------------------------------------------------
  [InlineData(0, 0, 10, 10, 5, 5, 15, 15, true)] // corner overlap
  [InlineData(0, 0, 10, 10, 0, 0, 10, 10, true)] // identical
  // ---- zero-area contact: Overlaps' question, and OverlappingArea's is 0 ------------
  [InlineData(0, 0, 10, 10, 10, 0, 20, 10, true)] // flush edge
  [InlineData(0, 0, 10, 10, 10, 10, 20, 20, true)] // flush corner
  // ---- disjoint ---------------------------------------------------------------------
  [InlineData(0, 0, 10, 10, 20, 20, 30, 30, false)] // separated
  // ---- degenerate: the reason the >= 1 floor was written, now handled by the
  // ---- closed interval alone --------------------------------------------------------
  [InlineData(0, 0, 10, 10, 5, 5, 5, 5, true)] //   point inside
  [InlineData(0, 0, 10, 10, 10, 5, 10, 5, true)] // point on boundary
  [InlineData(0, 0, 10, 10, 11, 5, 11, 5, false)] // point just outside
  [InlineData(5, 5, 5, 5, 5, 5, 5, 5, true)] //      point vs same point
  [InlineData(5, 5, 5, 5, 6, 6, 6, 6, false)] //     point vs different point
  [InlineData(5, 0, 5, 10, 0, 0, 10, 10, true)] //   zero-width line crossing
  [InlineData(10, 0, 10, 10, 0, 0, 10, 10, true)] // zero-width line on the boundary
  [InlineData(11, 0, 11, 10, 0, 0, 10, 10, false)] // zero-width line just outside
  // ---- only the sentinel short-circuits; size alone must not ------------------------
  [InlineData(-1e308, -1e308, 1e308, 1e308, 0, 0, 1, 1, true)]
  public void OverlapsIsSymmetricAndTrueForContainment(
    double ax,
    double ay,
    double bx,
    double by,
    double cx,
    double cy,
    double dx,
    double dy,
    bool expected
  )
  {
    var a = new BoundingBox(ax, ay, bx, by);
    var b = new BoundingBox(cx, cy, dx, dy);

    Assert.Equal(expected, a.Overlaps(b));
    Assert.Equal(expected, b.Overlaps(a));
  }

  /// <summary>
  /// T-21. The guard is the only thing that makes Overlaps false for a full-range box, and
  /// it is the guard P-66 is about: an Empty node is pruned, subtree and all.
  /// </summary>
  [Fact]
  public void OverlapsIsFalseWhenEitherSideIsEmpty()
  {
    var real = new BoundingBox(0, 0, 10, 10);

    Assert.False(BoundingBox.Empty.Overlaps(real));
    Assert.False(real.Overlaps(BoundingBox.Empty));
    Assert.False(BoundingBox.Empty.Overlaps(BoundingBox.Empty));
  }

  /// <summary>
  /// P-39, characterisation. Corrects an earlier claim in Issues.md: -double.MaxValue IS
  /// double.MinValue, so a box built from +/-MaxValue on both axes normalises to exactly
  /// Empty and addFeature's first check rejects it. The gap that survives is one step
  /// narrower -- a near-full-range box is not Empty, is finite at every corner, clears both
  /// gates, and still overflows. When P-39 adds an overflow bound, add the Throws case.
  /// </summary>
  [Fact]
  public void NegatedMaxValueIsTheSentinelButNearMaxValueOverflowsUnnoticed()
  {
    Assert.Equal(
      BoundingBox.Empty,
      new BoundingBox(-double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue)
    );

    var nearly = new BoundingBox(-1e308, -1e308, 1e308, 1e308);

    Assert.NotEqual(BoundingBox.Empty, nearly);
    Assert.True(double.IsFinite(nearly.MinX) && double.IsFinite(nearly.MaxX));
    Assert.True(double.IsPositiveInfinity(nearly.Area())); // 2e308 > double.MaxValue
  }
}
