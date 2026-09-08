using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Io;
using Nsi.Geospatial.Projections;
using OSGeo.OSR;
using Xunit;

namespace Nsi.Geospatial.Io.Tests;

/// <summary>
/// CrsInspector and the reader plumbing that turns an inspected CRS into working
/// derived metrics. These are the tests for the claim that a caller can ask for
/// AreaSquareMeters and get square metres, in either CRS kind, with no transform.
/// </summary>
[Trait("Category", "Gdal")]
public class CrsInspectionTests
{
  [Fact]
  public void GeographicWktInspectsAsGeographicWithNoLinearUnit()
  {
    // The regression this guards: GDAL reports the ANGULAR unit factor (1.0,
    // "degree") from GetLinearUnits on a geographic SRS. Recording that as a
    // linear unit would make Part.AreaSquareMeters multiply square degrees by 1.0
    // and hand back a plausible-looking, meaningless number.
    CrsInfo info = CrsInspector.Inspect(Projection.Wgs84.Wkt);

    Assert.Equal(CrsKind.Geographic, info.Kind);
    Assert.Equal(LinearUnit.Unknown, info.Unit);
    Assert.Equal(0.0, info.UnitToMeters);
    Assert.Equal(4326, info.EpsgCode);
    Assert.False(string.IsNullOrWhiteSpace(info.Wkt));
  }

  [Fact]
  public void ProjectedWktInspectsAsProjectedInMetres()
  {
    CrsInfo info = CrsInspector.Inspect(Projection.AlbersUsa.Wkt);

    Assert.Equal(CrsKind.Projected, info.Kind);
    Assert.Equal(LinearUnit.Meter, info.Unit);
    Assert.Equal(1.0, info.UnitToMeters, 12);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("   ")]
  [InlineData("not a spatial reference at all")]
  public void UnreadableInputInspectsAsUnknown(string? wkt) =>
    Assert.Equal(CrsKind.Unknown, CrsInspector.Inspect(wkt).Kind);

  [Fact]
  public void NullSpatialReferenceInspectsAsUnknown() =>
    Assert.Equal(CrsKind.Unknown, CrsInspector.Inspect((SpatialReference?)null).Kind);

  [Fact]
  public void InspectionDoesNotMutateTheSuppliedReference()
  {
    // ReadEpsgCode runs AutoIdentifyEPSG, which writes AUTHORITY nodes into the
    // object it touches. The layer's own SRS is not ours to modify.
    var srs = new SpatialReference(Projection.Nad83.Wkt);
    try
    {
      string before = Export(srs);
      CrsInspector.Inspect(srs);
      Assert.Equal(before, Export(srs));
    }
    finally
    {
      srs.Dispose();
    }

    static string Export(SpatialReference s)
    {
      s.ExportToWkt(out string w, Array.Empty<string>());
      return w;
    }
  }

  [Fact]
  public void GeographicRoundTripYieldsWorkingSquareMetresWithoutAnyTransform()
  {
    // The headline feature, end to end: write lon/lat, read it back, and get an
    // area in square metres that matches the hand-computed sphere.
    string dir = TempDir();
    try
    {
      var fc = new FeatureCollection
      {
        Name = "cells",
        ShapeType = ShapeType.Polygon,
        Crs = new CrsInfo
        {
          Kind = CrsKind.Geographic,
          Wkt = Projection.Wgs84.Wkt,
          EpsgCode = 4326,
        },
      };
      fc.Schema.AddField("id", FieldType.IntegerFT, 0, 0);

      var f = new Feature { ShapeType = ShapeType.Polygon };
      f.Attributes["id"] = 1;
      var ring = new Part { Direction = true };
      foreach (var (x, y) in Cell(0, 44))
        ring.AddVertex(new Vertex(x, y));
      ring.CloseRing();
      f.AddPart(ring);
      fc.AddFeature(f);

      string path = Path.Combine(dir, "cell.shp");
      new SpatialWriter().Write(fc, path, "ESRI Shapefile");

      var read = new SpatialReader().Read(path);

      Assert.Equal(CrsKind.Geographic, read.Crs.Kind);
      Assert.Equal(LinearUnit.Unknown, read.Crs.Unit);
      Assert.Equal(0.0, read.Crs.UnitToMeters);

      double? area = read[0].Parts[0].AreaSquareMeters;
      Assert.NotNull(area);
      double expected = 8.8187588297044e9; // 1x1 degree cell at 44N, authalic sphere
      double rel = Math.Abs(area!.Value - expected) / expected;
      Assert.True(rel < 1e-9, $"area {area:E6} differs from {expected:E6} (rel {rel:E3})");

      // The planar cache is still square degrees; only the new property is usable.
      Assert.Equal(1.0, read[0].Parts[0].Area, 6);
    }
    finally
    {
      Cleanup(dir);
    }
  }

