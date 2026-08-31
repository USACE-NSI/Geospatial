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
        IRTree? pointTree = null,
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

        var tree = pointTree ?? BuildTree(points);

        var matched = new List<long>();
        for (long polyIdx = 0; polyIdx < polygons.Count; polyIdx++)
        {
            if (exteriorOnly is not null && !exteriorOnly.Equals(polyIdx) && !ContainsIndex(exteriorOnly, polyIdx))
                continue;

            var poly = polygons[polyIdx];
            var candidates = tree.Query(poly.Mbr.Enlarged(0.0)); // refine below with exact distance

            double? best = null;
            List<Feature> nearestPoints = new();
            foreach (var (fIdx, _) in candidates)
            {
                var p = points[fIdx];
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
                object? value = Aggregate(nearestPoints, sourceFields[i], joinType);
                poly.Attributes[field] = value;
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
        IRTree? polyTree = null)
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

        var tree = polyTree ?? BuildTree(polygons);
        for (int pIdx = 0; pIdx < points.Count; pIdx++)
        {
            var p = points[pIdx];
            double? best = null;
            Feature? bestPoly = null;
            foreach (var (fIdx, _) in tree.Query(p.Mbr))
            {
                var poly = polygons[fIdx];
                double d = DistanceFeatureToFeature(p, poly);
                if (best is null || d < best) { best = d; bestPoly = poly; }
            }
            if (bestPoly is not null)
            {
                for (int i = 0; i < destFields.Length; i++)
                    p.Attributes[destFields[i]] = bestPoly.Attributes.TryGetValue(sourceFields[i], out var v) ? v : null;
            }
        }
    }

    public static IRTree BuildTree(FeatureCollection fc)
    {
        var tree = new RTree();
        for (int i = 0; i < fc.Count; i++)
        {
            var f = fc[i];
            if (f.Mbr != BoundingBox.Empty)
                tree.Add(f.Id, 0, f.Mbr);
        }
        return tree;
    }

    private static double DistanceFeatureToFeature(Feature point, Feature polygon)
    {
        if (polygon.Parts.Count == 0) return double.MaxValue;
        double best = double.MaxValue;
        var px = point.Mbr.MinX, py = point.Mbr.MinY;
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
                var a = part.Vertices[i], b = part.Vertices[i + 1];
                best = Math.Min(best, GeometryMath.PointToSegmentDistance((px, py), (a.X, a.Y), (b.X, b.Y)));
            }
        }
        return best;
    }

    private static object? Aggregate(List<Feature> points, string sourceField, JoinType joinType)
    {
        var values = points.Select(p => p.Attributes.TryGetValue(sourceField, out var v) ? v?.ToString() : null)
                           .Where(v => !string.IsNullOrEmpty(v)).ToList();
        return joinType switch
        {
            JoinType.First => values.FirstOrDefault(),
            JoinType.Count => values.Count,
            JoinType.Sum => values.Sum(v => double.TryParse(v, out var d) ? d : 0),
            JoinType.Average => values.Count == 0 ? 0d :
                values.Where(v => double.TryParse(v, out _)).Average(v => double.Parse(v!)),
            _ => null,
        };
    }

    private static bool ContainsIndex(int index, long candidate) => candidate == index;

    private static int? ParseIndex(int? value) => value;
}

internal static class BoundingBoxExtensions
{
    /// <summary>A slightly padded copy of the box, for candidate refinement.</summary>
    public static BoundingBox Enlarged(this BoundingBox box, double pad)
        => box == BoundingBox.Empty ? box :
        new(box.MinX - pad, box.MinY - pad, box.MaxX + pad, box.MaxY + pad);
}