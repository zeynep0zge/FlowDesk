using System.Text.Json;
using FlowDesk.Ai.Entities;
using FlowDesk.Ai.Interfaces;
using FlowDesk.Ai.Models;
using FlowDesk.Ai.Options;
using FlowDesk.Ai.Repositories.Interfaces;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FlowDesk.Ai.Services;

public sealed class AnalystAiWorkflowService : IAnalystAiWorkflowService
{
    private const string ForbiddenMessage =
        "Bu işlemi gerçekleştirme yetkiniz bulunmuyor.";
    private const string NotFoundMessage = "Talep bulunamadı.";
    private const string MissingDetailsMessage =
        "Talep bilgileri alınamadı.";
    private const string EmptyDescriptionMessage =
        "Düzenlenecek talep açıklaması boş olamaz.";
    private const string DraftNotFoundMessage =
        "AI taslağı bulunamadı.";
    private const string InvalidDraftMessage =
        "Kayıtlı AI taslağı okunamadı.";
    private const string EmptyEditedRequestMessage =
        "Düzenlenmiş AI taslağı boş olamaz.";
    private const string LongEditedRequestMessage =
        "Düzenlenmiş AI taslağı en fazla 4000 karakter olabilir.";
    private const string InvalidRowVersionMessage =
        "AI taslağı sürüm bilgisi geçersiz.";
    private const string ConcurrencyMessage =
        "AI taslağı başka bir işlem tarafından güncellendi. " +
        "Sayfayı yenileyip tekrar deneyin.";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IAuthorizedWorkItemQueryService _queryService;
    private readonly IAnalystRequestRewriteService _rewriteService;
    private readonly IWorkItemAiDraftRepository _draftRepository;
    private readonly GeminiOptions _geminiOptions;

    public AnalystAiWorkflowService(
        IAuthorizedWorkItemQueryService queryService,
        IAnalystRequestRewriteService rewriteService,
        IWorkItemAiDraftRepository draftRepository,
        IOptions<GeminiOptions> geminiOptions)
    {
        _queryService = queryService;
        _rewriteService = rewriteService;
        _draftRepository = draftRepository;
        _geminiOptions = geminiOptions.Value;
    }

    public async Task<ServiceResult<AnalystAiDraftResult>>
        RewriteWorkItemAsync(
            int workItemId,
            ActorContext actor,
            CancellationToken cancellationToken = default)
    {
        ServiceResult<WorkItemDetailsResult> authorizationResult =
            await GetAuthorizedWorkItemAsync(workItemId, actor);

        if (!authorizationResult.IsSuccess || authorizationResult.Data == null)
        {
            return MapAuthorizationFailure(authorizationResult);
        }

        WorkItemAiDraft? existingDraft = await _draftRepository
            .GetByWorkItemIdAsNoTrackingAsync(
                workItemId,
                cancellationToken);

        if (existingDraft != null)
        {
            return CreateDraftResult(existingDraft);
        }

        WorkItemDetailsResult workItem = authorizationResult.Data;
        if (string.IsNullOrWhiteSpace(workItem.RequestDescription))
        {
            return ServiceResult<AnalystAiDraftResult>.Failure(
                EmptyDescriptionMessage);
        }

        ServiceResult<AnalystRequestRewriteResult> rewriteResult =
            await _rewriteService.RewriteAsync(
                workItem.RequestDescription,
                cancellationToken);

        if (!rewriteResult.IsSuccess || rewriteResult.Data == null)
        {
            return ServiceResult<AnalystAiDraftResult>.Failure(
                rewriteResult.ErrorMessage ??
                "AI düzenleme işlemi tamamlanamadı.");
        }

        string generatedRequest = rewriteResult.Data.RewrittenRequest.Trim();
        if (generatedRequest.Length > WorkItemAiDraft.RequestMaximumLength)
        {
            return ServiceResult<AnalystAiDraftResult>.Failure(
                LongEditedRequestMessage);
        }

        string modelName = _geminiOptions.Model?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(modelName) ||
            modelName.Length > WorkItemAiDraft.ModelNameMaximumLength)
        {
            return ServiceResult<AnalystAiDraftResult>.Failure(
                "Gemini model bilgisi geçersiz.");
        }

        DateTime now = DateTime.UtcNow;
        var draft = new WorkItemAiDraft
        {
            WorkItemId = workItemId,
            GeneratedRequest = generatedRequest,
            EditedRequest = generatedRequest,
            AbbreviationsJson = JsonSerializer.Serialize(
                rewriteResult.Data.Abbreviations,
                JsonOptions),
            AmbiguitiesJson = JsonSerializer.Serialize(
                rewriteResult.Data.Ambiguities,
                JsonOptions),
            UnresolvedTermsJson = JsonSerializer.Serialize(
                rewriteResult.Data.UnresolvedTerms,
                JsonOptions),
            ModelName = modelName,
            GeneratedAt = now,
            UpdatedAt = now
        };

        _draftRepository.Add(draft);

