namespace Nsi.Geospatial.Geometry;

/// <summary>
/// Pure geometry math.
/// No GDAL dependency.
/// </summary>
public static class GeometryMath
{
  public static double Distance((double X, double Y) a, (double X, double Y) b)
  {
    double dx = a.X - b.X,
      dy = a.Y - b.Y;
    return Math.Sqrt(dx * dx + dy * dy);
  }

  /// <summary>Shoelace area of a polygon ring (absolute value).</summary>
  public static double Area(IEnumerable<(double X, double Y)> ring)
  {
    var pts = ring.ToList();
    if (pts.Count < 3)
      return 0;
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
    if (pts.Count == 0)
      return (0, 0);
    if (pts.Count == 1)
      return (pts[0].X, pts[0].Y);

    double a = 0,
      cx = 0,
      cy = 0;
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
      double mx = pts.Average(p => p.X),
        my = pts.Average(p => p.Y);
      return (mx, my);
    }
    return (cx / (6 * a), cy / (6 * a));
  }

  /// <summary>Distance from point p to the segment [a,b] (for point-to-line joins).</summary>
  public static double PointToSegmentDistance(
    (double X, double Y) p,
    (double X, double Y) a,
    (double X, double Y) b
  )
  {
    var ab = (X: b.X - a.X, Y: b.Y - a.Y);
    var ap = (X: p.X - a.X, Y: p.Y - a.Y);
    double abLen2 = ab.X * ab.X + ab.Y * ab.Y;
    if (abLen2 < 1e-12)
      return Distance(p, a);
    double t = Math.Clamp((ap.X * ab.X + ap.Y * ab.Y) / abLen2, 0, 1);
    var proj = (a.X + t * ab.X, a.Y + t * ab.Y);
    return Distance(p, proj);
  }
}


/// <summary>
    /// Authalic (equal-area) Earth radius, metres — the sphere matching WGS84's
    /// surface area. Correct radius for areas; equatorial 6378137 inflates them
    /// ~0.22%, comparable to the whole sphere-vs-ellipsoid error.
    /// </summary>
    public const double EarthRadiusAuthalicMeters = 6371007.181;

    /// <summary>IUGG mean Earth radius, metres. Conventional for distances.</summary>
    public const double EarthRadiusMeanMeters = 6371008.7714;

    /// <summary>AlexRyanUSACE GeospatialTools radius: 6,371,000 m in feet.</summary>
    public const double EarthRadiusFeet = 20925524.9;

    /// <summary>
    /// Spherical-excess area of a ring of (X=longitude, Y=latitude) in DEGREES, in
    /// SQUARE METRES. Exact on a sphere; the only error is sphere-vs-ellipsoid,
    /// about 0.20% low at 44N and 0.63% low at 65N.
    ///
    /// Use instead of Area when coordinates are geographic, where planar shoelace
    /// yields square degrees. Pass EarthRadiusFeet for square feet. Single ring:
    /// subtract holes yourself, their sign is not inferred.
    /// </summary>
    public static double SphericalArea(
        IEnumerable<(double X, double Y)> ring,
        double radiusMeters = EarthRadiusAuthalicMeters
    )
    {
        var pts = ring.ToList();
        if (pts.Count < 3)
            return 0;

        // sum( dLon * (sin(lat1) + sin(lat2)) ), radians.
        double total = 0;
        for (int i = 0; i < pts.Count; i++)
        {
            var (lon1, lat1) = pts[i];
            var (lon2, lat2) = pts[(i + 1) % pts.Count];
            total += LongDeltaRadians(lon1, lon2)
                * (Math.Sin(ToRadians(lat1)) + Math.Sin(ToRadians(lat2)));
        }
        return Math.Abs(total * radiusMeters * radiusMeters / 2.0);
    }

    /// <summary>Great-circle (haversine) distance, metres, for (lon, lat) in DEGREES.</summary>
    public static double SphericalDistance(
        (double X, double Y) a,
        (double X, double Y) b,
        double radiusMeters = EarthRadiusMeanMeters
    )
    {
        double phi1 = ToRadians(a.Y), phi2 = ToRadians(b.Y);
        double dPhi = phi2 - phi1;
        double dLon = LongDeltaRadians(a.X, b.X);

        // Haversine, not the spherical law of cosines: stays accurate at the
        // sub-metre separations that matter between footprint vertices.
        double h =
            Math.Sin(dPhi / 2) * Math.Sin(dPhi / 2)
            + Math.Cos(phi1) * Math.Cos(phi2) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radiusMeters * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
    }

    /// <summary>Great-circle length, metres, of a (lon, lat) degree polyline.</summary>
    public static double SphericalPerimeter(
        IEnumerable<(double X, double Y)> ring,
        double radiusMeters = EarthRadiusMeanMeters
    )
    {
        var pts = ring.ToList();
        if (pts.Count < 2)
            return 0;

        double total = 0;
        for (int i = 0; i < pts.Count; i++)
            total += SphericalDistance(pts[i], pts[(i + 1) % pts.Count], radiusMeters);
        return total;
    }

    /// <summary>
    /// Cross-track distance, metres, from p to segment [a,b] for (lon, lat) in
    /// DEGREES. Spherical analogue of PointToSegmentDistance. Returns the distance
    /// to the nearer endpoint when the perpendicular falls outside the segment.
    /// </summary>
    public static double SphericalPointToSegmentDistance(
        (double X, double Y) p,
        (double X, double Y) a,
        (double X, double Y) b,
        double radiusMeters = EarthRadiusMeanMeters
    )
    {
        double delta12 = SphericalDistance(a, b, radiusMeters) / radiusMeters;
        if (delta12 < 1e-12)
            return SphericalDistance(p, a, radiusMeters);

        double delta13 = SphericalDistance(p, a, radiusMeters) / radiusMeters;
        double deltaXT = Math.Asin(
            Math.Clamp(
                Math.Sin(delta13) * Math.Sin(InitialBearingRadians(a, p) - InitialBearingRadians(a, b)),
                -1.0,
                1.0
            )
        );

        // Outside the segment -> nearest endpoint.
        double deltaAT = Math.Acos(Math.Clamp(Math.Cos(delta13) / Math.Cos(deltaXT), -1.0, 1.0));
        if (deltaAT > delta12)
            return Math.Min(
                radiusMeters * delta13,
                SphericalDistance(p, b, radiusMeters)
            );

        return radiusMeters * Math.Abs(deltaXT);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

    /// <summary>Longitude difference in radians, wrapped to (-pi, pi] so rings may cross 180.</summary>
    private static double LongDeltaRadians(double lon1, double lon2)
    {
        double d = ToRadians(lon2 - lon1);
        while (d > Math.PI)
            d -= 2 * Math.PI;
        while (d < -Math.PI)
            d += 2 * Math.PI;
        return d;
    }

    private static double InitialBearingRadians((double X, double Y) a, (double X, double Y) b)
    {
        double phi1 = ToRadians(a.Y), phi2 = ToRadians(b.Y);
        double dLon = LongDeltaRadians(a.X, b.X);
        return Math.Atan2(
            Math.Sin(dLon) * Math.Cos(phi2),
            Math.Cos(phi1) * Math.Sin(phi2) - Math.Sin(phi1) * Math.Cos(phi2) * Math.Cos(dLon)
        );
    }