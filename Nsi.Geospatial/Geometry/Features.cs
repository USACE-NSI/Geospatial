using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Projections;
using Nsi.Geospatial.Spatial;

namespace Nsi.Geospatial.Geometry;

/// <summary>A set of features sharing a common attribute schema.</summary>
public sealed class Features
{
  public string? Name { get; set; }
  public ShapeType ShapeType { get; set; }

  /// <summary>The single CRS for everything in this collection.</summary>
  public CrsInfo Crs
  {
    get => _crs;
    set
    {
      if (ReferenceEquals(_crs, value))
        return;
      _crs = value;
      foreach (var f in FeatureSet)
      {
        foreach (var p in f.Parts)
          p.InvalidateMetrics();
      }
    }
  }
  private CrsInfo _crs = Projections.CrsInfo.Unknown;

  public AttributeTable Schema { get; } = new();
  public List<Feature> FeatureSet { get; } = new();

  public int Count => FeatureSet.Count;
  public RTreeManager? RTree { get; set; }
  public Feature this[int index] => FeatureSet[index];

  public int AddFeature(Feature feature)
  {
    feature.Owner = this;
    feature.Id = FeatureSet.Count;
    FeatureSet.Add(feature);
    RTree = null; // invalidate RTree
    return feature.Id;
  }

  public void RemoveFeature(int index)
  {
    FeatureSet.RemoveAt(index);
    RTree = null; // invalidate RTree
    for (int i = index; i < FeatureSet.Count; i++)
      FeatureSet[i].Id = i;
  }
  public Feature GetFeature(int index)
  {
    if(FeatureSet.Count > index) { return FeatureSet[index]; }
    return null;
  }
}
