using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using OSGeo.OGR;
using OSGeo.OSR;

namespace Nsi.Geospatial.Io;

/// <summary>
/// GDAL/OGR-backed feature writer. <c>driverName</c> selects the OGR output driver.
/// </summary>
public sealed class SpatialWriter : IFeatureSink
{
  /// Every ESRI shapefile sidecar, so pre-cleanup removes all files a previous run left.
  private static readonly string[] ShapefileSidecars = { ".shp", ".shx", ".dbf", ".prj" };

  public void Write(FeatureCollection collection, string path, string driverName = "ESRI Shapefile")
  {
    Ogr.RegisterAll();

    var driver =
      Ogr.GetDriverByName(driverName)
      ?? throw new ArgumentException($"Unknown OGR driver: {driverName}", nameof(driverName));

    string target = path;
    if (string.Equals(driverName, "ESRI Shapefile", StringComparison.OrdinalIgnoreCase))
    {
      // a shapefile is a multi-file format (.shp + .shx + .dbf + .prj).
      // Delete every sidecar up front and detect a locked one (File.Delete throws,
      // or the file is still there afterwards), so OGR never opens a dataset we
      // could not fully replace. The old code deleted only .shp and its lock
      // check was commented out, which left stale .shx/.dbf/.prj and
      // half-deleted files on failure.
      string baseName = global::System.IO.Path.ChangeExtension(path, null) ?? path;
      foreach (string ext in ShapefileSidecars)
      {
        string sidecar = baseName + ext;
        if (!global::System.IO.File.Exists(sidecar))
          continue;

        try
        {
          global::System.IO.File.Delete(sidecar);
        }
        catch (Exception ex)
          when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
          throw new System.IO.IOException($"Existing shapefile is locked: {sidecar}", ex);
        }

        if (global::System.IO.File.Exists(sidecar))
          throw new System.IO.IOException($"Existing shapefile is locked: {sidecar}");
      }
      target = baseName + ".shp";
    }

    string? collectionWkt = collection.Crs.Wkt;
    using var srs = new SpatialReference(
      string.IsNullOrEmpty(collectionWkt) ? null : collectionWkt
    );
    using var ds = driver.CreateDataSource(target, Array.Empty<string>());
    using var layer = ds.CreateLayer(
      collection.Name ?? "layer",
      srs,
      MapShapeTypeToOgr(collection.ShapeType),
      Array.Empty<string>()
    );

    var defn = layer.GetLayerDefn();

    foreach (var col in collection.Schema.ColumnNames)
    {
      var c = collection.Schema[col];
      var fdefn = new FieldDefn(c.Name, MapFieldType(c.FieldType));
      fdefn.SetWidth(c.Length);
      fdefn.SetPrecision(c.DecimalPlaces);
      layer.CreateField(fdefn, 1);
    }

