using Nsi.Geospatial.Attributes;
using Nsi.Geospatial.Enums;
using Xunit;

namespace Nsi.Geospatial.Tests;

public class AttributeTableTests
{
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

