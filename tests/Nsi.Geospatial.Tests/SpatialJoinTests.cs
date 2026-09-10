using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Spatial;
using Xunit;

namespace Nsi.Geospatial.Core.Tests;

public class SpatialJoinTests
{
  [Fact]
  public void NearestPointsToPolygonsFirstJoinCopiesValue()
  {
    var polys = new Features { ShapeType = ShapeType.Polygon };
    var p1 = new Feature { ShapeType = ShapeType.Polygon };
    p1.Parts.Add(new Part(PartType.Ring) { IsHole = false });
    p1.Parts[0].AddVertex(new Vertex(0, 0));
    p1.Parts[0].AddVertex(new Vertex(10, 0));
    p1.Parts[0].AddVertex(new Vertex(0, 10));
    p1.Parts[0].AddVertex(new Vertex(0, 0));
    p1.Parts[0].Seal();
    p1.ComputeBoundingBox();
    polys.AddFeature(p1);
    polys.Schema.AddField("VALUE", FieldType.DoubleFT, 12, 2);

    var pnts = new Features { ShapeType = ShapeType.Point };
    var pp = new Feature();
    pp.Parts.Add(new Part(PartType.Point));
    pp.Parts[0].AddVertex(new Vertex(1, 1));
    pp.ComputeBoundingBox();
    pp.Attributes["VALUE"] = 42.0;
    pnts.AddFeature(pp);
    pnts.Schema.AddField("VALUE", FieldType.DoubleFT, 12, 2);

    SpatialJoins.NearestPointsToPolygons(
      polys,
      pnts,
      destFields: ["VALUE"],
      sourceFields: ["VALUE"],
      joinType: JoinType.First,
      pointTree: SpatialJoins.BuildTree(pnts)
    );

    Assert.Equal(42.0, p1.Attributes["VALUE"]);
  }
}