    foreach (var feat in collection.Features)
    {
      using var of = new OSGeo.OGR.Feature(defn);
      foreach (var kv in feat.Attributes)
      {
        SetOgrField(of, kv.Key, kv.Value);
      }

      // emit geometry for every shape type, not just Point.
      // Built through the OGR geometry API rather than WKT parsing: the
      // OSGeo.OGR 3.11.3 CreateFromWkt path rejects valid polygon WKT in this
      // native environment ("OGR Error %d: General Error"); the programmatic
      // API works for all shape types.
      var geom = BuildOgrGeometry(collection.ShapeType, feat);
      if (geom is not null)
      {
        of.SetGeometry(geom); // OGR copies the geometry into the feature
        geom.Dispose(); // the binding returns a Geometry we own
      }

      layer.CreateFeature(of);
    }
  }

  /// Builds the feature's OGR geometry directly from its parts, or null when it has none.
  private static OSGeo.OGR.Geometry? BuildOgrGeometry(
    ShapeType shapeType,
    Nsi.Geospatial.Geometry.Feature feat
  )
  {
    var parts = feat.Parts.Where(p => p.Vertices.Count > 0).ToList();
    if (parts.Count == 0)
      return null;

    switch (shapeType)
    {
      case ShapeType.Point:
      case ShapeType.PointM:
      {
        // Both point shapes emit a plain 2D POINT: shapefiles carry no M axis,
        // so PointM stays representable without a driver-specific variant.
        var v = parts[0].Vertices[0];
        var g = new OSGeo.OGR.Geometry(wkbGeometryType.wkbPoint);
        g.AddPoint(v.X, v.Y, v.Z);
        return g;
      }

      case ShapeType.Line:
      {
        if (parts.Count == 1)
        {
          if (parts[0].Vertices.Count < 2)
            throw new System.IO.InvalidDataException(
              $"Feature {feat.Id}: line part has fewer than 2 vertices"
            );
          var g = new OSGeo.OGR.Geometry(wkbGeometryType.wkbLineString);
          foreach (var v in parts[0].Vertices)
            g.AddPoint(v.X, v.Y, v.Z);
          return g;
        }

        if (parts.Any(p => p.Vertices.Count < 2))
          throw new System.IO.InvalidDataException(
            $"Feature {feat.Id}: multi-part line has a part with fewer than 2 vertices"
          );
        var multi = new OSGeo.OGR.Geometry(wkbGeometryType.wkbMultiLineString);
        foreach (var part in parts)
        {
          var seg = new OSGeo.OGR.Geometry(wkbGeometryType.wkbLineString);
          foreach (var v in part.Vertices)
            seg.AddPoint(v.X, v.Y, v.Z);
          multi.AddGeometry(seg);
        }
        return multi;
      }

      case ShapeType.Polygon:
      {
        // Ring 0 is the exterior ring; the rest are holes. A WKT/OGR ring must be
        // closed: repeat the first vertex unless the ring already is.
        var poly = new OSGeo.OGR.Geometry(wkbGeometryType.wkbPolygon);
        for (int i = 0; i < parts.Count; i++)
        {
          var verts = parts[i].Vertices;
          if (i == 0 && verts.Count < 3)
            throw new System.IO.InvalidDataException(
              $"Feature {feat.Id}: polygon exterior ring has fewer than 3 unique vertices"
            );
          if (i > 0 && verts.Count < 3)
            throw new System.IO.InvalidDataException(
              $"Feature {feat.Id}: polygon hole {i} has fewer than 3 unique vertices"
            );

          var ring = new OSGeo.OGR.Geometry(wkbGeometryType.wkbLinearRing);
          foreach (var v in verts)
            ring.AddPoint(v.X, v.Y, v.Z);
          var first = verts[0];
          if (verts[^1].Coordinates != first.Coordinates)
            ring.AddPoint(first.X, first.Y, first.Z); // close the ring
          poly.AddGeometry(ring);
        }
        return poly;
      }

      default:
        return null;
    }
  }

  /// A WKT ring must be closed: repeat the first vertex unless the ring already is.
  private static List<Vertex> ClosedRing(Part part)
  {
    var verts = new List<Vertex>(part.Vertices);
    if (verts.Count > 1 && verts[0].Coordinates != verts[^1].Coordinates)
      verts.Add(verts[0]);
    return verts;
  }

  /// "x y, x y, ..." for one ring or line, invariant-culture coordinates.
  private static string RingWkt(IReadOnlyList<Vertex> ring) =>
    string.Join(", ", ring.Select(v => $"{Fmt(v.X)} {Fmt(v.Y)}"));

  private static string Fmt(double value) =>
    value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);

  // The OSGeo binding's SetField takes a field NAME plus a typed value (no object
  // overload, and no Layer.FieldIndex in 3.11.3), so resolve by name and dispatch
  // on the CLR value type. The string setter works for any OGR field type.
  // P0-4 (long→int cast, Long→OFTString, bool "1"/"0") is a known adjacent issue,
  // intentionally left unchanged in this class.
  private static void SetOgrField(OSGeo.OGR.Feature f, string name, object? value)
  {
    if (value is null)
      return; // leave the field NULL
    switch (value)
    {
      case string s:
        f.SetField(name, s);
        break;
      case double d:
        f.SetField(name, d);
        break;
      case int i:
        f.SetField(name, i);
        break;
      case long l:
        f.SetField(name, (int)l);
        break;
      case float fl:
        f.SetField(name, (double)fl);
        break;
      case bool b:
        f.SetField(name, b ? "1" : "0");
        break;
      default:
        f.SetField(
          name,
          global::System.Convert.ToString(
            value,
            global::System.Globalization.CultureInfo.InvariantCulture
          )
        );
        break;
    }
  }

  private static wkbGeometryType MapShapeTypeToOgr(ShapeType s) =>
    s switch
    {
      ShapeType.Point => wkbGeometryType.wkbPoint,
      ShapeType.PointM => wkbGeometryType.wkbPointM,
      ShapeType.Line => wkbGeometryType.wkbLineString,
      ShapeType.Polygon => wkbGeometryType.wkbPolygon,
      _ => wkbGeometryType.wkbPoint,
    };

  private static OSGeo.OGR.FieldType MapFieldType(Nsi.Geospatial.Enums.FieldType t) =>
    t switch
    {
      Nsi.Geospatial.Enums.FieldType.IntegerFT => OSGeo.OGR.FieldType.OFTInteger,
      Nsi.Geospatial.Enums.FieldType.DoubleFT
      or Nsi.Geospatial.Enums.FieldType.FloatFT
      or Nsi.Geospatial.Enums.FieldType.NumericFT
      or Nsi.Geospatial.Enums.FieldType.SingleFT => OSGeo.OGR.FieldType.OFTReal,
      Nsi.Geospatial.Enums.FieldType.DateFT => OSGeo.OGR.FieldType.OFTDate,
      _ => OSGeo.OGR.FieldType.OFTString,
    };
}

