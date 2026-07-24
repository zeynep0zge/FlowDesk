using FlowDesk.Common;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.Analyst;
using FlowDesk.ViewModels.DepartmentManager;

namespace FlowDesk.Services
{
    public class AnalystWorkflowService : IAnalystWorkflowService
    {
        private readonly IWorkItemRepository _workItemRepository;

        public AnalystWorkflowService(
            IWorkItemRepository workItemRepository)
        {
            _workItemRepository = workItemRepository;
        }

        public async Task<ServiceResult<AnalystInboxViewModel>>
            GetInboxAsync()
        {
            List<WorkItem> workItems =
                await _workItemRepository.GetAnalystInboxAsync();

            int returnedCount =
                await _workItemRepository
                    .GetReturnedRequestsCountAsync();

            AnalystInboxViewModel viewModel = new()
            {
                WorkItems = workItems
                    .Select(MapToInboxItemViewModel)
                    .ToList(),

                ReturnedCount = returnedCount
            };

            return ServiceResult<AnalystInboxViewModel>
                .Success(viewModel);
        }

        public async Task<
            ServiceResult<List<AnalystInboxItemViewModel>>>
            GetReturnedRequestsAsync()
        {
            List<WorkItem> workItems =
                await _workItemRepository.GetReturnedRequestsAsync();

            List<AnalystInboxItemViewModel> viewModels =
                workItems
                    .Select(MapToInboxItemViewModel)
                    .ToList();

            return ServiceResult<List<AnalystInboxItemViewModel>>
                .Success(viewModels);
        }

        public async Task<ServiceResult<AnalystReviewViewModel>>
            GetReviewAsync(int id)
        {
            if (id <= 0)
            {
                return ServiceResult<AnalystReviewViewModel>
                    .Failure("Geçersiz talep ID.");
            }

            WorkItem? workItem =
                await _workItemRepository
                    .GetByIdAsNoTrackingAsync(id);

            if (workItem == null)
            {
                return ServiceResult<AnalystReviewViewModel>
                    .NotFound("Talep bulunamadı.");
            }

            if (!CanAnalystEdit(workItem.WorkflowStatus))
            {
                return ServiceResult<AnalystReviewViewModel>
                    .Failure(
                        "Bu talep analist tarafından " +
                        "düzenlenebilecek aşamada değildir."
                    );
            }

            AnalystReviewViewModel viewModel =
                MapToReviewViewModel(workItem);

            return ServiceResult<AnalystReviewViewModel>
                .Success(viewModel);
        }

        public async Task<ServiceResult> StartReviewAsync(int id)
        {
            if (id <= 0)
            {
                return ServiceResult.Failure(
                    "Geçersiz talep ID."
                );
            }

            WorkItem? workItem =
                await _workItemRepository.GetByIdAsync(id);

            if (workItem == null)
            {
                return ServiceResult.NotFound(
                    "Talep bulunamadı."
                );
            }

            if (!CanAnalystEdit(workItem.WorkflowStatus))
            {
                return ServiceResult.Failure(
                    "Bu talep analist tarafından " +
                    "incelenebilecek aşamada değildir."
                );
            }

            if (workItem.WorkflowStatus ==
                WorkflowStatus.Submitted)
            {
                workItem.WorkflowStatus =
                    WorkflowStatus.UnderAnalystReview;

                workItem.CurrentStatus =
                    "Analist İncelemesinde";

                workItem.UpdatedAt = DateTime.UtcNow;

                await _workItemRepository.SaveChangesAsync();
            }

            return ServiceResult.Success();
        }

