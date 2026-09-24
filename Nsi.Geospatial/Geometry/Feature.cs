using System.Globalization;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Projections;

namespace Nsi.Geospatial.Geometry;

/// <summary>
/// A single feature: geometry (parts + MBR) *and* its attribute row, held together.
/// Replaces the old Feat parallel lists (_parts[i]/_vertices[i]/row i), where any add
/// or remove on one list silently desynced the others.
/// </summary>
public sealed class Feature
{
  public int Id { get; set; }
  public ShapeType ShapeType { get; set; }
  public string? Wkt { get; set; }
  public string? Path { get; set; }
  public string? Name { get; set; }
  private double _centroidX;
  private double _centroidY;
  private bool _measured = false;

  /// <summary>Set by FeatureCollection.AddFeature.</summary>
  internal Features? Owner
  {
    get => _owner;
    set
    {
      if (ReferenceEquals(_owner, value))
        return;
      _owner = value;
      foreach (var p in Parts)
        p.InvalidateMetrics();
    }
  }
  private Features? _owner;

  /// <summary>The dependable CRS source for this feature's geometry.</summary>
  public CrsInfo Crs => Owner?.Crs ?? Projections.CrsInfo.Unknown;
  public List<Part> Parts { get; } = new();
  public BoundingBox BoundingBox { get; private set; } = BoundingBox.Empty;

  /// <summary>This feature's attribute values, keyed by column name. Null-safe.</summary>
  public Dictionary<string, object?> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

  public Feature(int id = 0) => Id = id;

  public void AddPart(Part part)
  {
    part.Owner = this;
    Parts.Add(part);
    BoundingBox = BoundingBox.Union(part.BoundingBox);
  }

  public BoundingBox ComputeBoundingBox()
  {
    BoundingBox = BoundingBox.Empty;
    foreach (var p in Parts)
      BoundingBox = BoundingBox.Union(p.BoundingBox);
    return BoundingBox;
  }

  public T? GetAttribute<T>(string name)
  {
    if (!Attributes.TryGetValue(name, out var raw) || raw is null)
      return default;
    if (raw is T typed)
      return typed;
    return (T)Convert.ChangeType(raw, typeof(T), CultureInfo.InvariantCulture);
  }

  public string GetAttributeAsString(string name) =>
    GetAttribute<object?>(name)?.ToString() ?? string.Empty;

  /// <summary>
  /// Area in square metres: the FIRST part not flagged IsHole, minus every part flagged
  /// IsHole. Null when there is no such part, when the CRS is unknown, or when any part
  /// that contributes has an unknown area -- Part documents null as a real answer and this
  /// member does not turn it into a number.
  /// Parts beyond the first unflagged one that are not holes contribute nothing: a flat
  /// Parts list cannot say which exterior a hole belongs to, so a feature with two shells
  /// reports one shell and is not detectably wrong here. That is a decision, not an
  /// oversight, and FeatureAreaTests row 1 (nothing flagged -> 6400) is the only place the
  /// behaviour is observable.
  /// </summary>
  public double? AreaSquareMeters
  {
    get
    {
      if (Parts.Count == 0 || Crs.Kind == Projections.CrsKind.Unknown)
        return null;

      int shell = -1;
      for (int i = 0; i < Parts.Count; i++)
      {
        if (!Parts[i].IsHole)
        {
          shell = i;
          break;
        }
      }
      if (shell < 0)
        return null;

      double? total = Parts[shell].AreaSquareMeters;
      if (total is null)
        return null;

      for (int i = 0; i < Parts.Count; i++)
      {
        if (i == shell || !Parts[i].IsHole)
          continue;
        double? hole = Parts[i].AreaSquareMeters;
        if (hole is null)
          return null;
        total -= hole.Value;
      }
      return total;
    }
  }
  private void EnsureMeasured() //Do we want to only do this feature level measurement when called by spatial join, or is it more efficient to do it when parts are measured?
  {
    if(_measured == false)
    {
      if (Parts.Count == 0) //|| Crs.Kind == Projections.CrsKind.Unknown)
      {        
        return;
      }
      switch (ShapeType)
      {
        case ShapeType.Line:
          double sumL = 0;
          double sumCxL = 0;
          double sumCyL = 0;         
          foreach (var part in Parts)
          {
            double L = part.LengthMeters ?? 0;
            sumL += L;
            sumCxL += L * part.CentroidX;
            sumCyL += L * part.CentroidY;           
          }
          if (sumL == 0)
          {
            _centroidX = 0;
            _centroidY = 0;
          }
          else
          {
            _centroidX = sumCxL / sumL;
            _centroidY = sumCyL / sumL;
          }         
          break;

        case ShapeType.Point:
        case ShapeType.PointM:
          _centroidX = Parts[0].CentroidX;
          _centroidY = Parts[0].CentroidY;
          break;

        case ShapeType.Polygon:
          double sumA = 0;
          double sumCxA = 0;
          double sumCyA = 0;          
          foreach (var part in Parts)
          {
            double A = (part.Area ?? 0) * (part.IsHole == true ? -1d : 1d);  //Zero as only used for weighting. Assign - if hole
            sumA += A;
            sumCxA += A * part.CentroidX;
            sumCyA += A * part.CentroidY;            
          }
          if(sumA == 0) //Assumes area > 0, which should be true as hole area cannot exceed area of container, but may need some check beyond just div/0
          {
            _centroidX = 0;
            _centroidY = 0;
          }
          else
          {
            _centroidX = sumCxA / sumA;
            _centroidY = sumCyA / sumA;
          }         
          break;
      }
     
      _measured = true;
    }    
  }
  public double CentroidX
  {
    get
    {
      EnsureMeasured();
      return _centroidX;
    }
  }
  public double CentroidY
  {
    get
    {
      EnsureMeasured();
      return _centroidY;
    }
  }
  public Vertex Centroid
  {
    get
    {
      EnsureMeasured();
      return new Vertex(_centroidX, _centroidY);
    }
  }
}

