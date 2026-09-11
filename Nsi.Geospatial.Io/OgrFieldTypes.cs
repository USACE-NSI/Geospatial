using Nsi.Geospatial.Enums;

namespace Nsi.Geospatial.Io;

/// <summary>
/// The only two places Nsi's FieldType and OSGeo.OGR.FieldType are related (P-65). Both
/// directions sit side by side on purpose: P-06 was LongFT present in one direction and absent
/// from the other, which round-trips as a column of a different type and is invisible to any
/// test that reads back what it wrote with the same gap.
/// No `using OSGeo.OGR;` here, so both FieldType spellings are qualified. Io resolves bare
/// names against enclosing namespaces before file-level usings, which is what makes bare
/// Geometry a CS0118 and bare Feature OGR's rather than ours.
/// </summary>
internal static class OgrFieldTypes
{
  /// <summary>
  /// OGR to ours. The catch-all is load-bearing, not a shortcut: OGR's type set is open (the
  /// list types, OFTTime, OFTBinary) and ours is closed, so something has to absorb them. What
  /// it does is D-F's undecided question, answered silently as "text". If D-F becomes "refuse",
  /// this single line is where it changes.
  /// VERIFY these arms against SpatialReader.MapFieldType before deleting that method. Its body
  /// is the one part of this I have not read -- every provider cuts that file short. If it
  /// mapped OFTDateTime, or anything else, to a type other than TextFT, keep the arm: dropping
  /// one silently turns a real date column into text, and no existing test would notice.
  /// </summary>
  internal static FieldType ToFieldType(OSGeo.OGR.FieldType t) =>
    t switch
    {
      OSGeo.OGR.FieldType.OFTInteger => FieldType.IntegerFT,
      OSGeo.OGR.FieldType.OFTInteger64 => FieldType.LongFT,
      OSGeo.OGR.FieldType.OFTReal => FieldType.DoubleFT,
      OSGeo.OGR.FieldType.OFTDate => FieldType.DateFT,
      _ => FieldType.TextFT,
    };

  /// <summary>
  /// Ours to OGR. Exhaustive over a closed enum, no default arm, so a new FieldType member
  /// cannot hide behind OFTString the way LongFT did. The `// new` that used to sit on the
  /// LongFT line in SpatialWriter is that scar; it goes with the method, since
  /// FieldTypeTests.LongColumnIsDeclaredInteger64OnDisk now guards the arm on disk.
  /// </summary>
  internal static OSGeo.OGR.FieldType ToOgrFieldType(FieldType t) =>
    t switch
    {
      FieldType.IntegerFT => OSGeo.OGR.FieldType.OFTInteger,
      FieldType.LongFT => OSGeo.OGR.FieldType.OFTInteger64,
      FieldType.DoubleFT or FieldType.FloatFT or FieldType.SingleFT or FieldType.NumericFT => OSGeo
        .OGR
        .FieldType
        .OFTReal,
      FieldType.DateFT => OSGeo.OGR.FieldType.OFTDate,
      FieldType.TextFT => OSGeo.OGR.FieldType.OFTString,
      // OFTString because that is what it writes today. Not "fixed" here: the value side
      // writes "1"/"0" and the read side uses bool.TryParse, which accepts neither. That
      // pair is P-21's row and moves together or not at all.
      FieldType.BooleanFT => OSGeo.OGR.FieldType.OFTString,
      _ => throw new ArgumentOutOfRangeException(
        nameof(t),
        t,
        "FieldType has no OGR field type; add it here rather than falling through to OFTString."
      ),
    };
}
