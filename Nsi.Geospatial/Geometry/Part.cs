using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Projections;

namespace Nsi.Geospatial.Geometry;

/// <summary>
/// A single geometry part — a ring, an open polyline, or a point — with cached
/// metrics over its vertices.
///
/// Planar metrics (Area, Perimeter, CentroidX/Y) are expressed in the linear units
/// of the owning collection's CRS; ask AreaSquareMeters / LengthMeters for a named
/// unit. Null from the named accessors is a real answer, not a failure.
///
/// INVARIANT: a Part is built and sealed BEFORE it is attached to a Feature and
/// FeatureCollection, so Crs is Unknown at seal time. CRS-dependent values are
/// therefore never cached inside Seal(); Measure() keys the cache on the CrsInfo
/// instance it was computed for and recomputes when that identity changes.
///
/// Vertices is read-only by design: the metrics cache and the incremental MBR are
/// only sound if AddVertex is the only way geometry changes.
/// </summary>
public sealed class Part
{
  private readonly List<Vertex> _vertices = new();

  /// <summary>Set by Measure(). False until the metrics match _vertices.</summary>
  private bool _measured;

  /// <summary>The CRS _areaSquareMeters/_lengthMeters were computed for. Compared by
  /// reference: CrsInfo is treated as immutable (see P-54 — make its setters init).</summary>
  private CrsInfo? _measuredCrs;

  private double? _areaSquareMeters;
  private double? _lengthMeters;

  public Part(PartType kind) => Kind = kind;

  /// <summary>What this part is: Ring, Polyline, or Point. Fixed at construction,
  /// because the meaning of every cached metric depends on it.</summary>
  public PartType Kind { get; }

  /// <summary>Rings close for length and area; polylines and points never do.</summary>
  public bool IsRing => Kind == PartType.Ring;

  /// <summary>Read-only view of the vertices in authored order. For a ring this is
  /// stored as supplied — open or closed — because the metric primitives close
  /// implicitly and a duplicated closing vertex contributes nothing. Use AddVertex
  /// to extend; there is no way to remove or reorder, which is what keeps the
  /// cached metrics and the incremental MBR valid.</summary>
  public IReadOnlyList<Vertex> Vertices => _vertices;

  /// <summary>Minimum bounding rectangle, maintained incrementally by AddVertex.
  /// BoundingBox.Empty until a vertex is added.</summary>
  public BoundingBox BoundingBox { get; private set; } = BoundingBox.Empty;

  /// <summary>True when this part is a hole to be subtracted from its feature's
  /// exterior. Set by SpatialReader from ring order (index > 0) and by callers
  /// building geometry by hand. Not derived from winding: the shapefile convention
  /// is positional, and orientation can flip under projection.</summary>
  public bool IsHole { get; set; }
  private double _centroidX;
  private double _centroidY;

  /// <summary>
  /// Area-weighted centroid, in the linear units of <see cref="Crs"/>. (0, 0) unless this
  /// is a ring with at least three vertices — a polyline or point has no area-weighted
  /// centroid. Reading it measures the part if needed, for the same reason as
  /// <see cref="Area"/>: without that, an unmeasured part is indistinguishable from one
  /// that genuinely has no centroid.
  /// </summary>
  public double CentroidX
  {
    get
    {
      EnsureMeasured();
      return _centroidX;
    }
  }

  /// <summary>
  /// Area-weighted centroid, in the linear units of <see cref="Crs"/>. (0, 0) unless this
  /// is a ring with at least three vertices — a polyline or point has no area-weighted
  /// centroid. Reading it measures the part if needed, for the same reason as
  /// <see cref="Area"/>: without that, an unmeasured part is indistinguishable from one
  /// that genuinely has no centroid.
  /// </summary>
  public double CentroidY
  {
    get
    {
      EnsureMeasured();
      return _centroidY;
    }
  }

  private double? _area;
  private double _perimeter;

  /// <summary>
  /// Planar shoelace area in the square linear units of <see cref="Crs"/>, or null when
  /// this geometry has no area: a Polyline, a Point, or fewer than three vertices. Zero
  /// is reported only for a degenerate ring (collinear or coincident vertices). For a
  /// usable unit ask <see cref="AreaSquareMeters"/> — this value is in whatever the CRS's
  /// units are, which for a geographic CRS is square degrees.
  ///
  /// Reading it measures the part if needed, so the answer does not depend on whether
  /// Seal() was called — and null means "no area", never "not yet measured", which is
  /// why the getter must measure: an unmeasured part would otherwise report the same
  /// value as a degenerate one.
  /// </summary>
  public double? Area
  {
    get
    {
      EnsureMeasured();
      return _area;
    }
  }

  /// <summary>
  /// Walk length in the linear units of <see cref="Crs"/>: every edge of a ring including
  /// the closing edge, every edge of a polyline excluding one. Zero for a single vertex.
  ///
  /// Reading it measures the part if needed; without that, an unmeasured part reports 0 —
  /// the honest answer for a single vertex — and no caller can tell the two apart.
  /// </summary>
  public double Perimeter
  {
    get
    {
      EnsureMeasured();
      return _perimeter;
    }
  }