        try
        {
            await _draftRepository.SaveChangesAsync(cancellationToken);
            return CreateDraftResult(draft);
        }
        catch (DbUpdateException)
        {
            WorkItemAiDraft? concurrentDraft = await _draftRepository
                .GetByWorkItemIdAsNoTrackingAsync(
                    workItemId,
                    cancellationToken);

            if (concurrentDraft == null)
            {
                throw;
            }

            return CreateDraftResult(concurrentDraft);
        }
    }

    public async Task<ServiceResult<AnalystAiDraftResult>>
        SaveEditedDraftAsync(
            int workItemId,
            ActorContext actor,
            SaveAnalystAiDraftRequest request,
            CancellationToken cancellationToken = default)
    {
        ServiceResult<WorkItemDetailsResult> authorizationResult =
            await GetAuthorizedWorkItemAsync(workItemId, actor);

        if (!authorizationResult.IsSuccess || authorizationResult.Data == null)
        {
            return MapAuthorizationFailure(authorizationResult);
        }

        string editedRequest = request?.EditedRequest?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(editedRequest))
        {
            return ServiceResult<AnalystAiDraftResult>.Failure(
                EmptyEditedRequestMessage);
        }

        if (editedRequest.Length > WorkItemAiDraft.RequestMaximumLength)
        {
            return ServiceResult<AnalystAiDraftResult>.Failure(
                LongEditedRequestMessage);
        }

        if (!TryDecodeRowVersion(request?.RowVersion, out byte[] rowVersion))
        {
            return ServiceResult<AnalystAiDraftResult>.Failure(
                InvalidRowVersionMessage);
        }

        WorkItemAiDraft? draft = await _draftRepository
            .GetByWorkItemIdAsync(workItemId, cancellationToken);

        if (draft == null)
        {
            return ServiceResult<AnalystAiDraftResult>.NotFound(
                DraftNotFoundMessage);
        }

        _draftRepository.SetOriginalRowVersion(draft, rowVersion);
        draft.EditedRequest = editedRequest;
        draft.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _draftRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ServiceResult<AnalystAiDraftResult>.Conflict(
                ConcurrencyMessage);
        }

        return CreateDraftResult(draft);
    }

    private async Task<ServiceResult<WorkItemDetailsResult>>
        GetAuthorizedWorkItemAsync(
            int workItemId,
            ActorContext actor)
    {
        if (workItemId <= 0)
        {
            return ServiceResult<WorkItemDetailsResult>.NotFound(
                NotFoundMessage);
        }

        if (actor is null ||
            actor.UserId <= 0 ||
            actor.CanAccessAllDepartments ||
            !string.Equals(
                actor.Role,
                AppRoles.Analyst,
                StringComparison.Ordinal))
        {
            return ServiceResult<WorkItemDetailsResult>.Forbidden(
                ForbiddenMessage);
        }

        return await _queryService.GetDetailsAsync(workItemId, actor);
    }

    private static ServiceResult<AnalystAiDraftResult>
        MapAuthorizationFailure(
            ServiceResult<WorkItemDetailsResult> result)
    {
        if (result.IsForbidden)
        {
            return ServiceResult<AnalystAiDraftResult>.Forbidden(
                result.ErrorMessage ?? ForbiddenMessage);
        }

        if (result.IsNotFound)
        {
            return ServiceResult<AnalystAiDraftResult>.NotFound(
                result.ErrorMessage ?? NotFoundMessage);
        }

        return ServiceResult<AnalystAiDraftResult>.Failure(
            result.ErrorMessage ?? MissingDetailsMessage);
    }

    private static ServiceResult<AnalystAiDraftResult> CreateDraftResult(
        WorkItemAiDraft draft)
    {
        try
        {
            IReadOnlyList<AbbreviationExplanation>? abbreviations =
                JsonSerializer.Deserialize<List<AbbreviationExplanation>>(
                    draft.AbbreviationsJson,
                    JsonOptions);
            IReadOnlyList<string>? ambiguities =
                JsonSerializer.Deserialize<List<string>>(
                    draft.AmbiguitiesJson,
                    JsonOptions);
            IReadOnlyList<string>? unresolvedTerms =
                JsonSerializer.Deserialize<List<string>>(
                    draft.UnresolvedTermsJson,
                    JsonOptions);

            if (abbreviations == null || ambiguities == null ||
                unresolvedTerms == null || draft.RowVersion.Length == 0)
            {
                return ServiceResult<AnalystAiDraftResult>.Failure(
                    InvalidDraftMessage);
            }

            string rewrittenRequest = string.IsNullOrWhiteSpace(
                draft.EditedRequest)
                ? draft.GeneratedRequest
                : draft.EditedRequest;

            return ServiceResult<AnalystAiDraftResult>.Success(
                new AnalystAiDraftResult
                {
                    RewrittenRequest = rewrittenRequest,
                    Abbreviations = abbreviations,
                    Ambiguities = ambiguities,
                    UnresolvedTerms = unresolvedTerms,
                    RowVersion = Convert.ToBase64String(draft.RowVersion),
                    GeneratedAt = draft.GeneratedAt,
                    UpdatedAt = draft.UpdatedAt
                });
        }
        catch (JsonException)
        {
            return ServiceResult<AnalystAiDraftResult>.Failure(
                InvalidDraftMessage);
        }
    }

    private static bool TryDecodeRowVersion(
        string? value,
        out byte[] rowVersion)
    {
        rowVersion = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            rowVersion = Convert.FromBase64String(value);
            return rowVersion.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
