using System.Text;

namespace OrchardCore.Contents.Services;

/// <summary>
/// Parses small spreadsheet-style CSV uploads before any content items are created.
/// The column names are retained for a later, explicitly validated content-field mapping.
/// </summary>
internal static class BatchContentCsvParser
{
    public const int MaxContentItems = 100;
    public const int MaxColumns = 20;
    public const int MaxCharacters = 262_144;

    public static BatchContentCsvData Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        if (string.IsNullOrWhiteSpace(csv))
        {
            throw new FormatException("The CSV file is empty.");
        }

        if (csv.Length > MaxCharacters)
        {
            throw new FormatException($"The CSV file exceeds the {MaxCharacters} character limit.");
        }

        // UTF-8 CSV exported by Excel may start with a byte-order mark.
        if (csv[0] == '\uFEFF')
        {
            csv = csv[1..];
        }

        var records = new List<string[]>();
        var currentRecord = new List<string>();
        var currentField = new StringBuilder();
        var insideQuotes = false;
        var afterClosingQuote = false;

        void CompleteField()
        {
            currentRecord.Add(currentField.ToString());
            currentField.Clear();
            afterClosingQuote = false;
        }

        void CompleteRecord()
        {
            CompleteField();
            records.Add(currentRecord.ToArray());
            currentRecord.Clear();
        }

        for (var i = 0; i < csv.Length; i++)
        {
            var character = csv[i];

            if (insideQuotes)
            {
                if (character == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++;
                    }
                    else
                    {
                        insideQuotes = false;
                        afterClosingQuote = true;
                    }
                }
                else
                {
                    currentField.Append(character);
                }

                continue;
            }

            if (character == ',')
            {
                CompleteField();
                continue;
            }

            if (character is '\r' or '\n')
            {
                CompleteRecord();
                if (character == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n')
                {
                    i++;
                }

                continue;
            }

            if (character == '"' && currentField.Length == 0 && !afterClosingQuote)
            {
                insideQuotes = true;
                continue;
            }

            if (character == '"' || afterClosingQuote)
            {
                throw new FormatException($"Invalid CSV quoting in record {records.Count + 1}.");
            }

            currentField.Append(character);
        }

        if (insideQuotes)
        {
            throw new FormatException("The CSV file contains an unterminated quoted value.");
        }

        if (currentRecord.Count > 0 || currentField.Length > 0 || afterClosingQuote)
        {
            CompleteRecord();
        }

        if (records.Count == 0)
        {
            throw new FormatException("The CSV file does not contain a header row.");
        }

        var columns = records[0].Select(column => column.Trim()).ToArray();
        if (columns.Length > MaxColumns)
        {
            throw new FormatException($"The CSV file contains more than {MaxColumns} columns.");
        }

        if (columns.Any(string.IsNullOrWhiteSpace))
        {
            throw new FormatException("Every CSV column must have a name.");
        }

        if (columns.Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
        {
            throw new FormatException("The CSV file contains duplicate column names.");
        }

        var rows = new List<IReadOnlyList<string>>();

        for (var index = 1; index < records.Count; index++)
        {
            var record = records[index];

            // Ignore empty trailing lines and blank spreadsheet rows.
            if (record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            if (record.Length != columns.Length)
            {
                throw new FormatException($"CSV record {index + 1} has {record.Length} values instead of {columns.Length}.");
            }

            rows.Add(record);

            if (rows.Count > MaxContentItems)
            {
                throw new FormatException($"A batch cannot contain more than {MaxContentItems} content items.");
            }
        }

        return new BatchContentCsvData(columns, rows);
    }
}

/// <summary>
/// Tabular input data, not yet mapped to content items or persisted.
/// </summary>
internal sealed record BatchContentCsvData(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows);
