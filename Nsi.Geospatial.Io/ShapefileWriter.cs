using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace Nsi.Geospatial.Io;

/// <summary>GDAL/OGR-backed shapefile writer (replaces the old commented-out WriteToFile).</summary>
public sealed class ShapefileWriter : IFeatureSink
{
    public void Write(FeatureCollection collection, string path)
    {
        Gdal.Configure();
        Ogr.RegisterAll();

        string baseName = Path.ChangeExtension(path, null) ?? path;
        if (File.Exists(baseName + ".shp") && !File.Delete(baseName + ".shp"))
            throw new IOException("Existing shapefile is locked.");

        var driver = Ogr.GetDriverByName("ESRI Shapefile");
        using var ds = driver.Create(baseName + ".shp", 0, 0, Array.Empty<string>());
        using var layer = ds.CreateLayer(
            collection.Name ?? "layer",
            MapShapeTypeToOgr(collection.ShapeType),
            Ogr.CreateSpatialRefFromWkt(collection.Wkt));

        foreach (var col in collection.Schema.ColumnNames)
        {
            var c = collection.Schema[col];
            layer.CreateField(new FieldDefn(c.Name, MapFieldType(c.FieldType), c.Length, c.DecimalPlaces));
        }

        foreach (var feat in collection.Features)
        {
            using var of = new Feature(layer);
            foreach (var kv in feat.Attributes)
                if (layer.FieldIndex(kv.Key) >= 0)
                    of.SetField(layer.FieldIndex(kv.Key), kv.Value);

            if (feat.Parts.Count > 0 && collection.ShapeType == ShapeType.Point)
            {
                var p = feat.Parts[0].Vertices[0];
                of.SetGeometry(new OSGeo.OGR.Point(p.X, p.Y));
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

    private static OSGeo.OGR.FieldType MapFieldType(FieldType t) => t switch
    {
        FieldType.Integer => OSGeo.OGR.FieldType.OFTInteger,
        FieldType.Double or FieldType.Float or FieldType.Numeric => OSGeo.OGR.FieldType.OFTReal,
        FieldType.Date => OSGeo.OGR.FieldType.OFTDate,
        _ => OSGeo.OGR.FieldType.OFTString,
    };
}