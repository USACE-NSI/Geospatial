using Nsi.Geospatial.Enums;

namespace Nsi.Geospatial.Geometry;

/// <summary>
/// A single feature: geometry (parts + MBR) *and* its attribute row, held together.
/// fix: replaces the old Feat parallel lists (_parts[i]/_vertices[i]/row i) where
/// any add/remove on one list silently desynced the others.
/// </summary>
public sealed class Feature
{
  public int Id { get; set; }
  public ShapeType ShapeType { get; set; }
  public string? Wkt { get; set; }
  public string? Path { get; set; }
  public string? Name { get; set; }

  public List<Part> Parts { get; } = new();
  public BoundingBox BoundingBox { get; private set; } = BoundingBox.Empty;

  /// <summary>This feature's attribute values, keyed by column name. Null-safe.</summary>
  public Dictionary<string, object?> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

  public Feature(int id = 0) => Id = id;

  public void AddPart(Part part)
  {
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
    return (T)Convert.ChangeType(raw, typeof(T));
  }

  public string GetAttributeAsString(string name) =>
    GetAttribute<object?>(name)?.ToString() ?? string.Empty;
}
