using System.Globalization;
using Nsi.Geospatial.Geometry;
using Xunit;

namespace Nsi.Geospatial.Tests;

/// <summary>
/// Feature.GetAttribute&lt;T&gt; falls through to Convert.ChangeType, which is why a
/// value-only assertion cannot detect a mis-typed column. Recorded as a test rather than
/// as prose so it cannot rot (P-06's rationale, P-19's one true claim).
/// </summary>
public class FeatureAttributeTests
{
  /// <summary>
  /// The digits are right while the type lies: a LongFT column stored as OFTString yields
  /// a perfectly correct long from GetAttribute&lt;long&gt;, and the boxed value is still a
  /// string. Any test that asserts only the value passes on the broken schema.
  /// </summary>
  [Fact]
  public void GetAttributeCoercesSoAValueOnlyAssertionCannotDetectAMistypedColumn()
  {
    var f = new Feature(1);
    f.Attributes["BIG"] = "4000000000";

    Assert.Equal(4_000_000_000L, f.GetAttribute<long>("BIG")); // right answer...
    Assert.IsType<string>(f.Attributes["BIG"]); // ...from the wrong declared type
    Assert.IsNotType<long>(f.Attributes["BIG"]);
  }

  [Fact]
  public void GetAttributeWidensAnIntToLongWithoutLosingTheValue()
  {
    var f = new Feature(1);
    f.Attributes["SMALL"] = 7;

    Assert.Equal(7L, f.GetAttribute<long>("SMALL"));
    Assert.IsType<int>(f.Attributes["SMALL"]); // the reader's Integer/Integer64 split holds
  }

  [Fact]
  public void MissingAndNullColumnsYieldDefaultsRatherThanThrowing()
  {
    var f = new Feature(1);
    f.Attributes["NULLY"] = null;

    Assert.Equal(0, f.GetAttribute<int>("absent"));
    Assert.Null(f.GetAttribute<string>("absent"));
    Assert.Equal(0, f.GetAttribute<int>("NULLY"));
    Assert.Null(f.GetAttribute<string>("NULLY"));
    Assert.Equal(string.Empty, f.GetAttributeAsString("absent"));
    Assert.Equal(string.Empty, f.GetAttributeAsString("NULLY"));
  }

  [Fact]
  public void AttributesAreMatchedCaseInsensitively()
  {
    var f = new Feature(1);
    f.Attributes["BIG"] = 7;

    Assert.Equal(7, f.GetAttribute<int>("big"));
    Assert.Equal(7, f.GetAttribute<int>("BiG"));
  }

  /// <summary>
  /// P-19. GetAttribute passes CultureInfo.InvariantCulture explicitly. Under de-DE, "1.5"
  /// parses as 15 (period is the group separator), so a dropped culture argument turns
  /// this red instead of silently shifting every decimal in a locale.
  /// </summary>
  [Fact]
  public void GetAttributeParsesStringsInvariantlyRegardlessOfCurrentCulture()
  {
    var saved = CultureInfo.CurrentCulture;
    try
    {
      CultureInfo.CurrentCulture = new CultureInfo("de-DE");
      var f = new Feature(1);
      f.Attributes["D"] = "1.5";

      Assert.Equal(1.5, f.GetAttribute<double>("D"));
    }
    finally
    {
      CultureInfo.CurrentCulture = saved;
    }
  }

  /// <summary>
  /// Feature.AreaSquareMeters guards on Parts.Count == 0 and on an unknown CRS. Both are
  /// reachable with a bare Feature, because Owner is internal and a detached feature
  /// resolves Crs to CrsInfo.Unknown. The last assertion is the one that matters beyond
  /// this file: a zero-part feature carries exactly the box addFeature refuses (T-18),
  /// which is P-39 step 1's premise -- previously carried here as "confirm with one grep".
  /// </summary>
  [Fact]
  public void AreaOfAnEmptyOrUnreferencedFeatureIsUnknown()
  {
    var f = new Feature(1);

    Assert.Empty(f.Parts);
    Assert.Null(f.AreaSquareMeters);
    Assert.Equal(Nsi.Geospatial.Projections.CrsKind.Unknown, f.Crs.Kind);
    Assert.Equal(BoundingBox.Empty, f.BoundingBox);
  }
}
