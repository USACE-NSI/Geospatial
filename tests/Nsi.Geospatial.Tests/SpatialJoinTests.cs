using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Spatial;
using Xunit;

namespace Nsi.Geospatial.Core.Tests;

public class SpatialJoinTests
{
    [Fact]
    public void NearestPointsToPolygons_FirstJoin_CopiesValue()
    {
        var polys = new FeatureCollection { ShapeType = ShapeType.Polygon };
        var p1 = new Feature { ShapeType = ShapeType.Polygon };
        p1.Parts.Add(new Part { IsHole = false });
        p1.Parts[0].AddVertex(new Vertex(0, 0));
        p1.Parts[0].AddVertex(new Vertex(10, 0));
        p1.Parts[0].AddVertex(new Vertex(0, 10));
        p1.Parts[0].AddVertex(new Vertex(0, 0));
        p1.Parts[0].CloseRing();
        p1.ComputeMbr();
        polys.AddFeature(p1);
        polys.Schema.AddField("VALUE", FieldType.Double, 12, 2);

        var pnts = new FeatureCollection { ShapeType = ShapeType.Point };
        var pp = new Feature();
        pp.Parts.Add(new Part());
        pp.Parts[0].AddVertex(new Vertex(1, 1));
        pp.ComputeMbr();
        pp.Attributes["VALUE"] = 42.0;
        pnts.AddFeature(pp);
        pnts.Schema.AddField("VALUE", FieldType.Double, 12, 2);

        SpatialJoins.NearestPointsToPolygons(
            polys, pnts,
            destFields: ["VALUE"], sourceFields: ["VALUE"],
            joinType: JoinType.First,
            pointTree: SpatialJoins.BuildTree(pnts));

        Assert.Equal(42.0, p1.Attributes["VALUE"]);
    }
}