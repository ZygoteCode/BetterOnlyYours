using System.Text;
using OnlyYours.Core.Security.Memory;

namespace OnlyYours.Core.ImportExport;

/// <summary>A cell as a range inside the decoded file, unescaped on demand into secure memory.</summary>
public readonly record struct CsvCell(int Start, int Length, bool Quoted);

/// <summary>
/// RFC 4180 reader over a <see cref="SecureCharBuffer"/>: quoted fields, doubled quotes,
/// embedded newlines, CRLF/LF, BOM, and comma / semicolon / tab delimiters
/// (auto-detected). Cells are exposed as ranges so a password column can be sealed
/// straight from the buffer without becoming a string.
/// </summary>
public sealed class CsvDocument : IDisposable
{
    private readonly SecureCharBuffer _text;

    private CsvDocument(SecureCharBuffer text, char delimiter, List<CsvCell[]> rows)
    {
        _text = text;
        Delimiter = delimiter;
        Rows = rows;
    }

    public char Delimiter { get; }

    public IReadOnlyList<CsvCell[]> Rows { get; }

    public static CsvDocument Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try
        {
            return Parse(bytes);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public static CsvDocument Parse(ReadOnlySpan<byte> utf8)
    {
        if (utf8.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
        {
            utf8 = utf8[3..];
        }

        var text = SecureCharBuffer.FromUtf8(utf8);
        try
        {
            var delimiter = DetectDelimiter(text.Span);
            return new CsvDocument(text, delimiter, Split(text.Span, delimiter));
        }
        catch
        {
            text.Dispose();
            throw;
        }
    }

    public static CsvDocument Parse(string content) => Parse(Encoding.UTF8.GetBytes(content));

    /// <summary>Plain value of a cell (for non-secret columns).</summary>
    public string Text(CsvCell cell)
    {
        using var chars = Value(cell);
        return chars.Span.ToString();
    }

    /// <summary>Unescaped value of a cell in secure memory.</summary>
    public SecureCharBuffer Value(CsvCell cell)
    {
        var raw = _text.Span.Slice(cell.Start, cell.Length);
        var output = new SecureCharBuffer(Math.Max(cell.Length, 8));
        if (!cell.Quoted)
        {
            output.Append(raw);
            return output;
        }

        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '"' && i + 1 < raw.Length && raw[i + 1] == '"')
            {
                output.Append('"');
                i++;
            }
            else
            {
                output.Append(raw[i]);
            }
        }

        return output;
    }

    public bool IsEmpty(CsvCell cell) => cell.Length == 0 || _text.Span.Slice(cell.Start, cell.Length).IsWhiteSpace();

    private static char DetectDelimiter(ReadOnlySpan<char> text)
    {
        var end = text.IndexOfAny('\r', '\n');
        var header = end >= 0 ? text[..end] : text;
        int commas = 0, semicolons = 0, tabs = 0;
        var quoted = false;
        foreach (var c in header)
        {
            if (c == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted)
            {
                switch (c)
                {
                    case ',':
                        commas++;
                        break;
                    case ';':
                        semicolons++;
                        break;
                    case '\t':
                        tabs++;
                        break;
                }
            }
        }

        return tabs > commas && tabs > semicolons ? '\t' : semicolons > commas ? ';' : ',';
    }

    private static List<CsvCell[]> Split(ReadOnlySpan<char> text, char delimiter)
    {
        var rows = new List<CsvCell[]>();
        var row = new List<CsvCell>();
        var i = 0;
        while (i <= text.Length)
        {
            if (i == text.Length)
            {
                if (row.Count > 0)
                {
                    rows.Add([.. row]);
                }

                break;
            }

            if (text[i] == '"')
            {
                var start = i + 1;
                var j = start;
                while (j < text.Length)
                {
                    if (text[j] == '"')
                    {
                        if (j + 1 < text.Length && text[j + 1] == '"')
                        {
                            j += 2;
                            continue;
                        }

                        break;
                    }

                    j++;
                }

                row.Add(new CsvCell(start, Math.Max(0, j - start), true));
                i = Math.Min(j + 1, text.Length);
                while (i < text.Length && text[i] != delimiter && text[i] != '\r' && text[i] != '\n')
                {
                    i++; // garbage after a closing quote is ignored, as spreadsheets do
                }
            }
            else
            {
                var start = i;
                while (i < text.Length && text[i] != delimiter && text[i] != '\r' && text[i] != '\n')
                {
                    i++;
                }

                row.Add(new CsvCell(start, i - start, false));
            }

            if (i < text.Length && text[i] == delimiter)
            {
                i++;
                if (i == text.Length)
                {
                    row.Add(new CsvCell(i, 0, false));
                }

                continue;
            }

            // End of line.
            if (row.Count > 0 && !(row.Count == 1 && row[0].Length == 0))
            {
                rows.Add([.. row]);
            }

            row.Clear();
            if (i < text.Length && text[i] == '\r')
            {
                i++;
            }

            if (i < text.Length && text[i] == '\n')
            {
                i++;
            }

            if (i == text.Length)
            {
                break;
            }
        }

        return rows;
    }

    public void Dispose() => _text.Dispose();
}

/// <summary>Writes RFC 4180 CSV into secure memory.</summary>
public sealed class CsvWriter : IDisposable
{
    private readonly SecureCharBuffer _output = new(4096);
    private bool _rowStarted;

    public void Cell(ReadOnlySpan<char> value)
    {
        if (_rowStarted)
        {
            _output.Append(',');
        }

        _rowStarted = true;
        // Values are kept byte-exact (a password must survive a round-trip), so cells that
        // spreadsheets could interpret (=, +, -, @) are quoted rather than altered.
        var needsQuotes = value.IndexOfAny(",\"\r\n;") >= 0
            || (value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]) || value[0] is '=' or '+' or '-' or '@'));
        if (!needsQuotes)
        {
            _output.Append(value);
            return;
        }

        _output.Append('"');
        foreach (var c in value)
        {
            if (c == '"')
            {
                _output.Append('"');
            }

            _output.Append(c);
        }

        _output.Append('"');
    }

    public void EndRow()
    {
        _output.Append("\r\n");
        _rowStarted = false;
    }

    public void WriteTo(string path)
    {
        using var utf8 = _output.ToUtf8();
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.Write([0xEF, 0xBB, 0xBF]);
        stream.Write(utf8.Span);
        stream.Flush(flushToDisk: true);
    }

    public ReadOnlySpan<char> Content => _output.Span;

    public void Dispose() => _output.Dispose();
}
