using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Io;

public interface IFeatureSource
{
    FeatureCollection Read(string path);
}