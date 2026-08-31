using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Spatial;

public interface IRTree
{
    void Add(int featureIndex, int subPartIndex, BoundingBox box);
    IReadOnlyList<(int FeatureIndex, int SubPartIndex)> Query(BoundingBox box);
    IReadOnlyList<(int FeatureIndex, int SubPartIndex)> QueryPoint(double x, double y);
    int Count { get; }
}