  /// <summary>Set by Feature.AddPart. The single route to a CRS.</summary>
  internal Feature? Owner
  {
    get => _owner;
    set
    {
      if (ReferenceEquals(_owner, value))
        return;
      _owner = value;
      // Warm-up only: Measure() also detects the CRS change by identity, so a
      // re-parenting path that misses this costs a recompute, not a wrong answer.
      _measured = false;
    }
  }

  private Feature? _owner;

  /// <summary>The dependable CRS source: walks Part -> Feature -> FeatureCollection.
  /// Unknown for a standalone Part, which is honest — synthetic geometry has no CRS.
  /// Resolvable lazily, because the owner chain is assembled after the geometry is
  /// built; see the invariant on the class.</summary>
  public CrsInfo Crs => Owner?.Crs ?? Projections.CrsInfo.Unknown;

  /// <summary>
  /// Area in square metres, or null when the geometry has no area: an unknown CRS,
  /// a Polyline or Point, or fewer than three vertices. Geographic rings are
  /// measured spherically (planar shoelace on degrees is square degrees); projected
  /// rings use the declared unit, no transform needed. Reading this measures the
  /// part if needed, so the answer never depends on whether Seal() was called or on
  /// when the CRS was attached. Null is a real answer, not a failure — callers that
  /// coalesce it to 0 will silently mis-sum holes and mixed-geometry features.
  /// </summary>
  public double? AreaSquareMeters
  {
    get
    {
      EnsureMeasured();
      return _areaSquareMeters;
    }
  }

  /// <summary>
  /// Length in metres, or null when the CRS is unknown. A Ring measures its closed
  /// perimeter; a Polyline measures the open walk with no closing edge; a Point
  /// measures zero. Both CRS branches sum the same edges and differ only in metric
  /// (planar vs great-circle), so a length is comparable across a reprojection.
  /// Reading this measures the part if needed.
  /// </summary>
  public double? LengthMeters
  {
    get
    {
      EnsureMeasured();
      return _lengthMeters;
    }
  }

  /// <summary>
  /// Append a vertex and extend the MBR. No metric is computed here — Measure()
  /// derives all of them, so adding a vertex after a measurement is always safe.
  /// </summary>
  public void AddVertex(Vertex vertex)
  {
    _vertices.Add(vertex);
    _measured = false;
    BoundingBox = BoundingBox.Union(BoundingBox.Point(vertex.X, vertex.Y));
  }

  /// <summary>
  /// Declare the part complete and warm its metric cache. Optional: every accessor
  /// measures on demand, so omitting this yields correct numbers, and calling it
  /// twice is harmless. Never mutates the vertex list.
  /// </summary>
  public void Seal() => EnsureMeasured();

  /// <summary>Drop the cached metrics. For the owner-chain setters; the identity check
  /// in EnsureMeasured() makes this an optimisation rather than a requirement.</summary>
  internal void InvalidateMetrics() => _measured = false;

  /// <summary>Measure if the geometry or the CRS has changed since the last measurement.</summary>
  private void EnsureMeasured()
  {
    var crs = Crs;
    if (!_measured || !ReferenceEquals(_measuredCrs, crs))
      Measure(crs);
  }

  /// <summary>
  /// Single writer for every cached metric. O(n) in vertices; called at most once per
  /// (geometry, CRS) pair. Materialises the coordinate list once for all four walks.
  /// </summary>
  private void Measure(CrsInfo crs)
  {
    _measured = true;
    _measuredCrs = crs;

    if (_vertices.Count == 0)
    {
      _perimeter = 0;
      _area = null;
      _centroidX = 0;
      _centroidY = 0;
      _lengthMeters = null;
      _areaSquareMeters = null;
      return;
    }

    var xy = _vertices.Select(v => v.XY).ToList();
    bool hasArea = IsRing && _vertices.Count >= 3;

    _perimeter = IsRing ? GeometryMath.ClosedWalk(xy) : GeometryMath.OpenWalk(xy);
    _area = hasArea ? GeometryMath.Area(xy) : null;
    if (hasArea)
      (_centroidX, _centroidY) = GeometryMath.Centroid(xy);
    else
    {
      _centroidX = 0;
      _centroidY = 0;
    }

    _lengthMeters = crs.Kind switch
    {
      Projections.CrsKind.Unknown => null,
      Projections.CrsKind.Projected => Perimeter * crs.UnitToMetersOrMeter,
      _ => IsRing ? GeometryMath.SphericalPerimeter(xy) : GeometryMath.SphericalLength(xy),
    };

    _areaSquareMeters = Area is null
      ? null
      : crs.Kind switch
      {
        Projections.CrsKind.Unknown => null,
        Projections.CrsKind.Projected => Area * crs.UnitToMetersOrMeter * crs.UnitToMetersOrMeter,
        _ => GeometryMath.SphericalArea(xy),
      };
  }
}
