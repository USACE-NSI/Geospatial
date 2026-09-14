namespace Nsi.Geospatial.Geometry;

/// <summary>Axis-aligned 2D bounding box. Replaces the old double[4] MBR arrays.</summary>
public struct BoundingBox
{
  public double MinX { get; set; }
  public double MinY { get; set; }
  public double MaxX { get; set; }
  public double MaxY { get; set; }

  /// <summary>
  /// Creates a box from two corners, exactly as given. The corners are NOT ordered and NOT
  /// normalised: <c>new(10, 10, 0, 0)</c> is stored as written and <see cref="IsEmpty"/>
  /// reports it as empty. That is what makes <see cref="Empty"/> empty — the extremes are only
  /// empty because nothing rewrites them. The cost is that a call site handing the corners
  /// over in the wrong order gets an identity box that contributes nothing to a union rather
  /// than a corrected one. Whether to reject here is D-F's decision, not this ctor's (P-72).
  /// </summary>
  public BoundingBox(double minX, double minY, double maxX, double maxY)
  {
    MinX = minX;
    MinY = minY;
    MaxX = maxX;
    MaxY = maxY;
  }

  /// <summary>
  /// Not a box. <c>Min &gt; Max</c> on both axes, so it holds no point and no area.
  /// <para>
  /// The extremes are load-bearing, not sloppiness. They are the identities of
  /// <see cref="Math.Max"/> and <see cref="Math.Min"/>, so a fold or a node seeded with
  /// <see cref="Empty"/> absorbs its first child for free. They are also the ONLY thing that
  /// makes the value empty now that the ctor does not normalise: a tidier inversion such as
  /// <c>new(1, 1, 0, 0)</c> is a valid box for a positive-extent fold and a wrong one for any
  /// CRS with a negative bound, which is every projected CRS.
  /// </para>
  /// <para>
  /// Comparing and unioning are consistent with no sentinel test — <see cref="ContainsPoint"/>
  /// is false because no x satisfies <c>MaxValue &lt;= x &lt;= MinValue</c>, and
  /// <see cref="Union"/> returns the other side.
  /// </para>
  /// <para>
  /// The price falls on the members that turn a box into a NUMBER, and those members do NOT
  /// guard, by choice, so that <see cref="Area"/> stays one expression. Width and height are
  /// <c>MinValue - MaxValue</c>, which overflows to <c>-∞</c>: <see cref="Area"/> is <c>+∞</c>,
  /// <see cref="Perimeter"/> is <c>-∞</c>, and <see cref="EnlargementToContain"/> is
  /// asymmetric — a real box grows by <c>0</c> to hold <see cref="Empty"/>, <see cref="Empty"/>
  /// "grows" by <c>-∞</c> to hold a real box, and <see cref="Empty"/> to
  /// <see cref="Empty"/> is <c>NaN</c> (<c>∞ - ∞</c>). Anything that sorts or compares by area
  /// must test <see cref="IsEmpty"/> first.
  /// </para>
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
  /// True when the box cannot hold a point: <c>Min &gt; Max</c> on either axis. Catches
  /// <see cref="Empty"/> and every hand-authored inversion, so this is the test and
  /// <c>this == Empty</c> is not — equality sees only the sentinel.
  /// <para>
  /// Callers today: <see cref="Overlaps"/>, <see cref="Contains"/>,
  /// <see cref="OverlappingArea"/>, <see cref="Union"/>. Deliberately absent from
  /// <see cref="Area"/> and <see cref="Perimeter"/> (they return ±∞, a wrong answer that is at
  /// least not plausible), from <see cref="ContainsPoint"/> (already false by construction) and
  /// from <see cref="EnlargementToContain"/> (documented as meaningless for an empty side).
  /// </para>
  /// <para>
  /// <c>NaN</c> compares false against everything, so a box holding NaN is never empty.
  /// <see cref="FromVertices"/> keeps its own NaN policy and the two are not reconciled (D-F).
  /// </para>
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

