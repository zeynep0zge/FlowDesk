using FlowDesk.Common;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.Constants;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.DepartmentManager;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.Services
{
    public class DepartmentManagerWorkflowService
        : IDepartmentManagerWorkflowService
    {
        private readonly IWorkItemRepository _workItemRepository;
        private readonly UserManager<ApplicationUser> _userManager;

        public DepartmentManagerWorkflowService(
            IWorkItemRepository workItemRepository,
            UserManager<ApplicationUser> userManager)
        {
            _workItemRepository = workItemRepository;
            _userManager = userManager;
        }

        public async Task<ServiceResult<ManagerInboxViewModel>>
            GetInboxAsync(int? managerUserId)
        {
            ServiceResult<string> departmentResult =
                await GetManagerDepartmentAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult<ManagerInboxViewModel>.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            List<WorkItem> workItems =
                await _workItemRepository
                    .GetWaitingManagerApprovalAsync(
                        departmentResult.Data!);

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
                GetApprovedRequestForExportAsync(
                    int id,
                    int? managerUserId)
        {
            ServiceResult<string> departmentResult =
                await GetManagerDepartmentAsync(managerUserId);

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
                    .GetByIdInDepartmentAsNoTrackingAsync(
                        id,
                        departmentResult.Data!);

            if (workItem == null)
            {
                return ServiceResult<ManagerReviewViewModel>
                    .NotFound("Talep bulunamadı.");
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Approved)
            {
                return ServiceResult<ManagerReviewViewModel>
                    .Failure("Yalnızca onaylanmış talepler Excel olarak indirilebilir.");
            }

            return ServiceResult<ManagerReviewViewModel>
                .Success(MapToReviewViewModel(workItem));
        }
        public async Task<ServiceResult<ManagerReviewViewModel>>
            GetReviewAsync(int id, int? managerUserId)
        {
            ServiceResult<string> departmentResult =
                await GetManagerDepartmentAsync(managerUserId);

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
                    .GetByIdInDepartmentAsNoTrackingAsync(
                        id,
                        departmentResult.Data!);

            if (workItem == null)
            {
                return ServiceResult<ManagerReviewViewModel>
                    .NotFound("Talep bulunamadı.");
            }

            if (workItem.WorkflowStatus !=
                WorkflowStatus.WaitingManagerApproval)
            {
                return ServiceResult<ManagerReviewViewModel>
                    .Failure(
                        "Bu talep yönetici onayı beklemiyor."
                    );
            }

            ManagerReviewViewModel viewModel =
                MapToReviewViewModel(workItem);

            return ServiceResult<ManagerReviewViewModel>
                .Success(viewModel);
        }

        public async Task<ServiceResult>
            ApproveRequestAsync(
                ApproveRequestDto dto,
                int? managerUserId)
        {
            ServiceResult<string> departmentResult =
                await GetManagerDepartmentAsync(managerUserId);

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
                    .GetByIdInDepartmentAsync(
                        dto.WorkItemId,
                        departmentResult.Data!);

            if (workItem == null)
            {
                return ServiceResult.NotFound(
                    "Talep bulunamadı."
                );
            }

            if (workItem.WorkflowStatus !=
                WorkflowStatus.WaitingManagerApproval)
            {
                return ServiceResult.Failure(
                    "Yalnızca yönetici onayı bekleyen " +
                    "talepler onaylanabilir."
                );
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

            DateTime now = DateTime.UtcNow;

            workItem.ManagerNote = managerNote;
            workItem.WorkflowStatus = WorkflowStatus.Approved;
            workItem.CurrentStatus =
                "Departman Yöneticisi Tarafından Onaylandı";
            workItem.ApprovedAt = now;
            workItem.UpdatedAt = now;

            await _workItemRepository.SaveChangesAsync();

            return ServiceResult.Success();
        }

        public async Task<ServiceResult>
            ReturnToAnalystAsync(
                ReturnToAnalystDto dto,
                int? managerUserId)
        {
            ServiceResult<string> departmentResult =
                await GetManagerDepartmentAsync(managerUserId);

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
                    .GetByIdInDepartmentAsync(
                        dto.WorkItemId,
                        departmentResult.Data!);

            if (workItem == null)
            {
                return ServiceResult.NotFound(
                    "Talep bulunamadı."
                );
            }

            if (workItem.WorkflowStatus !=
                WorkflowStatus.WaitingManagerApproval)
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
                "Departman Yöneticisi Tarafından Analiste İade Edildi";
            workItem.ApprovedAt = null;
            workItem.UpdatedAt = DateTime.UtcNow;

            await _workItemRepository.SaveChangesAsync();

            return ServiceResult.Success();
        }

        public async Task<
            ServiceResult<List<ManagerInboxItemViewModel>>>
            GetApprovedRequestsAsync(int? managerUserId)
        {
            ServiceResult<string> departmentResult =
                await GetManagerDepartmentAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult<
                    List<ManagerInboxItemViewModel>>.Forbidden(
                        departmentResult.ErrorMessage!);
            }

            List<WorkItem> workItems =
                await _workItemRepository
                    .GetApprovedRequestsAsync(
                        departmentResult.Data!);

            List<ManagerInboxItemViewModel> viewModels =
                workItems
                    .Select(MapToInboxItemViewModel)
                    .ToList();

            return ServiceResult<
                List<ManagerInboxItemViewModel>>
                .Success(viewModels);
        }

        private async Task<ServiceResult<string>>
            GetManagerDepartmentAsync(int? managerUserId)
        {
            if (!managerUserId.HasValue || managerUserId.Value <= 0)
            {
                return ServiceResult<string>.Forbidden(
                    "Gecerli departman yoneticisi kimligi bulunamadi.");
            }

            ApplicationUser? manager = await _userManager.FindByIdAsync(
                managerUserId.Value.ToString());

            if (manager == null ||
                !DepartmentOptions.Contains(manager.Department) ||
                !await _userManager.IsInRoleAsync(
                    manager,
                    AppRoles.DepartmentManager))
            {
                return ServiceResult<string>.Forbidden(
                    "Departman yoneticisi departmani gecersiz.");
            }

            return ServiceResult<string>.Success(manager.Department!);
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

        private static ManagerReviewViewModel
            MapToReviewViewModel(WorkItem workItem)
        {
            return new ManagerReviewViewModel
            {
                Id = workItem.Id,
                RequestNumber = workItem.RequestNumber,
                RequestDescription =
                    workItem.RequestDescription,
                Department = workItem.Department,
                Priority = workItem.Priority,
                WorkflowStatus = workItem.WorkflowStatus,
                CreatedAt = workItem.CreatedAt,
                AnalystId = workItem.AnalystId,
                DeveloperId = workItem.DeveloperId,
                ReleaseDate = workItem.ReleaseDate,
                BanksoftDeliveryDate =
                    workItem.BanksoftDeliveryDate,
                ExpectedStatus = workItem.ExpectedStatus,
                CurrentStatus = workItem.CurrentStatus,
                AnalystNote = workItem.AnalystNote,
                ManagerNote = workItem.ManagerNote
            };
        }

        private static string? NormalizeNullableText(
            string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
        }
    }
}
