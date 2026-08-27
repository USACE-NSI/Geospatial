using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlexGeospatial
{
    public class Vertex
    {
        public double X_Cord { get; set; }
        public double Y_Cord { get; set; }
        public double Z_Cord { get; set; }

        public Vertex(double x, double y, double z = 0)
        {
            X_Cord = x; Y_Cord = y; Z_Cord = z;
        }
        public double[] getPoints { get { return new double[] { X_Cord, Y_Cord, Z_Cord }; } }
       
    }
}
