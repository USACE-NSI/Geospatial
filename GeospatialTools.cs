using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualBasic;
using OSGeo;
using OSGeo.OGR;
using OSGeo.OSR;

namespace AlexGeospatial
{
    public static class GeospatialTools
    {
        public static string _NAD83WKT = "GEOGCS[\"NAD83\",DATUM[\"North_American_Datum_1983\",SPHEROID[\"GRS 1980\",6378137,298.257222101,AUTHORITY[\"EPSG\",\"7019\"]],AUTHORITY[\"EPSG\",\"6269\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4269\"]]";
        public static string _ALBERS = "PROJCS[\"USA_Contiguous_Albers_Equal_Area_Conic\",GEOGCS[\"GCS_North_American_1983\",DATUM[\"North_American_Datum_1983\",SPHEROID[\"GRS_1980\",6378137,298.257222101]],PRIMEM[\"Greenwich\",0],UNIT[\"Degree\",0.017453292519943295]],PROJECTION[\"Albers_Conic_Equal_Area\"],PARAMETER[\"False_Easting\",0],PARAMETER[\"False_Northing\",0],PARAMETER[\"longitude_of_center\",-96],PARAMETER[\"Standard_Parallel_1\",29.5],PARAMETER[\"Standard_Parallel_2\",45.5],PARAMETER[\"latitude_of_center\",37.5],UNIT[\"Meter\",1],AUTHORITY[\"EPSG\",\"102003\"]]";
        public static string _WGS84WKT = "GEOGCS[\"WGS84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS84\",6378137,298.257223563,AUTHORITY[\"EPSG\",\"7030\"]],AUTHORITY[\"EPSG\",\"6326\"]],PRIMEM[\"Greenwich\",0,AUTHORITY[\"EPSG\",\"8901\"]],UNIT[\"degree\",0.01745329251994328,AUTHORITY[\"EPSG\",\"9122\"]],AUTHORITY[\"EPSG\",\"4326\"]]";
        public enum joinType
        {
            First = 0,
            Sum = 1,
            Average = 2,
            Count = 3
        }
        public enum eShapeType
        {
            shpPoint,
            shpPointM,
            shpLine,
            shpPoly
        }
        public enum eFieldType
        {
            shpBoolean,            
            shpFloat,
            shpDouble,
            shpText,
            shpInteger,
            shpLong,
            shpDate,
            shpSingle,
            shpNumeric
        }
        public static List<long> spatialJoinNearestToPolyFromPnts(ref Feat polyFeat, Feat pntFeat, string[] destinationFileFieldNames, string[] joinFileFieldNames, joinType joinEnum, bool checkforcontains, HashSet<long> exteriorIndices = null, List<long> interiorIndices = null)
        {
            var joinsDict = new Dictionary<long, List<List<object>>>();
            if (pntFeat._parts.Count > 0 & polyFeat._parts.Count > 0)
            {
                for (int i = 0, loopTo = destinationFileFieldNames.Count() - 1; i <= loopTo; i++)
                {
                    if (!polyFeat._attTable._columns.ContainsKey(destinationFileFieldNames[i]))
                    {
                        polyFeat._attTable.AddField(destinationFileFieldNames[i], pntFeat._attTable._columns[joinFileFieldNames[i]]._efldType, pntFeat._attTable._columns[joinFileFieldNames[i]]._length, pntFeat._attTable._columns[joinFileFieldNames[i]]._decimal);
                    }
                }

                joinsDict = spatialJoinNearestBase(polyFeat, pntFeat, true, destinationFileFieldNames, joinFileFieldNames, checkforcontains, exteriorIndices, interiorIndices);

                foreach (var kvp in joinsDict)
                {
                    for (int i = 0, loopTo1 = destinationFileFieldNames.Count() - 1; i <= loopTo1; i++)
                    {
                        string fld = destinationFileFieldNames[i];
                        switch (joinEnum)
                        {
                            case joinType.First:
                            {
                                polyFeat._attTable.writeColData(fld, kvp.Value[i][0], kvp.Key);
                                break;
                            }
                            case joinType.Sum:
                            {
                                double sumval = 0d;
                                foreach (var numval in kvp.Value[i])
                                {
                                    double dval = 0;
                                    if(double.TryParse(numval.ToString(), out dval)) { sumval += dval; }
                                }
                                polyFeat._attTable.writeColData(fld, sumval, kvp.Key);
                                break;
                            }
                            case joinType.Average:
                            {
                                double sumval = 0d;
                                foreach (var numval in kvp.Value[i])
                                {
                                    double dval = 0;
                                    if (double.TryParse(numval.ToString(), out dval)) { sumval += dval; }
                                }
                                polyFeat._attTable.writeColData(fld, sumval / kvp.Value.Count, kvp.Key);
                                break;
                            }
                            case joinType.Count:
                            {
                                polyFeat._attTable.writeColData(fld, kvp.Value[0].Count, kvp.Key);
                                break;
                            }
                        }
                    }
                }
            }
            return joinsDict.Keys.ToList();
        }
        public static void spatialJoinNearestPolyToPoints(ref Feat pntsFeat, Feat polyFeat, string[] destinationFileFieldNames, string[] joinFileFieldNames, bool checkForContains = true, HashSet<long> exteriorIndices = null, List<long> interiorIndices = null)
        {
            if (pntsFeat._parts.Count > 0 & polyFeat._parts.Count > 0)
            {
                var pointCoordDict = new Dictionary<int, double[]>();
                var sortedPointDict = new Dictionary<int, double[]>();
                var polyExtentDict = new Dictionary<int, double[]>();
                var sortedPolyDict = new Dictionary<int, double[]>();

                var xyCoords = pntsFeat.getXYcoords;

                for (int i = 0, loopTo = destinationFileFieldNames.Count() - 1; i <= loopTo; i++)
                {
                    if (!pntsFeat._attTable._columns.ContainsKey(destinationFileFieldNames[i]))
                    {
                        pntsFeat._attTable.AddField(destinationFileFieldNames[i], polyFeat._attTable._columns[joinFileFieldNames[i]]._efldType, polyFeat._attTable._columns[joinFileFieldNames[i]]._length, polyFeat._attTable._columns[joinFileFieldNames[i]]._decimal);
                    }
                }

                // reproject target feature points to match polyfeature
                var reprojedPnts = GeospatialTools.reprojectPntsDS(xyCoords, pntsFeat._WKT, polyFeat._WKT);

                // Create dictionary of point feature coordinates, format of {X, Y}
                if (!(interiorIndices == null))
                {
                    for (int i = 0, loopTo1 = interiorIndices.Count - 1; i <= loopTo1; i++)
                        pointCoordDict.Add((int)interiorIndices[i], reprojedPnts[i]);
                }
                else
                {
                    for (int i = 0, loopTo2 = reprojedPnts.Count - 1; i <= loopTo2; i++)
                        pointCoordDict.Add(i, reprojedPnts[i]);
                }

                // sort pointCoordDict by Y values descending
                sortedPointDict = pointCoordDict.OrderByDescending(x => x.Value[1]).ToDictionary(k => k.Key, v => v.Value);

                double polyMinX = double.MaxValue;
                double polyMaxX = 0d;
                double polyMinY = double.MaxValue;
                double polyMaxY = 0d;
                for (int i = 0, loopTo3 = polyFeat._parts.Count - 1; i <= loopTo3; i++)
                {
                    if (!(exteriorIndices == null))
                    {
                        if (exteriorIndices.Contains(i))
                        {
                            polyExtentDict.Add(i, new[] { polyFeat._parts[i][0].MBRXMin, polyFeat._parts[i][0].MBRXMax, polyFeat._parts[i][0].MBRYMin, polyFeat._parts[i][0].MBRYMax });
                        }
                    }
                    else
                    {
                        polyExtentDict.Add(i, new[] { polyFeat._parts[i][0].MBRXMin, polyFeat._parts[i][0].MBRXMax, polyFeat._parts[i][0].MBRYMin, polyFeat._parts[i][0].MBRYMax });
                    }

                    if (polyExtentDict.ContainsKey(i))
                    {
                        if (polyExtentDict[i][0] < polyMinX)
                        {
                            polyMinX = polyExtentDict[i][0];
                        }
                        if (polyExtentDict[i][1] > polyMaxX)
                        {
                            polyMaxX = polyExtentDict[i][1];
                        }
                        if (polyExtentDict[i][2] < polyMinY)
                        {
                            polyMinY = polyExtentDict[i][2];
                        }
                        if (polyExtentDict[i][3] > polyMaxY)
                        {
                            polyMaxY = polyExtentDict[i][3];
                        }
                    }
                }

                // sort polyExtentDict by minY
                var sortlist2 = from pair in polyExtentDict
                                orderby pair.Value[2] descending
                                select pair;
                // Convert to dictionary
                sortedPolyDict = sortlist2.ToDictionary(p => p.Key, p => p.Value);
                var subsetPoints = new Dictionary<int, double[]>();
                var subsetPolys = new Dictionary<int, double[]>();
                long curStruc = 0L;
                int reTileEveryXstrucs = (int)Math.Round(Math.Sqrt(sortedPolyDict.Count / ((polyMaxY - polyMinY) / (polyMaxX - polyMinX))) * 1.5d);
                subsetPolys = sortedPolyDict;
                foreach (KeyValuePair<int, double[]> Point in sortedPointDict)
                {
                    double closestDist = double.MaxValue;
                    long bestFitInd = -1;
                    curStruc += 1L;
                    // If (curStruc Mod reTileEveryXstrucs) = 0 Then
                    // subsetPolys = TryCast((From polys In sortedPolyDict Where PolySouthOfLine(polys.Value, Point.Value(1)) = True
                    // Select polys).ToDictionary(Function(x) x.Key, Function(y) y.Value), Dictionary(Of Int32, Double()))
                    // End If
                    foreach (KeyValuePair<int, double[]> poly in subsetPolys)
                    {
                        var MBRdists = new List<double>();
                        MBRdists.Add(CalcLineDist(new[] { poly.Value[0], poly.Value[2] }, Point.Value)); // dist between point and poly Xmin, Ymin
                        MBRdists.Add(CalcLineDist(new[] { poly.Value[0], poly.Value[3] }, Point.Value)); // dist between point and poly Xmin, Ymax
                        MBRdists.Add(CalcLineDist(new[] { poly.Value[1], poly.Value[2] }, Point.Value)); // dist between point and poly Xmax, Ymin
                        MBRdists.Add(CalcLineDist(new[] { poly.Value[1], poly.Value[3] }, Point.Value)); // dist between point and poly Xmax, Ymax
                        double minMBRdist = MBRdists.Min();

                        // update closest polygon point
                        if (minMBRdist < closestDist)
                        {
                            closestDist = GeospatialTools.getClosestVertex(polyFeat._parts[poly.Key], polyFeat._vertices[poly.Key], Point.Value);
                            bestFitInd = poly.Key;
                        }

                        // check if polygon contains point
                        if (checkForContains)
                        {
                            if (GeospatialTools.PointWithinSinglePoly(polyFeat._parts[poly.Key], polyFeat._vertices[poly.Key], Point.Value) == true)
                            {
                                bestFitInd = poly.Key;
                                break;
                            }
                        }

                        if (Point.Value[1] - poly.Value[2] > closestDist)
                            break; // if vertical difference between point and upper most side of poly mbr exceeds the current closest distance then exit loop as no further checks can result in closer polygon
                    }

                    if (!(bestFitInd == -1))
                    {
                        for (int i = 0, loopTo4 = destinationFileFieldNames.Count() - 1; i <= loopTo4; i++)
                            pntsFeat._attTable._columns[destinationFileFieldNames[i]]._rows[Point.Key] = Conversion.CTypeDynamic(polyFeat._attTable._columns[joinFileFieldNames[i]]._rows[(int)bestFitInd], pntsFeat._attTable._columns[destinationFileFieldNames[i]].getEFldType);
                    }
                }
            }
        }



