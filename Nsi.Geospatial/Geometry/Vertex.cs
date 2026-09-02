namespace Nsi.Geospatial.Geometry;

public readonly struct Vertex
{
  public double X { get; }
  public double Y { get; }
  public double Z { get; }

  public Vertex(double x, double y, double z = 0)
  {
    X = x;
    Y = y;
    Z = z;
  }

  public (double X, double Y, double Z) Coordinates => (X, Y, Z);

  public override string ToString() => $"({X}, {Y}, {Z})";
}