        public async Task<ServiceResult<AnalystReviewViewModel>>
            SaveAnalysisAsync(SaveAnalysisDto dto)
        {
            if (dto.WorkItemId <= 0)
            {
                return ServiceResult<AnalystReviewViewModel>
                    .Failure("Geçersiz talep ID.");
            }

            WorkItem? workItem =
                await _workItemRepository
                    .GetByIdAsync(dto.WorkItemId);

            if (workItem == null)
            {
                return ServiceResult<AnalystReviewViewModel>
                    .NotFound("Talep bulunamadı.");
            }

            if (!CanAnalystEdit(workItem.WorkflowStatus))
            {
                return ServiceResult<AnalystReviewViewModel>
                    .Failure(
                        "Bu talep analist tarafından " +
                        "düzenlenebilecek aşamada değildir."
                    );
            }

            string? expectedStatus =
                NormalizeNullableText(dto.ExpectedStatus);

            string? currentStatus =
                NormalizeNullableText(dto.CurrentStatus);

            string? analystNote =
                NormalizeNullableText(dto.AnalystNote);

            List<string> validationErrors = new();

            if (dto.AnalystId.HasValue &&
                dto.AnalystId.Value <= 0)
            {
                validationErrors.Add(
                    "Analist ID 0'dan büyük olmalıdır."
                );
            }

            if (dto.DeveloperId.HasValue &&
                dto.DeveloperId.Value <= 0)
            {
                validationErrors.Add(
                    "Yazılımcı ID 0'dan büyük olmalıdır."
                );
            }

            if (dto.ReleaseDate.HasValue &&
                dto.BanksoftDeliveryDate.HasValue &&
                dto.BanksoftDeliveryDate.Value.Date >
                dto.ReleaseDate.Value.Date)
            {
                validationErrors.Add(
                    "Banksoft teslim tarihi, " +
                    "sürüm tarihinden sonra olamaz."
                );
            }

            if (analystNote != null &&
                analystNote.Length > 1000)
            {
                validationErrors.Add(
                    "Analist notu en fazla " +
                    "1000 karakter olabilir."
                );
            }

            if (validationErrors.Count > 0)
            {
                return ServiceResult<AnalystReviewViewModel>
                    .Failure(
                        string.Join(" ", validationErrors)
                    );
            }

            workItem.AnalystId = dto.AnalystId;
            workItem.DeveloperId = dto.DeveloperId;
            workItem.ReleaseDate = dto.ReleaseDate;

            workItem.BanksoftDeliveryDate =
                dto.BanksoftDeliveryDate;

            workItem.ExpectedStatus = expectedStatus;
            workItem.AnalystNote = analystNote;

            workItem.CurrentStatus =
                currentStatus ?? "Analist İncelemesinde";

            workItem.WorkflowStatus =
                WorkflowStatus.UnderAnalystReview;

            workItem.UpdatedAt = DateTime.UtcNow;

            await _workItemRepository.SaveChangesAsync();

            AnalystReviewViewModel viewModel =
                MapToReviewViewModel(workItem);

            return ServiceResult<AnalystReviewViewModel>
                .Success(viewModel);
        }

