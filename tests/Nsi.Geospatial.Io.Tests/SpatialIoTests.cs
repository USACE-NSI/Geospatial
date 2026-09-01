using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Io;
using Xunit;

namespace Nsi.Geospatial.Io.Tests;

/// <summary>
/// Round-trip tests for <see cref="SpatialWriter"/> / <see cref="SpatialReader"/>.
/// Geometry is built in memory, written to a real OGR file, read back, and asserted.
/// No network or committed fixtures are required: the spatial files are produced
/// by the test itself in a temp directory.
/// </summary>
[Trait("Category", "Gdal")]
public class SpatialIoTests
{
    private const double Tol = 1e-9;

    // ------------------------------------------------------------------ points

    [Fact]
    public void PointsShapefileRoundTrip()
    {
        string dir = TempDir();
        try
        {
            var fc = BuildPoints();
            string path = Path.Combine(dir, "points.shp");

            new SpatialWriter().Write(fc, path, "ESRI Shapefile");

            var read = new SpatialReader().Read(path);
            Assert.Equal(ShapeType.Point, read.ShapeType);
            Assert.Equal(fc.Count, read.Count);

            foreach (var orig in fc.Features)
            {
                var back = read[orig.Id];
                Assert.Equal(orig.GetAttribute<int>("id"), back.GetAttribute<int>("id"));
                Assert.Equal(orig.GetAttribute<string>("name"), back.GetAttribute<string>("name"));
                Assert.Equal(orig.GetAttribute<double>("value"), back.GetAttribute<double>("value"), Tol);

                var o = orig.Parts[0].Vertices[0];
                var b = back.Parts[0].Vertices[0];
                Assert.Equal(o.X, b.X, Tol);
                Assert.Equal(o.Y, b.Y, Tol);
            }
        }
        finally
        {
            Cleanup(dir);
        }
    }

    // ------------------------------------------------------------------- lines

    [Fact]
    public void LinesShapefileRoundTrip()
    {
        string dir = TempDir();
        try
        {
            var fc = BuildLines();
            string path = Path.Combine(dir, "lines.shp");

            new SpatialWriter().Write(fc, path, "ESRI Shapefile");

            var read = new SpatialReader().Read(path);
            Assert.Equal(ShapeType.Line, read.ShapeType);
            Assert.Equal(fc.Count, read.Count);

            foreach (var orig in fc.Features)
            {
                var back = read[orig.Id];
                Assert.Equal(orig.GetAttribute<int>("id"), back.GetAttribute<int>("id"));
                Assert.Equal(orig.GetAttribute<double>("value"), back.GetAttribute<double>("value"), Tol);

                // The reader closes every part, so assert on the leading (real) vertices.
                var o = orig.Parts[0].Vertices;
                var b = back.Parts[0].Vertices;
                Assert.True(b.Count >= o.Count, "line lost vertices on round-trip");
                for (int i = 0; i < o.Count; i++)
                {
                    Assert.Equal(o[i].X, b[i].X, Tol);
                    Assert.Equal(o[i].Y, b[i].Y, Tol);
                }
            }
        }
        finally
        {
            Cleanup(dir);
        }
    }

    // ---------------------------------------------------------------- polygons

    [Fact]
    public void PolygonsShapefileRoundTrip()
    {
        string dir = TempDir();
        try
        {
            var fc = BuildPolygons();
            string path = Path.Combine(dir, "polys.shp");

            new SpatialWriter().Write(fc, path, "ESRI Shapefile");

            var read = new SpatialReader().Read(path);
            Assert.Equal(ShapeType.Polygon, read.ShapeType);
            Assert.Equal(fc.Count, read.Count);

            foreach (var orig in fc.Features)
            {
                var back = read[orig.Id];
                Assert.Equal(orig.GetAttribute<int>("id"), back.GetAttribute<int>("id"));

                // MBR must survive the round-trip.
                Assert.Equal(orig.Mbr.MinX, back.Mbr.MinX, Tol);
                Assert.Equal(orig.Mbr.MinY, back.Mbr.MinY, Tol);
                Assert.Equal(orig.Mbr.MaxX, back.Mbr.MaxX, Tol);
                Assert.Equal(orig.Mbr.MaxY, back.Mbr.MaxY, Tol);

                // And the geometry must still contain the same points.
                var ring = back.Parts[0].Vertices;
                Assert.True(PointInPolygon((5, 5), ring), "interior point lost after round-trip");
                Assert.False(PointInPolygon((15, 5), ring), "exterior point now inside");
            }
        }
        finally
        {
            Cleanup(dir);
        }
    }