        public static List<long> spatialJoinToPntsFromPoly(ref Feat pntFeat, Feat polyFeat, string[] destinationFileFieldNames, string[] joinFileFieldNames)
        {
            var joinsDict = new Dictionary<long, List<List<object>>>();
            if (pntFeat._parts.Count > 0 & polyFeat._parts.Count > 0)
            {
                for (int i = 0, loopTo = destinationFileFieldNames.Count() - 1; i <= loopTo; i++)
                {
                    if (!pntFeat._attTable._columns.ContainsKey(destinationFileFieldNames[i]))
                    {
                        pntFeat._attTable.AddField(destinationFileFieldNames[i], polyFeat._attTable._columns[joinFileFieldNames[i]]._efldType, polyFeat._attTable._columns[joinFileFieldNames[i]]._length, polyFeat._attTable._columns[joinFileFieldNames[i]]._decimal);
                    }
                }

                joinsDict = SpatialJoinBase(polyFeat, pntFeat, false, destinationFileFieldNames, joinFileFieldNames);

                foreach (var kvp in joinsDict)
                {
                    for (int i = 0, loopTo1 = destinationFileFieldNames.Count() - 1; i <= loopTo1; i++)
                    {
                        string fld = destinationFileFieldNames[i];
                        pntFeat._attTable.writeColData(fld, kvp.Value[i][0], kvp.Key);
                    }
                }
            }
            return joinsDict.Keys.ToList();
        }

        public static List<long> spatialJoinToPolyFromPnts(ref Feat polyFeat, Feat pntFeat, string[] destinationFileFieldNames, string[] joinFileFieldNames, joinType[] joinEnum)
        {
            var joinsDict = new Dictionary<long, List<List<object>>>();
            if (pntFeat._parts.Count > 0 & polyFeat._parts.Count > 0)
            {
                for (int i = 0, loopTo = destinationFileFieldNames.Count() - 1; i <= loopTo; i++)
                {
                    if (!polyFeat._attTable._columns.ContainsKey(destinationFileFieldNames[i]))
                    {
                        polyFeat._attTable.AddField(destinationFileFieldNames[i], pntFeat._attTable._columns[joinFileFieldNames[i]]._efldType, pntFeat._attTable._columns[joinFileFieldNames[i]]._length, pntFeat._attTable._columns[joinFileFieldNames[i]]._decimal);
                    }
                }

                joinsDict = SpatialJoinBase(polyFeat, pntFeat, true, destinationFileFieldNames, joinFileFieldNames);

                foreach (var kvp in joinsDict)
                {

                    for (int i = 0, loopTo1 = destinationFileFieldNames.Count() - 1; i <= loopTo1; i++)
                    {
                        string fld = destinationFileFieldNames[i];
                        switch (joinEnum[i])
                        {
                            case joinType.First:
                            {
                                polyFeat._attTable.writeColData(fld, kvp.Value[i][0], kvp.Key);
                                break;
                            }
                            case joinType.Sum:
                            {
                                double sumval = 0d;
                                foreach (var numval in kvp.Value[i])
                                {
                                    double dval = 0;
                                    if (double.TryParse(numval.ToString(), out dval)) { sumval += dval; }
                                }
                                polyFeat._attTable.writeColData(fld, sumval, kvp.Key);
                                break;
                            }
                            case joinType.Average:
                            {
                                double sumval = 0d;
                                foreach (var numval in kvp.Value[i])
                                {
                                    double dval = 0;
                                    if (double.TryParse(numval.ToString(), out dval)) { sumval += dval; }
                                }
                                polyFeat._attTable.writeColData(fld, sumval / kvp.Value.Count, kvp.Key);
                                break;
                            }
                            case joinType.Count:
                            {
                                polyFeat._attTable.writeColData(fld, kvp.Value[0].Count, kvp.Key);
                                break;
                            }
                        }
                    }
                }
            }
            return joinsDict.Keys.ToList();
        }

        public static Dictionary<long, List<List<object>>> SpatialJoinBase(Feat containingFeat, Feat interiorFear, bool joinToContainer, string[] destinationFileFieldNames, string[] joinFileFieldNames)
        {
            if (containingFeat._parts.Count > 0 & interiorFear._parts.Count > 0)
            {
                var pointCoordDict = new Dictionary<int, double[]>();
                var sortedPointDict = new Dictionary<int, double[]>();
                var polyExtentDict = new Dictionary<int, double[]>();
                var sortedPolyDict = new Dictionary<int, double[]>();

                var xyCoords = interiorFear.getXYcoords;

                // reproject join feature points to match polyfeature
                var reprojedPnts = GeospatialTools.reprojectPntsDS(xyCoords, interiorFear._WKT, containingFeat._WKT);

                for (int i = 0, loopTo = reprojedPnts.Count - 1; i <= loopTo; i++)
                    pointCoordDict.Add(i, reprojedPnts[i]);

                // sort pointCoordDict Y values
                sortedPointDict = pointCoordDict.OrderByDescending(x => x.Value[1]).ToDictionary(k => k.Key, v => v.Value);

                double polyMinX = double.MaxValue;
                double polyMaxX = 0d;
                double polyMinY = double.MaxValue;
                double polyMaxY = 0d;
                for (int i = 0, loopTo1 = containingFeat._parts.Count - 1; i <= loopTo1; i++)
                {
                    polyExtentDict.Add(i, new[] { containingFeat._parts[i].Min(x => x.MBRXMin), containingFeat._parts[i].Max(x => x.MBRXMax), containingFeat._parts[i].Min(y => y.MBRYMin), containingFeat._parts[i].Max(y => y.MBRYMax) });

                    if (polyExtentDict.ContainsKey(i))
                    {
                        if (polyExtentDict[i][0] < polyMinX)
                        {
                            polyMinX = polyExtentDict[i][0];
                        }
                        if (polyExtentDict[i][1] > polyMaxX)
                        {
                            polyMaxX = polyExtentDict[i][1];
                        }
                        if (polyExtentDict[i][2] < polyMinY)
                        {
                            polyMinY = polyExtentDict[i][2];
                        }
                        if (polyExtentDict[i][3] > polyMaxY)
                        {
                            polyMaxY = polyExtentDict[i][3];
                        }
                    }

                }

                // sort polyExtentDict by minY
                var sortlist2 = from pair in polyExtentDict
                                orderby pair.Value[2] descending
                                select pair;
                // Convert to dictionary
                sortedPolyDict = sortlist2.ToDictionary(p => p.Key, p => p.Value);
                var subsetPoints = new Dictionary<int, double[]>();
                var subsetPolys = new Dictionary<int, double[]>();
                long curStruc = 0L;
                int reTileEveryXstrucs = (int)Math.Round(Math.Sqrt(sortedPolyDict.Count / ((polyMaxY - polyMinY) / (polyMaxX - polyMinX))) * 1.5d);
                subsetPolys = sortedPolyDict;

                var joinsDict = new Dictionary<long, List<List<object>>>();
                long joinKey = 0L;

                foreach (KeyValuePair<int, double[]> Point in sortedPointDict)
                {
                    curStruc += 1L;
                    if (curStruc % reTileEveryXstrucs == 0L)
                    {
                        var listOnorthernmostPoints = new List<double[]>();
                        if (interiorFear._shapeType == eShapeType.shpPoint | interiorFear._shapeType == eShapeType.shpPointM)
                        {
                            foreach (var pnt in interiorFear._vertices[Point.Key][0])
                                listOnorthernmostPoints.Add(new[] { 0d, pnt.Y_Cord });
                        }
                        else
                        {
                            var interiorFeatPart = interiorFear._parts[Point.Key];
                            foreach (var part in interiorFeatPart)
                                listOnorthernmostPoints.Add(new[] { 0d, part.MBRYMax });
                        }

                        var reprojNorthPnts = GeospatialTools.reprojectPntsDS(listOnorthernmostPoints, interiorFear._WKT, containingFeat._WKT);
                        subsetPolys = sortedPolyDict.Where(x => PolySouthOfLine(x.Value, reprojNorthPnts)).ToDictionary(k => k.Key, v => v.Value);
                        // subsetPolys = TryCast((From polys In sortedPolyDict Where PolySouthOfLine(polys.Value, reprojNorthPnts) = True
                        // Select polys).ToDictionary(Function(x) x.Key, Function(y) y.Value), Dictionary(Of Int32, Double()))

                    }
                    foreach (KeyValuePair<int, double[]> poly in subsetPolys)
                    {
                        if (poly.Value[2] > Point.Value[1])
                            continue;
                        if (poly.Value[3] < Point.Value[1])
                            continue;
                        if (poly.Value[0] > Point.Value[0])
                            continue;
                        if (poly.Value[1] >= Point.Value[0])
                        {
                            if (GeospatialTools.PointWithinSinglePoly(containingFeat._parts[poly.Key], containingFeat._vertices[poly.Key], Point.Value) == true)
                            {
                                if (joinToContainer)
                                {
                                    joinKey = poly.Key;
                                }
                                else
                                {
                                    joinKey = Point.Key;
                                }
                                if (!joinsDict.ContainsKey(joinKey))
                                    joinsDict.Add(joinKey, new List<List<object>>());
                                if (joinsDict[joinKey].Count == 0)
                                {
                                    for (int n = 0, loopTo2 = destinationFileFieldNames.Count() - 1; n <= loopTo2; n++)
                                        joinsDict[joinKey].Add(new List<object>());
                                }
                                // Dim match As New List(Of Object)
                                for (int i = 0, loopTo3 = destinationFileFieldNames.Count() - 1; i <= loopTo3; i++)
                                {
                                    object joinFldVal;
                                    Type joinFldType;
                                    if (joinToContainer)
                                    {
                                        joinFldVal = interiorFear._attTable._columns[joinFileFieldNames[i]]._rows[Point.Key];
                                        joinFldType = containingFeat._attTable._columns[destinationFileFieldNames[i]].getEFldType;
                                    }
                                    else
                                    {
                                        joinFldVal = containingFeat._attTable._columns[joinFileFieldNames[i]]._rows[poly.Key];
                                        joinFldType = interiorFear._attTable._columns[destinationFileFieldNames[i]].getEFldType;
                                    }

                                    joinsDict[joinKey][i].Add(Conversion.CTypeDynamic(joinFldVal, joinFldType));

                                }
                                break;
                            }
                        }

                    }

                }

                return joinsDict;
            }
            else
            {
                return new Dictionary<long, List<List<object>>>();
            }
        }

