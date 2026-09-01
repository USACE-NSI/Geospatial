using Nsi.Geospatial.Enums;

namespace Nsi.Geospatial.Attributes;

/// <summary>
/// The attribute *schema* (columns + order). Row data lives on each Feature,
/// so the table can no longer drift out of sync with geometry (see Feature).
/// </summary>
public sealed class AttributeTable
{
    private readonly Dictionary<string, AttributeColumn> _columns = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> ColumnNames => _columns.Keys;

    public AttributeColumn this[string name] => _columns[name];

    public bool HasColumn(string name) => _columns.ContainsKey(name);

    public void AddField(string name, FieldType type, int length, int decimalPlaces)
    {
        if (_columns.TryAdd(name, new AttributeColumn(name, type, length, decimalPlaces)))
        {
            // no-op for backfill: rows now live per-feature.
        }
    }

    public void RemoveField(string name) => _columns.Remove(name);

    public void RenameColumn(string from, string to)
    {
        if (!_columns.TryGetValue(from, out var col)) return;
        _columns.Remove(from);
        col.Name = to;
        _columns.Add(to, col);
    }

    public void Reorder(Dictionary<string, int> order)
    {
        var sorted = _columns
            .OrderBy(c => order.TryGetValue(c.Key, out var v) ? v : int.MaxValue)
            .ToDictionary(c => c.Key, c => c.Value, StringComparer.OrdinalIgnoreCase);
        _columns.Clear();
        foreach (var kv in sorted) _columns[kv.Key] = kv.Value;
    }

    /// <summary>Coerce a raw row value for <paramref name="column"/>.</summary>
    public object? Coerce(string column, object? value) => _columns[column].Coerce(value);

    public Dictionary<string, object?> CoerceRow(Dictionary<string, object?> values)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in values)
            if (_columns.ContainsKey(kv.Key))
                result[kv.Key] = _columns[kv.Key].Coerce(kv.Value);
        return result;
    }
}