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
    MinX = minX;
    MinY = minY;
    MaxX = maxX;
    MaxY = maxY;
  }

  /// <summary>
  /// The empty box: invalid by construction, Min &gt; Max on both axes, so it contains no
  /// point and no area. The extremes are not sloppiness — they are what make it the
  /// identity for Union (min of MaxValue, max of MinValue), so a node seeded with Empty
  /// absorbs its first child for free and no insert path needs a sentinel test.
  ///
  /// That choice has a price: width and height are MinValue - MaxValue, which overflows,
  /// so Area and Perimeter on an empty box are ±∞ rather than 0, and
  /// EnlargementToContain is asymmetric (a real box grows by 0 to hold Empty; Empty
  /// "grows" by -∞ to hold a real box). Anything that turns a box into a NUMBER must
  /// test for emptiness first — anything that compares or unions one must not.
  /// </summary>
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
    if (this.IsEmpty() || other.IsEmpty())
      return false;
    return MinX <= other.MaxX && other.MinX <= MaxX && MinY <= other.MaxY && other.MinY <= MaxY;
  }

  public bool Contains(BoundingBox other)
  {
    if (this.IsEmpty() || other.IsEmpty())
      return false;
    return MinX <= other.MinX && MaxX >= other.MaxX && MinY <= other.MinY && MaxY >= other.MaxY;
  }

  public bool ContainsPoint(double x, double y) => MinX <= x && x <= MaxX && MinY <= y && y <= MaxY;

  /// <summary>
  /// Area shared with <paramref name="other"/>; 0 when the boxes miss or merely touch.
  /// Zero-area contact is <see cref="Overlaps"/>'s question, not this one's.
  /// </summary>
  public double OverlappingArea(BoundingBox other)
  {
    if (this.IsEmpty() || other.IsEmpty())
      return 0;

    double dx = Math.Min(MaxX, other.MaxX) - Math.Max(MinX, other.MinX);
    if (dx <= 0)
      return 0;

    double dy = Math.Min(MaxY, other.MaxY) - Math.Max(MinY, other.MinY);
    if (dy <= 0)
      return 0;

    return dx * dy;
  }

  public double Area() => (MaxX - MinX) * (MaxY - MinY);

  public double Perimeter() => 2 * ((MaxX - MinX) + (MaxY - MinY));

  public BoundingBox Union(BoundingBox other)
  {
    if (this.IsEmpty())
      return other;
    if (other.IsEmpty())
      return this;
    return new(
      Math.Min(MinX, other.MinX),
      Math.Min(MinY, other.MinY),
      Math.Max(MaxX, other.MaxX),
      Math.Max(MaxY, other.MaxY)
    );
  }

  /// <summary>
  /// True when the box cannot contain a point: Min &gt; Max on either axis. Catches
  /// <see cref="Empty"/> AND any hand-authored inversion, which is why the members that
  /// turn a box into a number test this rather than comparing to Empty.
  /// </summary>
  public bool IsEmpty() => MinX > MaxX || MinY > MaxY;

  /// <summary>Extra area required to absorb <paramref name="other"/>.</summary>
  public double EnlargementToContain(BoundingBox other) => Union(other).Area() - Area();

  public override bool Equals(object? obj) => obj is BoundingBox b && b == this;

  public override int GetHashCode() => HashCode.Combine(MinX, MinY, MaxX, MaxY);

  public override string ToString() => $"[{MinX},{MinY}]-[{MaxX},{MaxY}]";

  public static bool operator ==(BoundingBox a, BoundingBox b) =>
    a.MinX == b.MinX && a.MinY == b.MinY && a.MaxX == b.MaxX && a.MaxY == b.MaxY;

  public static bool operator !=(BoundingBox a, BoundingBox b) => !(a == b);
}

