using System.Collections.Generic;
using System.Linq;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Spatial;
using Xunit;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// Tests for the BoundingBox
/// </summary>
public class BoundingBoxTests
{
  /// <summary>
  /// P-39. The guard #28 owed: the ctor stores what it is handed. Before #28 these same
  /// arguments normalised to [0,0]-[10,10]. The claim is not the field values, it is that an
  /// inverted box is the identity for Union -- it vanishes from a fold instead of correcting
  /// itself, which is P-72.
  /// </summary>
  [Fact]
  public void CtorKeepsCornerOrderSoAnInvertedPairIsNotACorrectedBox()
  {
    var inverted = new BoundingBox(10, 10, 0, 0);

    Assert.Equal(10d, inverted.MinX);
    Assert.Equal(0d, inverted.MaxX);
    Assert.True(inverted.IsEmpty());

    Assert.False(inverted.ContainsPoint(5, 5));
    Assert.False(inverted.Overlaps(BoundingBox.Point(5, 5)));
    Assert.Equal(new BoundingBox(0, 0, 10, 10), new BoundingBox(0, 0, 10, 10).Union(inverted));
    Assert.True(double.IsNaN(new BoundingBox(-1e308, 5, 1e308, 5).Area()));
  }

  /// <summary>
  /// The answer #28 flipped: while the ctor normalised, Empty was the full-range box and this
  /// was true of every point on earth. Now false, with no guard -- no x satisfies
  /// MaxValue &lt;= x &lt;= MinValue.
  /// </summary>
  [Fact]
  public void EmptyContainsNoPoint()
  {
    Assert.False(BoundingBox.Empty.ContainsPoint(0, 0));
    Assert.False(BoundingBox.Empty.ContainsPoint(double.MaxValue, double.MaxValue));
    Assert.False(BoundingBox.Empty.ContainsPoint(double.MinValue, double.MinValue));
  }

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

  [Fact]
  public void NegatedMaxValueIsTheSentinelButNearMaxValueOverflowsUnnoticed()
  {
    // -double.MaxValue IS double.MinValue, so this is the FULL-RANGE box: min below max
    // on both axes, contains every real coordinate. It is a third kind of box this type
    // admits -- neither Empty nor a real box -- and Overlaps/Contains/Union treat it as a
    // real one, so it silently absorbs everything it is unioned with. Nothing prevents
    // constructing it: the ctor and the setters are public.
    var fullRange = new BoundingBox(
      -double.MaxValue,
      -double.MaxValue,
      double.MaxValue,
      double.MaxValue
    );

    Assert.False(fullRange.IsEmpty()); // see below
    Assert.NotEqual(BoundingBox.Empty, fullRange);
    Assert.True(fullRange.ContainsPoint(0, 0));
    Assert.Equal(fullRange, new BoundingBox(0, 0, 10, 10).Union(fullRange));
    // A finite box overflows too, and nothing notices.
    var nearFullRange = new BoundingBox(-1e308, -1e308, 1e308, 1e308);
    Assert.True(double.IsPositiveInfinity(nearFullRange.Area()));
  }

  /// <summary>
  /// P-41's guard. The fix is `return BoundingBox.EnlargementToContain(bbox);` -- a
  /// one-line swap into a member with no test. Hand goldens, not the inclusion-exclusion
  /// identity: Union returns the bounding box of the two, so this is MBR growth, not the
  /// area added by the geometry.
  /// </summary>
  [Theory]
  [InlineData(0, 0, 10, 10, 2, 2, 8, 8, 0)] //    nested: no growth at all
  [InlineData(0, 0, 10, 10, 5, 5, 15, 15, 125)] // [0,15]^2=225 minus 100
  [InlineData(0, 0, 10, 10, 20, 20, 30, 30, 800)] // [0,30]^2=900 minus 100, not 100
  [InlineData(0, 0, 10, 10, 0, 0, 10, 10, 0)] //   identical
  [InlineData(5, 5, 5, 5, 0, 0, 10, 10, 100)] //   point growing to a box
  public void EnlargementToContainIsMbrGrowth(
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

    Assert.Equal(expected, a.EnlargementToContain(b));
  }

