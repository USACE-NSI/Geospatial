using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Spatial;
using Xunit;

namespace Nsi.Geospatial.Core.Tests;

public class SpatialJoinTests
{
  /// CA1861: the join methods take string[] and are called from more than one test here.
  private static readonly string[] IdFields = ["ID"];

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

  /// <summary>
  /// T-7 / P-56: the nearest polygon must not depend on whether the caller typed
  /// the closing vertex. Part permits either authoring (see its Vertices docstring)
  /// and the metric primitives close implicitly, so the join has to as well.
  ///
  /// The answer and the failure pick different winners, which is the only way to
  /// guard a private distance method through the public API:
  ///   A measures 1.0 (foot at (0,5) on its left edge) but its nearest VERTEX is
  ///   (0,0) at sqrt(26) = 5.0990.  B measures 3.6401, vertex and edge alike.
  /// So a loop that degenerates to nearest-vertex picks B, and a loop that skips
  /// the closing edge picks B for the open authoring only.
  /// </summary>
  [Theory]
  [InlineData(false)] // authored open: the wrap supplies the closing edge
  [InlineData(true)] // authored closed: the closing edge is an ordinary edge
  public void NearestPolygonIsIdenticalUnderEitherRingAuthoring(bool closeAuthoredRing)
  {
    var polys = new Features { ShapeType = ShapeType.Polygon };
    polys.Schema.AddField("ID", FieldType.IntegerFT, 10, 0);
    polys.AddFeature(Rectangle(0, 0, 10, 10, id: 0, closeAuthoredRing)); // truth 1.0
    polys.AddFeature(Rectangle(0, -1.5, 10, 1.5, id: 1, closeAuthoredRing)); // truth 3.6401

    var pts = new Features { ShapeType = ShapeType.Point };
    pts.AddFeature(PointAt(-1, 5));

    SpatialJoins.NearestPolygonsToPoints(pts, polys, destFields: IdFields, sourceFields: IdFields);

    Assert.Equal(0, pts[0].Attributes["ID"]);
  }

  private static Feature Rectangle(
    double minX,
    double minY,
    double maxX,
    double maxY,
    int id,
    bool closeAuthoredRing
  )
  {
    var ring = new Part(PartType.Ring);
    ring.AddVertex(new Vertex(minX, minY));
    ring.AddVertex(new Vertex(maxX, minY));
    ring.AddVertex(new Vertex(maxX, maxY));
    ring.AddVertex(new Vertex(minX, maxY));
    if (closeAuthoredRing)
    {
      ring.AddVertex(new Vertex(minX, minY));
    }
    ring.Seal();

    var feature = new Feature { ShapeType = ShapeType.Polygon };
    feature.AddPart(ring);
    feature.ComputeBoundingBox();
    feature.Attributes["ID"] = id;
    return feature;
  }

  private static Feature PointAt(double x, double y)
  {
    var point = new Part(PartType.Point);
    point.AddVertex(new Vertex(x, y));
    point.Seal();

    var feature = new Feature { ShapeType = ShapeType.Point };
    feature.AddPart(point);
    feature.ComputeBoundingBox(); // DistanceFeatureToFeature reads BoundingBox.MinX/MinY
    return feature;
  }
}
