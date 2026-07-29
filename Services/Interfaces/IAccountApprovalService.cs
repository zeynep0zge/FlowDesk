using FlowDesk.Common;
using FlowDesk.ViewModels.DepartmentManager;

namespace FlowDesk.Services.Interfaces
{
    public interface IAccountApprovalService
    {
        Task<ServiceResult<IReadOnlyList<PendingUserViewModel>>>
            GetPendingUsersAsync(int? managerUserId);

        Task<ServiceResult> ApproveUserAsync(
            int userId,
            int? managerUserId);
    }
}
