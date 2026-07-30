using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.Analyst;
using FlowDesk.ViewModels.DepartmentManager;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.Services
{
    public class AnalystWorkflowService : IAnalystWorkflowService
    {
        private readonly IWorkItemRepository _workItemRepository;
        private readonly UserManager<ApplicationUser> _userManager;

        public AnalystWorkflowService(
            IWorkItemRepository workItemRepository,
            UserManager<ApplicationUser> userManager)
        {
            _workItemRepository = workItemRepository;
            _userManager = userManager;
        }

        public async Task<ServiceResult<AnalystInboxViewModel>>
            GetInboxAsync(int? currentAnalystId)
        {
            if (!IsValidUserId(currentAnalystId))
            {
                return ServiceResult<AnalystInboxViewModel>.Forbidden(
                    "Gecerli analist kimligi bulunamadi.");
            }

            List<WorkItem> workItems =
                await _workItemRepository.GetAnalystInboxAsync(
                    currentAnalystId.GetValueOrDefault());

            int returnedCount =
                await _workItemRepository
                    .GetReturnedRequestsCountAsync(
                        currentAnalystId.GetValueOrDefault());

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
            GetReturnedRequestsAsync(int? currentAnalystId)
        {
            if (!IsValidUserId(currentAnalystId))
            {
                return ServiceResult<
                    List<AnalystInboxItemViewModel>>.Forbidden(
                        "Gecerli analist kimligi bulunamadi.");
            }

            List<WorkItem> workItems =
                await _workItemRepository.GetReturnedRequestsAsync(
                    currentAnalystId.GetValueOrDefault());

            List<AnalystInboxItemViewModel> viewModels =
                workItems
                    .Select(MapToInboxItemViewModel)
                    .ToList();

            return ServiceResult<List<AnalystInboxItemViewModel>>
                .Success(viewModels);
        }

        public async Task<ServiceResult<AnalystReviewViewModel>>
            GetReviewAsync(int id, int? currentAnalystId)
        {
            if (!IsValidUserId(currentAnalystId))
            {
                return ServiceResult<AnalystReviewViewModel>.Forbidden(
                    "Gecerli analist kimligi bulunamadi.");
            }

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

            if (workItem.AnalystId !=
                currentAnalystId.GetValueOrDefault())
            {
                return ServiceResult<AnalystReviewViewModel>.Forbidden(
                    "Bu talebe erisim yetkiniz bulunmuyor.");
            }

            if (!WorkflowStatusPolicy.CanAnalystSaveAnalysis(
                    workItem.WorkflowStatus))
            {
                return ServiceResult<AnalystReviewViewModel>
                    .Failure(
                        "Bu talep analist tarafından " +
                        "düzenlenebilecek aşamada değildir."
                    );
            }

            AnalystReviewViewModel viewModel =
                await MapToReviewViewModelAsync(workItem);

            return ServiceResult<AnalystReviewViewModel>
                .Success(viewModel);
        }

        public async Task<ServiceResult> StartReviewAsync(
            int id,
            int? currentAnalystId)
        {
            if (!IsValidUserId(currentAnalystId))
            {
                return ServiceResult.Forbidden(
                    "Gecerli analist kimligi bulunamadi.");
            }

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

            if (workItem.WorkflowStatus != WorkflowStatus.Submitted &&
                workItem.AnalystId !=
                    currentAnalystId.GetValueOrDefault())
            {
                return ServiceResult.Forbidden(
                    "Bu talebe erisim yetkiniz bulunmuyor.");
            }

            if (workItem.WorkflowStatus == WorkflowStatus.Submitted &&
                workItem.AnalystId.HasValue &&
                workItem.AnalystId !=
                    currentAnalystId.GetValueOrDefault())
            {
                return ServiceResult.Forbidden(
                    "Bu talep baska bir analist tarafindan sahiplenilmis.");
            }

            if (!WorkflowStatusPolicy.CanAnalystStartReview(
                    workItem.WorkflowStatus))
            {
                return ServiceResult.Failure(
                    "Bu talep analist tarafından " +
                    "incelenebilecek aşamada değildir."
                );
            }

            if (workItem.WorkflowStatus ==
                WorkflowStatus.Submitted)
            {
                workItem.AnalystId =
                    currentAnalystId.GetValueOrDefault();
                workItem.WorkflowStatus =
                    WorkflowStatus.UnderAnalystReview;

                workItem.CurrentStatus =
                    WorkflowStatusDescriptions.GetDescription(
                        WorkflowStatus.UnderAnalystReview);

                workItem.UpdatedAt = DateTime.UtcNow;

                await _workItemRepository.SaveChangesAsync();
            }

            return ServiceResult.Success();
        }

        public async Task<ServiceResult<AnalystReviewViewModel>>
            SaveAnalysisAsync(
                SaveAnalysisDto dto,
                int? currentAnalystId)
        {
            if (!IsValidUserId(currentAnalystId))
            {
                return ServiceResult<AnalystReviewViewModel>.Forbidden(
                    "Gecerli analist kimligi bulunamadi.");
            }

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

            if (workItem.AnalystId !=
                currentAnalystId.GetValueOrDefault())
            {
                return ServiceResult<AnalystReviewViewModel>.Forbidden(
                    "Bu talebi degistirme yetkiniz bulunmuyor.");
            }

            if (!WorkflowStatusPolicy.CanAnalystSaveAnalysis(
                    workItem.WorkflowStatus))
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

            if (currentStatus != null &&
                currentStatus.Length >
                    AnalystCurrentStatusOptions.MaximumLength)
            {
                validationErrors.Add(
                    "Mevcut statü en fazla " +
                    $"{AnalystCurrentStatusOptions.MaximumLength} " +
                    "karakter olabilir."
                );
            }
            else if (currentStatus != null &&
                     !AnalystCurrentStatusOptions.Contains(currentStatus))
            {
                validationErrors.Add(
                    "Geçerli bir mevcut statü seçiniz."
                );
            }

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

            ServiceResult developerResult =
                await ValidateDeveloperAsync(
                    dto.DeveloperId,
                    workItem.Department);

            if (!developerResult.IsSuccess)
            {
                return ServiceResult<AnalystReviewViewModel>.Failure(
                    developerResult.ErrorMessage!);
            }

            workItem.AnalystId =
                currentAnalystId.GetValueOrDefault();
            workItem.DeveloperId = dto.DeveloperId;
            workItem.ReleaseDate = dto.ReleaseDate;

            workItem.BanksoftDeliveryDate =
                dto.BanksoftDeliveryDate;

            workItem.ExpectedStatus = expectedStatus;
            workItem.AnalystNote = analystNote;

            workItem.CurrentStatus =
                currentStatus ?? WorkflowStatusDescriptions.GetDescription(
                    WorkflowStatus.UnderAnalystReview);

            workItem.WorkflowStatus =
                WorkflowStatus.UnderAnalystReview;

            workItem.UpdatedAt = DateTime.UtcNow;

            await _workItemRepository.SaveChangesAsync();

            AnalystReviewViewModel viewModel =
                await MapToReviewViewModelAsync(workItem);

            return ServiceResult<AnalystReviewViewModel>
                .Success(viewModel);
        }

        public async Task<ServiceResult>
            SubmitForApprovalAsync(
                SubmitForApprovalDto dto,
                int? currentAnalystId)
        {
            if (!IsValidUserId(currentAnalystId))
            {
                return ServiceResult.Forbidden(
                    "Gecerli analist kimligi bulunamadi.");
            }

            dto.AnalystId = currentAnalystId.GetValueOrDefault();

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

            if (workItem.AnalystId !=
                currentAnalystId.GetValueOrDefault())
            {
                return ServiceResult.Forbidden(
                    "Bu talebi gonderme yetkiniz bulunmuyor.");
            }

            if (workItem.WorkflowStatus ==
                WorkflowStatus.WaitingManagerApproval)
            {
                return ServiceResult.Failure(
                    "Bu talep zaten departman yöneticisi " +
                    "onayına gönderilmiş."
                );
            }

            if (!WorkflowStatusPolicy.CanAnalystSubmitForManagerApproval(
                    workItem.WorkflowStatus))
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

            ServiceResult developerResult =
                await ValidateDeveloperAsync(
                    dto.DeveloperId,
                    workItem.Department);

            if (!developerResult.IsSuccess)
            {
                return developerResult;
            }

            workItem.AnalystId =
                currentAnalystId.GetValueOrDefault();
            workItem.DeveloperId = dto.DeveloperId;
            workItem.ReleaseDate = dto.ReleaseDate;

            workItem.BanksoftDeliveryDate =
                dto.BanksoftDeliveryDate;

            workItem.ExpectedStatus = expectedStatus;
            workItem.AnalystNote = analystNote;

            workItem.WorkflowStatus =
                WorkflowStatus.WaitingManagerApproval;

            workItem.CurrentStatus =
                WorkflowStatusDescriptions.GetDescription(
                    WorkflowStatus.WaitingManagerApproval);

            workItem.UpdatedAt = DateTime.UtcNow;

            await _workItemRepository.SaveChangesAsync();

            return ServiceResult.Success();
        }

        private async Task<ServiceResult> ValidateDeveloperAsync(
            int? developerId,
            string workItemDepartment)
        {
            if (!developerId.HasValue)
            {
                return ServiceResult.Success();
            }

            ApplicationUser? developer = await _userManager.FindByIdAsync(
                developerId.Value.ToString());

            if (developer == null ||
                !developer.EmailConfirmed ||
                !developer.IsApproved ||
                string.IsNullOrWhiteSpace(developer.BusinessCode) ||
                !await _userManager.IsInRoleAsync(
                    developer,
                    AppRoles.Employee) ||
                !string.Equals(
                    developer.Department,
                    workItemDepartment,
                    StringComparison.Ordinal))
            {
                return ServiceResult.Failure(
                    "Secilen yazilimci bu talep icin yetkili degil.");
            }

            return ServiceResult.Success();
        }

        private static bool IsValidUserId(int? userId)
        {
            return userId.HasValue && userId.Value > 0;
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

        private async Task<AnalystReviewViewModel>
            MapToReviewViewModelAsync(WorkItem workItem)
        {
            ApplicationUser? analyst = workItem.AnalystId.HasValue
                ? await _userManager.FindByIdAsync(
                    workItem.AnalystId.Value.ToString())
                : null;

            IList<ApplicationUser> employees =
                await _userManager.GetUsersInRoleAsync(AppRoles.Employee);

            List<UserSelectionOptionViewModel> developerOptions = employees
                .Where(user =>
                    user.EmailConfirmed &&
                    user.IsApproved &&
                    !string.IsNullOrWhiteSpace(user.BusinessCode) &&
                    string.Equals(
                        user.Department,
                        workItem.Department,
                        StringComparison.Ordinal))
                .OrderBy(user => user.FullName)
                .ThenBy(user => user.BusinessCode)
                .Select(user => new UserSelectionOptionViewModel
                {
                    Id = user.Id,
                    DisplayText = $"{user.FullName} \u2014 {user.BusinessCode}"
                })
                .ToList();

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

                AnalystDisplayName = analyst == null
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(analyst.BusinessCode)
                        ? analyst.FullName
                        : $"{analyst.FullName} \u2014 {analyst.BusinessCode}",

                DeveloperId =
                    workItem.DeveloperId,

                DeveloperOptions = developerOptions,

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
