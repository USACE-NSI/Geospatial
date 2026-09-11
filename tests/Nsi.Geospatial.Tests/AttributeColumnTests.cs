using System.Globalization;
using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Xunit;

namespace Nsi.Geospatial.Tests;

public class AttributeColumnTests
{
  /// <summary>
  /// P-65: the six TryParse calls this consolidates used the host culture. Asserted without
  /// mutating CurrentCulture, because xunit parallelises across assemblies and Io.Tests'
  /// parallelisation guard does not cover this one (P-12). On the en-US CI host the old code
  /// parsed "1,5" as fifteen via the default AllowThousands mask, so the null assertion fails
  /// before the change and passes after, on any host.
  /// </summary>
  [Fact]
  public void NumericCoercionIsCultureIndependent()
  {
    var column = new AttributeColumn("N", FieldType.DoubleFT, length: 0, decimalPlaces: 1);

    Assert.Equal(1.5, Assert.IsType<double>(column.Coerce("1.5")));
    Assert.Null(column.Coerce("1,5"));

    var whole = new AttributeColumn("I", FieldType.IntegerFT, length: 0, decimalPlaces: 0);
    Assert.Equal(
      4000000000,
      Assert.IsType<long>(new AttributeColumn("L", FieldType.LongFT, 0, 0).Coerce("4000000000"))
    );
    Assert.Null(whole.Coerce("1.5"));
  }

  /// <summary>
  /// Coerce's boxed type must be the type the column declares, or ClrType is decoration.
  /// Asserted as an agreement rather than a hard-coded type so the test can't be satisfied by
  /// changing whichever half is easier. This is what caught SingleFT boxing a double while
  /// CsharpType promised a float.
  /// </summary>
  [Theory]
  [InlineData(FieldType.IntegerFT, "7")]
  [InlineData(FieldType.LongFT, "4000000000")]
  [InlineData(FieldType.DoubleFT, "1.5")]
  [InlineData(FieldType.SingleFT, "1.5")]
  [InlineData(FieldType.TextFT, "abc")]
  [InlineData(FieldType.BooleanFT, "true")]
  public void CoerceBoxesWhatTheColumnDeclares(FieldType type, string raw)
  {
    var column = new AttributeColumn("C", type, length: 8, decimalPlaces: 1);
    object? value = column.Coerce(raw);

    Assert.NotNull(value);
    Assert.Equal(column.CsharpType, value.GetType());
  }
}

