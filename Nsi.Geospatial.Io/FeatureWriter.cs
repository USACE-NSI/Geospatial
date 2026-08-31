using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using OSGeo.OGR;
using OSGeo.OSR;

namespace Nsi.Geospatial.Io;

/// <summary>GDAL/OGR-backed feature writer. `driverName` selects the OGR output driver.</summary>
public sealed class FeatureWriter : IFeatureSink
{
    public void Write(FeatureCollection collection, string path, string driverName = "ESRI Shapefile")
    {
        Ogr.RegisterAll();

        var driver = Ogr.GetDriverByName(driverName)
            ?? throw new ArgumentException($"Unknown OGR driver: {driverName}", nameof(driverName));

        // Shapefiles are a multi-file format (.shp + .shx + .dbf); the other drivers
        // we target are single-file. Only pre-clean the .shp for the shapefile driver.
        string target = path;
        if (string.Equals(driverName, "ESRI Shapefile", StringComparison.OrdinalIgnoreCase))
        {
            string baseName = global::System.IO.Path.ChangeExtension(path, null) ?? path;
            string shp = baseName + ".shp";
            if (global::System.IO.File.Exists(shp))
            {
                System.IO.File.Delete(shp);
                //if (!deleted)
               //     throw new global::System.IO.IOException($"Existing shapefile is locked: {shp}");
            }
            target = shp;
        }

        var srs = new SpatialReference(string.IsNullOrEmpty(collection.Wkt) ? null : collection.Wkt);
        using var ds = driver.CreateDataSource(target, Array.Empty<string>());
        using var layer = ds.CreateLayer(
            collection.Name ?? "layer",
            srs,
            MapShapeTypeToOgr(collection.ShapeType),
            Array.Empty<string>());

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

            if (feat.Parts.Count > 0 && collection.ShapeType == ShapeType.Point)
            {
                var p = feat.Parts[0].Vertices[0];
                string wktPt = $"POINT({p.X.ToString(global::System.Globalization.CultureInfo.InvariantCulture)} " +
                               $"{p.Y.ToString(global::System.Globalization.CultureInfo.InvariantCulture)})";
                of.SetGeometry(OSGeo.OGR.Geometry.CreateFromWkt(wktPt));
            }
            layer.CreateFeature(of);
        }
    }

    // The OSGeo binding's SetField takes a field NAME plus a typed value (no object
    // overload, and no Layer.FieldIndex in 3.11.3), so resolve by name and dispatch
    // on the CLR value type. The string setter works for any OGR field type.
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
                f.SetField(name, global::System.Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture));
                break;
        }
    }

    private static wkbGeometryType MapShapeTypeToOgr(ShapeType s) => s switch
    {
        ShapeType.Point => wkbGeometryType.wkbPoint,
        ShapeType.PointM => wkbGeometryType.wkbPointM,
        ShapeType.Line => wkbGeometryType.wkbLineString,
        ShapeType.Polygon => wkbGeometryType.wkbPolygon,
        _ => wkbGeometryType.wkbPoint,
    };

    private static OSGeo.OGR.FieldType MapFieldType(Nsi.Geospatial.Enums.FieldType t) => t switch
    {
        Nsi.Geospatial.Enums.FieldType.Integer => OSGeo.OGR.FieldType.OFTInteger,
        Nsi.Geospatial.Enums.FieldType.Double or Nsi.Geospatial.Enums.FieldType.Float or Nsi.Geospatial.Enums.FieldType.Numeric or Nsi.Geospatial.Enums.FieldType.Single => OSGeo.OGR.FieldType.OFTReal,
        Nsi.Geospatial.Enums.FieldType.Date => OSGeo.OGR.FieldType.OFTDate,
        _ => OSGeo.OGR.FieldType.OFTString,
    };
}