  [Fact]
  public void ProjectedRoundTripYieldsSquareMetresFromThePrjFile()
  {
    string dir = TempDir();
    try
    {
      var fc = new FeatureCollection
      {
        Name = "squares",
        ShapeType = ShapeType.Polygon,
        Crs = new CrsInfo
        {
          Kind = CrsKind.Projected,
          Unit = LinearUnit.Meter,
          UnitToMeters = 1.0,
          Wkt = Projection.AlbersUsa.Wkt,
          EpsgCode = 102003,
        },
      };
      fc.Schema.AddField("id", FieldType.IntegerFT, 0, 0);

      var f = new Feature { ShapeType = ShapeType.Polygon };
      f.Attributes["id"] = 1;
      var ring = new Part { Direction = true };
      foreach (var (x, y) in new[] { (0.0, 0.0), (1000.0, 0.0), (1000.0, 1000.0), (0.0, 1000.0) })
        ring.AddVertex(new Vertex(x, y));
      ring.CloseRing();
      f.AddPart(ring);
      fc.AddFeature(f);

      string path = Path.Combine(dir, "square.shp");
      new SpatialWriter().Write(fc, path, "ESRI Shapefile");

      var read = new SpatialReader().Read(path);

      Assert.Equal(CrsKind.Projected, read.Crs.Kind);
      Assert.Equal(LinearUnit.Meter, read.Crs.Unit);
      Assert.Equal(1.0, read.Crs.UnitToMeters, 9);

      double? area = read[0].Parts[0].AreaSquareMeters;
      Assert.NotNull(area);
      Assert.True(Math.Abs(area!.Value - 1_000_000.0) < 1e-3, $"expected 1e6 m2, got {area:E6}");
    }
    finally
    {
      Cleanup(dir);
    }
  }

  [Fact]
  public void RequireInspectableCrsThrowsWhenTheSourceHasNoPrj()
  {
    // Default behaviour is null metrics; the option is the fail-loud variant, so
    // a misconfigured pipeline cannot silently produce a table of empty areas.
    string dir = TempDir();
    try
    {
      var fc = new FeatureCollection { Name = "bare", ShapeType = ShapeType.Polygon };
      fc.Schema.AddField("id", FieldType.IntegerFT, 0, 0);
      var f = new Feature { ShapeType = ShapeType.Polygon };
      f.Attributes["id"] = 1;
      var ring = new Part { Direction = true };
      foreach (var (x, y) in Cell(0, 44))
        ring.AddVertex(new Vertex(x, y));
      ring.CloseRing();
      f.AddPart(ring);
      fc.AddFeature(f);

      string path = Path.Combine(dir, "bare.shp");
      new SpatialWriter().Write(fc, path, "ESRI Shapefile");

      var lenient = new SpatialReader().Read(path);
      Assert.Equal(CrsKind.Unknown, lenient.Crs.Kind);
      Assert.Null(lenient[0].Parts[0].AreaSquareMeters);

      Assert.Throws<InvalidOperationException>(() =>
        new SpatialReader(new SpatialReaderOptions { RequireInspectableCrs = true }).Read(path)
      );
    }
    finally
    {
      Cleanup(dir);
    }
  }