  /// <summary>
  /// P-39, P-41. A NaN growth needs the RECEIVER's overflow to survive into the Union, so
  /// every row is non-empty: EnlargementToContain is Union(other).Area() - Area(), and with
  /// Area() = +∞ and Union keeping the receiver's corners, that is ∞ - ∞. Emptiness is NOT
  /// the cause -- see EnlargementToContainIsAsymmetricAboutEmpty, where an Empty receiver
  /// gives -∞ instead, because Union's guard returns the argument and throws the receiver's
  /// +∞ away before the subtraction.
  /// It matters because every comparison against NaN is false: seed a min-selection with one
  /// and `next < best` never fires again, so the node keeps whichever child it looked at
  /// first. Unlike -∞, NaN does not even sort consistently.
  /// Rows are the receiver (minX,minY,maxX,maxY); the argument is a real box.
  /// </summary>
  [Theory]
  [InlineData(-double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue)] // full range: -MaxValue IS MinValue
  [InlineData(-1e308, -1e308, 1e308, 1e308)] // finite corners, still overflows
  [InlineData(-1e308, 0, 1e308, 10)] // one axis is enough: ∞ × 10 is ∞
  public void EnlargementToContainIsNaNWhenTheReceiversAreaIsInfinite(
    double minX,
    double minY,
    double maxX,
    double maxY
  )
  {
    var receiver = new BoundingBox(minX, minY, maxX, maxY);
    var value = receiver.EnlargementToContain(new BoundingBox(1, 2, 3, 4));

    Assert.False(receiver.IsEmpty()); // the whole claim: emptiness is not required
    Assert.True(double.IsInfinity(receiver.Area()));
    Assert.True(double.IsNaN(value), $"actual {value}");
  }

  /// <summary>
  /// The two Empty directions are NOT symmetric, and P-41 depends on knowing that.
  /// Empty as the argument: Union short-circuits to this, so growth is Area - Area = 0.
  /// Empty as the receiver: Union short-circuits to the argument, then Area() is +inf, so
  /// the result is -infinity -- which sorts FIRST, so an Empty node would win every
  /// insertion and then prune the feature on search. Unreachable while addFeature's gate
  /// holds; that is exactly why the gate needs T-18 and P-15 needs closing.
  /// </summary>
  [Fact]
  public void EnlargementToContainIsAsymmetricAboutEmpty()
  {
    var box = new BoundingBox(1, 2, 3, 4); // area 4

    Assert.Equal(0, box.EnlargementToContain(BoundingBox.Empty));
    Assert.True(double.IsNegativeInfinity(BoundingBox.Empty.EnlargementToContain(box)));
    Assert.True(double.IsNaN(BoundingBox.Empty.EnlargementToContain(BoundingBox.Empty)));
  }

  /// <summary>
  /// P-66's mitigation rests entirely on this. addFeature's gate stops Empty LEAVES, but
  /// every fresh RTreeNode starts Empty and addChild does
  /// `BoundingBox = BoundingBox.Union(child.BoundingBox)` -- so a node only acquires a
  /// real box because Union treats Empty as the identity. Delete either guard in Union and
  /// the invariant dies silently; buildChildOptions passes canPropagateMBRup: false, so
  /// RecomputeMBR never papers over it there.
  /// </summary>
  [Fact]
  public void UnionTreatsEmptyAsTheIdentityElement()
  {
    var box = new BoundingBox(0, 0, 10, 10);

    Assert.Equal(box, BoundingBox.Empty.Union(box));
    Assert.Equal(box, box.Union(BoundingBox.Empty));
    Assert.NotEqual(BoundingBox.Empty, box.Union(BoundingBox.Empty)); // not swallowed
    Assert.Equal(BoundingBox.Empty, BoundingBox.Empty.Union(BoundingBox.Empty));
  }

  [Fact]
  public void UnionIsCommutativeAndNeverSmallerThanEitherSide()
  {
    var a = new BoundingBox(0, 0, 10, 10);
    var b = new BoundingBox(5, 5, 15, 15);

    Assert.Equal(a.Union(b), b.Union(a)); // both [0,15]^2
    Assert.Equal(new BoundingBox(0, 0, 15, 15), a.Union(b));
    Assert.True(a.Union(b).Area() >= a.Area());
    Assert.True(a.Union(b).Area() >= b.Area());
  }

