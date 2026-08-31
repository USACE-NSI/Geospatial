using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Spatial;
using Xunit;

namespace Nsi.Geospatial.Core.Tests;

public class RTreeTests
{
    [Fact]
    public void QueryPoint_FindsContainingFeature()
    {
        var tree = new RTree();
        tree.Add(0, 0, new BoundingBox(0, 0, 10, 10));
        tree.Add(1, 0, new BoundingBox(50, 50, 60, 60));

        var hits = tree.QueryPoint(5, 5);
        Assert.Contains((0, 0), hits);
        Assert.DoesNotContain((1, 0), hits);
    }

    [Fact]
    public void QueryBox_FullyCoveredNodeStillOverlaps()
    {
        // fix(#15): a query box that fully contains the node box must still overlap.
        var tree = new RTree();
        tree.Add(0, 0, new BoundingBox(10, 10, 20, 20));

        var hits = tree.Query(new BoundingBox(0, 0, 100, 100));
        Assert.Contains((0, 0), hits);
    }

    [Fact]
    public void BulkInsert_AllFeaturesRetrievable()
    {
        var tree = new RTree(minEntries: 3, maxEntries: 6);
        var expected = new HashSet<(int, int)>();
        for (int i = 0; i < 500; i++)
        {
            var box = new BoundingBox(i * 10, i * 10, i * 10 + 5, i * 10 + 5);
            tree.Add(i, 0, box);
            expected.Add((i, 0));
        }
        var all = tree.Query(new BoundingBox(-1000, -1000, 10000, 10000));
        Assert.Equal(expected, all.ToHashSet());
    }
}