        public static Dictionary<long, List<List<object>>> spatialJoinNearestBase(Feat containingFeat, Feat interiorFear, bool joinToContainer, string[] destinationFileFieldNames, string[] joinFileFieldNames, bool checkforcontains, HashSet<long> exteriorIndices = null, List<long> interiorIndices = null)
        {
            if (containingFeat._parts.Count > 0 & interiorFear._parts.Count > 0)
            {
                var pointCoordDict = new Dictionary<int, double[]>();
                var sortedPointDict = new Dictionary<int, double[]>();
                var polyExtentDict = new Dictionary<int, double[]>();
                var sortedPolyDict = new Dictionary<int, double[]>();

                var xyCoords = interiorFear.getXYcoords;

                // reproject join feature points to match polyfeature
                var reprojedPnts = GeospatialTools.reprojectPntsDS(xyCoords, interiorFear._WKT, containingFeat._WKT);

                // Create dictionary of interior feature coordinates, format of {X, Y}
                if (!(interiorIndices == null))
                {
                    for (int i = 0, loopTo = interiorIndices.Count - 1; i <= loopTo; i++)
                        pointCoordDict.Add((int)interiorIndices[i], reprojedPnts[i]);
                }
                else
                {
                    for (int i = 0, loopTo1 = reprojedPnts.Count - 1; i <= loopTo1; i++)
                        pointCoordDict.Add(i, reprojedPnts[i]);
                }

                // sort pointCoordDict by Y values descending
                sortedPointDict = pointCoordDict.OrderByDescending(x => x.Value[1]).ToDictionary(k => k.Key, v => v.Value);
                var PolyCentroids = new Dictionary<int, double[]>();
                var polyVertMaxDict = new Dictionary<int, double>();

                for (int i = 0, loopTo2 = containingFeat._parts.Count - 1; i <= loopTo2; i++)
                {
                    bool useInd = false;
                    if (!(exteriorIndices == null))
                    {
                        if (exteriorIndices.Contains(i))
                        {
                            useInd = true;
                        }
                    }
                    else
                    {
                        useInd = true;
                    }
                    if (useInd)
                    {
                        if (!PolyCentroids.ContainsKey(i))
                            PolyCentroids.Add(i, new[] { 0d, 0d });
                        if (!(containingFeat._parts[i][0].CentroidX == null) & !(containingFeat._parts[i][0].CentroidY == null))
                        {
                            PolyCentroids[i] = new[] { (double)containingFeat._parts[i][0].CentroidX, (double)containingFeat._parts[i][0].CentroidY };
                        }
                        else
                        {
                            PolyCentroids[i] = GeospatialTools.getCentroid(containingFeat._vertices[i][0]);
                        }

                        if (!polyVertMaxDict.ContainsKey(i))
                            polyVertMaxDict.Add(i, 0d);
                        if (!(containingFeat._parts[i][0].MBRYMin == null))
                        {
                            if (containingFeat._parts[i][0].MBRYMin > 0d)
                            {
                                polyVertMaxDict[i] = containingFeat._parts[i][0].MBRYMin;
                            }
                            else
                            {
                                polyVertMaxDict[i] = GeospatialTools.getMBR(containingFeat._vertices[i][0])[2];
                            }
                        }
                        else
                        {
                            polyVertMaxDict[i] = GeospatialTools.getMBR(containingFeat._vertices[i][0])[2];
                        }
                    }
                }

                // sort polyExtentDict by minY
                var sortlist2 = from pair in PolyCentroids
                                orderby pair.Value[1] descending
                                select pair;
                // Convert to dictionary
                sortedPolyDict = sortlist2.ToDictionary(p => p.Key, p => p.Value);
                long curStruc = 0L;
                // Dim reTileEveryXstrucs As Int32 = Math.Sqrt(sortedPolyDict.Count / ((polyMaxY - polyMinY) / (polyMaxX - polyMinX))) * 1.5

                var joinsDict = new Dictionary<long, List<List<object>>>();
                long joinKey = 0L;

                foreach (KeyValuePair<int, double[]> Point in sortedPointDict)
                {
                    double closestDist = double.MaxValue;
                    long bestFitInd = -1;
                    curStruc += 1L;

                    foreach (KeyValuePair<int, double[]> poly in sortlist2)
                    {

                        double testDist = 0d;
                        testDist = CalcLineDist(new[] { poly.Value[0], poly.Value[1] }, Point.Value);


                        // update closest polygon point
                        if (testDist < closestDist)
                        {
                            closestDist = testDist;
                            bestFitInd = poly.Key;
                        }

                        // check if polygon contains point
                        if (checkforcontains)
                        {
                            if (GeospatialTools.PointWithinSinglePoly(containingFeat._parts[poly.Key], containingFeat._vertices[poly.Key], Point.Value) == true)
                            {
                                bestFitInd = poly.Key;
                                break;
                            }
                        }

                        double disttoVertMax = Math.Abs(Point.Value[1] - polyVertMaxDict[poly.Key]);
                        if (disttoVertMax > closestDist)
                        {
                            // Exit For 'if vertical difference between point and upper most side of poly mbr exceeds the current closest distance then exit loop as no further checks can result in closer polygon
                        }
                    }

                    if (!(bestFitInd == -1))
                    {
                        if (joinToContainer)
                        {
                            joinKey = bestFitInd;
                        }
                        else
                        {
                            joinKey = Point.Key;
                        }
                        if (!joinsDict.ContainsKey(joinKey))
                            joinsDict.Add(joinKey, new List<List<object>>());
                        if (joinsDict[joinKey].Count == 0)
                        {
                            for (int n = 0, loopTo3 = destinationFileFieldNames.Count() - 1; n <= loopTo3; n++)
                                joinsDict[joinKey].Add(new List<object>());
                        }
                        for (int i = 0, loopTo4 = destinationFileFieldNames.Count() - 1; i <= loopTo4; i++)
                        {

                            object joinFldVal;
                            Type joinFldType;
                            if (joinToContainer)
                            {
                                joinFldVal = interiorFear._attTable._columns[joinFileFieldNames[i]]._rows[Point.Key];
                                joinFldType = containingFeat._attTable._columns[destinationFileFieldNames[i]].getEFldType;
                            }
                            else
                            {
                                joinFldVal = containingFeat._attTable._columns[joinFileFieldNames[i]]._rows[(int)bestFitInd];
                                joinFldType = interiorFear._attTable._columns[destinationFileFieldNames[i]].getEFldType;
                            }

                            joinsDict[joinKey][i].Add(Conversion.CTypeDynamic(joinFldVal, joinFldType));
                        }
                    }
                }
                return joinsDict;
            }
            else
            {
                return new Dictionary<long, List<List<object>>>();
            }
        }


        public static double[] getCentroid(List<double[]> vertices)
        {
            double[] centroid;
            double sumA = 0d;
            double sumCX = 0d;
            double sumCY = 0d;
            for (int i = 0, loopTo = vertices.Count - 2; i <= loopTo; i++)
            {
                double shoelace = vertices[i][0] * vertices[i + 1][1] - vertices[i + 1][0] * vertices[i][1];
                sumA += shoelace;
                sumCX += (vertices[i][0] + vertices[i + 1][0]) * shoelace;
                sumCY += (vertices[i][1] + vertices[i + 1][1]) * shoelace;
            }
            double shoelaceLast = vertices.Last()[0] * vertices[0][1] - vertices[0][0] * vertices.Last()[1];
            sumA += shoelaceLast;
            sumCX += (vertices.Last()[0] + vertices[0][0]) * shoelaceLast;
            sumCY += (vertices.Last()[1] + vertices[0][1]) * shoelaceLast;
            double A = sumA * 0.5d;
            double factor = 1d / (6d * A);
            double cX = factor * sumCX;
            double cY = factor * sumCY;
            centroid = new[] { cX, cY };
            return centroid;
        }
        public static double[] getCentroid(List<Vertex> vertices)
        {
            if (vertices.Count == 1)
                return new double[] { vertices[0].X_Cord, vertices[0].Y_Cord };
            double[] centroid;
            double sumA = 0d;
            double sumCX = 0d;
            double sumCY = 0d;
            for (int i = 0, loopTo = vertices.Count - 2; i <= loopTo; i++)
            {
                double shoelace = vertices[i].X_Cord * vertices[i + 1].Y_Cord - vertices[i + 1].X_Cord * vertices[i].Y_Cord;
                sumA += shoelace;
                sumCX += (vertices[i].X_Cord + vertices[i + 1].X_Cord) * shoelace;
                sumCY += (vertices[i].Y_Cord + vertices[i + 1].Y_Cord) * shoelace;
            }
            double shoelaceLast = vertices.Last().X_Cord * vertices[0].Y_Cord - vertices[0].X_Cord * vertices.Last().Y_Cord;
            sumA += shoelaceLast;
            sumCX += (vertices.Last().X_Cord + vertices[0].X_Cord) * shoelaceLast;
            sumCY += (vertices.Last().Y_Cord + vertices[0].Y_Cord) * shoelaceLast;
            double A = sumA * 0.5d;
            double factor = 1d / (6d * A);
            double cX = factor * sumCX;
            double cY = factor * sumCY;
            centroid = new[] { cX, cY };
            return centroid;
        }


