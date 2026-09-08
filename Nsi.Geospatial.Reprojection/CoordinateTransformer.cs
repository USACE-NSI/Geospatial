using Nsi.Geospatial.Projections;
using OSGeo.OSR;

namespace Nsi.Geospatial.Reprojection;

/// <summary>
/// A prepared from-&gt;to transform. Construct once and reuse: building the underlying
/// OGRCoordinateTransformation costs far more than transforming a ring, and
/// per-feature construction dominated reads at county footprint volumes.
/// </summary>
public sealed class CoordinateTransformer : IDisposable
{
  private readonly CoordinateTransformation _ct;
  private bool _disposed;

  public CoordinateTransformer(Projection from, Projection to)
  {
    using SpatialReference src = CreateSpatialReference(from, nameof(from));
    using SpatialReference dst = CreateSpatialReference(to, nameof(to));

    // SWIG throws when the transform cannot be built; there is no null to test.
    _ct = new CoordinateTransformation(src, dst);
  }

  public List<(double X, double Y)> Reproject(IEnumerable<(double X, double Y)> points)
  {
    ObjectDisposedException.ThrowIf(_disposed, this);

    IList<(double X, double Y)> list = points as IList<(double X, double Y)> ?? points.ToList();
    if (list.Count == 0)
    {
      return [];
    }

    var xs = new double[list.Count];
    var ys = new double[list.Count];
    var zs = new double[list.Count]; // the binding requires a non-null z buffer
    for (int i = 0; i < list.Count; i++)
    {
      xs[i] = list[i].X;
      ys[i] = list[i].Y;
    }

    // Batch form is void: the SWIG wrapper discards OCTTransform's TRUE/FALSE
    // return, so a per-point failure is not observable here. See IsPlausible.
    _ct.TransformPoints(list.Count, xs, ys, zs);

    var result = new List<(double X, double Y)>(list.Count);
    for (int i = 0; i < list.Count; i++)
    {
      if (!IsPlausible(xs[i]) || !IsPlausible(ys[i]))
      {
        throw new InvalidOperationException(
          $"Transform produced a non-finite or sentinel coordinate at index {i} "
            + $"({xs[i]}, {ys[i]}); the point could not be transformed."
        );
      }

      result.Add((xs[i], ys[i]));
    }

    return result;
  }

  public void Dispose()
  {
    if (_disposed)
    {
      return;
    }

    _ct.Dispose();
    _disposed = true;
  }

  /// <summary>
  /// OGR marks untransformable points with a HUGE_VAL sentinel rather than raising,
  /// and the void batch wrapper hides the failure flag, so the sentinel is the only
  /// signal available. Without this a bad point becomes a garbage vertex that
  /// silently corrupts the feature's MBR and area.
  /// </summary>
  private static bool IsPlausible(double v) => double.IsFinite(v) && Math.Abs(v) < 1e15;

  /// <summary>
  /// Builds and validates one SRS. The caller releases it right after the transform
  /// is constructed: OGRCoordinateTransformation clones both SRSes.
  /// </summary>
  private static SpatialReference CreateSpatialReference(Projection projection, string argName)
  {
    string token = Reprojector.CrsToken(projection, argName);
    var srs = new SpatialReference(null);
    if (srs.SetFromUserInput(token) != 0)
    {
      srs.Dispose();
      throw new InvalidOperationException($"OSR could not resolve {argName}.");
    }

    // GDAL 3+ honours the authority's axis order for EPSG-declared geographic
    // CRSes, which is lat/lon for EPSG:4326. The model stores x = longitude,
    // y = latitude, so both SRSes are pinned to traditional GIS order -- otherwise
    // every geographic transform is silently transposed.
    srs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);
    return srs;
  }
}
