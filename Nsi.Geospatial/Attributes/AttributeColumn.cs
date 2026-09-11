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
  public object? Coerce(object? raw) => FieldTypes.Coerce(raw, FieldType, Length, DecimalPlaces);

  public static Type FieldTypeToType(FieldType t) => FieldTypes.ClrType(t);
}
