using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlexGeospatial
{
    public class Part
    {
        public double MBRXMin { get; set; }
        public double MBRXMax { get; set; }
        public double MBRYMin { get; set; }
        public double MBRYMax { get; set; }
        public bool IsHole { get; set; }
        public double CentroidX { get; set; }
        public double CentroidY { get; set; }
        public double Area { get; set; }
        public int Begins { get; set; }
        public int Ends { get; set; }
        public bool Direction { get; set; }
        public double Perimeter { get; set; }
        public string _WKT = "";
        public List<Vertex> Vertices { get; set; } = new List<Vertex>();


        public Part(string WKT)
        {
            _WKT = WKT;
        }
        public void AddVertex(Vertex vertex)
        {
            //add perimeter if there is already a vertex in the list, otherwise perimeter is 0
            if (Vertices.Count > 0)
            {
                Perimeter += GeospatialTools.getFtDistBetweenPts(Vertices.LastOrDefault().getPoints, vertex.getPoints, _WKT, _WKT);
            }
            else
            {
                if(Direction == true) //if clockwise
                {
                    Begins = 0;
                    IsHole = false;
                }
                else
                {
                    Ends = 0;
                    IsHole = true;
                }
            }

            //Add vertex
            Vertices.Add(vertex);

            //update MBR values based on the new vertex coordinates
            if (vertex.X_Cord < MBRXMin || Vertices.Count == 1) { MBRXMin = vertex.X_Cord; }
            if(vertex.X_Cord > MBRXMax || Vertices.Count == 1) { MBRXMax = vertex.X_Cord; } 
            if(vertex.Y_Cord < MBRYMin || Vertices.Count == 1) { MBRYMin = vertex.Y_Cord; }
            if(vertex.Y_Cord > MBRYMax || Vertices.Count == 1) { MBRYMax = vertex.Y_Cord; }
        }
        public void closeRing()
        {
            //Set Ends to the last vertex index
            Ends = Vertices.Count - 1; //Need to come back and make this capable of handling counter-clockwise rings and holes. For now, just set Begins to 0 for the first vertex.

            //Compute Centroid
            double[] centroid = GeospatialTools.getCentroid(Vertices);
            CentroidX = centroid[0];
            CentroidY = centroid[1];

            //Compute Area
            Area = GeospatialTools.CalcArea(Vertices, _WKT);
        }
    }
}
