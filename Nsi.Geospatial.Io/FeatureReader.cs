using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Geometry;
using OSGeo.OGR;

namespace Nsi.Geospatial.Io;

/// <summary>GDAL/OGR-backed feature reader (deterministic disposal of OGR handles).</summary>
public sealed class FeatureReader : IFeatureSource
{
    public FeatureCollection Read(string path)
    {
        Ogr.RegisterAll();

        using var ds = Ogr.Open(path, 0)
            ?? throw new FileNotFoundException($"Could not open: {path}");
        using var layer = ds.GetLayerByIndex(0)
            ?? throw new InvalidOperationException("Layer 0 not found.");

        string wkt = "";
        layer.GetSpatialRef()?.ExportToWkt(out wkt, Array.Empty<string>());

        var fc = new FeatureCollection
        {
            Name = layer.GetName(),
            Wkt = wkt,
            ShapeType = MapGeomType(layer.GetGeomType()),
        };

        OSGeo.OGR.Feature? feat;
        while ((feat = layer.GetNextFeature()) is not null)
        {
            var f = new Nsi.Geospatial.Geometry.Feature();
            f.Wkt = wkt;
            f.Path = path;

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
                foreach (var part in ProcessGeometry(geom, wkt))
                    f.AddPart(part);
                f.ComputeMbr();
            }

            fc.AddFeature(f);
            feat.Dispose();
        }

        return fc;
    }

    private static object? ReadFieldValue(OSGeo.OGR.Feature feat, int i, Nsi.Geospatial.Enums.FieldType type) => type switch
    {
        Nsi.Geospatial.Enums.FieldType.Integer => feat.GetFieldAsInteger(i),
        Nsi.Geospatial.Enums.FieldType.Double or Nsi.Geospatial.Enums.FieldType.Float or Nsi.Geospatial.Enums.FieldType.Numeric or Nsi.Geospatial.Enums.FieldType.Single => feat.GetFieldAsDouble(i),
        // Shapefiles have no true date field; the OSGeo binding's GetFieldAsDateTime
        // returns void, so read the date as a string instead.
        Nsi.Geospatial.Enums.FieldType.Date => feat.IsFieldSet(i) ? feat.GetFieldAsString(i) : null,
        _ => feat.GetFieldAsString(i),
    };

    private static List<Part> ProcessGeometry(OSGeo.OGR.Geometry geom, string wkt)
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

    private static Nsi.Geospatial.Enums.FieldType MapFieldType(OSGeo.OGR.FieldType t) => t switch
    {
        OSGeo.OGR.FieldType.OFTInteger => Nsi.Geospatial.Enums.FieldType.Integer,
        OSGeo.OGR.FieldType.OFTReal => Nsi.Geospatial.Enums.FieldType.Double,
        OSGeo.OGR.FieldType.OFTString => Nsi.Geospatial.Enums.FieldType.Text,
        OSGeo.OGR.FieldType.OFTDate or OSGeo.OGR.FieldType.OFTDateTime => Nsi.Geospatial.Enums.FieldType.Date,
        _ => Nsi.Geospatial.Enums.FieldType.Text,
    };
}