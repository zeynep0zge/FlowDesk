using FlowDesk.Models;

namespace FlowDesk.Repositories.Interfaces
{
    public interface IWorkItemRepository
    {
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