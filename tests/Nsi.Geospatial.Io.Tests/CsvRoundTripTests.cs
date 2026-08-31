using Nsi.Geospatial.Io;
using Xunit;

namespace Nsi.Geospatial.Io.Tests;

public class CsvRoundTripTests
{
    [Fact]
    public void WriteThenRead_QuotingPreserved()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            CsvHelper.Write(tmp,
                headers: ["name", "value"],
                rows: new[]
                {
                    new[] { "plain", "1" },
                    new[] { "with,comma", "2" },
                    new[] { "with\"quote", "3" },
                });

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