using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents.Security;
using OrchardCore.Contents.Services;
using OrchardCore.Contents.ViewModels;
using OrchardCore.DisplayManagement.Notify;
using YesSql;

namespace OrchardCore.Contents.Controllers;

/// <summary>
/// Provides an opt-in batch entry screen for user-created content items.
/// The standard content manager and authorization policies remain authoritative.
/// </summary>
public sealed class BatchContentController : Controller
{
    private readonly IAuthorizationService _authorizationService;
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;
    private readonly ISession _session;
    private readonly INotifier _notifier;
    private readonly IHtmlLocalizer _htmlLocalizer;

    public BatchContentController(
        IAuthorizationService authorizationService,
        IContentDefinitionManager contentDefinitionManager,
        IContentManager contentManager,
        ISession session,
        INotifier notifier,
        IHtmlLocalizer<BatchContentController> htmlLocalizer)
    {
        _authorizationService = authorizationService;
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
        _session = session;
        _notifier = notifier;
        _htmlLocalizer = htmlLocalizer;
    }

    [HttpGet]
    [Admin("Contents/ContentTypes/{id}/BatchCreate", "BatchCreateContentItems")]
    public async Task<IActionResult> BatchCreate(string id)
    {
        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(id);
        if (contentType is null || !contentType.IsCreatable())
        {
            return NotFound();
        }

        if (!await CanCreateAsync(id))
        {
            return Forbid();
        }

        var suggestedColumns = contentType.Parts.Any(part => part.Name == "TitlePart")
            ? "DisplayText,TitlePart.Title"
            : "DisplayText";

        return View(new BatchContentCreateViewModel
        {
            ContentType = id,
            ContentTypeDisplayName = contentType.DisplayName,
            Csv = suggestedColumns + Environment.NewLine,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Admin("Contents/ContentTypes/{id}/BatchCreate", "BatchCreateContentItemsPost")]
    public async Task<IActionResult> BatchCreatePOST(string id, BatchContentCreateViewModel model, string operation)
    {
        if (operation is not ("preview" or "draft" or "publish"))
        {
            return BadRequest();
        }

        var contentType = await _contentDefinitionManager.GetTypeDefinitionAsync(id);
        if (contentType is null || !contentType.IsCreatable())
        {
            return NotFound();
        }

        if (!await CanCreateAsync(id))
        {
            return Forbid();
        }

        if (operation == "publish" && !await CanPublishAsync(id))
        {
            return Forbid();
        }

        model.ContentType = id;
        model.ContentTypeDisplayName = contentType.DisplayName;

        BatchContentCsvData document;
        IReadOnlyList<BatchContentColumnMapping> mappings;
        try
        {
            document = BatchContentCsvParser.Parse(model.Csv ?? string.Empty);
            mappings = BatchContentColumnMapper.Resolve(contentType, document.Columns);
            model.Columns = document.Columns;
            model.Rows = document.Rows;
        }
        catch (FormatException exception)
        {
            ModelState.AddModelError(nameof(model.Csv), exception.Message);
            return View("BatchCreate", model);
        }

        if (document.Rows.Count == 0)
        {
            ModelState.AddModelError(nameof(model.Csv), "Enter at least one content item.");
            return View("BatchCreate", model);
        }

        // A preview never creates or publishes content.
        if (operation == "preview")
        {
            return View("BatchCreate", model);
        }

        var pendingItems = new List<ContentItem>();

        try
        {
            // Validate the entire batch before the first persistence operation.
            for (var index = 0; index < document.Rows.Count; index++)
            {
                var contentItem = await _contentManager.NewAsync(id);
                if (!await _authorizationService.AuthorizeAsync(User, CommonPermissions.EditContent, contentItem)
                    || operation == "publish"
                    && !await _authorizationService.AuthorizeAsync(User, CommonPermissions.PublishContent, contentItem))
                {
                    await _session.CancelAsync();
                    return Forbid();
                }

                BatchContentColumnMapper.Apply(contentItem, mappings, document.Rows[index]);

                var validation = await _contentManager.ValidateAsync(contentItem);
                if (!validation.Succeeded)
                {
                    if (validation.Errors.Count == 0)
                    {
                        ModelState.AddModelError(nameof(model.Csv),
                            $"Record {index + 2} failed content validation.");
                    }

                    foreach (var error in validation.Errors)
                    {
                        ModelState.AddModelError(nameof(model.Csv),
                            $"Record {index + 2}: {error.ErrorMessage}");
                    }
                }

                pendingItems.Add(contentItem);
            }

            if (!ModelState.IsValid)
            {
                await _session.CancelAsync();
                return View("BatchCreate", model);
            }

            foreach (var contentItem in pendingItems)
            {
                if (!await _contentManager.CreateAsync(contentItem, VersionOptions.Draft))
                {
                    ModelState.AddModelError(nameof(model.Csv), "The content batch could not be saved.");
                    await _session.CancelAsync();
                    return View("BatchCreate", model);
                }

                if (operation == "publish" && !await _contentManager.PublishAsync(contentItem))
                {
                    ModelState.AddModelError(nameof(model.Csv), "The content batch could not be published.");
                    await _session.CancelAsync();
                    return View("BatchCreate", model);
                }
            }
        }
        catch
        {
            await _session.CancelAsync();
            throw;
        }

        await _notifier.SuccessAsync(_htmlLocalizer["Created {0} content items.", pendingItems.Count]);
        return RedirectToAction("List", "Admin", new
        {
            area = "OrchardCore.Contents",
            contentTypeId = id,
        });
    }

    private Task<bool> CanCreateAsync(string contentType) =>
        _authorizationService.AuthorizeContentTypeAsync(
            User, CommonPermissions.EditContent, contentType, User.FindFirstValue(ClaimTypes.NameIdentifier));

    private Task<bool> CanPublishAsync(string contentType) =>
        _authorizationService.AuthorizeContentTypeAsync(
            User, CommonPermissions.PublishContent, contentType, User.FindFirstValue(ClaimTypes.NameIdentifier));
}
