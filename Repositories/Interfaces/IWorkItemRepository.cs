using FlowDesk.Models;

namespace FlowDesk.Repositories.Interfaces
{
    public interface IWorkItemRepository
    {
        Task<List<WorkItem>>
            GetProjectManagerRequestsAsync(int currentUserId);

        Task<bool> RequestNumberExistsAsync(
            string requestNumber,
            int? excludedWorkItemId = null);

        void Add(WorkItem workItem);

        void Remove(WorkItem workItem);

        Task<List<WorkItem>>
            GetAnalystInboxAsync(int currentAnalystId);

        Task<List<WorkItem>>
            GetReturnedRequestsAsync(int currentAnalystId);

        Task<int>
            GetReturnedRequestsCountAsync(int currentAnalystId);

        Task<List<WorkItem>>
            GetWaitingManagerApprovalAsync(string department);

        Task<List<WorkItem>>
            GetApprovedRequestsAsync(string department);

        Task<WorkItem?> GetByIdInDepartmentAsync(
            int id,
            string department);

        Task<WorkItem?> GetByIdInDepartmentAsNoTrackingAsync(
            int id,
            string department);

        Task<List<WorkItem>>
            GetEmployeeAssignedWorkItemsAsync(int employeeId);

        Task<WorkItem?> GetEmployeeWorkItemByIdAsync(
            int workItemId,
            int employeeId);

        Task<WorkItem?> GetByIdAsync(int id);

        Task<WorkItem?>
            GetByIdAsNoTrackingAsync(int id);

        Task<int> SaveChangesAsync();
    }
}
