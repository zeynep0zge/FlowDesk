using FlowDesk.Services.Models;

namespace FlowDesk.Repositories.Interfaces;

public interface IWorkItemReadRepository
{
    Task<WorkItemDetailsResult?> GetDetailsAsync(
        int workItemId,
        ActorContext actor);

    Task<PagedResult<WorkItemSummaryResult>> SearchAsync(
        AuthorizedWorkItemSearchQuery query,
        ActorContext actor);
}
