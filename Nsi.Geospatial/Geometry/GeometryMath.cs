namespace Nsi.Geospatial.Geometry;

/// <summary>
/// Pure geometry math.
/// No GDAL dependency.
/// </summary>
public static class GeometryMath
{
    public static double Distance((double X, double Y) a, (double X, double Y) b)
    {
        double dx = a.X - b.X, dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>Shoelace area of a polygon ring (absolute value).</summary>
    public static double Area(IEnumerable<(double X, double Y)> ring)
    {
        var pts = ring.ToList();
        if (pts.Count < 3) return 0;
        double sum = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var (x1, y1) = pts[i];
            var (x2, y2) = pts[(i + 1) % pts.Count];
            sum += x1 * y2 - x2 * y1;
        }
        return Math.Abs(sum) / 2.0;
    }

    /// <summary>Area-weighted centroid of a polygon ring; falls back to vertex mean.</summary>
    public static (double X, double Y) Centroid(IEnumerable<(double X, double Y)> ring)
    {
        var pts = ring.ToList();
        if (pts.Count == 0) return (0, 0);
        if (pts.Count == 1) return (pts[0].X, pts[0].Y);

        double a = 0, cx = 0, cy = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var (x1, y1) = pts[i];
            var (x2, y2) = pts[(i + 1) % pts.Count];
            double cross = x1 * y2 - x2 * y1;
            a += cross;
            cx += (x1 + x2) * cross;
            cy += (y1 + y2) * cross;
        }
        a /= 2.0;
        if (Math.Abs(a) < 1e-12)
        {
            double mx = pts.Average(p => p.X), my = pts.Average(p => p.Y);
            return (mx, my);
        }
        return (cx / (6 * a), cy / (6 * a));
    }

    /// <summary>Distance from point p to the segment [a,b] (for point-to-line joins).</summary>
    public static double PointToSegmentDistance((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var ab = (X: b.X - a.X, Y: b.Y - a.Y);
        var ap = (X: p.X - a.X, Y: p.Y - a.Y);
        double abLen2 = ab.X * ab.X + ab.Y * ab.Y;
        if (abLen2 < 1e-12) return Distance(p, a);
        double t = Math.Clamp((ap.X * ab.X + ap.Y * ab.Y) / abLen2, 0, 1);
        var proj = (a.X + t * ab.X, a.Y + t * ab.Y);
        return Distance(p, proj);
    }
}