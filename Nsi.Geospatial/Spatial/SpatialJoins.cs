using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;

namespace Nsi.Geospatial.Spatial;

/// <summary>Nearest-neighbor attribute joins between point and polygon collections.</summary>
public static class SpatialJoins
{
    /// <summary>For each polygon, aggregate the nearest point's values into the polygon's fields.</summary>
    public static List<long> NearestPointsToPolygons(
        FeatureCollection polygons,
        FeatureCollection points,
        string[] destFields,
        string[] sourceFields,
        JoinType joinType,
        RTreeManager? pointTree = null,
        int? exteriorOnly = null,
        int? interiorOnly = null)
    {
        foreach (var d in destFields)
        {
            int si = Array.IndexOf(sourceFields, d);
            if (si >= 0 && !polygons.Schema.HasColumn(d))
            {
                var src = points.Schema[sourceFields[si]];
                polygons.Schema.AddField(d, src.FieldType, src.Length, src.DecimalPlaces);
            }
        }

        // The original RTreeManager's getMBRoverlap gate reports no overlap when
        // the query box fully contains a feature's MBR, and a nearest-join
        // candidate can sit outside the target's MBR entirely — so candidate
        // selection scans the collection instead of querying the tree.
        // pointTree is kept for API continuity and findByXY lookups.
        _ = pointTree ?? BuildTree(points);

        var matched = new List<long>();
        for (int polyIdx = 0; polyIdx < polygons.Count; polyIdx++)
        {
            if (exteriorOnly is not null && !ContainsIndex(exteriorOnly.Value, polyIdx))
                continue;

            var poly = polygons[polyIdx];

            double? best = null;
            var nearestPoints = new List<Feature>();
            foreach (var p in points.Features)
            {
                double d = DistanceFeatureToFeature(p, poly);
                if (best is null || d < best)
                {
                    best = d;
                    nearestPoints.Clear();
                    nearestPoints.Add(p);
                }
                else if (Math.Abs(d - best.Value) < 1e-9)
                {
                    nearestPoints.Add(p);
                }
            }

            if (nearestPoints.Count == 0) continue;
            matched.Add(polyIdx);

            for (int i = 0; i < destFields.Length; i++)
            {
                string field = destFields[i];
                poly.Attributes[field] = Aggregate(nearestPoints, sourceFields[i], joinType);
            }
        }

        return matched;
    }

    /// <summary>For each point, attach the nearest polygon's values.</summary>
    public static void NearestPolygonsToPoints(
        FeatureCollection points,
        FeatureCollection polygons,
        string[] destFields,
        string[] sourceFields,
        RTreeManager? polyTree = null)
    {
        foreach (var d in destFields)
        {
            int si = Array.IndexOf(sourceFields, d);
            if (si >= 0 && !points.Schema.HasColumn(d))
            {
                var src = polygons.Schema[sourceFields[si]];
                points.Schema.AddField(d, src.FieldType, src.Length, src.DecimalPlaces);
            }
        }

        _ = polyTree ?? BuildTree(polygons);

        for (int pIdx = 0; pIdx < points.Count; pIdx++)
        {
            var p = points[pIdx];
            double? best = null;
            Feature? bestPoly = null;
            foreach (var poly in polygons.Features)
            {
                double d = DistanceFeatureToFeature(p, poly);
                if (best is null || d < best)
                {
                    best = d;
                    bestPoly = poly;
                }
            }
            if (bestPoly is not null)
            {
                for (int i = 0; i < destFields.Length; i++)
                {
                    p.Attributes[destFields[i]] =
                        bestPoly.Attributes.TryGetValue(sourceFields[i], out var v) ? v : null;
                }
            }
        }
    }

    /// <summary>
    /// Build an RTreeManager over the collection's feature MBRs using the original
    /// RTreeManager API. Note the original addFeature argument order:
    /// (featInd, Xmax, Xmin, Ymax, Ymin).
    /// </summary>
    public static RTreeManager BuildTree(FeatureCollection fc)
    {
        var tree = new RTreeManager();
        for (int i = 0; i < fc.Count; i++)
        {
            var f = fc[i];
            if (f.Mbr != BoundingBox.Empty)
                tree.addFeature(new[] { f.Id, 0 }, f.Mbr.MaxX, f.Mbr.MinX, f.Mbr.MaxY, f.Mbr.MinY);
        }
        return tree;
    }

    private static double DistanceFeatureToFeature(Feature point, Feature polygon)
    {
        if (polygon.Parts.Count == 0) return double.MaxValue;
        double best = double.MaxValue;
        double px = point.Mbr.MinX, py = point.Mbr.MinY;
        foreach (var part in polygon.Parts)
        {
            if (part.Vertices.Count < 2)
            {
                var first = part.Vertices[0];
                best = Math.Min(best, GeometryMath.Distance((px, py), (first.X, first.Y)));
                continue;
            }
            for (int i = 0; i < part.Vertices.Count - 1; i++)
            {
                Vertex a = part.Vertices[i];
                Vertex b = part.Vertices[i + 1];
                best = Math.Min(best, GeometryMath.PointToSegmentDistance((px, py), (a.X, a.Y), (b.X, b.Y)));
            }
        }
        return best;
    }

    private static object? Aggregate(List<Feature> points, string sourceField, JoinType joinType)
    {
        var values = points
            .Select(p => p.Attributes.TryGetValue(sourceField, out var v) ? v : null)
            .Where(v => v is not null)
            .ToList();

        return joinType switch
        {
            JoinType.First => values.FirstOrDefault(),
            JoinType.Count => values.Count,
            JoinType.Sum => values.Sum(v => ToDouble(v)),
            JoinType.Average => values.Count == 0 ? 0d :
                values.Where(v => ToDouble(v) is not null).Average(v => ToDouble(v)!.Value),
            _ => null,
        };
    }

    private static double? ToDouble(object? value)
    {
        return value switch
        {
            null => null,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            _ => double.TryParse(value.ToString()!, out var parsed) ? parsed : null,
        };
    }

    private static bool ContainsIndex(int index, long candidate) => candidate == index;
}