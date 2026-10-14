using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents.Services;

namespace OrchardCore.Tests.Modules.OrchardCore.Contents;

public class BatchContentColumnMapperTests
{
    [Fact]
    public void Resolve_AcceptsDisplayTextTitleAndDefinedTextFields()
    {
        var mappings = BatchContentColumnMapper.Resolve(
            CreateType(),
            ["DisplayText", "TitlePart.Title", "Article.Summary.Text"]);

        Assert.Equal(3, mappings.Count);
        Assert.Null(mappings[0].PartName);
        Assert.Equal("TitlePart", mappings[1].PartName);
        Assert.Equal("Article", mappings[2].PartName);
        Assert.Equal("Summary", mappings[2].FieldName);
    }

    [Theory]
    [InlineData("Owner")]
    [InlineData("Published")]
    [InlineData("ContentItemId")]
    [InlineData("Article.Unknown.Text")]
    [InlineData("Article.Summary.Html")]
    [InlineData("Other.Summary.Text")]
    [InlineData("TitlePart.Secret")]
    [InlineData("Article.Summary.Text.Value")]
    public void Resolve_RejectsSystemFieldsUnknownFieldsAndArbitraryJsonPaths(string column)
    {
        Assert.Throws<FormatException>(() =>
            BatchContentColumnMapper.Resolve(CreateType(), [column]));
    }

    [Fact]
    public void Apply_ChangesOnlyApprovedValues()
    {
        var mappings = BatchContentColumnMapper.Resolve(
            CreateType(),
            ["DisplayText", "TitlePart.Title", "Article.Summary.Text"]);
        var item = new ContentItem { ContentType = "Article", Owner = "original-owner" };

        BatchContentColumnMapper.Apply(item, mappings, ["First", "First title", "Summary text"]);

        Assert.Equal("First", item.DisplayText);
        Assert.Equal("original-owner", item.Owner);

        var data = (JsonObject)item.Content;
        Assert.Equal("First title", data["TitlePart"]!["Title"]!.GetValue<string>());
        Assert.Equal("Summary text", data["Article"]!["Summary"]!["Text"]!.GetValue<string>());
    }

    [Fact]
    public void Apply_RejectsRowsWithWrongNumberOfCells()
    {
        var mappings = BatchContentColumnMapper.Resolve(CreateType(), ["DisplayText"]);

        Assert.Throws<ArgumentException>(() =>
            BatchContentColumnMapper.Apply(new ContentItem(), mappings, []));
    }

    private static ContentTypeDefinition CreateType()
    {
        var textField = new ContentPartFieldDefinition(
            new ContentFieldDefinition("TextField"), "Summary", new JsonObject());
        var article = new ContentTypePartDefinition(
            "Article",
            new ContentPartDefinition("Article", [textField], new JsonObject()),
            new JsonObject());
        var title = new ContentTypePartDefinition(
            "TitlePart",
            new ContentPartDefinition("TitlePart"),
            new JsonObject());

        return new ContentTypeDefinition("Article", "Article", [article, title], new JsonObject());
    }
}
