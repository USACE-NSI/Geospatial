using System.Collections.Generic;
using System.Linq;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Spatial;
using Xunit;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// Tests for the original RTreeManager/RTreeNode algorithm (restored verbatim).
/// These assert the original implementation's behavior — including its
/// getMBRoverlap gate semantics — and deliberately do NOT assert fixed behavior.
/// </summary>
public class RTreeTests
{
  static readonly int[] zerozero = new[] { 0, 0 };
  static readonly int[] onezero = new[] { 1, 0 };

  [Fact]
  public void FindByXYFindsContainingFeature()
  {
    var tree = new RTreeManager();
    tree.addFeature(zerozero, new BoundingBox(0, 0, 10, 10)); // Xmax=10, Xmin=0, Ymax=10, Ymin=0
    tree.addFeature(onezero, new BoundingBox(50, 50, 60, 60)); // Xmax=60, Xmin=50, Ymax=60, Ymin=50

    var hits = FeatureIndicesAt(tree, 5, 5);
    Assert.Contains(0, hits);
    Assert.DoesNotContain(1, hits);
  }

  [Fact]
  public void FindByXYPointOutsideFeatureMBRNotReturned()
  {
    var tree = new RTreeManager();
    tree.addFeature(zerozero, new BoundingBox(0, 0, 10, 10));
    tree.addFeature(onezero, new BoundingBox(50, 50, 60, 60));

    var hits = FeatureIndicesAt(tree, 55, 55);
    Assert.Contains(1, hits);
    Assert.DoesNotContain(0, hits);
  }

  [Fact]
  public void FindByIndReturnsLeafToRootPath()
  {
    var tree = new RTreeManager();
    for (int i = 0; i < 100; i++)
      tree.addFeature(new[] { i, 0 }, new BoundingBox(i * 10, i * 10, i * 10 + 5, i * 10 + 5));

    var path = tree.findByInd(42);
    Assert.NotEmpty(path);

    // The original findByInd returns the node path leaf -> root; the feature
    // node (with its _featureIndex) lives under the leaf.
    var leaf = path[0];
    var featureIndex = leaf
      .Children.Select(c => c.FeatureIndex)
      .FirstOrDefault(a => a is not null && a[0] == 42);
    Assert.NotNull(featureIndex);
  }

  [Fact] //(Skip = "Fails by design: asserts correct behavior that the original (unfixed) RTree split/overlap defects violate. Re-enable once the RTree defects are fixed.")]
  public void BulkInsertAllFeaturesFindableByPoint()
  {
    var tree = new RTreeManager(minChilds: 3, maxChilds: 6);
    for (int i = 0; i < 500; i++)
      tree.addFeature(new[] { i, 0 }, new BoundingBox(i * 10, i * 10, i * 10 + 5, i * 10 + 5));

    for (int i = 0; i < 500; i++)
    {
      var hits = FeatureIndicesAt(tree, i * 10 + 2.5, i * 10 + 2.5);
      if (!hits.Contains(i))
      {
        Assert.Fail();
      }
      Assert.Contains(i, hits);
    }
  }

  [Fact]
  public void GetEndNodesLeavesHoldAllFeatureIndices()
  {
    var tree = new RTreeManager(minChilds: 3, maxChilds: 6);
    for (int i = 0; i < 50; i++)
      tree.addFeature(new[] { i, 0 }, new BoundingBox(i * 10, i * 10, i * 10 + 5, i * 10 + 5));

    var leaves = tree.getEndNodes;
    Assert.NotEmpty(leaves);

    var allIndices = new List<int>();
    foreach (var leaf in leaves)
    {
      foreach (var child in leaf.Children)
      {
        var ind = child.FeatureIndex;
        if (ind is not null)
          allIndices.Add(ind[0]);
      }
    }
    Assert.Equal(50, allIndices.Distinct().Count());
  }

