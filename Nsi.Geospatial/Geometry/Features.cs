using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Projections;

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
  public Feature this[int index] => FeatureSet[index];

  public int AddFeature(Feature feature)
  {
    feature.Owner = this;
    feature.Id = FeatureSet.Count;
    FeatureSet.Add(feature);
    return feature.Id;
  }

  public void RemoveFeature(int index)
  {
    FeatureSet.RemoveAt(index);
    for (int i = index; i < FeatureSet.Count; i++)
      FeatureSet[i].Id = i;
  }
}
