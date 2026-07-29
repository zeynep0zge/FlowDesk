using FlowDesk.Common;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;

namespace FlowDesk.Services
{
    public sealed class ProjectManagerWorkItemService
        : IProjectManagerWorkItemService
    {
        private const string NotFoundMessage =
            "Talep bulunamadı.";

        private const string ForbiddenMessage =
            "Bu talebe erişim yetkiniz bulunmuyor.";

        private const string DuplicateRequestNumberMessage =
            "Bu talep numarası daha önce kullanılmış.";

        private const string EditStatusMessage =
            "Sadece 'Gönderildi' durumundaki talepler güncellenebilir.";

        private const string DeleteStatusMessage =
            "Sadece 'Gönderildi' durumundaki talepler silinebilir.";

        private readonly IWorkItemRepository _workItemRepository;

        public ProjectManagerWorkItemService(
            IWorkItemRepository workItemRepository)
        {
            _workItemRepository = workItemRepository;
        }

        public async Task<ServiceResult<List<WorkItem>>>
            GetMyRequestsAsync(int? currentUserId)
        {
            if (!IsValidUserId(currentUserId))
            {
                return ServiceResult<List<WorkItem>>.Forbidden(
                    ForbiddenMessage);
            }

            List<WorkItem> workItems =
                await _workItemRepository
                    .GetProjectManagerRequestsAsync(
                        currentUserId.GetValueOrDefault());

            return ServiceResult<List<WorkItem>>
                .Success(workItems);
        }

        public async Task<ServiceResult<WorkItem>> GetDetailsAsync(
            int id,
            int? currentUserId)
        {
            WorkItem? workItem = await _workItemRepository
                .GetByIdAsNoTrackingAsync(id);

            if (workItem == null)
            {
                return ServiceResult<WorkItem>
                    .NotFound(NotFoundMessage);
            }

            if (IsForbidden(workItem, currentUserId))
            {
                return ServiceResult<WorkItem>
                    .Forbidden(ForbiddenMessage);
            }

            return ServiceResult<WorkItem>.Success(workItem);
        }

        public async Task<ServiceResult<string>>
            ValidateCreateAsync(string requestNumber)
        {
            string normalizedRequestNumber = requestNumber.Trim();

            bool requestNumberExists =
                await _workItemRepository.RequestNumberExistsAsync(
                    normalizedRequestNumber);

            if (requestNumberExists)
            {
                return ServiceResult<string>
                    .Failure(DuplicateRequestNumberMessage);
            }

            return ServiceResult<string>
                .Success(normalizedRequestNumber);
        }

        public async Task<ServiceResult> CreateAsync(
            WorkItem workItem,
            string normalizedRequestNumber,
            int? currentUserId)
        {
            if (!IsValidUserId(currentUserId))
            {
                return ServiceResult.Forbidden(ForbiddenMessage);
            }

            workItem.RequestNumber = normalizedRequestNumber;
            workItem.RequestDescription =
                workItem.RequestDescription.Trim();
            workItem.Department = workItem.Department.Trim();
            workItem.CreatedByUserId = currentUserId.GetValueOrDefault();
            workItem.WorkflowStatus = WorkflowStatus.Submitted;
            workItem.CurrentStatus =
                "Analist İncelemesi Bekliyor";
            workItem.CreatedAt = DateTime.UtcNow;
            workItem.UpdatedAt = null;
            workItem.ApprovedAt = null;
            workItem.AnalystId = null;
            workItem.DeveloperId = null;
            workItem.ReleaseDate = null;
            workItem.BanksoftDeliveryDate = null;
            workItem.ExpectedStatus = null;
            workItem.AnalystNote = null;
            workItem.ManagerNote = null;

            _workItemRepository.Add(workItem);
            await _workItemRepository.SaveChangesAsync();

            return ServiceResult.Success();
        }

        public async Task<ServiceResult<WorkItem>> GetForEditAsync(
            int id,
            int? currentUserId)
        {
            WorkItem? workItem =
                await _workItemRepository.GetByIdAsync(id);

            if (workItem == null)
            {
                return ServiceResult<WorkItem>
                    .NotFound(NotFoundMessage);
            }

            if (IsForbidden(workItem, currentUserId))
            {
                return ServiceResult<WorkItem>
                    .Forbidden(ForbiddenMessage);
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Submitted)
            {
                return ServiceResult<WorkItem>
                    .Failure(EditStatusMessage);
            }

            return ServiceResult<WorkItem>.Success(workItem);
        }

        public async Task<ServiceResult<string>> ValidateUpdateAsync(
            int id,
            string requestNumber)
        {
            string normalizedRequestNumber = requestNumber.Trim();

            bool requestNumberExists =
                await _workItemRepository.RequestNumberExistsAsync(
                    normalizedRequestNumber,
                    id);

            if (requestNumberExists)
            {
                return ServiceResult<string>
                    .Failure(DuplicateRequestNumberMessage);
            }

            return ServiceResult<string>
                .Success(normalizedRequestNumber);
        }

        public async Task<ServiceResult> UpdateAsync(
            int id,
            WorkItem changes,
            string normalizedRequestNumber,
            int? currentUserId)
        {
            WorkItem? workItem =
                await _workItemRepository.GetByIdAsync(id);

            if (workItem == null)
            {
                return ServiceResult.NotFound(NotFoundMessage);
            }

            if (IsForbidden(workItem, currentUserId))
            {
                return ServiceResult.Forbidden(ForbiddenMessage);
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Submitted)
            {
                return ServiceResult.Failure(EditStatusMessage);
            }

            workItem.RequestNumber = normalizedRequestNumber;
            workItem.RequestDescription =
                changes.RequestDescription.Trim();
            workItem.Department = changes.Department.Trim();
            workItem.Priority = changes.Priority;
            workItem.UpdatedAt = DateTime.UtcNow;

            await _workItemRepository.SaveChangesAsync();

            return ServiceResult.Success();
        }

        public async Task<ServiceResult<WorkItem>> GetForDeleteAsync(
            int id,
            int? currentUserId)
        {
            WorkItem? workItem = await _workItemRepository
                .GetByIdAsNoTrackingAsync(id);

            if (workItem == null)
            {
                return ServiceResult<WorkItem>
                    .NotFound(NotFoundMessage);
            }

            if (IsForbidden(workItem, currentUserId))
            {
                return ServiceResult<WorkItem>
                    .Forbidden(ForbiddenMessage);
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Submitted)
            {
                return ServiceResult<WorkItem>
                    .Failure(DeleteStatusMessage);
            }

            return ServiceResult<WorkItem>.Success(workItem);
        }

        public async Task<ServiceResult> DeleteAsync(
            int id,
            int? currentUserId)
        {
            WorkItem? workItem =
                await _workItemRepository.GetByIdAsync(id);

            if (workItem == null)
            {
                return ServiceResult.NotFound(NotFoundMessage);
            }

            if (IsForbidden(workItem, currentUserId))
            {
                return ServiceResult.Forbidden(ForbiddenMessage);
            }

            if (workItem.WorkflowStatus != WorkflowStatus.Submitted)
            {
                return ServiceResult.Failure(DeleteStatusMessage);
            }

            _workItemRepository.Remove(workItem);
            await _workItemRepository.SaveChangesAsync();

            return ServiceResult.Success();
        }

        private static bool IsForbidden(
            WorkItem workItem,
            int? currentUserId)
        {
            return !IsValidUserId(currentUserId) ||
                   workItem.CreatedByUserId !=
                       currentUserId.GetValueOrDefault();
        }

        private static bool IsValidUserId(int? currentUserId)
        {
            return currentUserId.HasValue && currentUserId.Value > 0;
        }
    }
}
