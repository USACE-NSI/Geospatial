using System.Runtime.InteropServices;
using System.Text;
using Nsi.Geospatial.Projections;

namespace Nsi.Geospatial.Reprojection;

/// <summary>
/// Point reprojection between two CRSes (replaces reprojectPntsDS).
///
/// The gdal 3.11.3 C# binding exposes the CoordinateTransformation handle
/// constructor but not its Transform method (a known gap in the official
/// OSGeo binding), so this calls the stable GDAL C API directly against the
/// native library. No unsafe code.
/// </summary>
public static class Reprojector
{
  public static List<(double X, double Y)> Reproject(
    IEnumerable<(double X, double Y)> points,
    Projection from,
    Projection to
  )
  {
    var list = points.ToList();
    if (list.Count == 0)
      return new List<(double, double)>();

    var src = Native.OSRNewSpatialReference(null);
    var dst = Native.OSRNewSpatialReference(null);
    if (src == IntPtr.Zero || dst == IntPtr.Zero)
      throw new InvalidOperationException("OSRNewSpatialReference failed.");
    Native.Check(Native.OSRSetFromUserInput(src, CrsToken(from, nameof(from))), "source CRS");
    Native.Check(Native.OSRSetFromUserInput(dst, CrsToken(to, nameof(to))), "destination CRS");

    var ct = Native.OGRNewCoordinateTransformation(src, dst);
    if (ct == IntPtr.Zero)
      throw new InvalidOperationException("OGRNewCoordinateTransformation failed.");

    try
    {
      var xs = new double[list.Count];
      var ys = new double[list.Count];
      var zs = new double[list.Count];
      for (int i = 0; i < list.Count; i++)
      {
        xs[i] = list[i].X;
        ys[i] = list[i].Y;
      }

      Native.Check(Native.OGR_CT_Transform(ct, list.Count, xs, ys, zs, 0), "OGR_CT_Transform");
      return list.Select((_, i) => (xs[i], ys[i])).ToList();
    }
    finally
    {
      Native.OGR_CT_Destroy(ct);
      Native.OSRRelease(src);
      Native.OSRRelease(dst);
    }
  }

  private static string CrsToken(Projection p, string argName)
  {
    string? epsg = p.EpsgCode;
    if (!string.IsNullOrWhiteSpace(epsg))
      return epsg.StartsWith("EPSG:", StringComparison.OrdinalIgnoreCase) ? epsg : $"EPSG:{epsg}";
    if (!string.IsNullOrWhiteSpace(p.Wkt))
      return p.Wkt!;
    throw new ArgumentException($"{argName} must supply an EpsgCode or Wkt.", argName);
  }

  private static class Native
  {
    // The OSGeo.GDAL managed assembly has already loaded the native GDAL
    // library into this process; resolve it by the usual names.
    private static readonly Lazy<IntPtr> Handle = new(Load);

    private static IntPtr Load()
    {
      string[] candidates = OperatingSystem.IsWindows()
        ? new[] { "gdal.dll", "gdal311.dll" }
        : new[]
        {
          "libgdal.so",
          "libgdal",
          "libgdal.so.311",
          "libgdal.so.310",
          "libgdal.so.309",
          "libgdal.so.308",
        };
      foreach (var name in candidates)
        if (NativeLibrary.TryLoad(name, out var handle))
          return handle;
      throw new DllNotFoundException(
        "Could not locate the native GDAL library. Check PATH / LD_LIBRARY_PATH "
          + "(same requirement as the OSGeo.GDAL managed assembly), or add the exact "
          + "library name to the candidate list in Reprojector."
      );
    }

    // Delegate types matching the stable GDAL C API signatures. All names
    // are suffixed 'Fn' so none collides with the wrapper methods below.
    private delegate IntPtr OSRNewSRSFn(byte[]? argument);
    private delegate int OSRSetFromUserInputFn(IntPtr srs, byte[] input);
    private delegate IntPtr OGRNewCTFn(IntPtr source, IntPtr dest);
    private delegate int OGRCTTransformFn(
      IntPtr transform,
      int nFeatures,
      double[] x,
      double[] y,
      double[] z,
      int bSkipFid
    );
    private delegate void OGRCTDestroyFn(IntPtr transform);
    private delegate void OSRReleaseFn(IntPtr srs);

    private static T Get<T>(string symbol)
      where T : Delegate
    {
      IntPtr p = NativeLibrary.GetExport(Handle.Value, symbol);
      if (p == IntPtr.Zero)
        throw new DllNotFoundException($"Native symbol '{symbol}' not found in the GDAL library.");
      return Marshal.GetDelegateForFunctionPointer<T>(p);
    }

    public static IntPtr OSRNewSpatialReference(string? argument)
    {
      byte[]? bytes = argument is null ? null : NullTerminatedUTF8(argument);
      return Get<OSRNewSRSFn>("OSRNewSpatialReference")(bytes);
    }

    public static int OSRSetFromUserInput(IntPtr srs, string input) =>
      Get<OSRSetFromUserInputFn>("OSRSetFromUserInput")(srs, NullTerminatedUTF8(input));

    public static IntPtr OGRNewCoordinateTransformation(IntPtr source, IntPtr dest) =>
      Get<OGRNewCTFn>("OGRNewCoordinateTransformation")(source, dest);

    public static int OGR_CT_Transform(
      IntPtr ct,
      int n,
      double[] x,
      double[] y,
      double[] z,
      int skipFid
    ) => Get<OGRCTTransformFn>("OGR_CT_Transform")(ct, n, x, y, z, skipFid);

    public static void OGR_CT_Destroy(IntPtr ct) => Get<OGRCTDestroyFn>("OGR_CT_Destroy")(ct);

    public static void OSRRelease(IntPtr srs) => Get<OSRReleaseFn>("OSRRelease")(srs);

    public static void Check(int rc, string what)
    {
      if (rc != 0)
        throw new InvalidOperationException($"GDAL error (code {rc}) while processing {what}.");
    }

    private static byte[] NullTerminatedUTF8(string value)
    {
      var bytes = Encoding.UTF8.GetBytes(value);
      var buffer = new byte[bytes.Length + 1];
      bytes.CopyTo(buffer, 0);
      return buffer;
    }
  }
}
