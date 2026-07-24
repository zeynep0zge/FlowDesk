using FlowDesk.Models;

namespace FlowDesk.Repositories.Interfaces
{
    public interface IWorkItemRepository
    {
        Task<List<WorkItem>>
            GetProjectManagerRequestsAsync(int? currentUserId);

        Task<bool> RequestNumberExistsAsync(
            string requestNumber,
            int? excludedWorkItemId = null);

        void Add(WorkItem workItem);

        void Remove(WorkItem workItem);

        Task<List<WorkItem>> GetAnalystInboxAsync();

        Task<List<WorkItem>> GetReturnedRequestsAsync();

        Task<int> GetReturnedRequestsCountAsync();

        Task<List<WorkItem>>
            GetWaitingManagerApprovalAsync();

        Task<List<WorkItem>>
            GetApprovedRequestsAsync();

        Task<WorkItem?> GetByIdAsync(int id);

        Task<WorkItem?>
            GetByIdAsNoTrackingAsync(int id);

        Task<int> SaveChangesAsync();
    }
}