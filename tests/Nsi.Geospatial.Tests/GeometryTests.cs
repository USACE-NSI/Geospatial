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
}

