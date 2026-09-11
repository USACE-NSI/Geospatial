using System.Globalization;
using Nsi.Geospatial.Enums;

namespace Nsi.Geospatial.Attributes;

/// <summary>
/// The one place FieldType is related to a CLR type, and the one place a value is coerced to its
/// declared FieldType (P-65). Six mappings used to be hand-maintained across four files, which is
/// how P-06 happened: LongFT reached the enum and three of the six kept its absence as a silent
/// default. AttributeColumn's two members and both MapFieldType methods now route here.
/// SpatialReader keeps one per-type switch, ReadFieldValue, and should: it selects which OGR
/// accessor to call rather than translating between enums. That is the surviving seventh mapping.
/// Coercion is deliberately still a ToString/parse round trip -- that is what applies
/// DecimalPlaces rounding to a value that arrived already typed. Replacing it is its own change.
/// </summary>
internal static class FieldTypes
{
  /// <summary>
  /// CLR type a column of this FieldType holds. No default arm: FieldType is ours and closed, so
  /// every member is named and adding one fails analysis instead of quietly answering
  /// typeof(object). A non-member value throws where the old FieldTypeToType returned
  /// typeof(object) -- reachable only by a cast. DateFT is declared as DateTime while
  /// SpatialReader hands back a string for the same column; that disagreement is unresolved and
  /// tracked, and ClrType currently sides with Coerce.
  /// </summary>
  internal static Type ClrType(FieldType t) =>
    t switch
    {
      FieldType.BooleanFT => typeof(bool),
      FieldType.DateFT => typeof(DateTime),
      FieldType.DoubleFT => typeof(double),
      // Not typeof(float): Coerce widens to double (no Math.Round(float, int)) and the
      // writer casts float to double before SetField, so nothing ever holds a float.
      FieldType.SingleFT => typeof(double),
      FieldType.NumericFT => typeof(double),
      FieldType.IntegerFT => typeof(int),
      FieldType.LongFT => typeof(long),
      FieldType.TextFT => typeof(string),
      _ => throw new ArgumentOutOfRangeException(
        nameof(t),
        t,
        "FieldType has no CLR type; add it here rather than falling through."
      ),
    };

  /// <summary>
  /// Coerce raw to a column's declared type: text truncated to length, numerics rounded to
  /// decimalPlaces. Unparseable input yields null -- the same silent loss as CoerceRow's
  /// unknown-key filter (P-09, D-F), named here and not decided here.
  /// </summary>
  internal static object? Coerce(object? raw, FieldType t, int length, int decimalPlaces)
  {
    if (raw is null)
    {
      return null;
    }

    // Invariant first. A DateTime or double landing in a TextFT column used to render in the
    // host culture, so the same Features object wrote different bytes on different machines.
    // bool is not IFormattable, so "True"/"False" is unaffected (see P-21 for the "1"/"0"
    // mismatch on the write side).
    string text = raw is IFormattable formattable
      ? formattable.ToString(null, CultureInfo.InvariantCulture)
      : raw.ToString()!;

    // NumberStyles.Float alone, NOT Float | AllowThousands. The default mask treats "," as a
    // group separator, so "1,5" parses as fifteen: keeping it corrupts a decimal comma instead
    // of failing. Cost of that choice is "1,000" now being null rather than 1000.
    NumberFormatInfo culture = NumberFormatInfo.InvariantInfo;
    const NumberStyles Real = NumberStyles.Float;
    const NumberStyles Integral = NumberStyles.Integer;

    switch (t)
    {
      case FieldType.TextFT:
        // Zero length means "width unknown", which is what GeoJSON and CSV report, not
        // "store nothing". Truncating to it would blank every text value on a round trip.
        return length > 0 && text.Length > length ? text[..length] : text;
      case FieldType.DoubleFT:
      case FieldType.FloatFT:
      case FieldType.NumericFT:
        return double.TryParse(text, Real, culture, out double d) ? Round(d, decimalPlaces) : null;
      case FieldType.SingleFT:
        // There is no Math.Round(float, int), so this widens and boxes a double -- which
        // is what the pre-consolidation Coerce did, and why ClrType says double here
        // rather than typeof(float). See the SingleFT note in the commit message.
        return float.TryParse(text, Real, culture, out float s) ? Round(s, decimalPlaces) : null;
      case FieldType.IntegerFT:
        return int.TryParse(text, Integral, culture, out int i) ? i : null;
      case FieldType.LongFT:
        return long.TryParse(text, Integral, culture, out long l) ? l : null;
      case FieldType.BooleanFT:
        return bool.TryParse(text, out bool b) ? b : null;
      case FieldType.DateFT:
        // Boxes a DateTime, while SpatialReader hands back a string for the same column
        // (its GetFieldAsDateTime is void in this binding). Unresolved: pick one and make
        // ClrType, this arm and the reader agree in the same commit.
        return DateTime.TryParse(text, culture, DateTimeStyles.None, out DateTime dt) ? dt : null;
      default:
        // No fallthrough to text. Silent text is the mechanism that hid LongFT.
        throw new ArgumentOutOfRangeException(
          nameof(t),
          t,
          "FieldType has no coercion; add a case rather than returning text."
        );
    }
  }

  /// <summary>
  /// Math.Round throws outside 0..15, and DecimalPlaces arrives straight from defn.GetPrecision()
  /// on the read path, so an unusual precision would throw inside Write. Rounding at 0 is kept --
  /// it is what the pre-consolidation Coerce did.
  /// </summary>
  private static double Round(double value, int decimalPlaces) =>
    decimalPlaces is >= 0 and <= 15 ? Math.Round(value, decimalPlaces) : value;
}

