using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Io;

public interface IFeatureSink
{
    void Write(FeatureCollection collection, string path);
}