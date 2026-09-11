using Nsi.Geospatial.Enums;
using OgrFieldType = OSGeo.OGR.FieldType;

namespace Nsi.Geospatial.Io;

/// <summary>
/// The one place Nsi's FieldType and OSGeo.OGR.FieldType are related (P-65). Both directions sit
/// side by side on purpose: P-06 was LongFT present in one direction and absent from the other,
/// which round-trips as a column of a different type and is invisible to any test that reads back
/// what it wrote with the same gap.
/// OSGeo.OGR is not imported wholesale, so both FieldType spellings stay qualified. Io resolves
/// bare names against enclosing namespaces before file-level usings, which is what makes bare
/// Geometry a CS0118 and bare Feature OGR's rather than ours.
/// </summary>
internal static class OgrFieldTypes
{
  /// <summary>
  /// OGR to ours. The catch-all is load-bearing, not a shortcut: OGR's type set is open (the
  /// list types, OFTTime, OFTBinary) and ours is closed, so something must absorb them. What it
  /// does is D-F's undecided question, answered silently as "text" -- if D-F becomes "refuse",
  /// this one line is where it changes.
  /// Both date arms were present in the deleted SpatialReader.MapFieldType. Dropping OFTDateTime
  /// demotes a real datetime column to TextFT in the Schema only, because ReadFieldValue
  /// stringifies both -- so it is invisible on read and visible on write. Nothing in the suite
  /// authors a date column, so nothing would have caught it.
  /// </summary>
  internal static FieldType ToFieldType(OgrFieldType t) =>
    t switch
    {
      OgrFieldType.OFTInteger => FieldType.IntegerFT,
      OgrFieldType.OFTInteger64 => FieldType.LongFT,
      OgrFieldType.OFTReal => FieldType.DoubleFT,
      OgrFieldType.OFTDate => FieldType.DateFT,
      OgrFieldType.OFTDateTime => FieldType.DateFT,
      _ => FieldType.TextFT,
    };

  /// <summary>
  /// Ours to OGR. Exhaustive over a closed enum, no default arm, so a new FieldType member
  /// cannot hide behind OFTString the way LongFT did. The `// new` that used to sit on the
  /// LongFT line in SpatialWriter is that scar; it went with the method, since
  /// FieldTypeTests.LongColumnIsDeclaredInteger64OnDisk now guards the arm on disk.
  /// </summary>
  internal static OgrFieldType ToOgrFieldType(FieldType t) =>
    t switch
    {
      FieldType.IntegerFT => OgrFieldType.OFTInteger,
      FieldType.LongFT => OgrFieldType.OFTInteger64,
      FieldType.DoubleFT or FieldType.FloatFT or FieldType.SingleFT or FieldType.NumericFT =>
        OgrFieldType.OFTReal,
      FieldType.DateFT => OgrFieldType.OFTDate,
      FieldType.TextFT => OgrFieldType.OFTString,
      // OFTString because that is what it writes today. Not "fixed" here: the value side
      // writes "1"/"0" and the read side uses bool.TryParse, which accepts neither. That
      // pair is P-21's row and moves together or not at all.
      FieldType.BooleanFT => OgrFieldType.OFTString,
      _ => throw new ArgumentOutOfRangeException(
        nameof(t),
        t,
        "FieldType has no OGR field type; add it here rather than falling through to OFTString."
      ),
    };
}
