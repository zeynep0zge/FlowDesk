using FlowDesk.Common;
using FlowDesk.Models;

namespace FlowDesk.Services.Interfaces
{
    public interface IEmployeeWorkItemService
    {
        Task<ServiceResult<List<WorkItem>>>
            GetAssignedWorkItemsAsync(int? employeeId);

        Task<ServiceResult<WorkItem>> GetDetailsAsync(
            int workItemId,
            int? employeeId);
    }
}
