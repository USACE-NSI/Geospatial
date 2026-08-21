using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AlexGeospatial.RTree;
using Microsoft.VisualBasic;

namespace AlexGeospatial
{
    public class Feat
    {
        public string _path;
        public string _name;
        public List<List<Part>> _parts; // [feature index, [Parts]]
        public List<List<List<Vertex>>> _vertices; // [Feature index, [part index, [vertice]]]
        public AttTbl _attTable;
        public Projection _proj;
        public string _WKT;
        public GeospatialTools.eShapeType _shapeType;
        public RTreeManager _rTree;
        public Feat(string path = "", string name = "", Projection proj = null, GeospatialTools.eShapeType shpType = default)
        {
            _parts = new List<List<Part>>();
            _vertices = new List<List<List<Vertex>>>();
            _attTable = new AttTbl();
            _proj = new Projection();
            if (!(proj == null))
            {
                _proj = proj;
                _WKT = _proj.WKT;
            }
            _path = path;
            _name = name;
            _shapeType = shpType;
        }
        public Feat(string WKT, string path = "", string name = "", GeospatialTools.eShapeType shpType = default)
        {
            _parts = new List<List<Part>>();
            _vertices = new List<List<List<Vertex>>>();
            _attTable = new AttTbl();
            _proj = new Projection();
            _WKT = WKT;
            _path = path;
            _name = name;
            _shapeType = shpType;
        }

