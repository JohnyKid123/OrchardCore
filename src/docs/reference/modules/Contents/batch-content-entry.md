# Batch content entry (experimental)

The Contents module includes a batch-entry screen for creating several items of the **same content type** from a CSV table. This is an initial implementation of [#10231](https://github.com/OrchardCMS/OrchardCore/issues/10231).

## Using the screen

1. Open **Content Items** in the admin area.
2. Under **New**, choose **Batch create** for a creatable content type.
3. Paste CSV data with a header row, or export a table from a spreadsheet as CSV.
4. Click **Preview** to review rows before saving.
5. Choose **Create drafts** or **Create and publish**.

Example for a content type with the `TitlePart` and an `Article` part containing a `Summary` text field:

```csv
DisplayText,TitlePart.Title,Article.Summary.Text
First article,First article,A short summary
Second article,Second article,"A summary, with a comma"
```

The available fields depend on the type's content definition. For this initial version, supported column names are:

- `DisplayText` — content item display text.
- `TitlePart.Title` — the Title part's title, only when that part is attached.
- `PartName.FieldName.Text` — the value of a declared Orchard Core `TextField`.

Files can have at most 100 content rows, 20 columns, and 262,144 characters. Quoted commas, quotes and newlines are supported. Empty spreadsheet rows are ignored.

## Security and persistence

Bulk creation uses the **existing** content type and item-level edit permissions, and publishing also requires publish permission. A bulk editor does not grant access to the Import/Export module and does not require JSON knowledge.

The server validates all rows with `IContentManager.ValidateAsync` before creating the first item. Failed validation or persistence cancels the current YesSQL session, preventing a partial commit. Only explicitly declared text fields can be populated; CSV headers cannot target system metadata, IDs, ownership or publication flags.

## Known limitations and next steps

This first iteration accepts pasted CSV and shows a read-only table preview. The planned next step is an editable spreadsheet-like grid, as requested in the issue discussion. Rich editors, complex value types and configurable per-column mappings are not supported yet. Content types with other required fields may reject the batch through normal validation.

Before proposing the work upstream, run the module build, the two new test classes, and a manual admin smoke test covering permissions, draft/publish success and rollback on an invalid row. These checks have not yet been completed.
