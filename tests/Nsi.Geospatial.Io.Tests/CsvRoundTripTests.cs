using Nsi.Geospatial.Io;
using Xunit;

namespace Nsi.Geospatial.Io.Tests;

public class CsvRoundTripTests
{
  private static readonly string[] Headers = ["name", "value"];
  private static readonly string[] PlainRow = ["plain", "1"];
  private static readonly string[] CommaRow = ["with,comma", "2"];
  private static readonly string[] QuoteRow = ["with\"quote", "3"];

  [Fact]
  public void WriteThenReadQuotingPreserved()
  {
    var tmp = Path.GetTempFileName();
    try
    {
      CsvHelper.Write(tmp, headers: Headers, rows: [PlainRow, CommaRow, QuoteRow]);

      var names = CsvHelper.ReadUniqueColumn(tmp, 0, hasHeaders: true);
      Assert.Equal(3, names.Count);
      Assert.Contains("with,comma", names);
      Assert.Contains("with\"quote", names);
    }
    finally
    {
      File.Delete(tmp);
    }
  }
}