        public static bool PolySouthOfLine(double[] poly, List<double[]> interiorParts)
        {
            bool southof = false;
            double northernmostlat = interiorParts.Max(x => x[1]);
            if (poly[2] <= northernmostlat)
            {
                southof = true;
            }
            return southof;
        }
        public static bool PointWithinSinglePoly(List<Part> parts, List<List<Vertex>> polygon, double[] Point)
        {
            bool within = false;
            for (int i = 0, loopTo = parts.Count - 1; i <= loopTo; i++)
            {
                var coords = polygon[i];
                if (!parts[i].IsHole)
                {
                    if (within == false)
                        within = pointWithinRing(coords, Point, true);
                }
                else if (within == true)
                    within = !pointWithinRing(coords, Point, true);
            }
            return within;
        }
        public static bool pointWithinRing(List<Vertex> coords, double[] point, bool containsOnly)
        {
            bool within = false;
            int countCrosses = 0;
            int zPlus1 = 0;
            for (int z = 0, loopTo = coords.Count - 2; z <= loopTo; z++)
            {
                if (containsOnly == false)
                {
                    if (pointOnLine(new[] { coords[z].X_Cord, coords[z].Y_Cord }, new[] { coords[zPlus1].X_Cord, coords[zPlus1].Y_Cord }, point) == true)
                    {
                        return true;
                    }
                }
                zPlus1 = (z + 1) % (coords.Count - 1);
                if (coords[z].Y_Cord >= point[1] & coords[zPlus1].Y_Cord >= point[1])
                    continue;
                if (coords[z].Y_Cord < point[1] & coords[zPlus1].Y_Cord < point[1])
                    continue;
                if (coords[z].X_Cord < point[0] & coords[zPlus1].X_Cord < point[0])
                    continue;
                if ((coords[z].X_Cord <= point[0] | coords[zPlus1].X_Cord <= point[0]) & pointLeftofLine(new[] { coords[z].X_Cord, coords[z].Y_Cord }, new[] { coords[zPlus1].X_Cord, coords[zPlus1].Y_Cord }, point) == false)
                    continue;
                countCrosses += 1;
            }
            if (countCrosses % 2 != 0)
            {
                within = true;
            }
            return within;
        }
        public static bool pointOnLine(double[] aCoord, double[] bCoord, double[] point)
        {
            bool onLine = false;
            double slope = 0d;
            double yIntercept = 0d;
            if (aCoord[0] == bCoord[0])
            {
                slope = 1d;
            }
            else
            {
                slope = (aCoord[1] - bCoord[1]) / (aCoord[0] - bCoord[0]);
            }
            yIntercept = aCoord[1] - slope * aCoord[0];
            if (point[1] == slope * point[0] + yIntercept)
            {
                if (point[0] > Math.Min(aCoord[0], bCoord[0]) & point[0] < Math.Max(aCoord[0], bCoord[0]))
                {
                    if (point[1] > Math.Min(aCoord[1], bCoord[1]) & point[1] < Math.Max(aCoord[1], bCoord[1]))
                    {
                        onLine = true;
                    }
                }
            }
            return onLine;
        }
        public static bool pointLeftofLine(double[] aCoord, double[] bCoord, double[] point)
        {
            bool leftOfLine = false;
            double d = (point[0] - aCoord[0]) * (bCoord[1] - aCoord[1]) - (point[1] - aCoord[1]) * (bCoord[0] - aCoord[0]);
            double leftSign = (aCoord[0] - 1d - aCoord[0]) * (bCoord[1] - aCoord[1]) - (aCoord[1] - aCoord[1]) * (bCoord[0] - aCoord[0]);
            if (!(d * leftSign < 0d))
            {
                leftOfLine = true;
            }
            return leftOfLine;
        }
        public static double[] getMBR(List<Vertex> vertices) // [xmin, xmax, ymin, ymax]
        {
            double[] mbr = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue };
            for (int i = 0, loopTo = vertices.Count - 1; i <= loopTo; i++)
            {
                if (vertices[i].X_Cord < mbr[0])
                    mbr[0] = vertices[i].X_Cord;
                if (vertices[i].X_Cord > mbr[1])
                    mbr[1] = vertices[i].X_Cord;
                if (vertices[i].Y_Cord < mbr[2])
                    mbr[2] = vertices[i].Y_Cord;
                if (vertices[i].Y_Cord > mbr[3])
                    mbr[3] = vertices[i].Y_Cord;
            }
            return mbr;
        }
        public static double[] getPartMBRVal(List<Part> parts, string val)
        {
            double valMin = double.MaxValue;
            double valMax = double.MinValue;
            foreach (Part part in parts)
            {
                if (val == "x")
                {
                    if (part.MBRXMin < valMin)
                        valMin = part.MBRXMin;
                    if (part.MBRXMax > valMax)
                        valMax = part.MBRXMax;
                }
                else if (val == "y")
                {
                    if (part.MBRYMin < valMin)
                        valMin = part.MBRYMin;
                    if (part.MBRYMax > valMax)
                        valMax = part.MBRYMax;
                }
                else
                {

                }
                return new[] { valMax, valMin };
            }

            return default;
        }
        public static double getClosestVertex(List<Part> parts, List<List<Vertex>> polygon, double[] Point)
        {
            double mindist = double.MaxValue;
            // Dim nearestVert As Double()
            for (int i = 0, loopTo = parts.Count - 1; i <= loopTo; i++)
            {
                if (!parts[i].IsHole)
                {
                    var coords = polygon[i];
                    foreach (var vertex in coords)
                    {
                        double dist = CalcLineDist(new[] { vertex.X_Cord, vertex.Y_Cord }, Point);
                        if (dist < mindist)
                        {
                            mindist = dist;
                            // nearestVert = {vertex.X_Cord, vertex.Y_Cord}
                        }

                    }
                }
            }
            return mindist;
        }
        public static double[] getClosestPointAlongLine(Feat shape, double[] Point) // [part index, sub-part index, closest Vert index, closest point X, closest point Y]
        {
            double mindist = double.MaxValue;
            double[] nearestVert = new[] { 0d, 0d };
            double[] indices = new[] { 0d, 0d, 0d, 0d }; // [part index, sub-part index, closest Vert index]

            for (int z = 0, loopTo = shape._parts.Count - 1; z <= loopTo; z++)
            {
                for (int i = 0, loopTo1 = shape._parts[z].Count - 1; i <= loopTo1; i++)
                {
                    if (!shape._parts[z][i].IsHole)
                    {
                        var coords = shape._vertices[z][i];
                        for (int v = 0, loopTo2 = coords.Count - 2; v <= loopTo2; v++)
                        {
                            double[] pnt1 = new[] { coords[v].X_Cord, coords[v].Y_Cord };
                            double[] pnt2 = new[] { coords[v + 1].X_Cord, coords[v + 1].Y_Cord };
                            double thisDist = CalcLineDist(pnt1, Point);
                            double nextDist = CalcLineDist(pnt2, Point);
                            if (thisDist <= nextDist)
                            {
                                if (getAngleIsAcute(Point, pnt2, pnt1) | getAngleIsRight(Point, pnt2, pnt1))
                                {
                                    double m1 = getSlope(pnt1, pnt2);
                                    double m2 = -1 / m1;
                                    double b1 = pnt1[1] - m1 * pnt1[0];
                                    double b2 = Point[1] - m2 * Point[0];
                                    double[] newpoint = getlineIntersection(m1, b1, m2, b2);
                                    double dist = CalcLineDist(Point, newpoint);
                                    if (dist < mindist)
                                    {
                                        mindist = dist;
                                        nearestVert = newpoint;
                                        indices = new[] { z, i, (double)v };
                                    }
                                }
                                else
                                {
                                    double dist = CalcLineDist(Point, pnt1);
                                    if (dist < mindist)
                                    {
                                        mindist = dist;
                                        nearestVert = pnt1;
                                        indices = new[] { z, i, (double)v };
                                    }
                                }
                            }
                            else if (getAngleIsAcute(Point, pnt1, pnt2) | getAngleIsRight(Point, pnt1, pnt2))
                            {
                                double m1 = getSlope(pnt1, pnt2);
                                double m2 = -1 / m1;
                                double b1 = pnt1[1] - m1 * pnt1[0];
                                double b2 = Point[1] - m2 * Point[0];
                                double[] newpoint = getlineIntersection(m1, b1, m2, b2);
                                double dist = CalcLineDist(Point, newpoint);
                                if (dist < mindist)
                                {
                                    mindist = dist;
                                    nearestVert = newpoint;
                                    indices = new[] { z, i, (double)v };
                                }
                            }
                            else
                            {
                                double dist = CalcLineDist(Point, pnt2);
                                if (dist < mindist)
                                {
                                    mindist = dist;
                                    nearestVert = pnt2;
                                    indices = new[] { z, i, (double)v };
                                }
                            }
                            // Dim pnt1 As Double() = {coords(v).X_Cord, coords(v).Y_Cord}
                            // Dim pnt2 As Double() = {coords(v + 1).X_Cord, coords(v + 1).Y_Cord}
                            // Dim thisDist As Double = CalcLineDist(pnt1, Point)
                            // Dim nextDist As Double = CalcLineDist(pnt2, Point)
                            // If thisDist <= nextDist Then
                            // If getAngleIsAcute(Point, pnt2, pnt1) Or getAngleIsRight(Point, pnt2, pnt1) Then
                            // If thisDist < nearestDists(0) And nextDist < nearestDists(1) Then
                            // nearestDists(0) = thisDist
                            // nearestVerts(0) = pnt1
                            // nearestDists(1) = nextDist
                            // nearestVerts(1) = pnt2
                            // End If
                            // End If
                            // Else
                            // If getAngleIsAcute(Point, pnt1, pnt2) Or getAngleIsRight(Point, pnt1, pnt2) Then
                            // If nextDist < nearestDists(0) And thisDist < nearestDists(1) Then
                            // nearestDists(0) = nextDist
                            // nearestVerts(0) = pnt2
                            // nearestDists(1) = thisDist
                            // nearestVerts(1) = pnt1
                            // End If
                            // End If
                            // End If
                        }
                    }
                }
            }

            double[] returnArray = new[] { indices[0], indices[1], indices[2], nearestVert[0], nearestVert[1] };
            return returnArray;
        }
        public static double getLinePartLengthFt(Feat shape, long ind, string wkt)
        {
            double ftlength = 0d;
            for (int i = 1, loopTo = shape._vertices[(int)ind][0].Count - 1; i <= loopTo; i++)
            {
                double ftdist = GeospatialTools.getFtDistBetweenPts(new[] { shape._vertices[(int)ind][0][i - 1].X_Cord, shape._vertices[(int)ind][0][i - 1].Y_Cord }, new[] { shape._vertices[(int)ind][0][i].X_Cord, shape._vertices[(int)ind][0][i].Y_Cord }, wkt, wkt);
                ftlength += ftdist;
            }
            return ftlength;
        }
        public static bool getpntBetweenPnts(double[] pnt, double[] otherpnt1, double[] otherpnt2)
        {
            bool betweenthemareyoucrazy = true;
            if (otherpnt1[0] > pnt[0] & otherpnt2[0] > pnt[0])
            {
                if (otherpnt1[1] > pnt[1] & otherpnt2[1] > pnt[1])
                {
                    betweenthemareyoucrazy = false;
                }
                else if (otherpnt1[1] < pnt[1] & otherpnt2[1] < pnt[1])
                {
                    betweenthemareyoucrazy = false;
                }
            }
            else if (otherpnt1[0] < pnt[0] & otherpnt2[0] < pnt[0])
            {
                if (otherpnt1[1] > pnt[1] & otherpnt2[1] > pnt[1])
                {
                    betweenthemareyoucrazy = false;
                }
                else if (otherpnt1[1] < pnt[1] & otherpnt2[1] < pnt[1])
                {
                    betweenthemareyoucrazy = false;
                }
            }
            return betweenthemareyoucrazy;
        }
        public static bool getAngleIsAcute(double[] point1, double[] point2, double[] sharedpoint)
        {
            bool acute = false;
            double[] vector1 = new[] { sharedpoint[0] - point1[0], sharedpoint[1] - point1[1] };
            double[] vector2 = new[] { sharedpoint[0] - point2[0], sharedpoint[1] - point2[1] };
            double dotproduct = vector1[0] * vector2[0] + vector1[1] * vector2[1];
            if (dotproduct > 0d)
                acute = true;
            return acute;
        }
        public static bool getAngleIsRight(double[] point1, double[] point2, double[] sharedpoint)
        {
            bool right = false;
            double[] vector1 = new[] { sharedpoint[0] - point1[0], sharedpoint[1] - point1[1] };
            double[] vector2 = new[] { sharedpoint[0] - point2[0], sharedpoint[1] - point2[1] };
            double dotproduct = vector1[0] * vector2[0] + vector1[1] * vector2[1];
            if (dotproduct == 0d)
                right = true;
            return right;
        }
        public static double getAngle(double[] point1, double[] point2, double[] sharedpoint)
        {
            double m1 = getSlope(sharedpoint, point1);
            double m2 = getSlope(sharedpoint, point2);
            double test = Math.PI - Math.Abs(Math.Atan(m1) - Math.Atan(m2));
            double angle = Math.Atan((m2 - m1) / (1d + m1 * m2));
            double degreeAngle = angle * 180d / Math.PI;
            return angle;
        }
        public static double[] getlineIntersection(double m1, double b1, double m2, double b2)
        {
            double x;
            x = (b2 - b1) / (m1 - m2);
            double y;
            y = m1 * x + b1;
            return new[] { x, y };
        }
        public static double CalcLineDist(double[] pnt1, double[] pnt2)
        {
            double xDist = Math.Abs(pnt1[0] - pnt2[0]);
            double yDist = Math.Abs(pnt1[1] - pnt2[1]);
            double dist = Math.Sqrt(xDist * xDist + yDist * yDist);
            return dist;
        }
        public static double getSlope(double[] pn1, double[] pnt2)
        {
            double rise = pn1[1] - pnt2[1];
            double run = pn1[0] - pnt2[0];
            // Dim risetest As Double = getFtDistBetweenPts({0, pn1(1)}, {0, pnt2(1)}, _WGS84WKT, _WGS84WKT)
            // Dim runtest As Double = getFtDistBetweenPts({pn1(0), 0}, {pnt2(0), 0}, _WGS84WKT, _WGS84WKT)
            // Dim mtest As Double = risetest / runtest
            double m = rise / run;
            return m;
        }
        // Function reprojectPnts(ByVal pnts As List(Of Double()), ByVal fromCS As ArcShapeFile.Projection, targCS As ArcShapeFile.Projection) As List(Of Double())
        // Dim returnPnts As New List(Of Double())

