using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Repositories
{
    public class WorkItemRepository : IWorkItemRepository
    {
        private readonly AppDbContext _context;

        public WorkItemRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<WorkItem>>
            GetProjectManagerRequestsAsync(int currentUserId)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .Where(x => x.CreatedByUserId == currentUserId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<bool> RequestNumberExistsAsync(
            string requestNumber,
            int? excludedWorkItemId = null)
        {
            return await _context.WorkItems.AnyAsync(
                x => x.RequestNumber == requestNumber &&
                     (!excludedWorkItemId.HasValue ||
                      x.Id != excludedWorkItemId.Value)
            );
        }

        public void Add(WorkItem workItem)
        {
            _context.WorkItems.Add(workItem);
        }

        public void Remove(WorkItem workItem)
        {
            _context.WorkItems.Remove(workItem);
        }

        public async Task<List<WorkItem>>
            GetAnalystInboxAsync(int currentAnalystId)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .Where(x =>
                    x.WorkflowStatus == WorkflowStatus.Submitted ||
                    (x.WorkflowStatus ==
                         WorkflowStatus.UnderAnalystReview &&
                     x.AnalystId == currentAnalystId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<WorkItem>>
            GetReturnedRequestsAsync(int currentAnalystId)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .Where(x =>
                    x.WorkflowStatus ==
                        WorkflowStatus.ReturnedToAnalyst &&
                    x.AnalystId == currentAnalystId)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ToListAsync();
        }

        public async Task<int>
            GetReturnedRequestsCountAsync(int currentAnalystId)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .CountAsync(x =>
                    x.WorkflowStatus ==
                        WorkflowStatus.ReturnedToAnalyst &&
                    x.AnalystId == currentAnalystId);
        }

        public async Task<List<WorkItem>>
            GetWaitingManagerApprovalAsync(
                string department,
                bool canAccessAllDepartments)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .Where(x =>
                    x.WorkflowStatus ==
                        WorkflowStatus.WaitingManagerApproval &&
                    (canAccessAllDepartments ||
                     x.Department == department))
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<WorkItem>>
            GetApprovedRequestsAsync(
                string department,
                bool canAccessAllDepartments)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .Where(x =>
                    x.WorkflowStatus ==
                        WorkflowStatus.Approved &&
                    (canAccessAllDepartments ||
                     x.Department == department))
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ToListAsync();
        }

        public async Task<WorkItem?> GetManagerWorkItemByIdAsync(
            int id,
            string department,
            bool canAccessAllDepartments)
        {
            return await _context.WorkItems.FirstOrDefaultAsync(
                x => x.Id == id &&
                     (canAccessAllDepartments ||
                      x.Department == department));
        }

        public async Task<WorkItem?>
            GetManagerWorkItemByIdAsNoTrackingAsync(
                int id,
                string department,
                bool canAccessAllDepartments)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id &&
                         (canAccessAllDepartments ||
                          x.Department == department));
        }

        public async Task<List<WorkItem>>
            GetEmployeeAssignedWorkItemsAsync(int employeeId)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .Where(x => x.DeveloperId == employeeId)
                .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
                .ToListAsync();
        }

        public async Task<WorkItem?> GetEmployeeWorkItemByIdAsync(
            int workItemId,
            int employeeId)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.Id == workItemId &&
                    x.DeveloperId == employeeId);
        }

        public async Task<WorkItem?> GetByIdAsync(int id)
        {
            return await _context.WorkItems
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<WorkItem?>
            GetByIdAsNoTrackingAsync(int id)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
