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

  /// <summary>Above int.MaxValue. The (int) cast stored this as -294967296.</summary>
  private const long AboveInt32 = 4_000_000_000L;

  [Fact]
  public void ReadsAnInteger64AuthoredElsewhere()
  {
    const string GeoJson = """
      {"type":"FeatureCollection","features":[{"type":"Feature",
      "geometry":{"type":"Point","coordinates":[1.0,2.0]},
      "properties":{"BIG":4000000000,"SMALL":7,"NAME":"abc"}}]}
      """;

    string dir = TempDir();
    try
    {
      string path = Path.Combine(dir, "foreign.geojson");
      File.WriteAllText(path, GeoJson);

      var fc = new SpatialReader().Read(path);

      // Above int.MaxValue, so GDAL infers Integer64 rather than Integer.
      var big = Assert.IsType<long>(fc[0].Attributes["BIG"]);
      Assert.Equal(4_000_000_000L, big);
      Assert.Equal(FieldType.LongFT, fc.Schema["BIG"].FieldType);

      // And an in-range neighbour still reads as int, not long.
      Assert.IsType<int>(fc[0].Attributes["SMALL"]);
    }
    finally
    {
      Cleanup(dir);
    }
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