  /// <summary>
  /// T-18a. The gate added in 04118f4 was reached by no test in either direction, which is
  /// how a throw gets deleted by a later cleanup: silently, and green.
  /// </summary>
  [Fact]
  public void FeatureWithNoExtentIsRejected()
  {
    var tree = new RTreeManager();

    var ex = Assert.Throws<ArgumentException>(() => tree.addFeature(Id0, BoundingBox.Empty));

    Assert.Contains("no extent", ex.Message);
    Assert.Empty(tree.Root.Children); // rejected before mutating, not after
  }

  // CA1861. addFeature only stores the reference (RTreeNode.FeatureIndex = featInd, never
  // mutated) and each test builds its own tree, so one shared array per id is safe.
  private static readonly int[] Id0 = [0];
  private static readonly int[] Id7 = [7];
  private static readonly int[] Id99 = [99];

  /// <summary>
  /// T-18b. A non-finite corner is worse than an Empty one: Union propagates NaN into every
  /// ancestor (Math.Min/Max return NaN for a NaN operand), so one bad feature silently
  /// removes unrelated features from every search.
  /// </summary>
  [Theory]
  [InlineData(double.NaN, 0, 0, 0)]
  [InlineData(0, double.NaN, 0, 0)]
  [InlineData(0, 0, double.PositiveInfinity, 0)]
  [InlineData(double.NegativeInfinity, 0, 0, 0)]
  [InlineData(double.NegativeInfinity, 0, double.PositiveInfinity, 10)]
  public void FeatureWithNonFiniteExtentIsRejected(double a, double b, double c, double d)
  {
    var tree = new RTreeManager();

    Assert.Throws<ArgumentException>(() => tree.addFeature(Id0, new BoundingBox(a, b, c, d)));

    Assert.Empty(tree.Root.Children);
  }

  /// <summary>
  /// T-18c. The stated purpose of getMBRoverlap's Math.Max(overlap, 1) floor was that a
  /// zero-area feature must not be pruned. BoundingBox.Overlaps' closed interval covers
  /// that, and this is the test that says so -- which is also the reason the floor must not
  /// come back. Points are collinear on a diagonal on purpose: it exercises all four split
  /// orderings in buildChildOptions, and every box involved is degenerate.
  /// </summary>
  [Fact]
  public void PointShapedFeaturesSurviveSplitsAndAreFound()
  {
    const int count = 30; // defaults are min 4 / max 10, so this forces several splits
    var tree = new RTreeManager();

    for (int i = 0; i < count; i++)
    {
      tree.addFeature(new[] { i }, BoundingBox.Point(i, i));
    }
    tree.addFeature(Id99, BoundingBox.Point(15, 15)); // inserted after the splits

    for (int i = 0; i < count; i++)
    {
      Assert.True(Findable(tree, i, i, i), $"feature {i} at ({i},{i}) was pruned");
    }
    Assert.True(Findable(tree, 15, 15, 99), "point feature added after splits was pruned");

    // Negative control, so the helper cannot pass by returning true for everything: no
    // feature is anywhere near (500,500).
    Assert.False(Findable(tree, 500, 500, 0));
  }

  /// <summary>
  /// T-22, premise. getAddedSizeToAccomodate on a fresh Root is Area + featArea -
  /// OverlappingArea = inf + 4 - 0, because Root starts as Empty and OverlappingArea
  /// short-circuits to 0 for it. The insert loop seeds minExtension at double.MaxValue, so
  /// `inf < double.MaxValue` is false, bestCandidate stays null, and only
  /// `bestCandidate ??= TreeManager.Root` places the feature. Do not read that line as
  /// defensive cruft and delete it. If P-39 makes Area() return 0 for Empty, the fallback
  /// stops being the only route -- update this comment, keep T-22's assertions.
  /// </summary>
  [Fact]
  public void FreshRootHasInfiniteAreaSoTheComparisonNeverFires()
  {
    var tree = new RTreeManager();

    Assert.True(double.IsPositiveInfinity(tree.Root.Area));
    Assert.True(
      double.IsPositiveInfinity(tree.Root.getAddedSizeToAccomodate(new BoundingBox(1, 2, 3, 4)))
    );
    Assert.False(double.PositiveInfinity < double.MaxValue);
  }