  [Fact]
  public void LayerIndexSelectsTheRequestedLayerAndFailsClearlyOtherwise()
  {
    string dir = TempDir();
    try
    {
      var fc = new FeatureCollection
      {
        Name = "one",
        ShapeType = ShapeType.Point,
        Crs = new CrsInfo
        {
          Kind = CrsKind.Geographic,
          Wkt = Projection.Wgs84.Wkt,
          EpsgCode = 4326,
        },
      };
      fc.Schema.AddField("id", FieldType.IntegerFT, 0, 0);
      var p = new Feature { ShapeType = ShapeType.Point };
      p.Attributes["id"] = 1;
      var part = new Part();
      part.AddVertex(new Vertex(-93.0, 44.0));
      p.AddPart(part);
      fc.AddFeature(p);

      string path = Path.Combine(dir, "pts.shp");
      new SpatialWriter().Write(fc, path, "ESRI Shapefile");

      Assert.Equal(
        1,
        new SpatialReader(new SpatialReaderOptions { LayerIndex = 0 }).Read(path).Count
      );

      var ex = Assert.Throws<InvalidOperationException>(() =>
        new SpatialReader(new SpatialReaderOptions { LayerIndex = 5 }).Read(path)
      );
      Assert.Contains("5", ex.Message);
    }
    finally
    {
      Cleanup(dir);
    }
  }

  [Fact]
  public void ReprojectToPopulatesTheTargetCrsAndMakesPlanarMetricsEqualArea()
  {
    // Reading lon/lat straight into Albers: the collection's Crs must describe the
    // TARGET, and because Albers is equal-area the planar shoelace must now agree
    // with the spherical area to well within the sphere-vs-ellipsoid term.
    string dir = TempDir();
    try
    {
      var fc = new FeatureCollection
      {
        Name = "cell",
        ShapeType = ShapeType.Polygon,
        Crs = new CrsInfo
        {
          Kind = CrsKind.Geographic,
          Wkt = Projection.Wgs84.Wkt,
          EpsgCode = 4326,
        },
      };
      fc.Schema.AddField("id", FieldType.IntegerFT, 0, 0);
      var f = new Feature { ShapeType = ShapeType.Polygon };
      f.Attributes["id"] = 1;
      var ring = new Part { Direction = true };
      foreach (var (x, y) in Cell(-93.0, 44.0))
        ring.AddVertex(new Vertex(x, y));
      ring.CloseRing();
      f.AddPart(ring);
      fc.AddFeature(f);

      string path = Path.Combine(dir, "cell.shp");
      new SpatialWriter().Write(fc, path, "ESRI Shapefile");

      var read = new SpatialReader(
        new SpatialReaderOptions { ReprojectTo = Projection.AlbersUsa }
      ).Read(path);

      Assert.Equal(CrsKind.Projected, read.Crs.Kind);
      Assert.Equal(1.0, read.Crs.UnitToMeters, 9);

      double planar = read[0].Parts[0].AreaSquareMeters!.Value;

      // Spherical answer for the same cell, computed from the source vertices.
      var src = new Part { Direction = true };
      foreach (var (x, y) in Cell(-93.0, 44.0))
        src.AddVertex(new Vertex(x, y));
      src.CloseRing();
      var srcFc = new FeatureCollection
      {
        ShapeType = ShapeType.Polygon,
        Crs = new CrsInfo { Kind = CrsKind.Geographic },
      };
      srcFc.AddFeature(IntoFeature(src));
      double spherical = srcFc.Features[0].AreaSquareMeters!.Value;

      // WGS84 truth for this cell: 8.8373695264e9 m2. The sphere is 0.21% low;
      // Albers on the ellipsoid should land far closer than that.
      const double ellipsoid = 8.8373695264e9;
      double planarError = Math.Abs(planar - ellipsoid) / ellipsoid;
      double sphericalError = Math.Abs(spherical - ellipsoid) / ellipsoid;

      Assert.True(
        planarError < 1e-4,
        $"Albers planar area {planar:E6} is {planarError:E3} from WGS84 truth"
      );
      Assert.True(planarError < sphericalError, "reprojected area should beat the spherical one");
    }
    finally
    {
      Cleanup(dir);
    }
  }

  private static List<(double X, double Y)> Cell(
    double lon,
    double lat,
    double w = 1,
    double h = 1
  ) => new() { (lon, lat), (lon + w, lat), (lon + w, lat + h), (lon, lat + h) };

  private static Feature IntoFeature(Part part)
  {
    var f = new Feature();
    f.AddPart(part);
    return f;
  }

  private static string TempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "nsi-crs-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    return dir;
  }

  private static void Cleanup(string dir)
  {
    try
    {
      Directory.Delete(dir, true);
    }
    catch (IOException)
    {
      // Leftover temp files are not a test failure.
    }
  }
}

