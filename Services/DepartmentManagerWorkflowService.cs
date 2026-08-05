using FlowDesk.Common;
using FlowDesk.Ai.Repositories.Interfaces;
using FlowDesk.Constants;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using FlowDesk.ViewModels.Analyst;
using FlowDesk.ViewModels.DepartmentManager;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Services
{
    public class DepartmentManagerWorkflowService
        : IDepartmentManagerWorkflowService
    {
        private readonly IWorkItemRepository _workItemRepository;
        private readonly IManagerAccessScopeResolver
            _managerAccessScopeResolver;
        private readonly IWorkItemAiDraftRepository _draftRepository;
        private readonly IApprovedWorkItemExcelService _approvedExcelService;
        private readonly ILogger<DepartmentManagerWorkflowService> _logger;

        public DepartmentManagerWorkflowService(
            IWorkItemRepository workItemRepository,
            IManagerAccessScopeResolver managerAccessScopeResolver,
            Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>
                userManager,
            IWorkItemAiDraftRepository draftRepository,
            IApprovedWorkItemExcelService approvedExcelService,
            ILogger<DepartmentManagerWorkflowService> logger)
        {
            _workItemRepository = workItemRepository;
            _managerAccessScopeResolver = managerAccessScopeResolver;
            _userManager = userManager;
            _draftRepository = draftRepository;
            _approvedExcelService = approvedExcelService;
            _logger = logger;
        }

        private readonly Microsoft.AspNetCore.Identity.UserManager<
            ApplicationUser> _userManager;

        public async Task<ServiceResult<ManagerInboxViewModel>>
            GetInboxAsync(int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await _managerAccessScopeResolver.ResolveAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult<ManagerInboxViewModel>.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            List<WorkItem> workItems =
                await _workItemRepository
                    .GetWaitingManagerApprovalAsync(
                        departmentResult.Data!.Department,
                        departmentResult.Data.CanAccessAllDepartments);

            ManagerInboxViewModel viewModel = new()
            {
                Requests = workItems
                    .Select(MapToInboxItemViewModel)
                    .ToList()
            };

            return ServiceResult<ManagerInboxViewModel>
                .Success(viewModel);
        }
        public async Task<ServiceResult<ManagerReviewViewModel>>
            GetReviewAsync(int id, int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await _managerAccessScopeResolver.ResolveAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult<ManagerReviewViewModel>.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            if (id <= 0)
            {
                return ServiceResult<ManagerReviewViewModel>
                    .Failure("Geçersiz talep ID.");
            }

            WorkItem? workItem =
                await _workItemRepository
                    .GetManagerWorkItemByIdAsNoTrackingAsync(
                        id,
                        departmentResult.Data!.Department,
                        departmentResult.Data.CanAccessAllDepartments);

            if (workItem == null)
            {
                return ServiceResult<ManagerReviewViewModel>
                    .NotFound("Talep bulunamadı.");
            }

            if (!WorkflowStatusPolicy.CanDepartmentManagerReview(
                    workItem.WorkflowStatus))
            {
                return ServiceResult<ManagerReviewViewModel>
                    .Failure(
                        "Bu talep yönetici onayı beklemiyor."
                    );
            }

            ManagerReviewViewModel viewModel =
                await MapToReviewViewModelAsync(workItem);

            return ServiceResult<ManagerReviewViewModel>
                .Success(viewModel);
        }

        public async Task<ServiceResult>
            ApproveRequestAsync(
                ApproveRequestDto dto,
                int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await _managerAccessScopeResolver.ResolveAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            if (dto.WorkItemId <= 0)
            {
                return ServiceResult.Failure(
                    "Geçersiz talep ID."
                );
            }

            WorkItem? workItem =
                await _workItemRepository
                    .GetManagerWorkItemByIdAsync(
                        dto.WorkItemId,
                        departmentResult.Data!.Department,
                        departmentResult.Data.CanAccessAllDepartments);

            if (workItem == null)
            {
                return ServiceResult.NotFound(
                    "Talep bulunamadı."
                );
            }

            if (!WorkflowStatusPolicy.CanDepartmentManagerApprove(
                    workItem.WorkflowStatus))
            {
                return ServiceResult.Failure(
                    "Yalnızca yönetici onayı bekleyen " +
                    "talepler onaylanabilir."
                );
            }

            ServiceResult<ApplicationUser> developerResult =
                await ValidateDeveloperAsync(
                    dto.DeveloperId,
                    workItem.Department);
            if (!developerResult.IsSuccess || developerResult.Data == null)
            {
                return ServiceResult.Failure(
                    developerResult.ErrorMessage!);
            }

            string? managerNote =
                NormalizeNullableText(dto.ManagerNote);

            if (managerNote != null &&
                managerNote.Length > 1000)
            {
                return ServiceResult.Failure(
                    "Yönetici notu en fazla " +
                    "1000 karakter olabilir."
                );
            }

            var draft = await _draftRepository
                .GetByWorkItemIdAsNoTrackingAsync(workItem.Id);
            string analystDisplayName =
                await GetUserDisplayNameAsync(workItem.AnalystId);
            DateTime now = DateTime.UtcNow;

            workItem.ManagerNote = managerNote;
            workItem.DeveloperId = developerResult.Data.Id;
            workItem.WorkflowStatus = WorkflowStatus.Approved;
            workItem.CurrentStatus =
                WorkflowStatusDescriptions.GetDescription(
                    WorkflowStatus.Approved);
            workItem.ApprovedAt = now;
            workItem.UpdatedAt = now;

            await _workItemRepository.SaveChangesAsync();

            ServiceResult excelResult;
            try
            {
                excelResult = await _approvedExcelService.AppendAsync(
                    new ApprovedWorkItemExcelRow
                    {
                        AnalystDisplayName = analystDisplayName,
                        DeveloperDisplayName =
                            GetUserDisplayName(developerResult.Data),
                        ReleaseDate = workItem.ReleaseDate,
                        BanksoftDeliveryDate = workItem.BanksoftDeliveryDate,
                        ExpectedStatus = workItem.ExpectedStatus,
                        CurrentStatus = workItem.CurrentStatus,
                        RequestNumber = workItem.RequestNumber,
                        RequestDescription =
                            NormalizeNullableText(draft?.EditedRequest) ??
                            workItem.RequestDescription
                    });
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Onay sonrası Excel servisi çağrısı başarısız. Hata türü: {ErrorType}",
                    exception.GetType().Name);
                excelResult = ServiceResult.Failure(
                    "Excel kaydı tamamlanamadı.");
            }

            if (!excelResult.IsSuccess)
            {
                return ServiceResult.Success(
                    "Talep onaylandı ancak Excel kaydı tamamlanamadı.");
            }

            return ServiceResult.Success();
        }

        public async Task<ServiceResult>
            ReturnToAnalystAsync(
                ReturnToAnalystDto dto,
                int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await _managerAccessScopeResolver.ResolveAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            if (dto.WorkItemId <= 0)
            {
                return ServiceResult.Failure(
                    "Geçersiz talep ID."
                );
            }

            string? managerNote =
                NormalizeNullableText(dto.ManagerNote);

            if (managerNote == null)
            {
                return ServiceResult.Failure(
                    "Talebi analiste iade etmek için " +
                    "yönetici notu zorunludur."
                );
            }

            if (managerNote.Length > 1000)
            {
                return ServiceResult.Failure(
                    "Yönetici notu en fazla " +
                    "1000 karakter olabilir."
                );
            }

            WorkItem? workItem =
                await _workItemRepository
                    .GetManagerWorkItemByIdAsync(
                        dto.WorkItemId,
                        departmentResult.Data!.Department,
                        departmentResult.Data.CanAccessAllDepartments);

            if (workItem == null)
            {
                return ServiceResult.NotFound(
                    "Talep bulunamadı."
                );
            }

            if (!WorkflowStatusPolicy.CanDepartmentManagerReturnToAnalyst(
                    workItem.WorkflowStatus))
            {
                return ServiceResult.Failure(
                    "Yalnızca yönetici onayı bekleyen " +
                    "talepler analiste iade edilebilir."
                );
            }

            workItem.ManagerNote = managerNote;
            workItem.WorkflowStatus =
                WorkflowStatus.ReturnedToAnalyst;
            workItem.CurrentStatus =
                WorkflowStatusDescriptions.GetDescription(
                    WorkflowStatus.ReturnedToAnalyst);
            workItem.ApprovedAt = null;
            workItem.UpdatedAt = DateTime.UtcNow;

            await _workItemRepository.SaveChangesAsync();

            return ServiceResult.Success();
        }

        public async Task<ServiceResult>
            RejectRequestAsync(
                RejectRequestDto dto,
                int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await _managerAccessScopeResolver.ResolveAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            if (dto.WorkItemId <= 0)
            {
                return ServiceResult.Failure("Geçersiz talep ID.");
            }

            string? managerNote = NormalizeNullableText(dto.ManagerNote);
            if (managerNote == null)
            {
                return ServiceResult.Failure(
                    "Talebi reddetmek için yönetici notu zorunludur.");
            }

            if (managerNote.Length > 1000)
            {
                return ServiceResult.Failure(
                    "Yönetici notu en fazla 1000 karakter olabilir.");
            }

            WorkItem? workItem = await _workItemRepository
                .GetManagerWorkItemByIdAsync(
                    dto.WorkItemId,
                    departmentResult.Data!.Department,
                    departmentResult.Data.CanAccessAllDepartments);

            if (workItem == null)
            {
                return ServiceResult.NotFound("Talep bulunamadı.");
            }

            if (!WorkflowStatusPolicy.CanDepartmentManagerReject(
                    workItem.WorkflowStatus))
            {
                return ServiceResult.Failure(
                    "Yalnızca yönetici onayı bekleyen talepler reddedilebilir.");
            }

            workItem.ManagerNote = managerNote;
            workItem.WorkflowStatus = WorkflowStatus.Rejected;
            workItem.CurrentStatus =
                WorkflowStatusDescriptions.GetDescription(
                    WorkflowStatus.Rejected);
            workItem.ApprovedAt = null;
            workItem.UpdatedAt = DateTime.UtcNow;

            await _workItemRepository.SaveChangesAsync();
            return ServiceResult.Success();
        }

        public async Task<
            ServiceResult<List<ManagerInboxItemViewModel>>>
            GetApprovedRequestsAsync(int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await _managerAccessScopeResolver.ResolveAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult<
                    List<ManagerInboxItemViewModel>>.Forbidden(
                        departmentResult.ErrorMessage!);
            }

            List<WorkItem> workItems =
                await _workItemRepository
                    .GetApprovedRequestsAsync(
                        departmentResult.Data!.Department,
                        departmentResult.Data.CanAccessAllDepartments);

            List<ManagerInboxItemViewModel> viewModels =
                workItems
                    .Select(MapToInboxItemViewModel)
                    .ToList();

            return ServiceResult<
                List<ManagerInboxItemViewModel>>
                .Success(viewModels);
        }

        public async Task<ServiceResult<SharedExcelViewModel>>
            GetSharedExcelAsync(int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await _managerAccessScopeResolver.ResolveAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult<SharedExcelViewModel>.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            List<ApprovedWorkItemListItemResult> workItems =
                await _workItemRepository.GetApprovedWorkItemListAsync(
                    departmentResult.Data!.Department,
                    departmentResult.Data.CanAccessAllDepartments);
            IList<ApplicationUser> employees =
                await _userManager.GetUsersInRoleAsync(AppRoles.Employee);

            var viewModel = new SharedExcelViewModel
            {
                Requests = workItems.Select(workItem =>
                    new SharedExcelItemViewModel
                    {
                        Id = workItem.Id,
                        DeveloperId = workItem.DeveloperId,
                        AnalystDisplayName = GetUserDisplayName(
                            workItem.AnalystFullName,
                            workItem.AnalystBusinessCode),
                        DeveloperDisplayName = GetUserDisplayName(
                            workItem.DeveloperFullName,
                            workItem.DeveloperBusinessCode),
                        DeveloperOptions = employees
                            .Where(user => IsEligibleDeveloper(
                                user,
                                workItem.Department))
                            .OrderBy(user => user.FullName)
                            .ThenBy(user => user.BusinessCode)
                            .Select(user =>
                                new UserSelectionOptionViewModel
                                {
                                    Id = user.Id,
                                    DisplayText = GetUserDisplayName(user)
                                })
                            .ToList(),
                        ReleaseDate = workItem.ReleaseDate,
                        BanksoftDeliveryDate =
                            workItem.BanksoftDeliveryDate,
                        ExpectedStatus = workItem.ExpectedStatus,
                        CurrentStatus = workItem.CurrentStatus,
                        RequestNumber = workItem.RequestNumber,
                        RequestDescription = workItem.RequestDescription,
                        RowVersion = workItem.RowVersion == null
                            ? string.Empty
                            : Convert.ToBase64String(workItem.RowVersion)
                    })
                    .ToList()
            };

            return ServiceResult<SharedExcelViewModel>.Success(viewModel);
        }

        public async Task<ServiceResult> EditApprovedWorkItemAsync(
            EditApprovedWorkItemDto dto,
            int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await _managerAccessScopeResolver.ResolveAsync(managerUserId);
            if (!departmentResult.IsSuccess)
            {
                return ServiceResult.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            if (dto.WorkItemId <= 0)
            {
                return ServiceResult.Failure("Geçersiz talep ID.");
            }

            WorkItem? workItem = await _workItemRepository
                .GetManagerWorkItemByIdAsync(
                    dto.WorkItemId,
                    departmentResult.Data!.Department,
                    departmentResult.Data.CanAccessAllDepartments);
            if (workItem == null)
            {
                return ServiceResult.NotFound("Talep bulunamadı.");
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Approved)
            {
                return ServiceResult.Failure(
                    "Yalnızca onaylanmış talepler düzenlenebilir.");
            }

            ServiceResult<ApprovedWorkItemEditValues> validationResult =
                ValidateApprovedWorkItemEdit(dto);
            if (!validationResult.IsSuccess || validationResult.Data == null)
            {
                return ServiceResult.Failure(
                    validationResult.ErrorMessage!);
            }

            ServiceResult<ApplicationUser> developerResult =
                await ValidateDeveloperAsync(
                    dto.DeveloperId,
                    workItem.Department);
            if (!developerResult.IsSuccess || developerResult.Data == null)
            {
                return ServiceResult.Failure(
                    developerResult.ErrorMessage!);
            }

            if (!TryDecodeRowVersion(dto.RowVersion, out byte[] rowVersion))
            {
                return ServiceResult.Failure(
                    "Geçersiz eş zamanlılık bilgisi.");
            }

            ApprovedWorkItemEditValues values = validationResult.Data;
            _workItemRepository.SetOriginalRowVersion(workItem, rowVersion);
            workItem.DeveloperId = developerResult.Data.Id;
            workItem.ReleaseDate = dto.ReleaseDate;
            workItem.BanksoftDeliveryDate = dto.BanksoftDeliveryDate;
            workItem.ExpectedStatus = values.ExpectedStatus;
            workItem.CurrentStatus = values.CurrentStatus;
            workItem.RequestDescription = values.RequestDescription;
            workItem.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _workItemRepository.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return ServiceResult.Conflict(
                    "Kayıt başka bir kullanıcı tarafından güncellendi. " +
                    "Sayfayı yenileyip tekrar deneyin.");
            }

            ServiceResult excelResult;
            try
            {
                excelResult = await _approvedExcelService.AppendAsync(
                    new ApprovedWorkItemExcelRow
                    {
                        AnalystDisplayName = await GetUserDisplayNameAsync(
                            workItem.AnalystId),
                        DeveloperDisplayName = GetUserDisplayName(
                            developerResult.Data),
                        ReleaseDate = workItem.ReleaseDate,
                        BanksoftDeliveryDate =
                            workItem.BanksoftDeliveryDate,
                        ExpectedStatus = workItem.ExpectedStatus,
                        CurrentStatus = workItem.CurrentStatus,
                        RequestNumber = workItem.RequestNumber,
                        RequestDescription = workItem.RequestDescription
                    });
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Ortak Excel güncellemesi başarısız. Hata türü: {ErrorType}",
                    exception.GetType().Name);
                excelResult = ServiceResult.Failure(
                    "Ortak Excel güncellenemedi.");
            }

            return excelResult.IsSuccess
                ? ServiceResult.Success()
                : ServiceResult.Success(
                    "Değişiklikler kaydedildi ancak ortak Excel güncellenemedi.");
        }

        private static ManagerInboxItemViewModel
            MapToInboxItemViewModel(WorkItem workItem)
        {
            return new ManagerInboxItemViewModel
            {
                Id = workItem.Id,
                RequestNumber = workItem.RequestNumber,
                RequestDescription =
                    workItem.RequestDescription,
                Department = workItem.Department,
                Priority = workItem.Priority,
                WorkflowStatus = workItem.WorkflowStatus,
                CurrentStatus = workItem.CurrentStatus,
                CreatedAt = workItem.CreatedAt,
                UpdatedAt = workItem.UpdatedAt
            };
        }

        private async Task<ManagerReviewViewModel>
            MapToReviewViewModelAsync(WorkItem workItem)
        {
            string analystDisplayName =
                await GetUserDisplayNameAsync(workItem.AnalystId);
            string developerDisplayName =
                await GetUserDisplayNameAsync(workItem.DeveloperId);
            var draft = await _draftRepository
                .GetByWorkItemIdAsNoTrackingAsync(workItem.Id);
            IList<ApplicationUser> employees =
                await _userManager.GetUsersInRoleAsync(AppRoles.Employee);

            return new ManagerReviewViewModel
            {
                Id = workItem.Id,
                RequestNumber = workItem.RequestNumber,
                RequestDescription =
                    workItem.RequestDescription,
                AiEditedRequest = NormalizeNullableText(
                    draft?.EditedRequest),
                Department = workItem.Department,
                Priority = workItem.Priority,
                WorkflowStatus = workItem.WorkflowStatus,
                CreatedAt = workItem.CreatedAt,
                AnalystId = workItem.AnalystId,
                AnalystDisplayName = analystDisplayName,
                DeveloperId = workItem.DeveloperId,
                DeveloperDisplayName = developerDisplayName,
                DeveloperOptions = employees
                    .Where(user => IsEligibleDeveloper(
                        user,
                        workItem.Department))
                    .OrderBy(user => user.FullName)
                    .ThenBy(user => user.BusinessCode)
                    .Select(user => new UserSelectionOptionViewModel
                    {
                        Id = user.Id,
                        DisplayText = GetUserDisplayName(user)
                    })
                    .ToList(),
                ReleaseDate = workItem.ReleaseDate,
                BanksoftDeliveryDate =
                    workItem.BanksoftDeliveryDate,
                ExpectedStatus = workItem.ExpectedStatus,
                CurrentStatus = workItem.CurrentStatus,
                AnalystNote = workItem.AnalystNote,
                ManagerNote = workItem.ManagerNote
            };
        }

        private async Task<string> GetUserDisplayNameAsync(int? userId)
        {
            if (!userId.HasValue)
            {
                return string.Empty;
            }

            ApplicationUser? user = await _userManager.FindByIdAsync(
                userId.Value.ToString());

            if (user == null)
            {
                return string.Empty;
            }

            return GetUserDisplayName(user);
        }

        private async Task<ServiceResult<ApplicationUser>>
            ValidateDeveloperAsync(
                int? developerId,
                string workItemDepartment)
        {
            if (!developerId.HasValue || developerId.Value <= 0)
            {
                return ServiceResult<ApplicationUser>.Failure(
                    "Onay için yazılımcı seçimi zorunludur.");
            }

            ApplicationUser? developer = await _userManager.FindByIdAsync(
                developerId.Value.ToString());
            if (developer == null ||
                !IsEligibleDeveloper(developer, workItemDepartment) ||
                !await _userManager.IsInRoleAsync(
                    developer,
                    AppRoles.Employee))
            {
                return ServiceResult<ApplicationUser>.Failure(
                    "Seçilen yazılımcı bu talep için uygun değil.");
            }

            return ServiceResult<ApplicationUser>.Success(developer);
        }

        private static bool IsEligibleDeveloper(
            ApplicationUser user,
            string workItemDepartment)
        {
            return user.EmailConfirmed &&
                user.IsApproved &&
                !string.IsNullOrWhiteSpace(user.BusinessCode) &&
                string.Equals(
                    user.Department,
                    workItemDepartment,
                    StringComparison.Ordinal);
        }

        private static string GetUserDisplayName(ApplicationUser user)
        {
            return GetUserDisplayName(user.FullName, user.BusinessCode);
        }

        private static string GetUserDisplayName(
            string? fullName,
            string? businessCode)
        {
            string normalizedName = fullName?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(businessCode)
                ? normalizedName
                : $"{normalizedName} \u2014 {businessCode.Trim()}";
        }

        private static ServiceResult<ApprovedWorkItemEditValues>
            ValidateApprovedWorkItemEdit(EditApprovedWorkItemDto dto)
        {
            string? requestDescription = NormalizeNullableText(
                dto.RequestDescription);
            string? expectedStatus = NormalizeNullableText(
                dto.ExpectedStatus);
            string? currentStatus = NormalizeNullableText(
                dto.CurrentStatus);

            if (requestDescription == null)
            {
                return ServiceResult<ApprovedWorkItemEditValues>.Failure(
                    "Talep zorunludur.");
            }

            if (requestDescription.Length > 2000)
            {
                return ServiceResult<ApprovedWorkItemEditValues>.Failure(
                    "Talep en fazla 2000 karakter olabilir.");
            }

            if (!dto.ReleaseDate.HasValue ||
                !dto.BanksoftDeliveryDate.HasValue)
            {
                return ServiceResult<ApprovedWorkItemEditValues>.Failure(
                    "Sürüm tarihi ve Banksoft teslim tarihi zorunludur.");
            }

            if (dto.BanksoftDeliveryDate.Value.Date >
                dto.ReleaseDate.Value.Date)
            {
                return ServiceResult<ApprovedWorkItemEditValues>.Failure(
                    "Banksoft teslim tarihi, sürüm tarihinden sonra olamaz.");
            }

            if (expectedStatus == null)
            {
                return ServiceResult<ApprovedWorkItemEditValues>.Failure(
                    "Beklenen statü zorunludur.");
            }

            if (expectedStatus.Length > 100)
            {
                return ServiceResult<ApprovedWorkItemEditValues>.Failure(
                    "Beklenen statü en fazla 100 karakter olabilir.");
            }

            if (currentStatus == null)
            {
                return ServiceResult<ApprovedWorkItemEditValues>.Failure(
                    "Mevcut statü zorunludur.");
            }

            bool isApprovedStatus = string.Equals(
                currentStatus,
                WorkflowStatusDescriptions.GetDescription(
                    WorkflowStatus.Approved),
                StringComparison.Ordinal);
            if (!isApprovedStatus &&
                !AnalystCurrentStatusOptions.Contains(currentStatus))
            {
                return ServiceResult<ApprovedWorkItemEditValues>.Failure(
                    "Geçerli bir mevcut statü seçiniz.");
            }

            return ServiceResult<ApprovedWorkItemEditValues>.Success(
                new ApprovedWorkItemEditValues(
                    requestDescription,
                    expectedStatus,
                    currentStatus));
        }

        private static bool TryDecodeRowVersion(
            string? encodedRowVersion,
            out byte[] rowVersion)
        {
            rowVersion = [];
            if (string.IsNullOrWhiteSpace(encodedRowVersion))
            {
                return false;
            }

            try
            {
                rowVersion = Convert.FromBase64String(encodedRowVersion);
                return rowVersion.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static string? NormalizeNullableText(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }

        private sealed record ApprovedWorkItemEditValues(
            string RequestDescription,
            string ExpectedStatus,
            string CurrentStatus);
    }
}
