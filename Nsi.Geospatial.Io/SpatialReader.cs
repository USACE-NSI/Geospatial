using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using Nsi.Geospatial.Io;
using Nsi.Geospatial.Projections;
using Nsi.Geospatial.Reprojection;
using OSGeo.OGR;
using OSGeo.OSR;

namespace Nsi.Geospatial.Io;

/// GDAL/OGR-backed feature reader (deterministic disposal of OGR handles).
public sealed class SpatialReader : IFeatureSource
{
  private readonly SpatialReaderOptions _options;

  public SpatialReader(SpatialReaderOptions? options = null) =>
    _options = options ?? new SpatialReaderOptions();

  public FeatureCollection Read(string path)
  {
    Ogr.RegisterAll();

    using var ds = Ogr.Open(path, 0) ?? throw new FileNotFoundException($"Could not open: {path}");
    using var layer =
      ds.GetLayerByIndex(_options.LayerIndex)
      ?? throw new InvalidOperationException($"Layer {_options.LayerIndex} not found.");

    using SpatialReference? sourceSrs = layer.GetSpatialRef();
    CrsInfo sourceCrs = CrsInspector.Inspect(sourceSrs);

    if (_options.RequireInspectableCrs && sourceCrs.Kind == CrsKind.Unknown)
    {
      throw new InvalidOperationException(
        $"Source CRS for {path} could not be inspected; derived metrics would be unusable."
      );
    }

    // One transform for the whole read, and only when the source is not already
    // in the target CRS.
    Projection? target = _options.ReprojectTo;
    bool needsTransform = target is not null && !SameCrs(sourceCrs, target);

    using CoordinateTransformer? transformer = needsTransform
      ? new CoordinateTransformer(ToProjection(sourceCrs), target!)
      : null;

    CrsInfo crs = needsTransform ? CrsInspector.Inspect(WktOf(target!)) : sourceCrs;

    var fc = new FeatureCollection
    {
      Name = layer.GetName(),
      Crs = crs,
      ShapeType = MapGeomType(layer.GetGeomType()),
    };

    OSGeo.OGR.Feature? feat;
    while ((feat = layer.GetNextFeature()) is not null)
    {
      var f = new Nsi.Geospatial.Geometry.Feature { Path = path };

      for (int i = 0; i < feat.GetFieldCount(); i++)
      {
        using var defn = feat.GetFieldDefnRef(i);
        string name = defn.GetName();
        Nsi.Geospatial.Enums.FieldType type = MapFieldType(defn.GetFieldType());
        fc.Schema.AddField(name, type, defn.GetWidth(), defn.GetPrecision());
        f.Attributes[name] = ReadFieldValue(feat, i, type);
      }

      if (feat.GetGeometryRef() is { } geom)
      {
        foreach (var part in ProcessGeometry(geom, transformer))
        {
          f.AddPart(part);
        }
        f.ComputeBoundingBox();
      }

      fc.AddFeature(f);
      feat.Dispose();
    }

    return fc;
  }

  /// <summary>
  /// True when the source is already in the requested CRS, so the transform can be
  /// skipped. Compares EPSG codes when both sides have one (authoritative), else
  /// falls back to whitespace-stripped WKT equality. A source and target that are
  /// semantically identical but textually different will be treated as different --
  /// that costs one redundant transform, which is safe.
  /// </summary>
  private static bool SameCrs(CrsInfo source, Projection target)
  {
    int? targetCode = ParseEpsg(target.EpsgCode);
    if (source.EpsgCode is not null && targetCode is not null)
    {
      return source.EpsgCode == targetCode;
    }

    string Normalize(string? wkt) =>
      string.Concat((wkt ?? string.Empty).Where(c => !char.IsWhiteSpace(c)));

    string targetWkt = target.Wkt;
    return !string.IsNullOrWhiteSpace(targetWkt)
      && string.Equals(Normalize(source.Wkt), Normalize(targetWkt), StringComparison.Ordinal);
  }

  private static int? ParseEpsg(string? token) =>
    int.TryParse(
      token?.AsSpan("EPSG:".Length).TrimStart(),
      NumberStyles.Integer,
      CultureInfo.InvariantCulture,
      out int code
    )
      ? code
      : null;

