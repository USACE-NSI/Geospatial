using Nsi.Geospatial.Enums;

namespace Nsi.Geospatial.Attributes;

public sealed class AttributeColumn
{
  public string Name { get; set; }
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
    if (raw is null)
      return null;
    string text = raw.ToString()!;

    switch (FieldType)
    {
      case FieldType.TextFT:
        return text.Length <= Length ? text : text[..Length];
      case FieldType.DoubleFT:
      case FieldType.FloatFT:
      case FieldType.NumericFT:
        return double.TryParse(text, out var d) ? Math.Round(d, DecimalPlaces) : null;
      case FieldType.SingleFT:
        return Single.TryParse(text, out var s) ? Math.Round(s, DecimalPlaces) : null;
      case FieldType.IntegerFT:
        return int.TryParse(text, out var i) ? i : null;
      case FieldType.LongFT:
        return long.TryParse(text, out var l) ? l : null;
      case FieldType.BooleanFT:
        return bool.TryParse(text, out var b) ? b : null;
      case FieldType.DateFT:
        return DateTime.TryParse(text, out var dt) ? dt : null;
      default:
        return text;
    }
  }

  public static Type FieldTypeToType(FieldType t) =>
    t switch
    {
      FieldType.BooleanFT => typeof(bool),
      FieldType.DateFT => typeof(DateTime),
      FieldType.DoubleFT => typeof(double),
      FieldType.FloatFT => typeof(double),
      FieldType.NumericFT => typeof(double),
      FieldType.IntegerFT => typeof(int),
      FieldType.LongFT => typeof(long),
      FieldType.SingleFT => typeof(float),
      FieldType.TextFT => typeof(string),
      _ => typeof(object),
    };
}