        // Dim ccFact As CoordinateSystemFactory = New CoordinateSystemFactory()
        // Dim ctFact As Transformations.CoordinateTransformationFactory = New Transformations.CoordinateTransformationFactory()
        // Dim fromCSProjNet As CoordinateSystem = ccFact.CreateFromWkt(fromCS.WKT)
        // Dim toCSProjNet As CoordinateSystem = ccFact.CreateFromWkt(targCS.WKT)



        // Dim trans = ctFact.CreateFromCoordinateSystems(fromCSProjNet, toCSProjNet)
        // For Each pnt As Double() In pnts
        // returnPnts.Add(trans.MathTransform.Transform(pnt))
        // Next
        // Return returnPnts
        // End Function
        // Function reprojectPntsNTS(ByVal pnts As List(Of Double()), ByVal fromWKT As String, targWKT As String) As List(Of Double())
        // Dim returnPnts As New List(Of Double())
        // Dim csFactory = New CoordinateSystemFactory
        // Dim sourceCS = csFactory.CreateFromWkt(fromWKT)
        // Dim targetCS = csFactory.CreateFromWkt(targWKT)

        // Dim transformFacotry = New CoordinateTransformationFactory
        // Dim transformation = transformFacotry.CreateFromCoordinateSystems(sourceCS, targetCS)

        // For Each coord In pnts
        // returnPnts.Add(transformation.MathTransform.Transform(coord))
        // Next
        // Return returnPnts
        // End Function
        public static List<double[]> reprojectPntsDS(List<double[]> pnts, string fromWKT, string targWKT)
        {
            var returnPnts = new List<double[]>();
            if (!((fromWKT ?? "") == (targWKT ?? "")))
            {
                //var targproj = new ProjectionInfo();

                //bool targworked = targproj.TryParseEsriString(targWKT);
                //var fromproj = new ProjectionInfo();
                //bool fromworked = fromproj.TryParseEsriString(fromWKT);

                //var zCoords = new double[pnts.Count];
                //var xy = new double[(pnts.Count * 2)];
                //int ixy = 0;
                //for (int i = 0, loopTo = pnts.Count - 1; i <= loopTo; i++)
                //{
                //    xy[ixy] = pnts[i][0];
                //    xy[ixy + 1] = pnts[i][1];
                //    zCoords[i] = 0d;
                //    ixy += 2;
                //}
                //Reproject.ReprojectPoints(xy, zCoords, fromproj, targproj, 0, pnts.Count);
                //ixy = 0;
                //for (int i = 0, loopTo1 = pnts.Count - 1; i <= loopTo1; i++)
                //{
                //    returnPnts.Add(new[] { xy[ixy], xy[ixy + 1] });
                //    ixy += 2;
                //}
                SpatialReference src = new SpatialReference(fromWKT);
                SpatialReference dst = new SpatialReference(targWKT);

                // Create transformation
                CoordinateTransformation transform = new CoordinateTransformation(src, dst);

                var result = new List<(double X, double Y)>();
                foreach (var pnt in pnts)
                {
                    double[] point = new double[] { pnt[0], pnt[1] };
                    transform.TransformPoint(point);
                    result.Add((point[0], point[1]));
                }
            }
            else
            {
                returnPnts = pnts;
            }
            return returnPnts;
        }
        //public static void InitializeShape(ref ShapeFile shape, List<Field> Fields)
        //{
        //    foreach (var @field in Fields)
        //        shape.Fields.Add(@field);
        //    shape.WriteFieldDefs();
        //}
        //public static void InitializeShape(ref ShapeFile shape, Fields Fields)
        //{
        //    foreach (Field fld in Fields)
        //        shape.Fields.Add(fld.Name, fld.Type, fld.Size);
        //    shape.WriteFieldDefs();
        //}
        //public static void defineFeatureParts(ShapeFile shape, ref ShapeFile outputShape)
        //{
        //    for (int i = 0, loopTo = shape.Parts.Count - 1; i <= loopTo; i++)
        //    {
        //        var part = shape.Parts[i];
        //        for (int v = part.Begins, loopTo1 = part.Ends; v <= loopTo1; v++)
        //            outputShape.Vertices.Add(shape.Vertices[v]);
        //        outputShape.SetPartDirection(i, (eDirection)part.Direction);
        //        if (i == shape.Parts.Count - 1)
        //            break;
        //        outputShape.Vertices.NewPart();
        //    }
        //    outputShape.WriteShape();
        //}
        //public static void defineFeatureFields(ref ShapeFile outputShape, Fields fields)
        //{
        //    // load field values
        //    foreach (Field fld in fields)
        //        outputShape.Fields(fld.Value).Value = fld.Value;
        //}
        //public static void WritePRJ(ShapeFile shape, string wkt = "")
        //{
        //    string wktstring = wkt;
        //    if (string.IsNullOrEmpty(wkt) | wkt == null)
        //        wkt = shape.Projection.WKT;
        //    string prjPath = Strings.Left(shape.ShapeFileName, Strings.Len(shape.ShapeFileName) - 4) + ".prj";
        //    using (var createfile = File.Create(prjPath))
        //    {
        //        byte[] bytes = new System.Text.UTF8Encoding(true).GetBytes(wkt);
        //        createfile.Write(bytes, 0, bytes.Length);
        //    }
        //    // Using sw As New System.IO.StreamWriter(prjPath)
        //    // sw.WriteLine(shape.Projection.WKT)
        //    // End Using
        //}
        // Sub WritePolyShapeToGeoDatabase(ByVal polyfeat As Feat, outpath As String)
        // Dim polyRing As New OSGeo.OGR.Geometry(OSGeo.OGR.wkbGeometryType.wkbPolygon)
        // For i = 0 To polyfeat._parts.Count - 1
        // For v = 0 To polyfeat._vertices(i).Count - 1

