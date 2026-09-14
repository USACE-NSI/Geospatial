using Nsi.Geospatial.Projections;
using Nsi.Geospatial.Reprojection;
using Xunit;

namespace Nsi.Geospatial.Io.Tests;

/// <summary>
/// T-14, N-2. The axis-order guarantee, asserted by ordinate.
///
/// PR #8 (051e8d8) deleted Reprojector.cs and its hand-rolled NativeLibrary P/Invoke path --
/// not as cleanup but as a correctness fix, because that path never called
/// SetAxisMappingStrategy(OAMS_TRADITIONAL_GIS_ORDER) and returned TRANSPOSED coordinates for
/// any geographic CRS while CoordinateTransformer returned correct ones. What survives is ONE
/// call, inside CoordinateTransformer.CreateSpatialReference, applied to both SRSes. Deleting
/// it is a silent, plausible, transposed answer for every geographic transform in the library.
///
/// This file does not create the first guard, contrary to what T-14's row claimed. Removing
/// the call also fails
/// CrsInspectionTests.ReprojectToPopulatesTheTargetCrsAndMakesPlanarMetricsEqualArea, which
/// reprojects lon/lat into Albers through SpatialReaderOptions.ReprojectTo -- but that test
/// fires on a PROJ exception only because its fixture transposes to lat = -93, which aea
/// refuses. A mid-latitude fixture transposes to a LEGAL latitude and the signal disappears.
/// What was missing, and what these two facts add, is an assertion on the ORDINATES and any
/// coverage of the TARGET mapping, which nothing had.
///
/// Verified by deleting the call: three tests fail. This file's two fail with the transposed
/// ordinates quoted in their own doc comments; the pre-existing one throws.
///
/// Lives in the Io test project, which already initialises GDAL and reaches
/// Nsi.Geospatial.Reprojection. A third test project would be a third GDAL-consuming project,
/// copying GdalConfiguration.cs a third time and moving the warning baseline from 6 to 9.
/// </summary>
[Trait("Category", "Gdal")]
public class CoordinateTransformerTests
{
  /// <summary>
  /// EPSG:32616, UTM zone 16N: central meridian -87, false easting 500000, false northing 0.
  /// Wkt is empty on purpose -- CrsToken prefers EpsgCode and only falls back to Wkt when the
  /// code is blank, so this is a complete Projection for every path that resolves one. P-14's
  /// open half deletes that preference; if it lands and this throws ArgumentException, that is
  /// the P-14 refactor changing the fixture, not a broken axis-order guard.
  /// </summary>
  static readonly Projection Utm16N = new(string.Empty, "EPSG:32616");

  /// <summary>
  /// A geographic CRS is READ as x = longitude. The point is chosen so the expected answer
  /// comes from the PROJECTION'S OWN DEFINITION rather than from anything runnable: a point on
  /// the central meridian is exactly the false easting, and a point on the equator is exactly
  /// the false northing. Nothing here was derived by running the library and copying what it
  /// printed, which is the only way this assertion could have failed to fail.
  ///
  /// Transposed, GDAL reads -87 as a LATITUDE -- legal, inside -90..90 -- so IsPlausible does
  /// not fire. Observed with the strategy call deleted: an easting of 834637.88152599998 where
  /// 500000 is expected. A real, finite, wrong number is what needs pinning; an exception
  /// would have needed no test.
  /// </summary>
  [Fact]
  public void ReprojectUsesTraditionalGisOrderForTheSourceCrs()
  {
    using var transformer = new CoordinateTransformer(Projection.Wgs84, Utm16N);

    var (x, y) = Assert.Single(transformer.Reproject([(-87d, 0d)]));

    Assert.Equal(500000d, x, 6); // easting on the central meridian
    Assert.Equal(0d, y, 6); // northing on the equator
  }

  /// <summary>
  /// A geographic CRS is WRITTEN as x = longitude. The forward leg above can only pin the
  /// source mapping: a projected CRS's authority order is easting,northing either way, so a
  /// projected TARGET emits the same order with or without the strategy and cannot tell you
  /// anything. Only a geographic target can -- EPSG:4326 is lat/lon by authority, which is the
  /// whole reason this line exists. Without the strategy on the target, GDAL emits (lat, lon)
  /// and X is 0 rather than -87.
  ///
  /// Both legs must fail when the strategy call is removed. If only one does, that leg is
  /// vacuous and this file has one guard, not two.
  /// </summary>
  [Fact]
  public void ReprojectUsesTraditionalGisOrderForTheTargetCrs()
  {
    using var transformer = new CoordinateTransformer(Utm16N, Projection.Wgs84);

    var (x, y) = Assert.Single(transformer.Reproject([(500000d, 0d)]));

    Assert.Equal(-87d, x, 6); // longitude on the central meridian
    Assert.Equal(0d, y, 6); // latitude on the equator
  }
}

