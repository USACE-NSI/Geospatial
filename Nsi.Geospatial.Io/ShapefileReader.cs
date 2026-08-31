using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace Nsi.Geospatial.Io;

/// <summary>GDAL/OGR-backed shapefile reader. fix: deterministic disposal of OGR handles.</summary>
public sealed class ShapefileReader : IFeatureSource
{
    public FeatureCollection Read(string path)
    {
        Gdal.Configure();
        Ogr.RegisterAll();

        // fix: no more hardcoded C:\Software\GDAL GISInternals paths. Native runtime must be
        // discoverable via PATH / GDAL_DATA / PROJ_LIB; surface a clear error otherwise.
        using var ds = Ogr.Open(path, 0) ?? throw new FileNotFoundException($"Could not open shapefile: {path}");
        using var layer = ds.GetLayerByIndex(0) ?? throw new InvalidOperationException("Layer 0 not found.");

        string wkt = "";
        layer.GetSpatialRef().ExportToWkt(out wkt, Array.Empty<string>());

        var fc = new FeatureCollection
        {
            Name = layer.GetName(),
            Wkt = wkt,
            ShapeType = MapGeomType(layer.GetGeomType()),
        };

        Feature? feat;
        while ((feat = layer.GetNextFeature()) is not null)
        {
            var f = new Feature();
            f.Wkt = wkt;
            f.Path = path;

            for (int i = 0; i < feat.GetFieldCount(); i++)
            {
                using var defn = feat.GetFieldDefnRef(i);
                string name = defn.GetName();
                FieldType type = MapFieldType(defn.GetFieldType());
                fc.Schema.AddField(name, type, defn.GetWidth(), defn.GetPrecision());
                f.Attributes[name] = ReadFieldValue(feat, i, type);
            }

            if (feat.GetGeometryRef() is { } geom && geom is not null)
            {
                foreach (var part in ProcessGeometry(geom, wkt))
                    f.AddPart(part);
                f.ComputeMbr();
            }

            fc.AddFeature(f);
            feat.Dispose();
        }

        return fc;
    }

    private static object? ReadFieldValue(Feature feat, int i, FieldType type) => type switch
    {
        FieldType.Integer => feat.GetFieldAsInteger(i),
        FieldType.Double or FieldType.Float or FieldType.Numeric => feat.GetFieldAsDouble(i),
        FieldType.Date or FieldType.Single =>
            feat.GetFieldAsDateTime(i, out var yr, out var mo, out var dy, out var h, out var mi, out var s, out _)
                ? new DateTime(yr, mo, dy, h, mi, (int)s) : null,
        _ => feat.GetFieldAsString(i),
    };

    private static List<Part> ProcessGeometry(Geometry geom, string wkt)
    {
        var parts = new List<Part>();
        var type = geom.GetGeometryType();

        if (type is wkbGeometryType.wkbPolygon or wkbGeometryType.wkbPolygon25D)
        {
            for (int r = 0; r < geom.GetGeometryCount(); r++)
            {
                using var ring = geom.GetGeometryRef(r);
                var part = new Part(wkt) { Direction = ring.IsClockwise() };
                for (int v = 0; v < ring.GetPointCount(); v++)
                    part.AddVertex(new Vertex(ring.GetX(v), ring.GetY(v)));
                part.CloseRing();
                parts.Add(part);
            }
        }
        else if (type is wkbGeometryType.wkbLineString or wkbGeometryType.wkbLineString25D)
        {
            var part = new Part(wkt);
            for (int v = 0; v < geom.GetPointCount(); v++)
                part.AddVertex(new Vertex(geom.GetX(v), geom.GetY(v)));
            part.CloseRing();
            parts.Add(part);
        }
        else if (type is wkbGeometryType.wkbPoint or wkbGeometryType.wkbPoint25D or wkbGeometryType.wkbPointM)
        {
            var part = new Part(wkt);
            part.AddVertex(new Vertex(geom.GetX(0), geom.GetY(0)));
            part.CloseRing();
            parts.Add(part);
        }
        else if (type is wkbGeometryType.wkbMultiPolygon)
        {
            for (int i = 0; i < geom.GetGeometryCount(); i++)
            {
                using var poly = geom.GetGeometryRef(i);
                parts.AddRange(ProcessGeometry(poly, wkt));
            }
        }

        return parts;
    }

    private static ShapeType MapGeomType(wkbGeometryType t) => t switch
    {
        wkbGeometryType.wkbPoint or wkbGeometryType.wkbPoint25D => ShapeType.Point,
        wkbGeometryType.wkbPointM => ShapeType.PointM,
        wkbGeometryType.wkbLineString or wkbGeometryType.wkbLineString25D => ShapeType.Line,
        wkbGeometryType.wkbPolygon or wkbGeometryType.wkbPolygon25D or wkbGeometryType.wkbMultiPolygon => ShapeType.Polygon,
        _ => ShapeType.Point,
    };

    private static FieldType MapFieldType(OSGeo.OGR.FieldType t) => t switch
    {
        OSGeo.OGR.FieldType.OFTInteger => FieldType.Integer,
        OSGeo.OGR.FieldType.OFTReal => FieldType.Double,
        OSGeo.OGR.FieldType.OFTString => FieldType.Text,
        OSGeo.OGR.FieldType.OFTDate or OSGeo.OGR.FieldType.OFTDateTime => FieldType.Date,
        _ => FieldType.Text,
    };
}