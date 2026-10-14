namespace OrchardCore.Contents.ViewModels;

/// <summary>
/// A CSV-backed batch of new items for one existing content type.
/// Parsed preview rows are rebuilt from the submitted CSV, never trusted from model binding.
/// </summary>
public sealed class BatchContentCreateViewModel
{
    // Resolved from the route and content definition, never trusted from the POST.
    public string? ContentType { get; set; }

    public string? ContentTypeDisplayName { get; set; }

    public string? Csv { get; set; } = string.Empty;

    public IReadOnlyList<string> Columns { get; set; } = [];

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; set; } = [];
}