    // ---------------------------------------------------------------- GeoJSON

    [Fact]
    public void PointsGeoJsonRoundTrip()
    {
        string dir = TempDir();
        try
        {
            var fc = BuildPoints();
            string path = Path.Combine(dir, "points.geojson");

            new SpatialWriter().Write(fc, path, "GeoJSON");
            Assert.True(File.Exists(path), "GeoJSON file was not created");

            var read = new SpatialReader().Read(path);
            Assert.Equal(ShapeType.Point, read.ShapeType);
            Assert.Equal(fc.Count, read.Count);

            foreach (var orig in fc.Features)
            {
                var back = read[orig.Id];
                Assert.Equal(orig.GetAttribute<string>("name"), back.GetAttribute<string>("name"));
                var o = orig.Parts[0].Vertices[0];
                var b = back.Parts[0].Vertices[0];
                Assert.Equal(o.X, b.X, Tol);
                Assert.Equal(o.Y, b.Y, Tol);
            }
        }
        finally
        {
            Cleanup(dir);
        }
    }

    [Fact]
    public void PolygonsGeoJsonRoundTrip()
    {
        string dir = TempDir();
        try
        {
            var fc = BuildPolygons();
            string path = Path.Combine(dir, "polys.geojson");

            new SpatialWriter().Write(fc, path, "GeoJSON");

            var read = new SpatialReader().Read(path);
            Assert.Equal(ShapeType.Polygon, read.ShapeType);
            Assert.Equal(fc.Count, read.Count);

            var back = read[0];
            Assert.Equal(fc[0].Mbr.MinX, back.Mbr.MinX, Tol);
            Assert.Equal(fc[0].Mbr.MaxX, back.Mbr.MaxX, Tol);
            Assert.True(PointInPolygon((5, 5), back.Parts[0].Vertices));
        }
        finally
        {
            Cleanup(dir);
        }
    }

    // ------------------------------------------------------------- intersection

    [Fact]
    public void PointDatasetIntersectsPolygonDataset()
    {
        string dir = TempDir();
        try
        {
            var pts = BuildPoints();
            var polys = BuildPolygons();

            var writer = new SpatialWriter();
            string pPath = Path.Combine(dir, "pts.shp");
            string oPath = Path.Combine(dir, "polys.shp");
            writer.Write(pts, pPath, "ESRI Shapefile");
            writer.Write(polys, oPath, "ESRI Shapefile");

            var readPts = new SpatialReader().Read(pPath);
            var readPolys = new SpatialReader().Read(oPath);

            Assert.Equal(pts.Count, readPts.Count);
            Assert.Equal(polys.Count, readPolys.Count);

            // Every point must classify correctly against the read-back polygons.
            foreach (var p in readPts.Features)
            {
                var v = p.Parts[0].Vertices[0];
                bool insideAny = readPolys.Features.Any(poly =>
                    poly.Parts.Any(part => PointInPolygon((v.X, v.Y), part.Vertices)));

                // Point names are "in-*" / "out-*" to encode the expectation.
                string name = p.GetAttributeAsString("name");
                bool expectedInside = name.StartsWith("in", StringComparison.Ordinal);
                Assert.True(expectedInside == insideAny,
                    $"point '{name}' misclassified: expected inside={expectedInside}, actual={insideAny}");
            }
        }
        finally
        {
            Cleanup(dir);
        }
    }

