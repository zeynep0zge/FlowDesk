using FlowDesk.Ai.Entities;

namespace FlowDesk.Ai.Repositories.Interfaces;

public interface IWorkItemAiDraftRepository
{
    Task<WorkItemAiDraft?> GetByWorkItemIdAsNoTrackingAsync(
        int workItemId,
        CancellationToken cancellationToken = default);

    Task<WorkItemAiDraft?> GetByWorkItemIdAsync(
        int workItemId,
        CancellationToken cancellationToken = default);

    void Add(WorkItemAiDraft draft);

    void SetOriginalRowVersion(
        WorkItemAiDraft draft,
        byte[] rowVersion);

    Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default);
}
