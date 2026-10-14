using OrchardCore.Contents.Services;

namespace OrchardCore.Tests.Modules.OrchardCore.Contents;

public class BatchContentCsvParserTests
{
    [Fact]
    public void Parse_ImportsMultipleRows()
    {
        var parsed = BatchContentCsvParser.Parse("DisplayText,Article.Summary.Text\r\nFirst,Short\r\nSecond,Long\r\n");

        Assert.Equal(["DisplayText", "Article.Summary.Text"], parsed.Columns);
        Assert.Equal(2, parsed.Rows.Count);
        Assert.Equal(["First", "Short"], parsed.Rows[0]);
        Assert.Equal(["Second", "Long"], parsed.Rows[1]);
    }

    [Fact]
    public void Parse_HandlesExcelByteOrderMarkQuotedCommasNewlinesAndEscapedQuotes()
    {
        var parsed = BatchContentCsvParser.Parse(
            "\uFEFFDisplayText,Description\r\n\"Hello, world\",\"First line\r\nSecond \"\"quoted\"\" line\"\r\n");

        Assert.Equal(["DisplayText", "Description"], parsed.Columns);
        var row = Assert.Single(parsed.Rows);
        Assert.Equal("Hello, world", row[0]);
        Assert.Equal("First line\r\nSecond \"quoted\" line", row[1]);
    }

    [Fact]
    public void Parse_IgnoresBlankRowsWithoutDroppingEmptyFields()
    {
        var parsed = BatchContentCsvParser.Parse("DisplayText,Description\nFirst,\n\n  \nSecond,Text\n");

        Assert.Equal(2, parsed.Rows.Count);
        Assert.Equal(string.Empty, parsed.Rows[0][1]);
        Assert.Equal("Second", parsed.Rows[1][0]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n\t")]
    public void Parse_RejectsEmptyInput(string csv)
    {
        Assert.Throws<FormatException>(() => BatchContentCsvParser.Parse(csv));
    }

    [Theory]
    [InlineData("DisplayText,displaytext\nHello,World")]
    [InlineData("DisplayText,\nHello,World")]
    [InlineData("DisplayText,Description\nHello")]
    [InlineData("DisplayText\n\"unfinished")]
    [InlineData("DisplayText\n\"value\"extra")]
    [InlineData("DisplayText\nab\"cd")]
    public void Parse_RejectsMalformedCsv(string csv)
    {
        Assert.Throws<FormatException>(() => BatchContentCsvParser.Parse(csv));
    }

    [Fact]
    public void Parse_RejectsMoreThanTwentyColumns()
    {
        var csv = string.Join(',', Enumerable.Range(1, 21).Select(index => $"Column{index}"));

        Assert.Throws<FormatException>(() => BatchContentCsvParser.Parse(csv));
    }

    [Fact]
    public void Parse_RejectsMoreThanOneHundredItems()
    {
        var csv = "DisplayText\n" + string.Concat(Enumerable.Range(1, 101).Select(index => $"Item {index}\n"));

        Assert.Throws<FormatException>(() => BatchContentCsvParser.Parse(csv));
    }

    [Fact]
    public void Parse_RejectsOversizedUploads()
    {
        var csv = "DisplayText\n" + new string('x', BatchContentCsvParser.MaxCharacters);

        Assert.Throws<FormatException>(() => BatchContentCsvParser.Parse(csv));
    }

    [Fact]
    public void Parse_HeadersWithoutRowsAreAllowedForPreview()
    {
        var parsed = BatchContentCsvParser.Parse("DisplayText,Article.Summary.Text\n");

        Assert.Empty(parsed.Rows);
    }
}