  /// <summary>
  /// New member, reached only through RTreeNode.Perimeter, untested. Also corrects this
  /// file's claim that Empty's perimeter is "~7.2e308": MaxX - MinX is DBL_MAX + DBL_MAX,
  /// which is +infinity before the doubling, so there is no finite large value.
  /// </summary>
  [Theory]
  [InlineData(0, 0, 10, 10, 40)] //  2*(10+10)
  [InlineData(0, 0, 10, 5, 30)] //   2*(10+5)
  [InlineData(5, 5, 5, 5, 0)] //     point
  [InlineData(0, 0, 10, 0, 20)] //   horizontal segment: 2*(10+0)
  public void PerimeterIsTwiceTheSumOfTheExtents(
    double a,
    double b,
    double c,
    double d,
    double expected
  )
  {
    Assert.Equal(expected, new BoundingBox(a, b, c, d).Perimeter());
  }

  [Fact]
  public void PerimeterOfEmptyOverflows()
  {
    Assert.True(double.IsNegativeInfinity(BoundingBox.Empty.Perimeter()));
    Assert.True(double.IsPositiveInfinity(BoundingBox.Empty.Area()));
  }

  /// <summary>
  /// P-62 characterisation. The `double.IsPositiveInfinity(minX)` ternary can never fire
  /// (minX starts at MaxValue and only ever decreases), yet the empty-input path still
  /// lands on Empty because the constructor normalises (MaxValue,MaxValue,MinValue,MinValue)
  /// to the same four values Empty holds. So the ternary is dead code, not a bug.
  /// </summary>
  [Fact]
  public void FromVerticesOfNothingIsTheSentinel()
  {
    Assert.Equal(BoundingBox.Empty, BoundingBox.FromVertices(Array.Empty<(double X, double Y)>()));
  }

  /// <summary>
  /// P-39, P-72. Union short-circuits on IsEmpty, not on == Empty, so an empty ARGUMENT is
  /// dropped before any arithmetic and growth is exactly Area - Area. Rows are
  /// (aMinX,aMinY,aMaxX,aMaxY, bMinX,bMinY,bMaxX,bMaxY). The sentinel is spelled out rather
  /// than named so the row says what the four extremes are. Every row's receiver must have a
  /// FINITE Area -- see the other test: overflow turns this identity into NaN.
  /// </summary>
  [Theory]
  [InlineData(1, 2, 3, 4, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue)] // the sentinel
  [InlineData(1, 2, 3, 4, 10, 10, 0, 0)] //                                                        hand-authored inversion
  [InlineData(-100, -50, 300, 250, 10, 10, 0, 0)] //                                               negative bounds, projected-CRS-shaped
  [InlineData(5, 5, 5, 5, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue)] // degenerate receiver
  public void EnlargementOfARealBoxAgainstAnEmptyArgumentIsZero(
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
    Assert.Equal(
      0d,
      new BoundingBox(ax, ay, bx, by).EnlargementToContain(new BoundingBox(cx, cy, dx, dy))
    );
  }

  /// <summary>
  /// P-62 / P-39. A NaN vertex fails both `x < minX` and `x > maxX`, so it is dropped
  /// without a trace and the box is too small -- a finite box addFeature will accept,
  /// indexing the feature somewhere it is not. Latent only because nothing calls this.
  /// If FromVertices is kept, this is the behaviour it must stop having.
  /// </summary>
  [Fact]
  public void FromVerticesSilentlyDropsNonFiniteVertices()
  {
    var box = BoundingBox.FromVertices([(0, 0), (double.NaN, double.NaN)]);

    Assert.Equal(new BoundingBox(0, 0, 0, 0), box); // the NaN point vanished
    Assert.True(double.IsFinite(box.MinX)); // and addFeature would wave it through
  }

