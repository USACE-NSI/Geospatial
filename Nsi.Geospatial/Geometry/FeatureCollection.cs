using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Nsi.Geospatial.Projections;

namespace Nsi.Geospatial.Geometry;

/// <summary>A set of features sharing a common attribute schema.</summary>
public sealed class FeatureCollection
{
  public string? Name { get; set; }
  public ShapeType ShapeType { get; set; }

  /// <summary>The single CRS for everything in this collection.</summary>
  public CrsInfo Crs { get; set; } = Projections.CrsInfo.Unknown;

  public AttributeTable Schema { get; } = new();
  public List<Feature> Features { get; } = new();

  public int Count => Features.Count;
  public Feature this[int index] => Features[index];

  public int AddFeature(Feature feature)
  {
    feature.Owner = this;
    feature.Id = Features.Count;
    Features.Add(feature);
    return feature.Id;
  }

  public void RemoveFeature(int index)
  {
    Features.RemoveAt(index);
    for (int i = index; i < Features.Count; i++)
      Features[i].Id = i;
  }
}

