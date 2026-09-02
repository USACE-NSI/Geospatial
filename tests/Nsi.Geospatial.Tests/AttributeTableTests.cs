using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Xunit;

namespace Nsi.Geospatial.Core.Tests;

public class AttributeTableTests
{
  [Fact]
  public void CoerceNullYieldsNullNotThrow()
  {
    var table = new AttributeTable();
    table.AddField("NAME", FieldType.Text, 8, 0);
    Assert.Null(table.Coerce("NAME", null));
  }

  [Fact]
  public void CoerceTextTruncatesToLength()
  {
    var table = new AttributeTable();
    table.AddField("NAME", FieldType.Text, 3, 0);
    Assert.Equal("abc", table.Coerce("NAME", "abcdef"));
  }

  [Fact]
  public void CoerceDoubleRoundsToDecimals()
  {
    var table = new AttributeTable();
    table.AddField("NUM", FieldType.Double, 12, 1);
    Assert.Equal(3.1, table.Coerce("NUM", "3.14159"));
  }

  [Fact]
  public void RenameColumnMovesKey()
  {
    var table = new AttributeTable();
    table.AddField("OLD", FieldType.Integer, 4, 0);
    table.RenameColumn("OLD", "NEW");
    Assert.False(table.HasColumn("OLD"));
    Assert.True(table.HasColumn("NEW"));
  }
}
