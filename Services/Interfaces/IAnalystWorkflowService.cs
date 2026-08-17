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

        Task<ServiceResult> SendReviewFeedbackAsync(
            int workItemId,
            string? message,
            int? currentAnalystId);

        Task<ServiceResult<FlowDesk.Models.WorkItem>>
            GetFeedbackAsync(int id, int? currentAnalystId)
        {
            throw new NotSupportedException();
        }

        Task<ServiceResult> SendFeedbackAsync(
            SendAnalystFeedbackDto dto,
            int? currentAnalystId)
        {
            throw new NotSupportedException();
        }
    }
}
