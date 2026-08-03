using FlowDesk.Ai.Models;
using FlowDesk.Common;
using FlowDesk.Services.Models;

namespace FlowDesk.Ai.Interfaces;

public interface IAnalystAiWorkflowService
{
    Task<ServiceResult<AnalystAiDraftResult>> RewriteWorkItemAsync(
        int workItemId,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AnalystAiDraftResult>> SaveEditedDraftAsync(
        int workItemId,
        ActorContext actor,
        SaveAnalystAiDraftRequest request,
        CancellationToken cancellationToken = default);
}
