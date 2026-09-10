using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Io;

public interface IFeatureSource
{
  Features Read(string path);
}
