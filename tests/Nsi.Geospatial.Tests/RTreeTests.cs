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
    [Fact]
    public void FindByXY_FindsContainingFeature()
    {
        var tree = new RTreeManager();
        tree.addFeature(new[] { 0, 0 }, new BoundingBox(0, 0, 10, 10)); // Xmax=10, Xmin=0, Ymax=10, Ymin=0
        tree.addFeature(new[] { 1, 0 }, new BoundingBox(50, 50, 60, 60)); // Xmax=60, Xmin=50, Ymax=60, Ymin=50

        var hits = FeatureIndicesAt(tree, 5, 5);
        Assert.Contains(0, hits);
        Assert.DoesNotContain(1, hits);
    }

    [Fact]
    public void FindByXY_PointOutsideFeatureMBR_NotReturned()
    {
        var tree = new RTreeManager();
        tree.addFeature(new[] { 0, 0 }, new BoundingBox(0, 0, 10, 10));
        tree.addFeature(new[] { 1, 0 }, new BoundingBox(50, 50, 60, 60));

        var hits = FeatureIndicesAt(tree, 55, 55);
        Assert.Contains(1, hits);
        Assert.DoesNotContain(0, hits);
    }

    [Fact]
    public void FindByInd_ReturnsLeafToRootPath()
    {
        var tree = new RTreeManager();
        for (int i = 0; i < 100; i++)
            tree.addFeature(new[] { i, 0 }, new BoundingBox(i * 10, i * 10, i * 10 + 5, i * 10 + 5));

        var path = tree.findByInd(42);
        Assert.NotEmpty(path);

        // The original findByInd returns the node path leaf -> root; the feature
        // node (with its _featureIndex) lives under the leaf.
        var leaf = path[0];
        var featureIndex = leaf._children
            .Select(c => c._featureIndex)
            .FirstOrDefault(a => a is not null && a[0] == 42);
        Assert.NotNull(featureIndex);
    }

    [Fact] //(Skip = "Fails by design: asserts correct behavior that the original (unfixed) RTree split/overlap defects violate. Re-enable once the RTree defects are fixed.")]
    public void BulkInsert_AllFeaturesFindableByPoint()
    {
        var tree = new RTreeManager(minChilds: 3, maxChilds: 6);
        for (int i = 0; i < 500; i++)
            tree.addFeature(new[] { i, 0 }, new BoundingBox(i * 10, i * 10, i * 10 + 5, i * 10 + 5));

        for (int i = 0; i < 500; i++)
        {
            var hits = FeatureIndicesAt(tree, i * 10 + 2.5, i * 10 + 2.5);
            if (!hits.Contains(i))
            {
                string test = "WTF";
            }
            Assert.Contains(i, hits);
        }
    }

    [Fact]
    public void GetEndNodes_LeavesHoldAllFeatureIndices()
    {
        var tree = new RTreeManager(minChilds: 3, maxChilds: 6);
        for (int i = 0; i < 50; i++)
            tree.addFeature(new[] { i, 0 }, new BoundingBox(i * 10, i * 10, i * 10 + 5, i * 10 + 5));

        var leaves = tree.getEndNodes;
        Assert.NotEmpty(leaves);

        var allIndices = new List<int>();
        foreach (var leaf in leaves)
        {
            foreach (var child in leaf._children)
            {
                var ind = child._featureIndex;
                if (ind is not null)
                    allIndices.Add(ind[0]);
            }
        }
        Assert.Equal(50, allIndices.Distinct().Count());
    }

    /// Collect the feature indices of end nodes returned by findByXY that actually contain the point. 
    private static List<int> FeatureIndicesAt(RTreeManager tree, double x, double y)
    {
        var indices = new List<int>();
        foreach (var leaf in tree.findByXY(x, y))
        {
            foreach (var child in leaf._children)
            {
                var ind = child._featureIndex;
                if (ind is not null && child.getMBRoverlap(new BoundingBox(x, y, x, y)) > 0)
                    indices.Add(ind[0]);
            }
        }
        return indices;
    }
}