  /// <summary>
  /// P-73. Contains had no test at all: the only member of BoundingBox with no method in
  /// this file, and one of the four that guard Empty. Rows are shared with
  /// OverlapsIsSymmetricAndTrueForContainment wherever the same two boxes appear, so the
  /// pair reads as one table: where Overlaps is true and Contains is true BOTH ways the
  /// boxes are identical, and every other true is one-way.
  ///
  /// Expectations are derived from the definition -- "every point of other is a point of
  /// this" -- not read off the implementation. Rows are (this, other, this.Contains(other),
  /// other.Contains(this)).
  /// </summary>
  [Theory]
  // ---- proper containment: one way only ---------------------------------------------
  [InlineData(0, 0, 10, 10, 2, 2, 8, 8, true, false)] //   a contains b
  [InlineData(2, 2, 8, 8, 0, 0, 10, 10, false, true)] //   same pair, arguments swapped
  // ---- identical: the only row true both ways, and so consistent with antisymmetry --
  [InlineData(0, 0, 10, 10, 0, 0, 10, 10, true, true)] //  box vs itself
  [InlineData(5, 5, 5, 5, 5, 5, 5, 5, true, true)] //      point vs itself
  // ---- degenerate other: inside, on the boundary, just outside ----------------------
  [InlineData(0, 0, 10, 10, 5, 5, 5, 5, true, false)] //   point strictly inside
  [InlineData(0, 0, 10, 10, 10, 5, 10, 5, true, false)] // point ON the boundary: closed
  [InlineData(0, 0, 10, 10, 11, 5, 11, 5, false, false)] // point outside
  [InlineData(0, 0, 10, 10, 10, 0, 10, 10, true, false)] // zero-width line on the edge
  [InlineData(0, 0, 10, 10, 10, 0, 20, 10, false, false)] // flush but sticking out
  // ---- Overlaps true, Contains false both ways: the two members disagree by design -
  [InlineData(0, 0, 10, 10, 5, 5, 15, 15, false, false)] // corner overlap
  // ---- disjoint ---------------------------------------------------------------------
  [InlineData(0, 0, 10, 10, 20, 20, 30, 30, false, false)] // separated
  [InlineData(5, 5, 5, 5, 6, 6, 6, 6, false, false)] //      point vs different point
  // ---- Contains is comparisons, not arithmetic: an infinite Area changes nothing ----
  [InlineData(-1e308, -1e308, 1e308, 1e308, 0, 0, 1, 1, true, false)]
  public void ContainsIsNotSymmetricAndHandCorrect(
    double ax,
    double ay,
    double bx,
    double by,
    double cx,
    double cy,
    double dx,
    double dy,
    bool aContainsB,
    bool bContainsA
  )
  {
    var a = new BoundingBox(ax, ay, bx, by);
    var b = new BoundingBox(cx, cy, dx, dy);

    Assert.Equal(aContainsB, a.Contains(b));
    Assert.Equal(bContainsA, b.Contains(a));
  }

  /// <summary>
  /// P-73, P-39. The guard is what makes these false, and the inversion rows are the point:
  /// before #28 the ctor normalised, so new(10, 10, 0, 0) WAS [0,0]-[10,10] and contained
  /// itself. Now it is stored as written -- and a Contains written as four comparisons
  /// returns TRUE for inverted.Contains(inverted), because 10 >= 10 and 0 <= 0. So this row
  /// is the falsifier: if the IsEmpty() guard ever comes out, it is this assert that fails,
  /// not one of the Empty ones. Equality would not have caught it either way.
  /// </summary>
  [Fact]
  public void ContainsIsFalseWhenEitherSideIsNotABox()
  {
    var real = new BoundingBox(0, 0, 10, 10);
    var inverted = new BoundingBox(10, 10, 0, 0);

    Assert.False(BoundingBox.Empty.Contains(real));
    Assert.False(real.Contains(BoundingBox.Empty));
    Assert.False(BoundingBox.Empty.Contains(BoundingBox.Empty));

    Assert.False(inverted.Contains(real)); // other side guards
    Assert.False(real.Contains(inverted)); // this side guards
    Assert.False(inverted.Contains(inverted)); // four comparisons alone would say TRUE
  }

  /// <summary>P-73. Contains in terms of ContainsPoint, the definition the rows above are
  /// instances of. Holds for every row where a box has four distinct corners; degenerate
  /// rows are the boundary cases the closed interval decides.</summary>
  [Theory]
  [InlineData(0, 0, 10, 10, 2, 2, 8, 8)]
  [InlineData(0, 0, 10, 10, 5, 5, 15, 15)]
  [InlineData(0, 0, 10, 10, 20, 20, 30, 30)]
  public void ContainsAgreesWithItsFourCorners(
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

    Assert.Equal(
      a.ContainsPoint(cx, cy)
        && a.ContainsPoint(cx, dy)
        && a.ContainsPoint(dx, cy)
        && a.ContainsPoint(dx, dy),
      a.Contains(b)
    );
  }
}

