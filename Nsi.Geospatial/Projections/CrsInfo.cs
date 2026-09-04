namespace Nsi.Geospatial.Projections;

/// <summary>What a coordinate tuple means. Core cannot determine this from WKT
/// (that needs PROJ); Nsi.Geospatial.Io populates it when reading.</summary>
public enum CrsKind
{
  /// <summary>Not inspected. Derived metrics refuse to guess a unit and return null.</summary>
  Unknown,

  /// <summary>Longitude/latitude in degrees. No linear unit exists: a degree of
  /// longitude varies with latitude, so areas must be computed spherically.</summary>
  Geographic,

  /// <summary>Projected, in a fixed linear unit (UnitToMeters).</summary>
  Projected,
}

/// <summary>Linear unit of a projected CRS.</summary>
public enum LinearUnit
{
  Unknown,
  Meter,
  Foot,

  /// <summary>US survey foot, 1200/3937 m. State Plane foot data uses this, and
  /// confusing it with the international foot is a ~2e-6 scale error.</summary>
  UsSurveyFoot,
}

/// <summary>
/// The single dependable description of a geometry's coordinate reference system.
/// Owned by the FeatureCollection that read or created the features; Feature and
/// Part return it through their owner chain rather than holding a copy.
///
/// Distinct from Projection: Projection is a requested transform target, CrsInfo is
/// an inspected fact about data already in hand.
/// </summary>
public sealed record CrsInfo
{
  /// <summary>Exact international foot.</summary>
  public const double MetersPerFoot = 0.3048;

  /// <summary>US survey foot, exactly 1200/3937 metres.</summary>
  public const double MetersPerUsSurveyFoot = 1200.0 / 3937.0;

  public CrsKind Kind { get; init; } = CrsKind.Unknown;
  public LinearUnit Unit { get; init; } = LinearUnit.Unknown;

  /// <summary>Metres per linear unit. 0 when Kind is not Projected.</summary>
  public double UnitToMeters { get; init; }

  /// <summary>Authoritative WKT, or null when unknown. This is the one copy.</summary>
  public string? Wkt { get; init; }

  /// <summary>EPSG code when the authority block supplied one.</summary>
  public int? EpsgCode { get; init; }

  public static CrsInfo Unknown { get; } = new();

  /// <summary>Metres per unit, defaulting to metre for an unlabelled projected CRS.</summary>
  public double UnitToMetersOrMeter => UnitToMeters > 0 ? UnitToMeters : 1.0;

  public override string ToString() =>
    EpsgCode is not null ? $"EPSG:{EpsgCode} ({Kind})"
    : Kind == CrsKind.Unknown ? "CRS unknown"
    : $"{Kind}, {Unit}";
}