        public void readFromFile(string fullname, string name, bool append = false)
        {
            //if (!append)
            //{
            //    _parts = new List<List<Part>>();
            //    _vertices = new List<List<List<Vertex>>>();
            //    _attTable = new AttTbl();
            //}

            //_path = System.IO.Path.GetDirectoryName(fullname);
            //_name = name;
            //var shape = new ShapeFile();
            //shape.Open(fullname, true);


            //_shapeType = shape.ShapeType;
            //long atInd = _parts.Count;

            //while (shape.EOF == false)
            //{
            //    long partInd = 0L;
            //    if (shape.Parts.Count > 0)
            //    {
            //        _parts.Add(new List<Part>());
            //        _vertices.Add(new List<List<Vertex>>());
            //        foreach (Part part in shape.Parts)
            //        {
            //            var partCopy = GeospatialTools.copyPart(part);
            //            _parts[(int)atInd].Add(partCopy);
            //            _vertices[(int)atInd].Add(new List<Vertex>());
            //            for (int i = part.Begins, loopTo = part.Ends; i <= loopTo; i++)
            //            {
            //                var vertCopy = GeospatialTools.copyVertice(shape.Vertices[i]);
            //                _vertices[(int)atInd][(int)partInd].Add(vertCopy);
            //            }

            //            partInd += 1L;
            //        }
            //        foreach (Field col in shape.Fields)
            //            _attTable.addAppendColData(col, atInd);
            //        atInd += 1L;
            //    }

            //    shape.MoveNext();
            //}
            //_proj = shape.Projection;
            //_WKT = shape.Projection.WKT;
            //shape.Close();
        }
        public void WriteToFile(string newPath = "None", string newName = "None", bool overwrite = false, List<string> fieldsToWrite = null)
        {
            //if (!(newPath == "None"))
            //    _path = newPath;
            //if (!(newName == "None"))
            //    _name = newName;
            //if (!System.IO.Directory.Exists(_path))
            //    System.IO.Directory.CreateDirectory(_path);
            //string outfileName = _path + @"\" + _name;
            //bool goodtoGo = true;
            //if (System.IO.File.Exists(outfileName))
            //{
            //    if (overwrite)
            //    {
            //        System.IO.File.Delete(outfileName + ".shp");
            //        System.IO.File.Delete(outfileName + ".shx");
            //        System.IO.File.Delete(outfileName + ".prj");
            //        System.IO.File.Delete(outfileName + ".dbf");
            //    }
            //    else
            //    {
            //        goodtoGo = false;
            //    }
            //}
            //if (goodtoGo)
            //{
            //    var outShape = new ShapeFile();
            //    outShape.Open(outfileName + ".shp", _shapeType, true);
            //    foreach (KeyValuePair<string, AttColumn> fld in _attTable._columns)
            //    {
            //        if (!(fieldsToWrite == null))
            //        {
            //            if (fieldsToWrite.Contains(fld.Key))
            //            {
            //                outShape.Fields.Add(fld.Key, fld.Value._efldType, fld.Value._length, fld.Value._decimal);
            //            }
            //        }
            //        else
            //        {
            //            outShape.Fields.Add(fld.Key, fld.Value._efldType, fld.Value._length, fld.Value._decimal);
            //        }
            //    }
            //    outShape.WriteFieldDefs();
            //    // for each features
            //    for (int z = 0, loopTo = _parts.Count - 1; z <= loopTo; z++)
            //    {
            //        var shp = _parts[z];
            //        // for each feature part

            //        for (int i = 0, loopTo1 = shp.Count - 1; i <= loopTo1; i++)
            //        {
            //            // for each vertice in part
            //            foreach (Vertex vert in _vertices[z][i])
            //                outShape.Vertices.Add(vert);
            //            // outShape.SetPartDirection(i, _parts(shp.Key)(i).Direction)
            //            // outShape.Parts(i).IsHole = _parts(shp.Key)(i).IsHole
            //            if (i == shp.Count - 1)
            //                break;
            //            outShape.Vertices.NewPart();
            //        }
            //        foreach (Field fld in outShape.Fields)
            //        {
            //            if (_attTable._columns.ContainsKey(fld.Name))
            //            {
            //                var T = _attTable._columns[fld.Name].getEFldType;
            //                string readval = _attTable.getRowVal(fld.Name, (long)z);
            //                string val = Strings.Left(readval, Math.Min(_attTable._columns[fld.Name]._length + _attTable._columns[fld.Name]._decimal, readval.Length));
            //                try
            //                {
            //                    fld.Value = Conversion.CTypeDynamic(val, T);
            //                }
            //                catch (Exception ex)
            //                {

            //                }
            //            }
            //        }

            //        try
            //        {
            //            outShape.WriteShape();
            //        }
            //        catch (Exception ex)
            //        {
            //            outShape.Vertices.Clear();
            //            outShape.Parts.Clear();
            //        }

            //    }
            //    outShape.Projection = _proj;
            //    GeospatialTools.WritePRJ(outShape, _WKT);
            //    outShape.Close();
            //}
        }
        public void CopyTo(long featInd, ref Feat toFeat, bool addFields)
        {
            long nextInd = toFeat._parts.Count;
            if (addFields)
            {
                foreach (var col in _attTable._columns)
                {
                    if (!toFeat._attTable._columns.ContainsKey(col.Key))
                    {
                        toFeat._attTable._columns.Add(col.Key, new AttColumn(col.Value._Name, col.Value._efldType, col.Value._length, col.Value._decimal));
                    }
                }
            }
            foreach (var col in toFeat._attTable._columns)
            {
                if (_attTable._columns.ContainsKey(col.Key))
                {
                    col.Value._rows.Add(_attTable._columns[col.Key]._rows[(int)featInd]);
                }
            }
            toFeat._parts.Add(_parts[(int)featInd]);
            toFeat._vertices.Add(_vertices[(int)featInd]);

        }
        public void addFeaturePoint(double x, double y, Dictionary<string, object> fieldVals = null)
        {
            long newInd = _parts.Count;
            _parts.Add(new List<Part>());
            _vertices.Add(new List<List<Vertex>>());
            _vertices[(int)newInd].Add(new List<Vertex>());

            var newPoint = new Part(_WKT);
            newPoint.CentroidX = x;
            newPoint.CentroidY = y;
            newPoint.MBRXMax = x;
            newPoint.MBRXMin = x;
            newPoint.MBRYMax = y;
            newPoint.MBRYMin = y;
            _parts[(int)newInd].Add(newPoint);

            var newVertex = new Vertex(x, y);            
            _vertices[(int)newInd][0].Add(newVertex);

            _attTable.AddRow();

            if (!(fieldVals == null))
            {
                foreach (var col in fieldVals)
                {
                    if (_attTable._columns.ContainsKey(col.Key))
                    {
                        _attTable._columns[col.Key].recordVal(col.Value, newInd);
                    }
                }
            }
        }
        public void addFeaturePoly(List<Part> parts, List<List<Vertex>> vertices, Dictionary<string, object> fieldVals = null)
        {
            long newInd = _parts.Count;
            _parts.Add(parts);
            _vertices.Add(vertices);

            _attTable.AddRow();

            if (!(fieldVals == null))
            {
                foreach (var col in fieldVals)
                    _attTable._columns[col.Key].recordVal(col.Value, newInd);
            }
        }
        public void removeFeature(long featInd)
        {
            _parts.RemoveAt((int)featInd);
            _vertices.RemoveAt((int)featInd);
            _attTable.RemoveRow(featInd);
        }

