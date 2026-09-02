using System.Globalization;
using System.Text;

namespace Nsi.Geospatial.Io;

/// <summary>
/// Minimal correct CSV: proper quoting for embedded commas/quotes/newlines.
/// fix(#10): the old FindUniques was replaced by Distinct; fix(#11): the old
/// ReadCSVtoDict split on "," and would NRE on the trailing null read — both gone.
/// </summary>
public static class CsvHelper
{
  public static void Write(
    string path,
    IEnumerable<string> headers,
    IEnumerable<IEnumerable<string?>> rows
  )
  {
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir))
      Directory.CreateDirectory(dir);

    using var sw = new StreamWriter(path, append: false, Encoding.UTF8);
    sw.WriteLine(string.Join(",", headers.Select(Escape)));
    foreach (var row in rows)
      sw.WriteLine(string.Join(",", row.Select(v => Escape(v ?? string.Empty))));
  }

  public static List<string> ReadUniqueColumn(string path, int columnIndex, bool hasHeaders = false)
  {
    var lines = ReadAll(path, hasHeaders);
    return lines
      .Select(l => columnIndex < l.Count ? l[columnIndex] : null)
      .Where(v => !string.IsNullOrEmpty(v))
      .Select(v => v!)
      .Distinct(StringComparer.OrdinalIgnoreCase)
      .ToList();
  }

  private static List<List<string?>> ReadAll(string path, bool hasHeaders)
  {
    var result = new List<List<string?>>();
    using var sr = new StreamReader(path);
    string? line;
    int lineNo = 0;
    while ((line = sr.ReadLine()) is not null)
    {
      lineNo++;
      if (hasHeaders && lineNo == 1)
        continue;
      result.Add(ParseLine(line));
    }
    return result;
  }

  private static List<string?> ParseLine(string line)
  {
    var fields = new List<string?>();
    var sb = new StringBuilder();
    bool inQuotes = false;
    for (int i = 0; i < line.Length; i++)
    {
      char c = line[i];
      if (inQuotes)
      {
        if (c == '"')
        {
          if (i + 1 < line.Length && line[i + 1] == '"')
          {
            sb.Append('"');
            i++;
          }
          else
            inQuotes = false;
        }
        else
          sb.Append(c);
      }
      else
      {
        switch (c)
        {
          case '"':
            inQuotes = true;
            break;
          case ',':
            fields.Add(sb.ToString());
            sb.Clear();
            break;
          default:
            sb.Append(c);
            break;
        }
      }
    }
    fields.Add(sb.ToString());
    return fields;
  }

  private static string Escape(string value) =>
    value is null ? string.Empty
    : (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
      ? "\"" + value.Replace("\"", "\"\"") + "\""
    : value;
}
