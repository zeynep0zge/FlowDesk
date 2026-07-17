using FlowDesk.Common;
using FlowDesk.DTOs.Analyst;
using FlowDesk.ViewModels.Analyst;

namespace FlowDesk.Services.Interfaces
{
    public interface IAnalystWorkflowService
    {
        Task<ServiceResult<AnalystInboxViewModel>>
            GetInboxAsync();

        Task<ServiceResult<List<AnalystInboxItemViewModel>>>
            GetReturnedRequestsAsync();

        Task<ServiceResult<AnalystReviewViewModel>>
            GetReviewAsync(int id);

        Task<ServiceResult>
            StartReviewAsync(int id);

        Task<ServiceResult<AnalystReviewViewModel>>
            SaveAnalysisAsync(SaveAnalysisDto dto);

        Task<ServiceResult>
            SubmitForApprovalAsync(SubmitForApprovalDto dto);
    }
}