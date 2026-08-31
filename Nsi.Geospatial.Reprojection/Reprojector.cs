using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Projections;
using OSGeo.OSR;

namespace Nsi.Geospatial.Reprojection;

/// <summary>OSR-backed point reprojection between two CRSes (replaces reprojectPntsDS).</summary>
public static class Reprojector
{
    public static List<(double X, double Y)> Reproject(
        IEnumerable<(double X, double Y)> points,
        Projection from,
        Projection to)
    {
        var src = Ogr_SpatialRef();
        src.SetFromUserInput(from.EpsgCode ?? from.Wkt);
        var dst = Ogr_SpatialRef();
        dst.SetFromUserInput(to.EpsgCode ?? to.Wkt);

        var transform = src.Clone().BuildTransform(dst);
        var result = new List<(double, double)>(points.Count());
        foreach (var (x, y) in points)
        {
            transform.Transform(in x, in y, out _); // 3D-safe; Z=0
            result.Add((x, y));
        }
        return result;

        static OSGeo.OSR.SpatialReference Ogr_SpatialRef() => new();
    }
}