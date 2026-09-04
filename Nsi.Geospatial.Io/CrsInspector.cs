using Nsi.Geospatial.Projections;
using OSGeo.OSR;

namespace Nsi.Geospatial.Io;

/// <summary>
/// Resolves a spatial reference into a <see cref="CrsInfo"/>. Lives in Io because it
/// needs PROJ through the GDAL runtime; Nsi.Geospatial core stays free of GDAL and
/// only ever consumes the result.
///
/// Uses the managed OSGeo.OSR binding rather than raw P/Invoke: everything needed
/// here (IsGeographic, IsProjected, GetLinearUnits, authority lookup) is already
/// wrapped, which is why SpatialWriter's SpatialReference construction works today.
/// </summary>
public static class CrsInspector
{
  /// <summary>Tolerance for matching GDAL's reported unit factor against exact values.</summary>
  private const double UnitTolerance = 1e-12;

  /// <summary>
  /// Inspects a spatial reference already in hand — the one from
  /// <c>layer.GetSpatialRef()</c>. Avoids exporting to WKT only to re-import it.
  /// The supplied object is not mutated: EPSG auto-identification runs on a clone.
  /// Returns <see cref="CrsInfo.Unknown"/> for null or an unreadable reference.
  /// </summary>
  public static CrsInfo Inspect(SpatialReference? srs)
  {
    if (srs is null)
    {
      return CrsInfo.Unknown;
    }

    CrsKind kind =
      AsBoolean(srs.IsGeographic()) ? CrsKind.Geographic
      : AsBoolean(srs.IsProjected()) ? CrsKind.Projected
      : CrsKind.Unknown;

    if (kind == CrsKind.Unknown)
    {
      return CrsInfo.Unknown;
    }

    srs.ExportToWkt(out string? wkt, Array.Empty<string>());
    int? epsg = ReadEpsgCode(srs);

    // Only projected CRS have linear units. On a geographic SRS GDAL reports the
    // ANGULAR unit here (1.0 / "degree"), which is not a length and must not be
    // recorded as one.
    double unitToMeters = 0;
    LinearUnit unit = LinearUnit.Unknown;
    if (kind == CrsKind.Projected)
    {
      unitToMeters = srs.GetLinearUnits(out string unitName);
      unit = MapUnit(unitName, unitToMeters);
    }

    return new CrsInfo
    {
      Kind = kind,
      Unit = unit,
      UnitToMeters = unitToMeters,
      Wkt = wkt,
      EpsgCode = epsg,
    };
  }

  /// <summary>
  /// Inspects a WKT string. Prefer the <see cref="SpatialReference"/> overload when
  /// the object is already available.
  /// </summary>
  public static CrsInfo Inspect(string? wkt)
  {
    if (string.IsNullOrWhiteSpace(wkt))
    {
      return CrsInfo.Unknown;
    }

    var srs = new SpatialReference(null);
    try
    {
      if (srs.SetFromUserInput(wkt) != 0)
      {
        return CrsInfo.Unknown;
      }

      return Inspect(srs);
    }
    finally
    {
      srs.Dispose();
    }
  }

  /// <summary>
  /// Reads the EPSG code. An AUTHORITY node is often absent even for a well-known
  /// CRS, so AutoIdentifyEPSG is used as a database lookup. Both run against a
  /// clone: AutoIdentifyEPSG mutates the reference, and the object handed to us is
  /// usually the layer's own SRS, which we have no right to change.
  /// </summary>
  private static int? ReadEpsgCode(SpatialReference srs)
  {
    SpatialReference probe;
    try
    {
      probe = srs.Clone() ?? srs;
    }
    catch
    {
      return null;
    }

    bool ownsProbe = !ReferenceEquals(probe, srs);
    try
    {
      // GEOGCS for geographic, PROJCS for projected; try both so one overload
      // does not silently miss the other shape.
      int? code =
        ParseCode(probe.GetAuthorityCode("PROJCS")) ?? ParseCode(probe.GetAuthorityCode("GEOGCS"));

      if (code is not null)
      {
        return code;
      }

      if (probe.AutoIdentifyEPSG() != 0)
      {
        return null;
      }

      return ParseCode(probe.GetAuthorityCode("PROJCS"))
        ?? ParseCode(probe.GetAuthorityCode("GEOGCS"));
    }
    catch
    {
      return null;
    }
    finally
    {
      if (ownsProbe)
      {
        probe.Dispose();
      }
    }
  }

  private static int? ParseCode(string? raw) =>
    int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code) && code > 0
      ? code
      : null;

  /// <summary>
  /// Maps GDAL's reported unit to a named unit. Matched on the metre factor, not the
  /// label: WKT unit names are author-supplied free text ("metre", "Meter",
  /// "Foot (International)"), while the factor is the definition.
  ///
  /// Unknown labels still convert correctly, because UnitToMeters carries the scale
  /// and CrsInfo.UnitToMetersOrMeter is what consumers multiply by. The enum is for
  /// readability and diagnostics only -- never gate a conversion on it.
  /// </summary>
  private static LinearUnit MapUnit(string? name, double toMeters)
  {
    if (Math.Abs(toMeters - 1.0) < UnitTolerance)
    {
      return LinearUnit.Meter;
    }

    // US survey foot is exactly 1200/3937 m. State Plane foot zones use it;
    // mistaking it for the international foot is a ~2e-6 error, which is 2 parts
    // per million of area -- small, but it is a silent, systematic one.
    if (Math.Abs(toMeters - CrsInfo.MetersPerUsSurveyFoot) < UnitTolerance)
    {
      return LinearUnit.UsSurveyFoot;
    }

    if (Math.Abs(toMeters - CrsInfo.MetersPerFoot) < UnitTolerance)
    {
      return LinearUnit.Foot;
    }

    return LinearUnit.Unknown;
  }

  /// <summary>
  /// OGR's boolean accessors surface as int (TRUE/FALSE) through SWIG.
  /// </summary>
  private static bool AsBoolean(int value) => value != 0;
}
