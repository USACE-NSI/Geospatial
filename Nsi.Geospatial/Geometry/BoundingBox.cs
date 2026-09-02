namespace Nsi.Geospatial.Geometry;

/// <summary>Axis-aligned 2D bounding box. Replaces the old double[4] MBR arrays.</summary>
public struct BoundingBox
{
  public double MinX { get; set; }
  public double MinY { get; set; }
  public double MaxX { get; set; }
  public double MaxY { get; set; }

  public BoundingBox(double minX, double minY, double maxX, double maxY)
  {
    MinX = Math.Min(minX, maxX);
    MinY = Math.Min(minY, maxY);
    MaxX = Math.Max(minX, maxX);
    MaxY = Math.Max(minY, maxY);
  }

  public static readonly BoundingBox Empty = new(
    double.MaxValue,
    double.MaxValue,
    double.MinValue,
    double.MinValue
  );

  public static BoundingBox Point(double x, double y) => new(x, y, x, y);

  public static BoundingBox FromVertices(IEnumerable<(double X, double Y)> points)
  {
    double minX = double.MaxValue,
      minY = double.MaxValue,
      maxX = double.MinValue,
      maxY = double.MinValue;
    foreach (var (x, y) in points)
    {
      if (x < minX)
        minX = x;
      if (x > maxX)
        maxX = x;
      if (y < minY)
        minY = y;
      if (y > maxY)
        maxY = y;
    }
    return double.IsPositiveInfinity(minX) ? Empty : new(minX, minY, maxX, maxY);
  }

  /// <summary>
  /// fix(#15): correct closed-interval overlap test. The original used
  /// "(queryMax in [min,max]) || (queryMin in [min,max])", which returned
  /// 0 when the query box fully *contained* the node box.
  /// </summary>
  public bool Overlaps(BoundingBox other)
  {
    if (this == Empty || other == Empty)
      return false;
    return MinX <= other.MaxX && other.MinX <= MaxX && MinY <= other.MaxY && other.MinY <= MaxY;
  }

  public bool Contains(BoundingBox other)
  {
    if (this == Empty || other == Empty)
      return false;
    return MinX <= other.MinX && MaxX >= other.MaxX && MinY <= other.MinY && MaxY >= other.MaxY;
  }

  public bool ContainsPoint(double x, double y) => MinX <= x && x <= MaxX && MinY <= y && y <= MaxY;

  public double Area() => (MaxX - MinX) * (MaxY - MinY);

  public BoundingBox Union(BoundingBox other)
  {
    if (this == Empty)
      return other;
    if (other == Empty)
      return this;
    return new(
      Math.Min(MinX, other.MinX),
      Math.Min(MinY, other.MinY),
      Math.Max(MaxX, other.MaxX),
      Math.Max(MaxY, other.MaxY)
    );
  }

  /// <summary>Extra area required to absorb <paramref name="other"/>.</summary>
  public double EnlargementToContain(BoundingBox other) => Union(other).Area() - Area();

  public override bool Equals(object? obj) => obj is BoundingBox b && b == this;

  public override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);

  public override string ToString() => $"[{MinX},{MinY}]-[{MaxX},{MaxY}]";

  public static bool operator ==(BoundingBox a, BoundingBox b) =>
    a.MinX == b.MinX && a.MinY == b.MinY && a.MaxX == b.MaxX && a.MaxY == b.MaxY;

  public static bool operator !=(BoundingBox a, BoundingBox b) => !(a == b);
}