        public async Task<ServiceResult>
            SubmitForApprovalAsync(
                SubmitForApprovalDto dto)
        {
            if (dto.WorkItemId <= 0)
            {
                return ServiceResult.Failure(
                    "Geçersiz talep ID."
                );
            }

            WorkItem? workItem =
                await _workItemRepository
                    .GetByIdAsync(dto.WorkItemId);

            if (workItem == null)
            {
                return ServiceResult.NotFound(
                    "Talep bulunamadı."
                );
            }

            if (workItem.WorkflowStatus ==
                WorkflowStatus.WaitingManagerApproval)
            {
                return ServiceResult.Failure(
                    "Bu talep zaten departman yöneticisi " +
                    "onayına gönderilmiş."
                );
            }

            if (!CanAnalystEdit(workItem.WorkflowStatus))
            {
                return ServiceResult.Failure(
                    "Bu talep yönetici onayına " +
                    "gönderilebilecek aşamada değildir."
                );
            }

            string? expectedStatus =
                NormalizeNullableText(dto.ExpectedStatus);

            string? analystNote =
                NormalizeNullableText(dto.AnalystNote);

            List<string> missingFields = new();
            List<string> invalidFields = new();

            if (!dto.AnalystId.HasValue)
            {
                missingFields.Add("analist");
            }
            else if (dto.AnalystId.Value <= 0)
            {
                invalidFields.Add("analist ID");
            }

            if (!dto.DeveloperId.HasValue)
            {
                missingFields.Add("yazılımcı");
            }
            else if (dto.DeveloperId.Value <= 0)
            {
                invalidFields.Add("yazılımcı ID");
            }

            if (!dto.ReleaseDate.HasValue)
            {
                missingFields.Add("sürüm tarihi");
            }

            if (!dto.BanksoftDeliveryDate.HasValue)
            {
                missingFields.Add(
                    "Banksoft teslim tarihi"
                );
            }

            if (expectedStatus == null)
            {
                missingFields.Add("beklenen statü");
            }

            if (missingFields.Count > 0)
            {
                return ServiceResult.Failure(
                    "Yönetici onayına göndermeden önce " +
                    "şu alanları doldurun: " +
                    string.Join(", ", missingFields) +
                    "."
                );
            }

            if (invalidFields.Count > 0)
            {
                return ServiceResult.Failure(
                    "Şu alanlar 0'dan büyük olmalıdır: " +
                    string.Join(", ", invalidFields) +
                    "."
                );
            }

            if (analystNote != null &&
                analystNote.Length > 1000)
            {
                return ServiceResult.Failure(
                    "Analist notu en fazla " +
                    "1000 karakter olabilir."
                );
            }

            DateTime releaseDate =
                dto.ReleaseDate.GetValueOrDefault();

            DateTime banksoftDeliveryDate =
                dto.BanksoftDeliveryDate
                    .GetValueOrDefault();

            if (banksoftDeliveryDate.Date >
                releaseDate.Date)
            {
                return ServiceResult.Failure(
                    "Banksoft teslim tarihi, " +
                    "sürüm tarihinden sonra olamaz."
                );
            }

            workItem.AnalystId = dto.AnalystId;
            workItem.DeveloperId = dto.DeveloperId;
            workItem.ReleaseDate = dto.ReleaseDate;

            workItem.BanksoftDeliveryDate =
                dto.BanksoftDeliveryDate;

            workItem.ExpectedStatus = expectedStatus;
            workItem.AnalystNote = analystNote;

            workItem.WorkflowStatus =
                WorkflowStatus.WaitingManagerApproval;

            workItem.CurrentStatus =
                "Departman Yöneticisi Onayı Bekliyor";

            workItem.UpdatedAt = DateTime.UtcNow;

            await _workItemRepository.SaveChangesAsync();

            return ServiceResult.Success();
        }

        private static bool CanAnalystEdit(
            WorkflowStatus workflowStatus)
        {
            return workflowStatus ==
                       WorkflowStatus.Submitted ||
                   workflowStatus ==
                       WorkflowStatus.UnderAnalystReview ||
                   workflowStatus ==
                       WorkflowStatus.ReturnedToAnalyst;
        }

        private static string? NormalizeNullableText(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private static AnalystInboxItemViewModel
            MapToInboxItemViewModel(WorkItem workItem)
        {
            return new AnalystInboxItemViewModel
            {
                Id = workItem.Id,

                RequestNumber =
                    workItem.RequestNumber,

                RequestDescription =
                    workItem.RequestDescription,

                Department =
                    workItem.Department,

                Priority =
                    workItem.Priority,

                WorkflowStatus =
                    workItem.WorkflowStatus,

                CurrentStatus =
                    workItem.CurrentStatus,

                ManagerNote =
                    workItem.ManagerNote,

                CreatedAt =
                    workItem.CreatedAt,

                UpdatedAt =
                    workItem.UpdatedAt
            };
        }

        private static AnalystReviewViewModel
            MapToReviewViewModel(WorkItem workItem)
        {
            return new AnalystReviewViewModel
            {
                Id = workItem.Id,

                RequestNumber =
                    workItem.RequestNumber,

                RequestDescription =
                    workItem.RequestDescription,

                Department =
                    workItem.Department,

                Priority =
                    workItem.Priority,

                WorkflowStatus =
                    workItem.WorkflowStatus,

                CreatedAt =
                    workItem.CreatedAt,

                AnalystId =
                    workItem.AnalystId,

                DeveloperId =
                    workItem.DeveloperId,

                ReleaseDate =
                    workItem.ReleaseDate,

                BanksoftDeliveryDate =
                    workItem.BanksoftDeliveryDate,

                ExpectedStatus =
                    workItem.ExpectedStatus,

                CurrentStatus =
                    workItem.CurrentStatus,

                AnalystNote =
                    workItem.AnalystNote,

                ManagerNote =
                    workItem.ManagerNote
            };
        }
    }
}