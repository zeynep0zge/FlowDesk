using FlowDesk.Common;
using FlowDesk.Services.Models;

namespace FlowDesk.Services.Interfaces;

public interface IAuthorizedWorkItemQueryService
{
    Task<ServiceResult<WorkItemDetailsResult>> GetDetailsAsync(
        int workItemId,
        ActorContext actor);

    Task<ServiceResult<PagedResult<WorkItemSummaryResult>>> SearchAsync(
        AuthorizedWorkItemSearchQuery query,
        ActorContext actor);
}
