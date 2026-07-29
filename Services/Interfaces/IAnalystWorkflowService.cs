using FlowDesk.Common;
using FlowDesk.DTOs.Analyst;
using FlowDesk.ViewModels.Analyst;
using FlowDesk.ViewModels.DepartmentManager;


namespace FlowDesk.Services.Interfaces
{
    public interface IAnalystWorkflowService
    {
        Task<ServiceResult<AnalystInboxViewModel>>
            GetInboxAsync(int? currentAnalystId);

        Task<ServiceResult<List<AnalystInboxItemViewModel>>>
            GetReturnedRequestsAsync(int? currentAnalystId);

        Task<ServiceResult<AnalystReviewViewModel>>
            GetReviewAsync(int id, int? currentAnalystId);

        Task<ServiceResult>
            StartReviewAsync(int id, int? currentAnalystId);

        Task<ServiceResult<AnalystReviewViewModel>>
            SaveAnalysisAsync(
                SaveAnalysisDto dto,
                int? currentAnalystId);

        Task<ServiceResult>
            SubmitForApprovalAsync(
                SubmitForApprovalDto dto,
                int? currentAnalystId);
    }
}
