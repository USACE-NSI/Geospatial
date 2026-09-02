using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Io;

public interface IFeatureSink
{
  /// <param name="driverName">GDAL/OGR driver name, e.g. "ESRI Shapefile", "GPKG", "GeoJSON".
  /// Defaults to "ESRI Shapefile" for callers using the interface.</param>
  void Write(FeatureCollection collection, string path, string driverName = "ESRI Shapefile");
}
