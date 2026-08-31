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
            string baseName = System.IO.Path.ChangeExtension(path, null) ?? path;
            string shp = baseName + ".shp";
            if (System.IO.File.Exists(shp) && System.IO.File.Delete(shp))
                throw new System.IO.IOException($"Existing shapefile is locked: {shp}");
            target = shp;
        }

        var srs = new SpatialReference(string.IsNullOrEmpty(collection.Wkt) ? null : collection.Wkt);
        using var ds = driver.Create(target, 0, 0, Array.Empty<string>());
        using var layer = ds.CreateLayer(
            collection.Name ?? "layer",
            MapShapeTypeToOgr(collection.ShapeType),
            srs);

        foreach (var col in collection.Schema.ColumnNames)
        {
            var c = collection.Schema[col];
            var fdefn = new FieldDefn(c.Name, MapFieldType(c.FieldType));
            fdefn.SetWidth(c.Length);
            fdefn.SetPrecision(c.DecimalPlaces);
            layer.CreateField(fdefn);
        }

        foreach (var feat in collection.Features)
        {
            using var of = new OSGeo.OGR.Feature(layer);
            foreach (var kv in feat.Attributes)
            {
                int idx = layer.FieldIndex(kv.Key);
                if (idx >= 0)
                    of.SetField(idx, kv.Value);
            }

            if (feat.Parts.Count > 0 && collection.ShapeType == ShapeType.Point)
            {
                var p = feat.Parts[0].Vertices[0];
                of.SetGeometry(OSGeo.OGR.Geometry.CreateFromWkt($"POINT({p.X} {p.Y})"));
            }
            layer.CreateFeature(of);
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