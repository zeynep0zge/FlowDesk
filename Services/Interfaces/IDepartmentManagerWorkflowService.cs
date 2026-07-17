using FlowDesk.Common;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.ViewModels.DepartmentManager;

namespace FlowDesk.Services.Interfaces
{
    public interface IDepartmentManagerWorkflowService
    {
        Task<ServiceResult<ManagerInboxViewModel>>
            GetInboxAsync();

        Task<ServiceResult<ManagerReviewViewModel>>
            GetReviewAsync(int id);

        Task<ServiceResult>
            ApproveRequestAsync(ApproveRequestDto dto);

        Task<ServiceResult>
            ReturnToAnalystAsync(ReturnToAnalystDto dto);

        Task<ServiceResult<List<ManagerInboxItemViewModel>>>
            GetApprovedRequestsAsync();

        Task<ServiceResult<ManagerReviewViewModel>>
            GetApprovedRequestForExportAsync(int id);
    }
}