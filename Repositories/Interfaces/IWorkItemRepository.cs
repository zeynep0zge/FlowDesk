using FlowDesk.Models;

namespace FlowDesk.Repositories.Interfaces
{
    public interface IWorkItemRepository
    {
        Task<List<WorkItem>> GetAnalystInboxAsync();

        Task<List<WorkItem>> GetReturnedRequestsAsync();

        Task<int> GetReturnedRequestsCountAsync();

        Task<WorkItem?> GetByIdAsync(int id);

        Task<WorkItem?> GetByIdAsNoTrackingAsync(int id);

        Task<int> SaveChangesAsync();
    }
}