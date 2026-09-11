using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Io;
using OSGeo.OGR;
using Xunit;
// Both FieldType and Feature exist in Nsi.Geospatial.* and in OSGeo.OGR. Aliasing the
// near ones (plus the OGR enum) keeps the body readable without ambiguity. The comment
// splits the block into two independently-sorted groups, both already alphabetical, so
// dotnet_sort_system_directives_first has nothing to reorder.
using Feature = Nsi.Geospatial.Geometry.Feature;
using FieldType = Nsi.Geospatial.Enums.FieldType;
using OgrFieldType = OSGeo.OGR.FieldType;

namespace Nsi.Geospatial.Io.Tests;

/// <summary>
/// LongFT through the writer and reader (P-06).
///
/// Two independent assertions on purpose. The CLR value alone cannot fail: a LongFT
/// column created as OFTString still yields "4000000000", and Feature.GetAttribute&lt;long&gt;
/// calls Convert.ChangeType, so a value-level check passes on a column stored as text.
/// The declared OGR field type, and the boxed type the reader puts in Attributes, are
/// the only things that discriminate.
/// </summary>
[Trait("Category", "Gdal")]
public class FieldTypeTests
{
  private const string Column = "BIG";
  private const string SmallColumn = "SMALL";

  /// <summary>Above int.MaxValue. The (int) cast stored this as -294967296.</summary>
  private const long AboveInt32 = 4_000_000_000L;

  /// <summary>
  /// T-17, the writer half of P-06. LongFT must reach OGR as OFTInteger64 and not fall through
  /// MapFieldType's `_ => OFTString`. Asserted on the field definition ON DISK, because the CLR
  /// value cannot discriminate: a LongFT column written as text still yields "4000000000", and
  /// Feature.GetAttribute&lt;long&gt; calls Convert.ChangeType, so a value-only test passes on the
  /// bug. Shapefile rather than GeoJSON because GeoJSON re-infers field types from the document
  /// when it reads, so its reported type is the driver's guess, not what the writer declared.
  /// </summary>
  [Fact]
  public void LongColumnIsDeclaredInteger64OnDisk()
  {
    string dir = TempDir();
    try
    {
      string path = Path.Combine(dir, "long.shp");
      new SpatialWriter().Write(WithLongColumn(), path);

      using (
        var ds =
          Ogr.Open(path, 0)
          ?? throw new InvalidOperationException("writer produced no readable dataset")
      )
      {
        using var layer = ds.GetLayerByIndex(0);
        var defn = layer.GetLayerDefn();
        using var field = defn.GetFieldDefn(0);
        Assert.Equal(Column, field.GetName());
        Assert.Equal(OgrFieldType.OFTInteger64, field.GetFieldType());
      }

      var read = new SpatialReader().Read(path);
      Assert.Equal(FieldType.LongFT, read.Schema[Column].FieldType);
      Assert.Equal(AboveInt32, Assert.IsType<long>(read[0].Attributes[Column]));
    }
    finally
    {
      Cleanup(dir);
    }
  }

  /// <summary>
  /// Negative control for the test above. The shapefile driver decides Integer vs Integer64 on
  /// read from the field's width, so if that rule ever made every numeric field report Int64 the
  /// assertion above would pass for the wrong reason. An IntegerFT column authored beside the
  /// LongFT one must still declare OFTInteger and still box as int.
  /// </summary>
  [Fact]
  public void IntegerColumnBesideItIsStillDeclaredInteger()
  {
    string dir = TempDir();
    try
    {
      string path = Path.Combine(dir, "long.shp");
      new SpatialWriter().Write(WithLongColumn(), path);

      using (
        var ds =
          Ogr.Open(path, 0)
          ?? throw new InvalidOperationException("writer produced no readable dataset")
      )
      {
        using var layer = ds.GetLayerByIndex(0);
        using var field = layer.GetLayerDefn().GetFieldDefn(1);
        Assert.Equal(SmallColumn, field.GetName());
        Assert.Equal(OgrFieldType.OFTInteger, field.GetFieldType());
      }

      var read = new SpatialReader().Read(path);
      Assert.Equal(7, Assert.IsType<int>(read[0].Attributes[SmallColumn]));
    }
    finally
    {
      Cleanup(dir);
    }
  }

  /// <summary>
  /// One point feature, two numeric columns. Widths matter: the DBF ceiling is 18, and the
  /// shapefile driver reads a zero-decimal numeric field back as Integer64 only when the width
  /// is wide enough to need it, so 18 and 9 are load-bearing, not decoration (see P-21: a width
  /// of 20 is what forces the OFTReal demotion).
  /// </summary>
  private static Features WithLongColumn()
  {
    var fc = new Features { ShapeType = ShapeType.Point };
    fc.Schema.AddField(Column, FieldType.LongFT, 18, 0);
    fc.Schema.AddField(SmallColumn, FieldType.IntegerFT, 9, 0);

    var point = new Part(PartType.Point);
    point.AddVertex(new Vertex(1.0, 2.0));
    point.Seal();

    var f = new Feature { ShapeType = ShapeType.Point };
    f.AddPart(point);
    f.ComputeBoundingBox();
    f.Attributes[Column] = AboveInt32;
    f.Attributes[SmallColumn] = 7; // int, so SetOgrField's `case int i:` is exercised too
    fc.AddFeature(f);
    return fc;
  }

  // Duplicated from SpatialIoTests / CrsInspectionTests -- hoist to a shared fixture
  // under P-36 rather than adding a fourth copy.
  private static string TempDir()
  {
    string dir = Path.Combine(Path.GetTempPath(), "nsi-long-" + Guid.NewGuid().ToString("N"));
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
    { /* leftover temp files are not a test failure */
    }
  }
}