    // ------------------------------------------------------------------ data

    private static FeatureCollection BuildPoints()
    {
        var fc = NewFc("points", ShapeType.Point);
        AddPoint(fc, 1, "in-center", 5, 5, 3.5);
        AddPoint(fc, 2, "in-corner", 1, 9, 1.25);
        AddPoint(fc, 3, "out-right", 15, 5, 7.0);
        AddPoint(fc, 4, "out-left", -2, 5, 0.5);
        AddPoint(fc, 5, "out-top", 5, 15, 9.75);
        return fc;
    }

    private static FeatureCollection BuildLines()
    {
        var fc = NewFc("lines", ShapeType.Line);
        AddLine(fc, 1, "diag", 2.5, new[] { (0.0, 0.0), (10.0, 10.0) });
        AddLine(fc, 2, "zig", 4.0, new[] { (0.0, 0.0), (5.0, 5.0), (10.0, 0.0) });
        return fc;
    }

    private static FeatureCollection BuildPolygons()
    {
        var fc = NewFc("polys", ShapeType.Polygon);
        AddSquare(fc, 1, "main", 0, 0, 10, 10, 100.0);
        return fc;
    }

    private static FeatureCollection NewFc(string name, ShapeType shapeType)
    {
        var fc = new FeatureCollection { Name = name, ShapeType = shapeType };
        fc.Schema.AddField("id", FieldType.Integer, 0, 0);
        fc.Schema.AddField("name", FieldType.Text, 20, 0);
        fc.Schema.AddField("value", FieldType.Double, 0, 2);
        return fc;
    }

    private static void AddPoint(FeatureCollection fc, int id, string name, double x, double y, double value)
    {
        var f = new Feature { ShapeType = ShapeType.Point };
        f.Attributes["id"] = id;
        f.Attributes["name"] = name;
        f.Attributes["value"] = value;
        var p = new Part();
        p.AddVertex(new Vertex(x, y));
        f.AddPart(p);
        fc.AddFeature(f);
    }

    private static void AddLine(FeatureCollection fc, int id, string name, double value, (double X, double Y)[] verts)
    {
        var f = new Feature { ShapeType = ShapeType.Line };
        f.Attributes["id"] = id;
        f.Attributes["name"] = name;
        f.Attributes["value"] = value;
        var part = new Part();
        foreach (var (x, y) in verts)
            part.AddVertex(new Vertex(x, y));
        f.AddPart(part);
        fc.AddFeature(f);
    }

    private static void AddSquare(FeatureCollection fc, int id, string name,
        double x0, double y0, double x1, double y1, double value)
    {
        var f = new Feature { ShapeType = ShapeType.Polygon };
        f.Attributes["id"] = id;
        f.Attributes["name"] = name;
        f.Attributes["value"] = value;
        var ring = new Part();
        ring.AddVertex(new Vertex(x0, y0));
        ring.AddVertex(new Vertex(x1, y0));
        ring.AddVertex(new Vertex(x1, y1));
        ring.AddVertex(new Vertex(x0, y1));
        f.AddPart(ring); // open ring; the writer closes it
        fc.AddFeature(f);
    }

    // --------------------------------------------------------------- helpers

    /// <summary>Even-odd point-in-polygon. Tolerates closed / double-closed rings.</summary>
    private static bool PointInPolygon((double X, double Y) p, List<Vertex> ring)
    {
        bool inside = false;
        int n = ring.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = ring[i].X, yi = ring[i].Y;
            double xj = ring[j].X, yj = ring[j].Y;
            if (xi == xj && yi == yj)
                continue; // skip the closing duplicate vertex
            bool crosses = (yi > p.Y) != (yj > p.Y)
                && p.X < (xj - xi) * (p.Y - yi) / (yj - yi) + xi;
            if (crosses)
                inside = !inside;
        }
        return inside;
    }

    private static string TempDir()
    {
        string d = Path.Combine(Path.GetTempPath(), "nsi-geo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    private static void Cleanup(string dir)
    {
        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}