        public List<double[]> getXYcoords
        {
            get
            {
                var xyCoords = new List<double[]>();
                if (_shapeType == GeospatialTools.eShapeType.shpPoint)
                {
                    foreach (var vert in _vertices)
                        xyCoords.Add(new[] { vert[0][0].X_Cord, vert[0][0].Y_Cord });
                }
                else
                {
                    for (int i = 0, loopTo = _parts.Count - 1; i <= loopTo; i++)
                        xyCoords.Add(getPartCentroid(i));
                }
                return xyCoords;
            }
        }
        public List<Part> getOuterRingParts(long ind)
        {
            var partlist = new List<Part>();
            foreach (var part in _parts[(int)ind])
            {
                if (part.IsHole == false)
                {
                    partlist.Add(part);
                }
            }
            return partlist;
        }
        public double[] getPartCentroid(long ind)
        {
            return GeospatialTools.getMultiPartCentroid(getOuterRingParts(ind));
        }
        public double[] getPointXY(long ind)
        {
            return new[] { _vertices[(int)ind][0][0].X_Cord, _vertices[(int)ind][0][0].Y_Cord };
        }
        public double[] getMBR(long index) // {Xmin, Xmax, Ymin, Ymax}
        {
            if (_shapeType == GeospatialTools.eShapeType.shpPoint)
            {
                return new[] { _vertices[(int)index][0][0].X_Cord, _vertices[(int)index][0][0].X_Cord, _vertices[(int)index][0][0].Y_Cord, _vertices[(int)index][0][0].Y_Cord };
            }
            else
            {
                double[] mbr = new[] { double.MaxValue, double.MinValue, double.MaxValue, double.MinValue };
                foreach (Part part in _parts[(int)index])
                {
                    if (part.MBRXMin < mbr[0])
                        mbr[0] = part.MBRXMin;
                    if (part.MBRXMax > mbr[1])
                        mbr[1] = part.MBRXMax;
                    if (part.MBRYMin < mbr[2])
                        mbr[2] = part.MBRYMin;
                    if (part.MBRYMax > mbr[3])
                        mbr[3] = part.MBRYMax;
                }
                return mbr;
            }
        }
        public double[] getPartMBR(int index, int partind)
        {
            if (_shapeType == GeospatialTools.eShapeType.shpPoint)
            {
                return new[] { _vertices[index][0][0].X_Cord, _vertices[index][0][0].X_Cord, _vertices[index][0][0].Y_Cord, _vertices[index][0][0].Y_Cord };
            }
            else
            {
                Part part = _parts[index][partind];
                double[] mbr = new[] { part.MBRXMin, part.MBRXMax, part.MBRYMin, part.MBRYMax };               
                return mbr;
            }
        }
        public void ConstRTree(int minKids, int maxKids)
        {
            _rTree = new RTreeManager(minKids, maxKids);
        }
        public void AddFeatPartToRTree(int featind, int partind)
        {
            double[] PartMBR = getPartMBR(featind, partind);
            if (_rTree != null) { _rTree.addFeature(new int[] { featind, partind }, PartMBR[1], PartMBR[0], PartMBR[3], PartMBR[2]); }
        }
    }
}