  /// <summary>WKT for the source, which always carries it after inspection.</summary>
  private static Projection ToProjection(CrsInfo crs)
  {
    if (string.IsNullOrWhiteSpace(crs.Wkt))
    {
      throw new InvalidOperationException(
        "Cannot reproject: the source CRS has no WKT, so its target transform is undefined."
      );
    }
    return new Projection(crs.Wkt, crs.EpsgCode is { } c ? $"EPSG:{c}" : null);
  }

  /// <summary>
  /// Expands a Projection to WKT so the reprojected collection's Crs describes the
  /// target and SpatialWriter emits a truthful .prj. Uses OSR to resolve an
  /// EPSG-only Projection, which is why this lives in Io rather than core.
  /// </summary>
  private static string WktOf(Projection projection)
  {
    if (!string.IsNullOrWhiteSpace(projection.Wkt))
    {
      return projection.Wkt;
    }

    var srs = new SpatialReference(null);
    try
    {
      string token =
        projection.EpsgCode
        ?? throw new ArgumentException(
          "Projection must supply a Wkt or an EpsgCode.",
          nameof(projection)
        );
      if (srs.SetFromUserInput(token) != 0)
      {
        throw new InvalidOperationException($"OSR could not resolve {token}.");
      }
      srs.ExportToWkt(out string wkt, Array.Empty<string>());
      return wkt;
    }
    finally
    {
      srs.Dispose();
    }
  }

  /// <summary>
  /// Rings are built with orientation taken from the source geometry and coordinates
  /// optionally reprojected. Direction must come from the source: IsClockwise is a
  /// property of the ring as authored, and Albers can flip handedness near the
  /// standard parallels. CloseRing runs after any transform so the cached metrics are
  /// in the collection's linear units.
  /// </summary>
  private static List<Part> ProcessGeometry(
    OSGeo.OGR.Geometry geom,
    CoordinateTransformer? transformer
  )
  {
    var parts = new List<Part>();
    var type = geom.GetGeometryType();

    if (type is wkbGeometryType.wkbPolygon or wkbGeometryType.wkbPolygon25D)
    {
      for (int r = 0; r < geom.GetGeometryCount(); r++)
      {
        using var ring = geom.GetGeometryRef(r);
        bool clockwise = ring.IsClockwise();
        var part = new Part { Direction = clockwise };
        foreach ((double x, double y) in Points(ring, transformer))
        {
          part.AddVertex(new Vertex(x, y));
        }
        part.CloseRing();
        parts.Add(part);
      }
    }
    else if (type is wkbGeometryType.wkbLineString or wkbGeometryType.wkbLineString25D)
    {
      var part = new Part { Direction = geom.IsClockwise() };
      foreach ((double x, double y) in Points(geom, transformer))
      {
        part.AddVertex(new Vertex(x, y));
      }
      part.CloseRing();
      parts.Add(part);
    }
    else if (
      type is wkbGeometryType.wkbPoint or wkbGeometryType.wkbPoint25D or wkbGeometryType.wkbPointM
    )
    {
      var part = new Part();
      double x = geom.GetX(0);
      double y = geom.GetY(0);
      if (transformer is not null)
      {
        (x, y) = transformer.Reproject([(x, y)])[0];
      }
      part.AddVertex(new Vertex(x, y));
      part.CloseRing();
      parts.Add(part);
    }
    else if (type is wkbGeometryType.wkbMultiPolygon)
    {
      for (int i = 0; i < geom.GetGeometryCount(); i++)
      {
        using var poly = geom.GetGeometryRef(i);
        parts.AddRange(ProcessGeometry(poly, transformer));
      }
    }

    return parts;
  }

  private static List<(double X, double Y)> Points(
    OSGeo.OGR.Geometry geom,
    CoordinateTransformer? transformer
  )
  {
    var points = new List<(double X, double Y)>(geom.GetPointCount());
    for (int v = 0; v < geom.GetPointCount(); v++)
    {
      points.Add((geom.GetX(v), geom.GetY(v)));
    }
    return transformer is null ? points : transformer.Reproject(points);
  }
}
