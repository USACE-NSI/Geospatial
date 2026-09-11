using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Spatial;
using Xunit;

namespace Nsi.Geospatial.Tests;

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

  /// <summary>
  /// P-11a. A part with no vertices used to reach `part.Vertices[0]` and throw
  /// IndexOutOfRangeException from inside a join, for every point in the collection.
  /// It is skipped now, so the feature's other part still measures and still wins.
  /// </summary>
  [Fact]
  public void PolygonWithAZeroVertexPartIsSkippedAndStillMeasurable()
  {
    var polys = new Features { ShapeType = ShapeType.Polygon };
    polys.Schema.AddField("ID", FieldType.IntegerFT, 10, 0);

    var near = Rectangle(0, 0, 10, 10, id: 0, closeAuthoredRing: false);
    near.AddPart(new Part(PartType.Ring)); // zero vertices, added last so order is not doing the work
    polys.AddFeature(near);

    polys.AddFeature(Rectangle(0, -1.5, 10, 1.5, id: 1, closeAuthoredRing: false)); // 3.6401

    var pts = new Features { ShapeType = ShapeType.Point };
    pts.AddFeature(PointAt(-1, 5));

    SpatialJoins.NearestPolygonsToPoints(pts, polys, destFields: IdFields, sourceFields: IdFields);

    // The empty part costs nothing: the ring still measures 1.0 and still wins.
    Assert.Equal(0, pts[0].Attributes["ID"]);
  }

  /// <summary>
  /// P-11b. A polygon with no parts is unmeasurable, not infinitely far. Before the fix it
  /// returned double.MaxValue, which was non-null, so every point tied against every other
  /// (|MaxValue - MaxValue| == 0 < 1e-9): the polygon landed in matched and Aggregate wrote a
  /// Count/Sum computed over the entire point collection into a feature with no geometry.
  /// </summary>
  [Fact]
  public void PolygonWithNoPartsIsNotMatchedAndWritesNoAttributes()
  {
    var polys = new Features { ShapeType = ShapeType.Polygon };
    polys.Schema.AddField("VALUE", FieldType.DoubleFT, 12, 2);

    var empty = new Feature { ShapeType = ShapeType.Polygon };
    empty.ComputeBoundingBox(); // no parts at all
    polys.AddFeature(empty); // index 0

    var square = Rectangle(0, 0, 10, 10, id: 1, closeAuthoredRing: false);
    square.Attributes["VALUE"] = 7.0;
    polys.AddFeature(square); // index 1

    var pts = new Features { ShapeType = ShapeType.Point };
    pts.AddFeature(PointAt(-1, 5));
    pts.Schema.AddField("VALUE", FieldType.DoubleFT, 12, 2);
    pts[0].Attributes["VALUE"] = 42.0;

    var matched = SpatialJoins.NearestPointsToPolygons(
      polys,
      pts,
      destFields: ["VALUE"],
      sourceFields: ["VALUE"],
      joinType: JoinType.First
    );

    Assert.Single(matched);
    Assert.Equal(1L, matched[0]);
    Assert.False(empty.Attributes.ContainsKey("VALUE")); // no fabrication, not even a null
    Assert.Equal(42.0, square.Attributes["VALUE"]);
  }

  /// <summary>
  /// P-11c. Unmeasurable candidates must not win by being first. best starts null, so the
  /// first candidate measured wins by default; with MaxValue candidates that default landed on
  /// an empty polygon and copied its attributes into the point. Now they are skipped, so when
  /// nothing is measurable nothing is written at all.
  /// </summary>
  [Fact]
  public void UnmeasurablePolygonsWinNothingEvenWhenTheyComeFirst()
  {
    var polys = new Features { ShapeType = ShapeType.Polygon };
    polys.Schema.AddField("ID", FieldType.IntegerFT, 10, 0);
    foreach (int id in new[] { 0, 1, 2 })
    {
      var f = new Feature { ShapeType = ShapeType.Polygon };
      f.Attributes["ID"] = id;
      polys.AddFeature(f);
    }

    var pts = new Features { ShapeType = ShapeType.Point };
    pts.AddFeature(PointAt(-1, 5));

    SpatialJoins.NearestPolygonsToPoints(pts, polys, destFields: IdFields, sourceFields: IdFields);

    Assert.False(pts[0].Attributes.ContainsKey("ID"));
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

