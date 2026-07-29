using FlowDesk.Common;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.ViewModels.DepartmentManager;

namespace FlowDesk.Services.Interfaces
{
    public interface IDepartmentManagerWorkflowService
    {
        Task<ServiceResult<ManagerInboxViewModel>>
            GetInboxAsync(int? managerUserId);

        Task<ServiceResult<ManagerReviewViewModel>>
            GetReviewAsync(int id, int? managerUserId);

        Task<ServiceResult>
            ApproveRequestAsync(
                ApproveRequestDto dto,
                int? managerUserId);

        Task<ServiceResult>
            ReturnToAnalystAsync(
                ReturnToAnalystDto dto,
                int? managerUserId);

        Task<ServiceResult<List<ManagerInboxItemViewModel>>>
            GetApprovedRequestsAsync(int? managerUserId);

        Task<ServiceResult<ManagerReviewViewModel>>
            GetApprovedRequestForExportAsync(
                int id,
                int? managerUserId);
    }
}
