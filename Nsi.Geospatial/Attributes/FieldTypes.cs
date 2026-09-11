using System.Globalization;
using Nsi.Geospatial.Enums;

namespace Nsi.Geospatial.Attributes;

/// <summary>
/// The one place FieldType is related to a CLR type, and the one place it is used to coerce a
/// value (P-65). This mapping used to be maintained in six spots across four files, which is how
/// P-06 happened: LongFT reached the enum and three of the six kept its absence as a silent
/// default. AttributeColumn's two members, both of SpatialReader's, SpatialWriter's and
/// AttributeTable.CoerceRow all route here now, so an omission is a gap at a call site rather
/// than a wrong answer at a boundary.
/// Coercion is deliberately still a ToString/parse round trip. That is what makes rounding to
/// DecimalPlaces apply to a value that arrived already typed, and replacing it is its own
/// behaviour change, not part of a move.
/// </summary>
internal static class FieldTypes
{
  /// <summary>
  /// CLR type a column of this FieldType holds. No default arm: FieldType is ours and closed,
  /// so every member is named and adding one makes this fail to compile-clean instead of
  /// quietly answering typeof(object). A non-member value throws where the old FieldTypeToType
  /// returned typeof(object) -- reachable only by a cast, and the first observable difference
  /// in the move.
  /// </summary>
  internal static Type ClrType(FieldType t) =>
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
      _ => throw new ArgumentOutOfRangeException(
        nameof(t),
        t,
        "FieldType has no CLR type; add it here rather than falling through."
      ),
    };

  /// <summary>
  /// Coerce raw to a column's declared type: text truncated to length, numerics rounded to
  /// decimalPlaces. Unparseable input yields null, which is the same silent loss that
  /// CoerceRow's unknown-key filter is (P-09, D-F) -- named here, not decided here.
  /// </summary>
  internal static object? Coerce(object? raw, FieldType t, int length, int decimalPlaces)
  {
    if (raw is null)
    {
      return null;
    }

    string text = raw.ToString()!;

    // InvariantCulture, everywhere. The six calls this replaces used the host culture, so
    // "1,5" and "1.5" were different numbers on different machines -- P-19's defect with six
    // instances. AllowThousands is kept because double.TryParse's own default includes it.
    NumberFormatInfo culture = NumberFormatInfo.InvariantInfo;
    const NumberStyles Real = NumberStyles.Float | NumberStyles.AllowThousands;
    const NumberStyles Integral = NumberStyles.Integer;

    switch (t)
    {
      case FieldType.TextFT:
        return text.Length <= length ? text : text[..length];
      case FieldType.DoubleFT:
      case FieldType.FloatFT:
      case FieldType.NumericFT:
        return double.TryParse(text, Real, culture, out double d)
          ? Math.Round(d, decimalPlaces)
          : null;
      case FieldType.SingleFT:
        // Math.Round(float, int) exists, so this still boxes a float and agrees with
        // ClrType. Verify the boxed type with a test rather than trusting that.
        return float.TryParse(text, Real, culture, out float s)
          ? Math.Round(s, decimalPlaces)
          : null;
      case FieldType.IntegerFT:
        return int.TryParse(text, Integral, culture, out int i) ? i : null;
      case FieldType.LongFT:
        return long.TryParse(text, Integral, culture, out long l) ? l : null;
      case FieldType.BooleanFT:
        // bool.TryParse accepts "True"/"False" and nothing else, while SpatialWriter
        // writes "1"/"0" into an OFTString column. Left alone -- that mismatch is P-21's
        // row and needs both ends moved at once, not one arm here.
        return bool.TryParse(text, out bool b) ? b : null;
      case FieldType.DateFT:
        // Boxes a DateTime, while SpatialReader hands back a string for the same column
        // (its GetFieldAsDateTime is void in this binding). The disagreement is real and
        // still here; P-65's job is to put both halves where one read sees them.
        return DateTime.TryParse(text, culture, DateTimeStyles.None, out DateTime dt) ? dt : null;
      default:
        // No fallthrough to text. Silent text was the mechanism that hid LongFT.
        throw new ArgumentOutOfRangeException(
          nameof(t),
          t,
          "FieldType has no coercion; add a case rather than returning text."
        );
    }
  }
}
