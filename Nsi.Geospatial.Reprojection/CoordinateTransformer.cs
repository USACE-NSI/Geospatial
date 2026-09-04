using Nsi.Geospatial.Projections;

namespace Nsi.Geospatial.Reprojection;

/// <summary>
/// A prepared from-&gt;to transform. Construct once and reuse: building the underlying
/// OGRCoordinateTransformation costs far more than transforming a ring, and
/// per-feature construction dominated reads at county footprint volumes.
/// </summary>
public sealed class CoordinateTransformer : IDisposable
{
  private IntPtr _ct;

  public CoordinateTransformer(Projection from, Projection to)
  {
    IntPtr src = Reprojector.Native.OSRNewSpatialReference(null);
    IntPtr dst = Reprojector.Native.OSRNewSpatialReference(null);
    try
    {
      if (src == IntPtr.Zero || dst == IntPtr.Zero)
      {
        throw new InvalidOperationException("OSRNewSpatialReference failed.");
      }

      Reprojector.Native.Check(
        Reprojector.Native.OSRSetFromUserInput(src, Reprojector.CrsToken(from, nameof(from))),
        "source CRS"
      );
      Reprojector.Native.Check(
        Reprojector.Native.OSRSetFromUserInput(dst, Reprojector.CrsToken(to, nameof(to))),
        "destination CRS"
      );

      _ct = Reprojector.Native.OGRNewCoordinateTransformation(src, dst);
      if (_ct == IntPtr.Zero)
      {
        throw new InvalidOperationException("OGRNewCoordinateTransformation failed.");
      }
    }
    finally
    {
      // OGRCoordinateTransformation clones both SRS on construction, so these are
      // ours to release immediately -- nothing here may retain them.
      if (src != IntPtr.Zero)
      {
        Reprojector.Native.OSRRelease(src);
      }
      if (dst != IntPtr.Zero)
      {
        Reprojector.Native.OSRRelease(dst);
      }
    }
  }

  public List<(double X, double Y)> Reproject(IEnumerable<(double X, double Y)> points)
  {
    ObjectDisposedException.ThrowIf(_ct == IntPtr.Zero, this);

    IList<(double X, double Y)> list = points as IList<(double X, double Y)> ?? points.ToList();
    if (list.Count == 0)
    {
      return [];
    }

    var xs = new double[list.Count];
    var ys = new double[list.Count];
    var zs = new double[list.Count];
    for (int i = 0; i < list.Count; i++)
    {
      xs[i] = list[i].X;
      ys[i] = list[i].Y;
    }

    Reprojector.Native.Check(
      Reprojector.Native.OGR_CT_Transform(_ct, list.Count, xs, ys, zs, 0),
      "OGR_CT_Transform"
    );

    var result = new List<(double X, double Y)>(list.Count);
    for (int i = 0; i < list.Count; i++)
    {
      result.Add((xs[i], ys[i]));
    }
    return result;
  }

  public void Dispose()
  {
    if (_ct == IntPtr.Zero)
    {
      return;
    }

    Reprojector.Native.OGR_CT_Destroy(_ct);
    _ct = IntPtr.Zero;
  }
}