        // Next
        // Next
        // End Sub
        public static void WritePointShapeToGeoJSON(Feat pointfeat, string outpath, string layername)
        {
            // GDALSetup.InitializeMultiplatform("C:\Temp\GDAL")
            OSGeo.GDAL.Gdal.SetConfigOption("PROJ_LIB", @"C:\Software\GDAL GISInternals\bin\proj9\share");
            OSGeo.GDAL.Gdal.AllRegister();
            OSGeo.OGR.Ogr.RegisterAll();

            // Define spatial reference (WGS84)
            var srs = new SpatialReference("");
            srs.ImportFromEPSG(4326);

            // Create in-memory data source
            var memDriver = OSGeo.OGR.Ogr.GetDriverByName("Memory");
            var memDs = memDriver.CreateDataSource("", (string[])null);

            // Create layer with point geometry
            var layer = memDs.CreateLayer("points", srs, wkbGeometryType.wkbPoint, (string[])null);

            // define fields
            for (int f = 0, loopTo = pointfeat._attTable._columns.Count - 1; f <= loopTo; f++)
            {
                string fldName = pointfeat._attTable._columns.Keys.ElementAtOrDefault(f);
                var fldType = pointfeat._attTable._columns[fldName].getEFldType;
                FieldDefn oFieldDefn;
                if (object.ReferenceEquals(fldType, typeof(string)))
                {
                    oFieldDefn = new FieldDefn(fldName, FieldType.OFTString);
                }
                else if (object.ReferenceEquals(fldType, typeof(int)))
                {
                    oFieldDefn = new FieldDefn(fldName, FieldType.OFTInteger);
                }
                else if (object.ReferenceEquals(fldType, typeof(double)))
                {
                    oFieldDefn = new FieldDefn(fldName, FieldType.OFTReal);
                }
                else if (object.ReferenceEquals(fldType, typeof(DateTime)))
                {
                    oFieldDefn = new FieldDefn(fldName, FieldType.OFTDateTime);
                }
                else
                {
                    oFieldDefn = new FieldDefn(fldName, FieldType.OFTString);
                }
                layer.CreateField(oFieldDefn, 1);
                oFieldDefn.Dispose();
            }

            for (int i = 0, loopTo1 = pointfeat._parts.Count - 1; i <= loopTo1; i++)
            {
                var featr = new Feature(layer.GetLayerDefn());
                double x = pointfeat._vertices[i][0][0].X_Cord;
                double y = pointfeat._vertices[i][0][0].Y_Cord;
                var pointGeom = new Geometry(wkbGeometryType.wkbPoint);
                pointGeom.AddPoint(x, y, 0d);
                featr.SetGeometry(pointGeom);

                for (int f = 0, loopTo2 = pointfeat._attTable._columns.Count - 1; f <= loopTo2; f++)
                {
                    string fldName = pointfeat._attTable._columns.Keys.ElementAtOrDefault(f);
                    var fldType = pointfeat._attTable._columns[fldName].getEFldType;
                    var fldVal = pointfeat._attTable._columns[fldName]._rows[i];
                    try
                    {
                        if (fldVal is not null && (fldVal.ToString() ?? "") == (fldVal.GetType().ToString() ?? ""))
                        {
                            if (object.ReferenceEquals(fldType, typeof(string)))
                            {
                                featr.SetField(fldName, "");
                            }
                            else if (object.ReferenceEquals(fldType, typeof(int)))
                            {
                                featr.SetField(fldName, 0);
                            }
                            else if (object.ReferenceEquals(fldType, typeof(double)))
                            {
                                featr.SetField(fldName, 0d);
                            }
                            else if (object.ReferenceEquals(fldType, typeof(DateTime)))
                            {
                                DateTime tempDate = new DateTime();
                                featr.SetField(fldName, tempDate.Year, tempDate.Month, tempDate.Day, tempDate.Hour, tempDate.Minute, tempDate.Second, 0);
                            }
                            else
                            {
                                // do nothing for now
                            }
                        }
                        else if (object.ReferenceEquals(fldType, typeof(string)))
                        {
                            featr.SetField(fldName, fldVal.ToString());
                        }
                        else if (object.ReferenceEquals(fldType, typeof(int)))
                        {
                            featr.SetField(fldName, (int)Convert.ChangeType(fldVal, typeof(int)));
                        }
                        else if (object.ReferenceEquals(fldType, typeof(double)))
                        {
                            featr.SetField(fldName, (int)Convert.ChangeType(fldVal, typeof(double)));
                        }
                        else if (object.ReferenceEquals(fldType, typeof(DateTime)))
                        {
                            DateTime tempDate = (DateTime)Convert.ChangeType(fldVal, typeof(DateTime));
                            featr.SetField(fldName, tempDate.Year, tempDate.Month, tempDate.Day, tempDate.Hour, tempDate.Minute, tempDate.Second, 0);
                        }
                        else
                        {
                            // do nothing for now
                        }
                    }

                    catch (Exception ex)
                    {

                    }

                }
                layer.CreateFeature(featr);
                featr.Dispose();
            }


            // ' Sample data: list of points with attributes
            // Dim points = {
            // New With {.X = -122.4194, .Y = 37.7749, .Name = "San Francisco", .Value = 100},
            // New With {.X = -74.006, .Y = 40.7128, .Name = "New York", .Value = 200},
            // New With {.X = -87.6298, .Y = 41.8781, .Name = "Chicago", .Value = 150}
            // }

            // ' Add features to the layer
            // For i = 0 To pointfeat._parts.Count - 1
            // Dim feature As New Feature(layer.GetLayerDefn())
            // feature.SetField("Name", pt.Name)
            // feature.SetField("Value", pt.Value)

            // Dim geom As OSGeo.OGR.Geometry = OSGeo.OGR.Geometry.CreateFromWkt($"POINT ({pt.X} {pt.Y})")
            // feature.SetGeometry(geom)

            // layer.CreateFeature(feature)
            // feature.Dispose()
            // Next

            // Write to GeoJSON
            var geojsonDriver = OSGeo.OGR.Ogr.GetDriverByName("GeoJSON");
            if (!Directory.Exists(outpath))
                Directory.CreateDirectory(outpath);
            var geojsonDs = geojsonDriver.CreateDataSource(outpath + @"\" + layername + ".geojson", (string[])null);
            geojsonDs.CopyLayer(layer, "points", (string[])null);

            // Cleanup
            geojsonDs.Dispose();
            memDs.Dispose();



















            // Dim gdbDriver As OSGeo.OGR.Driver = Ogr.GetDriverByName("GEOJSON")
            // If Directory.Exists(outpath & "\" & layername & ".gdb") Then
            // Directory.Delete(outpath & "\" & layername & ".gdb", True)
            // End If

            // Dim gdb As DataSource = gdbDriver.CreateDataSource(outpath & "\" & layername & ".gdb", Nothing)
            // Dim srs As New SpatialReference("")
            // srs.ImportFromWkt(pointfeat._proj.WKT)

            // Dim lyr As Layer = gdb.CreateLayer(layername, srs, wkbGeometryType.wkbPoint, Nothing)

            // 'define fields
            // For f = 0 To pointfeat._attTable._columns.Count - 1
            // Dim fldName As String = pointfeat._attTable._columns.Keys(f)
            // Dim fldType As Type = pointfeat._attTable._columns(fldName).getEFldType()
            // Dim oFieldDefn As OSGeo.OGR.FieldDefn
            // If fldType Is GetType(String) Then
            // oFieldDefn = New OSGeo.OGR.FieldDefn(fldName, OSGeo.OGR.FieldType.OFTString)
            // ElseIf fldType Is GetType(Int32) Then
            // oFieldDefn = New OSGeo.OGR.FieldDefn(fldName, OSGeo.OGR.FieldType.OFTInteger)
            // ElseIf fldType Is GetType(Double) Then
            // oFieldDefn = New OSGeo.OGR.FieldDefn(fldName, OSGeo.OGR.FieldType.OFTReal)
            // ElseIf fldType Is GetType(Date) Then
            // oFieldDefn = New OSGeo.OGR.FieldDefn(fldName, OSGeo.OGR.FieldType.OFTDateTime)
            // Else
            // oFieldDefn = New OSGeo.OGR.FieldDefn(fldName, OSGeo.OGR.FieldType.OFTString)
            // End If
            // lyr.CreateField(oFieldDefn, 1)
            // oFieldDefn.Dispose()
            // Next

            // 'write from pointfeat to gdb
            // Dim PointShape As New OSGeo.OGR.Geometry(OSGeo.OGR.wkbGeometryType.wkbPoint)
            // For i = 0 To pointfeat._parts.Count - 1
            // Dim featr As New Feature(lyr.GetLayerDefn())
            // Dim x As Double = pointfeat._vertices(i)(0)(0).X_Cord
            // Dim y As Double = pointfeat._vertices(i)(0)(0).Y_Cord
            // Dim z As Double = pointfeat._vertices(i)(0)(0).Z_Cord
            // Dim pointGeom As New OSGeo.OGR.Geometry(OSGeo.OGR.wkbGeometryType.wkbPoint)
            // pointGeom.AddPoint(x, y, z)
            // featr.SetGeometry(pointGeom)

            // For f = 0 To pointfeat._attTable._columns.Count - 1
            // Dim fldName As String = pointfeat._attTable._columns.Keys(f)
            // Dim fldType As Type = pointfeat._attTable._columns(fldName).getEFldType()
            // Dim fldVal As Object = pointfeat._attTable._columns(fldName)._rows(i)
            // If fldType Is GetType(String) Then
            // featr.SetField(fldName, CTypeDynamic(fldVal, GetType(String)))
            // ElseIf fldType Is GetType(Int32) Then
            // featr.SetField(fldName, CTypeDynamic(fldVal, GetType(Int32)))
            // ElseIf fldType Is GetType(Double) Then
            // featr.SetField(fldName, CTypeDynamic(fldVal, GetType(Double)))
            // ElseIf fldType Is GetType(Date) Then
            // featr.SetField(fldName, CTypeDynamic(fldVal, GetType(Date)))
            // Else
            // 'do nothing for now
            // End If
            // Next
            // lyr.CreateFeature(featr)
            // featr.Dispose()
            // Next

            // 'save gdb and clean up
            // 'save?
            // gdb.Dispose()
        }
        public static Feat mergeFeats(List<string> paths, string newFeatName)
        {
            var feat = new Feat();
            foreach (string shp in paths)
            {
                if (File.Exists(shp))
                {
                    feat.readFromFile(shp, newFeatName, true);
                }
            }
            return feat;
        }
        public static Vertex copyVertice(Vertex vert)
        {
            var newVert = new Vertex(vert.X_Cord, vert.Y_Cord, vert.Z_Cord);
            return newVert;
        }
        public static Part copyPart(Part part)
        {
            var newPart = new Part(part._WKT);
            newPart.Area = part.Area;
            newPart.Begins = part.Begins;
            newPart.Ends = part.Ends;
            newPart.CentroidX = part.CentroidX;
            newPart.CentroidY = part.CentroidY;
            newPart.Direction = part.Direction;
            newPart.IsHole = part.IsHole;
            newPart.MBRXMax = part.MBRXMax;
            newPart.MBRXMin = part.MBRXMin;
            newPart.MBRYMax = part.MBRYMax;
            newPart.MBRYMin = part.MBRYMin;           
            newPart.Perimeter = part.Perimeter;            
            return newPart;
        }
        public static void splitFeatByField(Feat shape, string fldName, string outpath, int substringStart = 1, int substringLen = int.MaxValue, List<string> fieldsToWrite = null)
        {
            var subShapeDict = new Dictionary<string, List<long>>();
            var subShapeFileNames = new Dictionary<string, string>();
            // first loop to build dictionary
            for (int i = 0, loopTo = shape._attTable._columns[fldName]._rows.Count - 1; i <= loopTo; i++)
            {
                string val = shape._attTable.getRowValAsString(fldName, (long)i);
                string uniqueVal = Strings.Mid(val, substringStart, Math.Min(substringLen, val.Length));
                if (substringLen < int.MaxValue)
                {
                    if (Information.IsNumeric(uniqueVal))
                    {
                        uniqueVal = uniqueVal.PadLeft(substringLen, '0');
                    }
                }
                if (!subShapeDict.ContainsKey(uniqueVal))
                {
                    subShapeDict.Add(uniqueVal, new List<long>());
                }
                subShapeDict[uniqueVal].Add(i);
            }
            // loop through dictionary and compose features into county level shapefiles
            foreach (KeyValuePair<string, List<long>> subShape in subShapeDict)
            {
                Feat newFeat;
                if (shape._proj.WKT == null)
                {
                    newFeat = new Feat(shape._WKT, outpath, subShape.Key, shape._shapeType);
                }
                else
                {
                    newFeat = new Feat(outpath, subShape.Key, shape._proj, shape._shapeType);
                }

                subShapeFileNames.Add(subShape.Key, shape._path);
                foreach (var featInd in subShape.Value)
                    shape.CopyTo(featInd, ref newFeat, true);
                newFeat.WriteToFile(fieldsToWrite: fieldsToWrite);
            }
        }
        public static void dropFields(ref Feat shape, List<string> fldsToKeep)
        {
            for (int i = shape._attTable._columns.Count - 1; i >= 0; i -= 1)
            {
                string fldName = shape._attTable._columns.Keys.ElementAtOrDefault(i);
                if (!fldsToKeep.Contains(fldName))
                {
                    shape._attTable.RemoveField(fldName);
                }
            }
        }
        public static double getFtDistBetweenPts(double[] pnt1, double[] pnt2, string wkt1, string wkt2)
        {
            var pnts = new List<double[]>();
            pnts.Add(reprojectPntsDS(new[] { pnt1 }.ToList(), wkt1, _ALBERS)[0]);
            pnts.Add(reprojectPntsDS(new[] { pnt2 }.ToList(), wkt2, _ALBERS)[0]);
            double dist = Math.Sqrt(Math.Pow(pnts[0][0] - pnts[1][0], 2d) + Math.Pow(pnts[0][1] - pnts[1][1], 2d)) * 3.28084d;
            return dist;
        }
        public static double getFtDistBetweenPtsNoRerpoj(double[] pnt1, double[] pnt2)
        {
            var pnts = new List<double[]>();
            pnts.Add(pnt1);
            pnts.Add(pnt2);
            double dist = Math.Sqrt(Math.Pow(pnts[0][0] - pnts[1][0], 2d) + Math.Pow(pnts[0][1] - pnts[1][1], 2d)) * 3.28084d;
            return dist;
        }
        public static double[] getPointOffset(double[] pnt1, double offsetX, double offsetY, string wkt1)
        {
            double[] pntAlbers = reprojectPntsDS(new[] { pnt1 }.ToList(), wkt1, _ALBERS)[0];
            double[] xOffsetPnt = new[] { pntAlbers[0] - offsetX / 3.28084d, pntAlbers[1] };
            double[] yOffsetPnt = new[] { pntAlbers[0], pntAlbers[1] - offsetY / 3.28084d };
            double[] outPntAlbers = new[] { xOffsetPnt[0], yOffsetPnt[1] };
            double[] outPnt = reprojectPntsDS(new[] { outPntAlbers }.ToList(), _ALBERS, wkt1)[0];
            return outPnt;
        }
        public static double Haversine(double[] coord1, double[] coord2, double earthRadius = 6371000.0d)
        {
            double radLat1 = coord1[0] * Math.PI / 180d;
            double radLat2 = coord2[0] * Math.PI / 180d;

            double deltaRadLat = (coord2[0] - coord1[0]) * Math.PI / 180d;
            double deltaRadLong = (coord2[1] - coord1[1]) * Math.PI / 180d;

            double a = Math.Pow(Math.Sin(deltaRadLat / 2d), 2d) + Math.Cos(radLat1) * Math.Cos(radLat2) * Math.Pow(Math.Sin(deltaRadLong / 2d), 2d);
            double c = Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
            double d = earthRadius * c;
            return d;
        }
        public static double[] getMultiPartCentroid(List<Part> parts)
        {
            var centroid = new double[2];
            double mbrXmin = double.MaxValue;
            double mbrXmax = double.MinValue;
            double mbrYmin = double.MaxValue;
            double mbryMax = double.MinValue;
            foreach (var part in parts)
            {
                if (part.CentroidX is { } arg1 && arg1 < mbrXmin)
                    mbrXmin = (double)part.CentroidX;
                if (part.CentroidX is { } arg2 && arg2 > mbrXmax)
                    mbrXmax = (double)part.CentroidX;
                if (part.CentroidY is { } arg3 && arg3 < mbrYmin)
                    mbrYmin = (double)part.CentroidY;
                if (part.CentroidY is { } arg4 && arg4 > mbryMax)
                    mbryMax = (double)part.CentroidY;
            }
            centroid[0] = (mbrXmax + mbrXmin) / 2d;
            centroid[1] = (mbryMax + mbrYmin) / 2d;

            return centroid;

            // For Each part In parts 

            // Next
        }
        public static double CalcArea(List<Vertex> verts, string fromWKT)
        {
            var convertedCoords = new List<double[]>();
            List<double[]> coords = verts.Select(v => v.getPoints).ToList();
            var reprojCoords = reprojectPntsDS(coords, fromWKT, _ALBERS);
            foreach (var coord in reprojCoords)
            {
                var newCoord = new double[2];
                // newCoord(0) = coord(0) * (10000 / 90) * 3280.4
                // newCoord(1) = coord(1) * (10000 / 90) * 3280.4
                // newCoord(0) = getFtDistBetweenPts({coord(0), 0}, {0, 0}, fromWKT, fromWKT)
                // newCoord(1) = getFtDistBetweenPts({0, coord(1)}, {0, 0}, fromWKT, fromWKT)
                newCoord[0] = coord[0] * 3.28084d;
                newCoord[1] = coord[1] * 3.28084d;
                convertedCoords.Add(newCoord);
            }

            // Dim test As Double = getFtDistBetweenPts(coords(1), coords(2), fromWKT, fromWKT)
            return shoeStringAreaFunction(convertedCoords);
        }
        public static double shoeStringAreaFunction(List<double[]> coords)
        {
            double XY = 0d;
            double YX = 0d;
            for (int i = 0, loopTo = coords.Count - 2; i <= loopTo; i++)
            {
                double xi = coords[i][0];
                double yi = coords[i][1];
                double xiPlus1 = coords[i + 1][0];
                double yiPlus1 = coords[i + 1][1];
                XY += xi * yiPlus1;
                YX += yi * xiPlus1;
            }
            double area = Math.Abs(XY - YX) / 2d;
            return area;
        }
        public static List<List<Vertex>> GetVerticesAroundPoint(double x, double y, double area, string wkt, double slope = 1d)
        {
            var outVerts = new List<List<Vertex>>();
            double dist = Math.Sqrt(area) / 2d;
            outVerts.Add(new List<Vertex>());

            double[] newpoint1 = getPointOffset(new[] { x, y }, dist, dist, wkt);
            var vertex1 = new Vertex(newpoint1[0], newpoint1[1]);           
            outVerts[0].Add(vertex1);

            double[] newpoint2 = getPointOffset(new[] { x, y }, -dist, dist, wkt);
            var vertex2 = new Vertex(newpoint2[0], newpoint2[1]);            
            outVerts[0].Add(vertex2);

            double[] newpoint3 = getPointOffset(new[] { x, y }, -dist, -dist, wkt);
            var vertex3 = new Vertex(newpoint3[0], newpoint3[1]);          
            outVerts[0].Add(vertex3);

            double[] newpoint4 = getPointOffset(new[] { x, y }, dist, -dist, wkt);
            var vertex4 = new Vertex(newpoint4[0], newpoint4[1]);            
            outVerts[0].Add(vertex4);

            outVerts[0].Add(vertex1);

            return outVerts;
        }
        public static double getPerimeter(List<double[]> vertslist, string coordSysWKT)
        {
            var outPerim = default(double);
            for (int i = 1, loopTo = vertslist.Count - 1; i <= loopTo; i++)
                outPerim += getFtDistBetweenPtsNoRerpoj(vertslist[i - 1], vertslist[i]);
            return outPerim;
        }
        public static double getPolyArea(List<double[]> vertices)
        {
            int n = vertices.Count;
            double area = 0d;

            for (int i = 0, loopTo = n - 1; i <= loopTo; i++)
            {
                double x1 = vertices[i][0];
                double y1 = vertices[i][1];
                double x2 = vertices[(i + 1) % n][0];
                double y2 = vertices[(i + 1) % n][1];
                area += x1 * y2 - x2 * y1;
            }
            return Math.Abs(area) / 2d;
        }
        public static List<Part> returnPartsAroundPoint(double x, double y, double area, string wkt, double slope = 1d)
        {
            var outParts = new List<Part>();
            double dist = Math.Sqrt(area) / 2d;
            var part = new Part(wkt);
            part.Area = area;
            part.CentroidX = x;
            part.CentroidY = y;
            double[] upperleft = getPointOffset(new[] { x, y }, dist, dist, wkt);
            double[] lowerRight = getPointOffset(new[] { x, y }, -dist, -dist, wkt);
            part.MBRXMax = upperleft[0];
            part.MBRXMin = lowerRight[0];
            part.MBRYMax = upperleft[1];
            part.MBRYMin = lowerRight[1];
            outParts.Add(part);
            return outParts;
        }
        public static List<Part> PartsFromGeomWKT(string WKT, string coordsysWKT)
        {
            string subWKT = WKT;
            subWKT = Strings.Right(subWKT, subWKT.Count() - subWKT.IndexOf("("));
            var partlist = new List<Part>();
            var stringlist = subWKT.Split(',').ToList();
            var coordlist = new List<double[]>();
            string firstCoordString = "";
            string secondCoordString = "";

            for (int i = 0, loopTo = stringlist.Count - 1; i <= loopTo; i++)
            {
                string stringval = stringlist[i].Trim();

                firstCoordString = IOtools.deleteTextFromString(stringval.Split(' ')[0], new[] { "(" });
                secondCoordString = IOtools.deleteTextFromString(stringval.Split(' ')[1], new[] { "(" });


                // check new part
                if (Strings.Left(stringval, 1) == "(")
                {
                    partlist.Add(new Part(coordsysWKT));
                    coordlist = new List<double[]>();
                    if (partlist.Count > 1)
                    {
                        if (partlist[partlist.Count - 2].IsHole == false)
                        {
                            partlist.Last().IsHole = true;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(firstCoordString) & !string.IsNullOrEmpty(secondCoordString))
                {
                    // add new point
                    double xcoord = double.MinValue;
                    double.TryParse(firstCoordString, out xcoord);
                    double ycoord = double.MinValue;
                    double.TryParse(secondCoordString, out ycoord);
                    if (xcoord > double.MinValue & ycoord > double.MinValue)
                    {
                        coordlist.Add(new[] { xcoord, ycoord });
                    }

                    // reset
                    firstCoordString = "";
                    secondCoordString = "";
                }

                if (Strings.Right(stringval, 1) == ")")
                {
                    // finished with parth
                    if (partlist.Count > 0 & coordlist.Count > 0)
                    {
                        double[] centroid = getCentroid(coordlist);
                        partlist.Last().Begins = 0;
                        partlist.Last().Ends = coordlist.Count - 1;
                        partlist.Last().CentroidX = centroid[0];
                        partlist.Last().CentroidY = centroid[1];
                        partlist.Last().MBRXMax = coordlist.Max(x => x[0]);
                        partlist.Last().MBRXMin = coordlist.Min(x => x[0]);
                        partlist.Last().MBRYMax = coordlist.Max(y => y[1]);
                        partlist.Last().MBRYMin = coordlist.Min(y => y[1]);
                        partlist.Last().Perimeter = getPerimeter(coordlist, coordsysWKT);
                        partlist.Last().Area = getPolyArea(coordlist);
                    }
                }
            }
            return partlist;
        }
        public static List<List<Vertex>> VertsFromGeomWKT(string WKT)
        {
            string subWKT = WKT;
            subWKT = Strings.Right(subWKT, subWKT.Count() - subWKT.IndexOf("("));
            var vertlist = new List<List<Vertex>>();
            var stringlist = subWKT.Split(',').ToList();
            var coordlist = new List<double[]>();
            string firstCoordString = "";
            string secondCoordString = "";

            for (int i = 0, loopTo = stringlist.Count - 1; i <= loopTo; i++)
            {
                string stringval = stringlist[i].Trim();

                firstCoordString = IOtools.deleteTextFromString(stringval.Split(' ')[0], new[] { "(" });
                secondCoordString = IOtools.deleteTextFromString(stringval.Split(' ')[1], new[] { "(" });

                // check new part
                if (Strings.Left(stringval, 1) == "(")
                {
                    vertlist.Add(new List<Vertex>());
                }

                if (!string.IsNullOrEmpty(firstCoordString) & !string.IsNullOrEmpty(secondCoordString))
                {
                    double xcoord = double.MinValue;
                    double.TryParse(firstCoordString, out xcoord);
                    double ycoord = double.MinValue;
                    double.TryParse(secondCoordString, out ycoord);
                    if (xcoord > double.MinValue & ycoord > double.MinValue)
                    {
                        vertlist.Last().Add(new Vertex(xcoord, ycoord));                       
                    }

                    // reset
                    firstCoordString = "";
                    secondCoordString = "";
                }





                // Dim subSplit As List(Of String) = stringlist(i).Split(" ").ToList()
                // Dim firstPointString = ""
                // Dim secondPointString = ""
                // If subSplit.Count > 2 Then
                // firstPointString = subSplit(1)
                // secondPointString = subSplit(2)
                // Else
                // firstPointString = subSplit(0)
                // secondPointString = subSplit(1)
                // End If

                // If Strings.Left(firstPointString, 1) = "(" Then
                // vertlist.Add(New List(Of Vertice))
                // End If

                // Dim xcoord As Double = Double.MinValue
                // Double.TryParse(firstPointString.Trim({"("c, ","c, ")"c}), xcoord)
                // Dim ycoord As Double = Double.MinValue
                // Double.TryParse(secondPointString.Trim({"("c, ","c, ")"c}), ycoord)
                // If xcoord = Double.MinValue Or ycoord = Double.MinValue Then
                // Return Nothing
                // End If
                // vertlist.Last().Add(New Vertice)
                // vertlist.Last().Last().X_Cord = xcoord
                // vertlist.Last().Last().Y_Cord = ycoord

                // 'Dim point As Double = Double.MinValue
                // 'Double.TryParse(stringlist(i).Trim({"("c, ","c, ")"c}), point)
                // 'If point = Double.MinValue Then
                // '    Return Nothing
                // 'End If

                // 'If Strings.Right(stringlist(i), 1) <> "," Then
                // '    coordlist.Last()(0) = point
                // 'Else
                // '    coordlist.Last()(1) = point
                // '    vertlist.Last().Add(New Vertice)
                // '    vertlist.Last().Last().X_Cord = coordlist.Last()(0)
                // '    vertlist.Last().Last().Y_Cord = coordlist.Last()(1)
                // '    coordlist.Add({0, 0})
                // 'End If
            }
            return vertlist;


        }

        public static List<Part> PartsFromTxtWKT(string WKT, string coordsysWKT)
        {
            string subWKT = Strings.Right(WKT, WKT.Count());
            subWKT = Strings.Left(subWKT, subWKT.Count());
            var partlist = new List<Part>();
            var stringlist = subWKT.Split(',').ToList();
            var coordlist = new List<double[]>();
            string firstCoordString = "";
            string secondCoordString = "";

            for (int i = 0, loopTo = stringlist.Count - 1; i <= loopTo; i++)
            {
                string stringval = stringlist[i].Trim();
                if (Strings.Left(stringval, 1) == "(")
                {
                    firstCoordString = IOtools.deleteTextFromString(stringval, new[] { "(" }).Trim();
                }
                else if (Strings.Right(stringval, 1) == ")")
                {
                    secondCoordString = IOtools.deleteTextFromString(stringval, new[] { ")" }).Trim();
                }

                // check new part
                if (Strings.Left(stringval, 2) == "((")
                {
                    partlist.Add(new Part(coordsysWKT));
                    coordlist = new List<double[]>();
                    if (partlist.Count > 1)
                    {
                        if (partlist[partlist.Count - 2].IsHole == false)
                        {
                            partlist.Last().IsHole = true;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(firstCoordString) & !string.IsNullOrEmpty(secondCoordString))
                {
                    // add new point
                    double xcoord = double.MinValue;
                    double.TryParse(firstCoordString, out xcoord);
                    double ycoord = double.MinValue;
                    double.TryParse(secondCoordString, out ycoord);
                    if (xcoord > double.MinValue & ycoord > double.MinValue)
                    {
                        coordlist.Add(new[] { xcoord, ycoord });
                    }

                    // reset
                    firstCoordString = "";
                    secondCoordString = "";
                }

                if (Strings.Right(stringval, 2) == "))")
                {
                    // finished with parth
                    if (partlist.Count > 0 & coordlist.Count > 0)
                    {
                        double[] centroid = getCentroid(coordlist);
                        partlist.Last().Begins = 0;
                        partlist.Last().Ends = coordlist.Count - 1;
                        partlist.Last().CentroidX = centroid[0];
                        partlist.Last().CentroidY = centroid[1];
                        partlist.Last().MBRXMax = coordlist.Max(x => x[0]);
                        partlist.Last().MBRXMin = coordlist.Min(x => x[0]);
                        partlist.Last().MBRYMax = coordlist.Max(y => y[1]);
                        partlist.Last().MBRYMin = coordlist.Min(y => y[1]);
                        partlist.Last().Perimeter = getPerimeter(coordlist, coordsysWKT);
                        partlist.Last().Area = getPolyArea(coordlist);
                    }
                }
            }
            return partlist;
        }
        public static List<List<Vertex>> VertsFromTxtWKT(string WKT)
        {
            var vertlist = new List<List<Vertex>>();
            string subWKT = Strings.Right(WKT, WKT.Count() - 1);
            subWKT = Strings.Left(subWKT, subWKT.Count() - 1);
            var stringlist = subWKT.Split(',').ToList();
            string firstCoordString = "";
            string secondCoordString = "";

            for (int i = 0, loopTo = stringlist.Count - 1; i <= loopTo; i++)
            {
                string stringval = stringlist[i].Trim();
                if (Strings.Left(stringval, 1) == "(")
                {
                    firstCoordString = IOtools.deleteTextFromString(stringval, new[] { "(" }).Trim();
                }
                else if (Strings.Right(stringval, 1) == ")")
                {
                    secondCoordString = IOtools.deleteTextFromString(stringval, new[] { ")" }).Trim();
                }

                // check new part
                if (Strings.Left(stringval, 2) == "((")
                {
                    vertlist.Add(new List<Vertex>());
                }

                if (!string.IsNullOrEmpty(firstCoordString) & !string.IsNullOrEmpty(secondCoordString))
                {
                    double xcoord = double.MinValue;
                    double.TryParse(firstCoordString, out xcoord);
                    double ycoord = double.MinValue;
                    double.TryParse(secondCoordString, out ycoord);
                    if (xcoord > double.MinValue & ycoord > double.MinValue)
                    {
                        vertlist.Last().Add(new Vertex(xcoord, ycoord));                       
                    }

                    // reset
                    firstCoordString = "";
                    secondCoordString = "";
                }





                // Dim subSplit As List(Of String) = stringlist(i).Split(" ").ToList()
                // Dim firstPointString = ""
                // Dim secondPointString = ""
                // If subSplit.Count > 2 Then
                // firstPointString = subSplit(1)
                // secondPointString = subSplit(2)
                // Else
                // firstPointString = subSplit(0)
                // secondPointString = subSplit(1)
                // End If

                // If Strings.Left(firstPointString, 1) = "(" Then
                // vertlist.Add(New List(Of Vertice))
                // End If

                // Dim xcoord As Double = Double.MinValue
                // Double.TryParse(firstPointString.Trim({"("c, ","c, ")"c}), xcoord)
                // Dim ycoord As Double = Double.MinValue
                // Double.TryParse(secondPointString.Trim({"("c, ","c, ")"c}), ycoord)
                // If xcoord = Double.MinValue Or ycoord = Double.MinValue Then
                // Return Nothing
                // End If
                // vertlist.Last().Add(New Vertice)
                // vertlist.Last().Last().X_Cord = xcoord
                // vertlist.Last().Last().Y_Cord = ycoord

                // 'Dim point As Double = Double.MinValue
                // 'Double.TryParse(stringlist(i).Trim({"("c, ","c, ")"c}), point)
                // 'If point = Double.MinValue Then
                // '    Return Nothing
                // 'End If

                // 'If Strings.Right(stringlist(i), 1) <> "," Then
                // '    coordlist.Last()(0) = point
                // 'Else
                // '    coordlist.Last()(1) = point
                // '    vertlist.Last().Add(New Vertice)
                // '    vertlist.Last().Last().X_Cord = coordlist.Last()(0)
                // '    vertlist.Last().Last().Y_Cord = coordlist.Last()(1)
                // '    coordlist.Add({0, 0})
                // 'End If
            }
            return vertlist;


        }
        public static void Runogr2ogr(string args)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = @"C:\Software\GDAL GISInternals\bin\gdal\apps\ogr2ogr.exe";
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            var proc = Process.Start(psi);
            string output = proc.StandardOutput.ReadToEnd();
            string errors = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            Console.WriteLine("Output: " + output);
            Console.WriteLine("Errors: " + errors);
        }
        public static void RunOrgInfo(string args)
        {
            var psi = new ProcessStartInfo();
            psi.FileName = @"C:\Software\GDAL GISInternals\bin\gdal\apps\ogrinfo.exe";
            psi.Arguments = args;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;

            var proc = Process.Start(psi);
            string output = proc.StandardOutput.ReadToEnd();
            string errors = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            Console.WriteLine("Output: " + output);
            Console.WriteLine("Errors: " + errors);
        }
    }
}
