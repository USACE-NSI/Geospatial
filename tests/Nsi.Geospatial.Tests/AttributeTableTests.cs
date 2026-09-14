using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Xunit;

namespace Nsi.Geospatial.Tests;

public class AttributeTableTests
{
  [Fact]
  public void CoerceNullYieldsNullNotThrow()
  {
    var table = new AttributeTable();
    table.AddField("NAME", FieldType.TextFT, 8, 0);
    Assert.Null(table.Coerce("NAME", null));
  }

  [Fact]
  public void CoerceTextTruncatesToLength()
  {
    var table = new AttributeTable();
    table.AddField("NAME", FieldType.TextFT, 3, 0);
    Assert.Equal("abc", table.Coerce("NAME", "abcdef"));
  }

  [Fact]
  public void CoerceDoubleRoundsToDecimals()
  {
    var table = new AttributeTable();
    table.AddField("NUM", FieldType.DoubleFT, 12, 1);
    Assert.Equal(3.1, table.Coerce("NUM", "3.14159"));
  }

  [Fact]
  public void UnknownWidthDoesNotTruncate()
  {
    // FieldTypes' `length > 0` guard: an unwidthed text column keeps its value rather than
    // truncating to "". Pinned because P-09's writer half inherits this convention --
    // "TextFT truncates to Length where a width exists" is only well-defined if length 0
    // means "no width declared" and not "width zero".
    var table = new AttributeTable();
    table.AddField("NAME", FieldType.TextFT, 0, 0);
    Assert.Equal("abcdef", table.Coerce("NAME", "abcdef"));
  }

  [Fact]
  public void RenameColumnMovesKey()
  {
    var table = new AttributeTable();
    table.AddField("OLD", FieldType.IntegerFT, 4, 0);
    table.RenameColumn("OLD", "NEW");
    Assert.False(table.HasColumn("OLD"));
    Assert.True(table.HasColumn("NEW"));
  }
}

