using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace OrchardCore.Contents.Services;

/// <summary>
/// Restricts CSV column mappings to writable text fields declared on the selected content type.
/// Arbitrary JSON paths and system-managed properties must never be writable from a bulk upload.
/// </summary>
internal static class BatchContentColumnMapper
{
    public static IReadOnlyList<BatchContentColumnMapping> Resolve(
        ContentTypeDefinition contentType,
        IReadOnlyList<string> columns)
    {
        ArgumentNullException.ThrowIfNull(contentType);
        ArgumentNullException.ThrowIfNull(columns);

        var mappings = new List<BatchContentColumnMapping>(columns.Count);

        foreach (var column in columns)
        {
            if (column == nameof(ContentItem.DisplayText))
            {
                mappings.Add(new BatchContentColumnMapping(column, null, null));
                continue;
            }

            if (column == "TitlePart.Title"
                && contentType.Parts.Any(part => part.Name == "TitlePart"
                    && part.PartDefinition.Name == "TitlePart"))
            {
                mappings.Add(new BatchContentColumnMapping(column, "TitlePart", null));
                continue;
            }

            var segments = column.Split('.');
            if (segments.Length == 3 && segments[2] == "Text")
            {
                var part = contentType.Parts.FirstOrDefault(part =>
                    part.Name == segments[0]
                    && part.PartDefinition.Fields.Any(field =>
                        field.Name == segments[1]
                        && field.FieldDefinition.Name == "TextField"));

                if (part != null)
                {
                    mappings.Add(new BatchContentColumnMapping(column, part.Name, segments[1]));
                    continue;
                }
            }

            throw new FormatException($"Column '{column}' is not a writable text field on content type '{contentType.Name}'.");
        }

        return mappings;
    }

    public static void Apply(
        ContentItem contentItem,
        IReadOnlyList<BatchContentColumnMapping> mappings,
        IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentNullException.ThrowIfNull(values);

        if (mappings.Count != values.Count)
        {
            throw new ArgumentException("The number of columns does not match the number of values.", nameof(values));
        }

        // Apply the content patch through ContentExtensions.Apply to invalidate any
        // previously materialized part/field objects after modifying JSON.
        var patch = new ContentItem();
        var patchData = (JsonObject)patch.Content;

        for (var index = 0; index < mappings.Count; index++)
        {
            var mapping = mappings[index];
            var value = values[index];

            if (mapping.ColumnName == nameof(ContentItem.DisplayText))
            {
                contentItem.DisplayText = value;
                continue;
            }

            if (patchData[mapping.PartName!] is not JsonObject part)
            {
                part = new JsonObject();
                patchData[mapping.PartName!] = part;
            }

            if (mapping.FieldName is null)
            {
                part["Title"] = value;
            }
            else
            {
                if (part[mapping.FieldName] is not JsonObject field)
                {
                    field = new JsonObject();
                    part[mapping.FieldName] = field;
                }

                field["Text"] = value;
            }
        }

        if (patchData.Count > 0)
        {
            contentItem.Apply(patch);
        }
    }
}

internal sealed record BatchContentColumnMapping(
    string ColumnName,
    string? PartName,
    string? FieldName);
