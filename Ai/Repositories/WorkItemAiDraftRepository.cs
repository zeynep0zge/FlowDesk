using FlowDesk.Ai.Entities;
using FlowDesk.Ai.Repositories.Interfaces;
using FlowDesk.Data;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Ai.Repositories;

public sealed class WorkItemAiDraftRepository
    : IWorkItemAiDraftRepository
{
    private readonly AppDbContext _context;

    public WorkItemAiDraftRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<WorkItemAiDraft?> GetByWorkItemIdAsNoTrackingAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        return _context.Set<WorkItemAiDraft>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                draft => draft.WorkItemId == workItemId,
                cancellationToken);
    }

    public Task<WorkItemAiDraft?> GetByWorkItemIdAsync(
        int workItemId,
        CancellationToken cancellationToken = default)
    {
        return _context.Set<WorkItemAiDraft>().SingleOrDefaultAsync(
            draft => draft.WorkItemId == workItemId,
            cancellationToken);
    }

    public void Add(WorkItemAiDraft draft)
    {
        _context.Set<WorkItemAiDraft>().Add(draft);
    }

    public void SetOriginalRowVersion(
        WorkItemAiDraft draft,
        byte[] rowVersion)
    {
        _context.Entry(draft)
            .Property(entity => entity.RowVersion)
            .OriginalValue = rowVersion;
    }

    public Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
