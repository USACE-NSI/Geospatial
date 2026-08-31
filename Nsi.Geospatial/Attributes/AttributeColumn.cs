using Nsi.Geospatial.Enums;

namespace Nsi.Geospatial.Attributes;

public sealed class AttributeColumn
{
    public string Name { get; }
    public FieldType FieldType { get; }
    public int Length { get; }
    public int DecimalPlaces { get; }

    public AttributeColumn(string name, FieldType fieldType, int length, int decimalPlaces)
    {
        Name = name;
        FieldType = fieldType;
        Length = length;
        DecimalPlaces = decimalPlaces;
    }

    public Type CsharpType => FieldTypeToType(FieldType);

    /// <summary>
    /// Coerce an incoming value to this column's type, truncating text to Length and
    /// rounding numerics to DecimalPlaces.
    /// fix(#9): null input now yields null instead of throwing NullReferenceException
    /// (the old recordVal called val.ToString() unconditionally).
    /// </summary>
    public object? Coerce(object? raw)
    {
        if (raw is null) return null;
        string text = raw.ToString()!;

        switch (FieldType)
        {
            case FieldType.Text:
                return text.Length <= Length ? text : text[..Length];
            case FieldType.Double:
            case FieldType.Float:
            case FieldType.Numeric:
                return double.TryParse(text, out var d) ? Math.Round(d, DecimalPlaces) : null;
            case FieldType.Single:
                return Single.TryParse(text, out var s) ? Math.Round(s, DecimalPlaces) : null;
            case FieldType.Integer:
                return int.TryParse(text, out var i) ? i : null;
            case FieldType.Long:
                return long.TryParse(text, out var l) ? l : null;
            case FieldType.Boolean:
                return bool.TryParse(text, out var b) ? b : null;
            case FieldType.Date:
                return DateTime.TryParse(text, out var dt) ? dt : null;
            default:
                return text;
        }
    }

    public static Type FieldTypeToType(FieldType t) => t switch
    {
        FieldType.Boolean => typeof(bool),
        FieldType.Date => typeof(DateTime),
        FieldType.Double => typeof(double),
        FieldType.Float => typeof(double),
        FieldType.Numeric => typeof(double),
        FieldType.Integer => typeof(int),
        FieldType.Long => typeof(long),
        FieldType.Single => typeof(float),
        FieldType.Text => typeof(string),
        _ => typeof(object),
    };
}