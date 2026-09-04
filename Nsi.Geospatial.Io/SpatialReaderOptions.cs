using Nsi.Geospatial.Projections;

namespace Nsi.Geospatial.Io;

/// <summary>Reader configuration. Deliberately not on IFeatureSource: reading a path
/// is the contract, this is how a particular reader instance behaves.</summary>
public sealed record SpatialReaderOptions
{
  /// <summary>Layer index to read. 0 by default.</summary>
  public int LayerIndex { get; init; }

  /// <summary>Transform every ring to this CRS at load. When set, the collection's
  /// Crs describes the TARGET, and Area/Perimeter/Centroid/BoundingBox become
  /// meaningful in the target's linear units for free. Null (default) leaves
  /// coordinates exactly as stored.</summary>
  public Projection? ReprojectTo { get; init; }

  /// <summary>Throw when the source CRS cannot be inspected, rather than returning
  /// features whose derived metrics are silently null.</summary>
  public bool RequireInspectableCrs { get; init; }
}

