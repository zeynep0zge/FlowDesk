using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Models;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Repositories;

public sealed class WorkItemReadRepository : IWorkItemReadRepository
{
    private readonly AppDbContext _context;

    public WorkItemReadRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<WorkItemDetailsResult?> GetDetailsAsync(
        int workItemId,
        ActorContext actor)
    {
        return ApplyAuthorizationFilter(
                _context.WorkItems.AsNoTracking(),
                actor)
            .Where(workItem => workItem.Id == workItemId)
            .Select(workItem => new WorkItemDetailsResult(
                workItem.Id,
                workItem.RequestNumber,
                workItem.RequestDescription,
                workItem.Department,
                workItem.Priority,
                workItem.WorkflowStatus,
                workItem.CreatedAt,
                workItem.UpdatedAt,
                workItem.AnalystNote,
                workItem.ManagerNote,
                _context.Users
                    .Where(user => user.Id == workItem.AnalystId)
                    .Select(user => user.FullName)
                    .FirstOrDefault(),
                _context.Users
                    .Where(user => user.Id == workItem.AnalystId)
                    .Select(user => user.BusinessCode)
                    .FirstOrDefault(),
                _context.Users
                    .Where(user => user.Id == workItem.DeveloperId)
                    .Select(user => user.FullName)
                    .FirstOrDefault(),
                _context.Users
                    .Where(user => user.Id == workItem.DeveloperId)
                    .Select(user => user.BusinessCode)
                    .FirstOrDefault()))
            .SingleOrDefaultAsync();
    }

    public async Task<PagedResult<WorkItemSummaryResult>> SearchAsync(
        AuthorizedWorkItemSearchQuery query,
        ActorContext actor)
    {
        IQueryable<WorkItem> authorizedQuery =
            ApplyAuthorizationFilter(
                _context.WorkItems.AsNoTracking(),
                actor);

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            string searchText = query.SearchText;
            authorizedQuery = authorizedQuery.Where(workItem =>
                workItem.RequestNumber.Contains(searchText) ||
                workItem.RequestDescription.Contains(searchText));
        }

        if (!string.IsNullOrWhiteSpace(query.Department))
        {
            authorizedQuery = authorizedQuery.Where(workItem =>
                workItem.Department == query.Department);
        }

        if (query.Priority.HasValue)
        {
            authorizedQuery = authorizedQuery.Where(workItem =>
                workItem.Priority == query.Priority.Value);
        }

        if (query.WorkflowStatus.HasValue)
        {
            authorizedQuery = authorizedQuery.Where(workItem =>
                workItem.WorkflowStatus == query.WorkflowStatus.Value);
        }

        int totalCount = await authorizedQuery.CountAsync();
        List<WorkItemSummaryResult> items = await authorizedQuery
            .OrderByDescending(workItem => workItem.CreatedAt)
            .ThenByDescending(workItem => workItem.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(workItem => new WorkItemSummaryResult(
                workItem.Id,
                workItem.RequestNumber,
                workItem.RequestDescription,
                workItem.Department,
                workItem.Priority,
                workItem.WorkflowStatus,
                workItem.CreatedAt,
                workItem.UpdatedAt))
            .ToListAsync();

        return new PagedResult<WorkItemSummaryResult>(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }

    private static IQueryable<WorkItem> ApplyAuthorizationFilter(
        IQueryable<WorkItem> query,
        ActorContext actor)
    {
        return actor.Role switch
        {
            AppRoles.ProjectManager => query.Where(workItem =>
                workItem.CreatedByUserId == actor.UserId),

            AppRoles.Analyst => query.Where(workItem =>
                (workItem.WorkflowStatus == WorkflowStatus.Submitted &&
                 (!workItem.AnalystId.HasValue ||
                  workItem.AnalystId == actor.UserId)) ||
                workItem.AnalystId == actor.UserId),

            AppRoles.DepartmentManager => query.Where(workItem =>
                (workItem.WorkflowStatus ==
                     WorkflowStatus.WaitingManagerApproval ||
                 workItem.WorkflowStatus == WorkflowStatus.Approved) &&
                (actor.CanAccessAllDepartments ||
                 workItem.Department == actor.Department)),

            _ => query.Where(_ => false)
        };
    }
}