  /// <summary>
  /// T-22. First insert into an empty tree, which is the one insert that survives purely by
  /// the Root fallback.
  /// </summary>
  [Fact]
  public void FirstInsertIntoEmptyTreeLandsOnRoot()
  {
    var tree = new RTreeManager();
    var box = new BoundingBox(1, 2, 3, 4);

    tree.addFeature(Id7, box);

    Assert.Single(tree.Root.Children);
    Assert.Equal(box, tree.Root.Children[0].BoundingBox);
    Assert.Equal(box, tree.Root.BoundingBox); // RecomputeMBR propagated a real box over Empty
    Assert.Same(tree.Root, tree.Root.Children[0].Parent);
    Assert.Equal(7, tree.Root.Children[0].FeatureIndex![0]);
    Assert.NotEmpty(tree.findByXY(2, 3));
    Assert.True(Findable(tree, 2, 3, 7));
  }

  [Fact]
  public void FindByIndReturnsAPathToTheFeature()
  {
    var tree = new RTreeManager();
    for (int i = 0; i < 30; i++)
    {
      tree.addFeature([i], BoundingBox.Point(i, i));
    }

    var path = tree.findByInd(7);

    Assert.NotEmpty(path);
    Assert.Same(tree.Root, path[^1]); // getPathReverse walks to the top
    Assert.Contains(
      path,
      n => n.Children.Any(c => c.FeatureIndex is { Length: > 0 } id && id[0] == 7)
    );
  }

  /// <summary>
  /// The line the compiler warns about (RTreeNode.cs:288 CS8602) is reached here. A fresh
  /// tree must return nothing rather than dereference FeatureIndex on a childless node --
  /// P-51's guard is for mixed-level children, which this is not.
  /// </summary>
  [Fact]
  public void FindByIndOnAnEmptyTreeReturnsNothing()
  {
    var tree = new RTreeManager();

    Assert.Empty(tree.findByInd(0));
    Assert.Empty(tree.findByXY(0, 0));
  }

  [Fact]
  public void FindByIndForAnAbsentIdReturnsNothing()
  {
    var tree = new RTreeManager();
    tree.addFeature(Id0, BoundingBox.Point(1, 1));

    Assert.Empty(tree.findByInd(999));
  }

  /// <summary>
  /// Area/Perimeter now delegate to BoundingBox (P-67). They feed split()'s second and
  /// third sort keys, so a wrong value silently picks a worse split.
  /// </summary>
  [Fact]
  public void NodeAreaAndPerimeterAreTheBoundingBoxes()
  {
    var tree = new RTreeManager();
    var box = new BoundingBox(0, 0, 10, 5);
    tree.addFeature(Id0, box);

    Assert.Equal(box.Area(), tree.Root.Area);
    Assert.Equal(box.Perimeter(), tree.Root.Perimeter);
    Assert.Equal(50, tree.Root.Area);
    Assert.Equal(30, tree.Root.Perimeter);
  }

  /// <summary>
  /// Deliberately does NOT call BoundingBox.Overlaps -- see P-48.2 / T-23. The existing
  /// FeatureIndicesAt oracle now calls the same predicate the traversal under test calls, so
  /// it agrees with the code by construction. This one asks only whether the traversal
  /// surfaced the feature, which is the question these tests actually need.
  /// </summary>
  private static bool Findable(RTreeManager tree, double x, double y, int featureId)
  {
    foreach (var endNode in tree.findByXY(x, y))
    {
      foreach (var child in endNode.Children)
      {
        if (child.FeatureIndex is { Length: > 0 } index && index[0] == featureId)
        {
          return true;
        }
      }
    }
    return false;
  }

  /// Collect the feature indices of end nodes returned by findByXY that actually contain the point.
  private static List<int> FeatureIndicesAt(RTreeManager tree, double x, double y)
  {
    var indices = new List<int>();
    foreach (var leaf in tree.findByXY(x, y))
    {
      foreach (var child in leaf.Children)
      {
        var ind = child.FeatureIndex;
        if (ind is not null && child.BoundingBox.Overlaps(new BoundingBox(x, y, x, y)))
          indices.Add(ind[0]);
      }
    }
    return indices;
  }
}
