using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.AccessControl;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.VisualBasic.FileIO;
using OSGeo.OGR;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace AlexGeospatial
{
    public static class IOtools
    {
        public static List<string> FindUniques(List<string> inputlist)
        {
            var uniquesList = new HashSet<string>();
            foreach (string Val in inputlist)
            {
                if (uniquesList.Contains(Val))
                {
                }
                else
                {
                    uniquesList.Add(Val);
                }
            }
            return uniquesList.ToList();
        }
        public static Feat ReadShapefile(string shapefilePath, bool buildRTree)
        {
            Ogr.RegisterAll();
            DataSource ds = Ogr.Open(shapefilePath, 0);
            if (ds == null) throw new Exception("Could not open shapefile.");            

            Layer layer = ds.GetLayerByIndex(0);
            Feature shpFeat;
            int featureIndex = 0;

            //get WKT
            string WKT = "";
            layer.GetSpatialRef().ExportToWkt(out WKT, new string[] { });
            
            //get shape type
            GeospatialTools.eShapeType shapeType = new GeospatialTools.eShapeType();
            wkbGeometryType geomType = layer.GetGeomType();
            switch (geomType)
            {
                case wkbGeometryType.wkbPoint:
                case wkbGeometryType.wkbPoint25D:                
                    shapeType = GeospatialTools.eShapeType.shpPoint;
                    break;
                case wkbGeometryType.wkbPointM:
                    shapeType = GeospatialTools.eShapeType.shpPointM;
                    break;
                case wkbGeometryType.wkbLineString:
                case wkbGeometryType.wkbLineString25D:
                    shapeType = GeospatialTools.eShapeType.shpLine;
                    break;
                case wkbGeometryType.wkbPolygon:
                case wkbGeometryType.wkbPolygon25D:
                    shapeType = GeospatialTools.eShapeType.shpPoly;
                    break;
                default:
                    shapeType = GeospatialTools.eShapeType.shpPoint;
                    break;
            }

            //build output feature
            Feat outputFeat = new Feat(WKT, shapefilePath, layer.GetName(), shapeType);
            if(buildRTree) { outputFeat.ConstRTree(5, 10); }

            while ((shpFeat = layer.GetNextFeature()) != null)
            {
                //Get all attributes by type
                for (int i = 0; i < shpFeat.GetFieldCount(); i++)
                {
                    FieldDefn defn = shpFeat.GetFieldDefnRef(i);
                    string name = defn.GetName();
                    object value = shpFeat.GetFieldAsString(i);
                    OSGeo.OGR.FieldType type = defn.GetFieldType();
                    switch (type)
                    {
                        case OSGeo.OGR.FieldType.OFTInteger:
                            int intVal = shpFeat.GetFieldAsInteger(i);                            
                            outputFeat._attTable.addAppendColData(name, GeospatialTools.eFieldType.shpInteger, defn.GetWidth(), defn.GetPrecision(), intVal, featureIndex);                            
                            break;

                        case OSGeo.OGR.FieldType.OFTReal:
                            double dblVal = shpFeat.GetFieldAsDouble(i);
                            outputFeat._attTable.addAppendColData(name, GeospatialTools.eFieldType.shpDouble, defn.GetWidth(), defn.GetPrecision(), dblVal, featureIndex);
                            break;

                        case OSGeo.OGR.FieldType.OFTString:
                            string strVal = shpFeat.GetFieldAsString(i);
                            outputFeat._attTable.addAppendColData(name, GeospatialTools.eFieldType.shpText, defn.GetWidth(), 0, strVal, featureIndex);
                            break;

                        case OSGeo.OGR.FieldType.OFTDate:
                        case OSGeo.OGR.FieldType.OFTDateTime:
                            int year, month, day, hour, minute, tzFlag;
                            float second;
                            shpFeat.GetFieldAsDateTime(i, out year, out month, out day, out hour, out minute, out second, out tzFlag);
                            var dt = new DateTime(year, month, day, hour, minute, (int)second);
                            outputFeat._attTable.addAppendColData(name, GeospatialTools.eFieldType.shpDate, defn.GetWidth(), defn.GetPrecision(), dt, featureIndex);
                            break;

                        default:
                            // As fallback for now store as string                            
                            outputFeat._attTable.addAppendColData(name, GeospatialTools.eFieldType.shpText, defn.GetWidth(), 0, shpFeat.GetFieldAsString(i), featureIndex);
                            break;
                    } 
                }

                //Geometry part
                Geometry geom = shpFeat.GetGeometryRef();
                var featureParts = new List<Part>();
                var featureVertices = new List<List<Vertex>>();

                if (geom != null)
                {
                    //Check in multi-part
                    int geomCount = geom.GetGeometryCount();
                    if (geomCount == 0)
                    {                        
                        featureParts.AddRange(ProcessGeometry(geom, featureVertices, WKT));
                    }
                    else
                    {
                        for (int g = 0; g < geomCount; g++)
                        {
                            Geometry subGeom = geom.GetGeometryRef(g);
                            featureParts.AddRange(ProcessGeometry(subGeom, featureVertices, WKT));
                        }
                    }
                }
                outputFeat._parts.Add(featureParts);
                outputFeat._vertices.Add(featureVertices);

                if(buildRTree)
                {
                    for(int p = 0; p < outputFeat._parts.Count; p++)
                    {
                        outputFeat.AddFeatPartToRTree(featureIndex, p);
                    }
                }

                shpFeat.Dispose();
                featureIndex++;
            }
            ds.Dispose();
            return outputFeat;
        }
        private static List<Part> ProcessGeometry(Geometry geom, List<List<Vertex>> featureVertices, string WKT)
        {
            var parts = new List<Part>();

            if (geom.GetGeometryType() == wkbGeometryType.wkbPolygon || geom.GetGeometryType() == wkbGeometryType.wkbPolygon25D)
            {
                //polygon
                for (int ringIndex = 0; ringIndex < geom.GetGeometryCount(); ringIndex++)
                {
                    Geometry ring = geom.GetGeometryRef(ringIndex);
                    var vertices = new List<Vertex>();
                    Part polyPart = new Part(WKT);
                    polyPart.Direction = ring.IsClockwise();
                    for (int v = 0; v < ring.GetPointCount(); v++)
                    {
                        Vertex polyVert = new Vertex(ring.GetX(v), ring.GetY(v));
                        vertices.Add(polyVert);
                        polyPart.AddVertex(polyVert);
                    }

                    featureVertices.Add(vertices);
                    parts.Add(polyPart); // store index/size info                    
                }
            }
            else if (geom.GetGeometryType() == wkbGeometryType.wkbLineString)
            {
                //polyline
                var vertices = new List<Vertex>();
                Part linePart = new Part(WKT);
                for (int v = 0; v < geom.GetPointCount(); v++)
                {
                    Vertex lineVert = new Vertex(geom.GetX(v), geom.GetY(v));
                    vertices.Add(lineVert);
                    linePart.AddVertex(lineVert);
                }
                featureVertices.Add(vertices);
                parts.Add(linePart);
            }
            else if (geom.GetGeometryType() == wkbGeometryType.wkbPoint)
            {
                //point
                var vertices = new List<Vertex>();
                Part pointPart = new Part(WKT);                
                Vertex pointVert = new Vertex(geom.GetX(0), geom.GetY(0));
                vertices.Add(pointVert);
                pointPart.AddVertex(pointVert);                
                featureVertices.Add(vertices);
                parts.Add(pointPart);
            }

            return parts;
        }
        
        public static Dictionary<string, string> ReadCSVtoDict(string filepath, short keyColIndex, short colIndex, bool hasHeaders)
        {
            var dict = new Dictionary<string, string>();
            string[] templine;
            using (var stream = new FileStream(
                            filepath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.ReadWrite))
            using (var sRead = new StreamReader(stream))
            {
                if (hasHeaders == true)
                {
                    templine = sRead.ReadLine().Split(',').ToArray();
                }
                do
                {
                    templine = sRead.ReadLine().Split(',');
                    if (dict.ContainsKey(templine[keyColIndex]))
                    {
                    }
                    // Ignore??
                    else
                    {
                        dict.Add(templine[keyColIndex], templine[colIndex]);
                    }
                }
                while (!sRead.EndOfStream);
            }
            return dict;
        }

        public static void renameDataTAbleCols(ref DataTable tbl, Dictionary<string, string> colXwlk)
        {
            foreach (KeyValuePair<string, string> pair in colXwlk)
            {
                if (tbl.Columns.Contains(pair.Key))
                {
                    tbl.Columns[pair.Key].ColumnName = pair.Value;
                }
            }
        }

        public static void writeDatatableToCSV(DataTable dtbl, string filepath, bool append, bool excludeHeaders = false)
        {
            if (dtbl != null)
            {
                if (!Directory.Exists(Path.GetDirectoryName(filepath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                }

                StreamWriter sw;
                sw = FileSystem.OpenTextFileWriter(filepath, append);
                var lineToWrite = new List<string>();

                for (int i = 0; i < dtbl.Columns.Count; i++)
                {
                    lineToWrite.Add(dtbl.Columns[i].ColumnName);
                }

                if (!excludeHeaders)
                {
                    sw.WriteLine(string.Join(",", lineToWrite.ToArray()));
                }
                foreach (DataRow row in dtbl.Rows)
                {
                    for (int n = 0; n < dtbl.Columns.Count; n++)
                    {
                        if (!(row[n] is DBNull))
                        {
                            lineToWrite[n] = row[n].ToString().Replace(",", ";");
                        }
                        else
                        {
                            lineToWrite[n] = "";
                        }
                    }
                    sw.WriteLine(string.Join(",", lineToWrite.ToArray()));
                }
                sw.Close();
                sw.Dispose();
            }
        }

        public static void writeListToCSV(List<string> vals, string filepath, bool appendRows, bool appendCols)
        {
            if (vals != null)
            {
                if (!Directory.Exists(Path.GetDirectoryName(filepath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filepath));
                }
                var existingValsArray = new string[vals.Count + 1];
                if (appendCols)
                {
                    var sr = new StreamReader(filepath);
                    int i = 0;
                    do
                    {
                        existingValsArray[i] = sr.ReadLine();
                        i += 1;
                    }
                    while (!sr.EndOfStream);
                    sr.Dispose();
                }
                var sw = new StreamWriter(filepath, appendRows);
                for (int i = 0; i < vals.Count; i++)
                {
                    if (appendCols)
                    {
                        sw.WriteLine(existingValsArray[i] + "," + vals[i]);
                    }
                    else
                    {
                        sw.WriteLine(vals[i]);
                    }
                }
                sw.Dispose();
            }
        }

        public static void createFolders(string studyPath, string simName)
        {
            if (!Directory.Exists(studyPath + @"\" + simName + @"\Outputs"))
            {
                Directory.CreateDirectory(studyPath + @"\" + simName + @"\Outputs");
            }

            if (!Directory.Exists(studyPath + @"\" + simName + @"\Outputs\Scenarios"))
            {
                Directory.CreateDirectory(studyPath + @"\" + simName + @"\Outputs\Scenarios");
            }

            if (!Directory.Exists(studyPath + @"\" + simName + @"\Outputs\Point Summary"))
            {
                Directory.CreateDirectory(studyPath + @"\" + simName + @"\Outputs\Point Summary");
            }

            if (!Directory.Exists(studyPath + @"\" + simName + @"\Simulation"))
            {
                Directory.CreateDirectory(studyPath + @"\" + simName + @"\Simulation");
            }

            if (!Directory.Exists(studyPath + @"\" + simName + @"\Simulation\Network"))
            {
                Directory.CreateDirectory(studyPath + @"\" + simName + @"\Simulation\Network");
            }

            if (!Directory.Exists(studyPath + @"\" + simName + @"\Simulation\Network\Projects"))
            {
                Directory.CreateDirectory(studyPath + @"\" + simName + @"\Simulation\Network\Projects");
            }
        }
       
        public static string RemoveInvalidPathChars(string input)
        {
            return string.Concat(input.Split(Path.GetInvalidPathChars()));
        }

        public static string RemoveInvalidFileNameChars(string input)
        {
            return string.Concat(input.Split(Path.GetInvalidFileNameChars()));
        }
        public static string ReplaceWithCount(string input, string oldValue, string newValue, int startIndex, int count)
        {
            int endIndex = Math.Min(input.Length, startIndex + count);
            return ReplaceInRange(input, oldValue, newValue, startIndex, endIndex);
        }
        public static string ReplaceInRange(string input, string oldValue, string newValue, int startIndex, int endIndex)
        {
            // Clamp indices
            startIndex = Math.Max(0, startIndex);
            endIndex = Math.Min(input.Length, endIndex);

            // Split into 3 parts
            string before = input.Substring(0, startIndex);
            string middle = input.Substring(startIndex, endIndex - startIndex);
            string after = input.Substring(endIndex);

            // Replace only inside the middle segment
            middle = middle.Replace(oldValue, newValue);

            return before + middle + after;
        }
        public static string deleteTextFromString(string input, string[] whatToRemove)
        {
            string output = input;
            foreach(string thingToRemove in whatToRemove)
            {
                if (output.Contains(thingToRemove))
                {
                    output = output.Replace(thingToRemove, "");
                }
            }
            return output;
        } 
    }
}
