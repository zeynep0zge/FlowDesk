using FlowDesk.Common;
using FlowDesk.ViewModels.DepartmentManager;

namespace FlowDesk.Services.Interfaces
{
    public interface IAccountApprovalService
    {
        Task<IReadOnlyList<PendingUserViewModel>>
            GetPendingUsersAsync();

        Task<ServiceResult> ApproveUserAsync(int userId);
    }
}