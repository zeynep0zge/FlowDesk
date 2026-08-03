using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;

namespace FlowDesk.Services;

public sealed class AuthorizedWorkItemQueryService
    : IAuthorizedWorkItemQueryService
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 50;

    private const string ForbiddenMessage =
        "Bu sorguyu çalıştırma yetkiniz bulunmuyor.";
    private const string NotFoundMessage = "Talep bulunamadı.";

    private readonly IWorkItemReadRepository _readRepository;

    public AuthorizedWorkItemQueryService(
        IWorkItemReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<ServiceResult<WorkItemDetailsResult>>
        GetDetailsAsync(int workItemId, ActorContext actor)
    {
        if (!CanQuery(actor))
        {
            return ServiceResult<WorkItemDetailsResult>.Forbidden(
                ForbiddenMessage);
        }

        if (workItemId <= 0)
        {
            return ServiceResult<WorkItemDetailsResult>.NotFound(
                NotFoundMessage);
        }

        WorkItemDetailsResult? result = await _readRepository
            .GetDetailsAsync(workItemId, actor);

        return result == null
            ? ServiceResult<WorkItemDetailsResult>.NotFound(
                NotFoundMessage)
            : ServiceResult<WorkItemDetailsResult>.Success(result);
    }

    public async Task<ServiceResult<PagedResult<WorkItemSummaryResult>>>
        SearchAsync(
            AuthorizedWorkItemSearchQuery query,
            ActorContext actor)
    {
        if (!CanQuery(actor))
        {
            return ServiceResult<PagedResult<WorkItemSummaryResult>>
                .Forbidden(ForbiddenMessage);
        }

        AuthorizedWorkItemSearchQuery normalized = Normalize(query);
        PagedResult<WorkItemSummaryResult> result =
            await _readRepository.SearchAsync(normalized, actor);

        return ServiceResult<PagedResult<WorkItemSummaryResult>>
            .Success(result);
    }

    private static bool CanQuery(ActorContext? actor)
    {
        if (actor == null || actor.UserId <= 0 ||
            actor.Role == AppRoles.Employee)
        {
            return false;
        }

        bool supportedRole = actor.Role is
            AppRoles.ProjectManager or
            AppRoles.Analyst or
            AppRoles.DepartmentManager;
        if (!supportedRole)
        {
            return false;
        }

        if (actor.Role != AppRoles.DepartmentManager &&
            actor.CanAccessAllDepartments)
        {
            return false;
        }

        return actor.Role != AppRoles.DepartmentManager ||
               actor.CanAccessAllDepartments ||
               !string.IsNullOrWhiteSpace(actor.Department);
    }

    private static AuthorizedWorkItemSearchQuery Normalize(
        AuthorizedWorkItemSearchQuery? query)
    {
        query ??= new AuthorizedWorkItemSearchQuery();
        int page = Math.Max(1, query.Page);
        int pageSize = query.PageSize <= 0
            ? DefaultPageSize
            : Math.Min(query.PageSize, MaximumPageSize);

        return query with
        {
            SearchText = NormalizeText(query.SearchText),
            Department = NormalizeText(query.Department),
            Page = page,
            PageSize = pageSize
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
