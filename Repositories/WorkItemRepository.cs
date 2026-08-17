using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Models;
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
                    (x.AnalystId == currentAnalystId &&
                     x.WorkflowStatus !=
                         WorkflowStatus.ReturnedToAnalyst))
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

        public Task<List<ApprovedWorkItemListItemResult>>
            GetApprovedWorkItemListAsync(
                string department,
                bool canAccessAllDepartments)
        {
            return (
                from workItem in _context.WorkItems.AsNoTracking()
                join analyst in _context.Users.AsNoTracking()
                    on workItem.AnalystId equals (int?)analyst.Id
                    into analysts
                from analyst in analysts.DefaultIfEmpty()
                join developer in _context.Users.AsNoTracking()
                    on workItem.DeveloperId equals (int?)developer.Id
                    into developers
                from developer in developers.DefaultIfEmpty()
                where workItem.WorkflowStatus == WorkflowStatus.Approved &&
                    (canAccessAllDepartments ||
                     workItem.Department == department)
                orderby workItem.UpdatedAt ?? workItem.CreatedAt descending
                select new ApprovedWorkItemListItemResult
                {
                    Id = workItem.Id,
                    DeveloperId = workItem.DeveloperId,
                    Department = workItem.Department,
                    AnalystFullName = analyst == null
                        ? string.Empty
                        : analyst.FullName,
                    AnalystBusinessCode = analyst == null
                        ? null
                        : analyst.BusinessCode,
                    DeveloperFullName = developer == null
                        ? string.Empty
                        : developer.FullName,
                    DeveloperBusinessCode = developer == null
                        ? null
                        : developer.BusinessCode,
                    ReleaseDate = workItem.ReleaseDate,
                    BanksoftDeliveryDate = workItem.BanksoftDeliveryDate,
                    ExpectedStatus = workItem.ExpectedStatus,
                    CurrentStatus = workItem.CurrentStatus,
                    RequestNumber = workItem.RequestNumber,
                    RequestDescription = workItem.RequestDescription,
                    RowVersion = workItem.RowVersion
                })
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
                .Where(x =>
                    x.DeveloperId == employeeId &&
                    x.WorkflowStatus == WorkflowStatus.Approved)
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
                    x.DeveloperId == employeeId &&
                    x.WorkflowStatus == WorkflowStatus.Approved);
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

        public async Task<WorkItem?>
            GetByIdWithFeedbackAsNoTrackingAsync(int id)
        {
            return await _context.WorkItems
                .AsNoTracking()
                .Include(x => x.FeedbackMessages
                    .OrderBy(message => message.CreatedAt)
                    .ThenBy(message => message.Id))
                .ThenInclude(message => message.SenderUser)
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public void AddFeedbackMessage(FeedbackMessage message)
        {
            _context.FeedbackMessages.Add(message);
        }

        public async Task MarkFeedbackMessagesReadAsync(
            int workItemId,
            int recipientUserId,
            DateTime readAt)
        {
            List<FeedbackMessage> messages = await _context
                .FeedbackMessages
                .Where(message =>
                    message.WorkItemId == workItemId &&
                    message.SenderUserId != recipientUserId &&
                    !message.IsRead)
                .ToListAsync();

            foreach (FeedbackMessage message in messages)
            {
                message.IsRead = true;
                message.ReadAt = readAt;
            }

            if (messages.Count > 0)
            {
                await _context.SaveChangesAsync();
            }
        }

        public void SetOriginalRowVersion(
            WorkItem workItem,
            byte[] rowVersion)
        {
            _context.Entry(workItem)
                .Property(entity => entity.RowVersion)
                .OriginalValue = rowVersion;
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}
