using FlowDesk.Common;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;

namespace FlowDesk.Services;

public sealed class ProjectManagerWorkItemService
    : IProjectManagerWorkItemService
{
    private const string NotFoundMessage = "Talep bulunamad\u0131.";
    private const string ForbiddenMessage =
        "Bu talebe eri\u015fim yetkiniz bulunmuyor.";
    private const string EditStatusMessage =
        "Sadece 'G\u00f6nderildi' durumundaki talepler g\u00fcncellenebilir.";
    private const string DeleteStatusMessage =
        "Sadece 'G\u00f6nderildi' durumundaki talepler silinebilir.";
    private const string InvalidPriorityMessage =
        "Geçerli bir öncelik seçiniz.";

    private readonly IWorkItemRepository _workItemRepository;
    private readonly IIdentifierGenerator _identifierGenerator;

    public ProjectManagerWorkItemService(
        IWorkItemRepository workItemRepository,
        IIdentifierGenerator identifierGenerator)
    {
        _workItemRepository = workItemRepository;
        _identifierGenerator = identifierGenerator;
    }

    public async Task<ServiceResult<List<WorkItem>>> GetMyRequestsAsync(
        int? currentUserId)
    {
        if (!IsValidUserId(currentUserId))
        {
            return ServiceResult<List<WorkItem>>.Forbidden(
                ForbiddenMessage);
        }

        List<WorkItem> workItems = await _workItemRepository
            .GetProjectManagerRequestsAsync(
                currentUserId.GetValueOrDefault());
        return ServiceResult<List<WorkItem>>.Success(workItems);
    }

    public async Task<ServiceResult<WorkItem>> GetDetailsAsync(
        int id,
        int? currentUserId)
    {
        WorkItem? workItem =
            await _workItemRepository.GetByIdAsNoTrackingAsync(id);
        return AuthorizeRead(workItem, currentUserId);
    }

    public async Task<ServiceResult> CreateAsync(
        WorkItem workItem,
        int? currentUserId)
    {
        if (!IsValidUserId(currentUserId))
        {
            return ServiceResult.Forbidden(ForbiddenMessage);
        }

        if (!IsValidPriority(workItem.Priority))
        {
            return ServiceResult.Failure(InvalidPriorityMessage);
        }

        try
        {
            workItem.RequestNumber =
                await _identifierGenerator.GenerateWorkItemCodeAsync();
        }
        catch (InvalidOperationException)
        {
            return ServiceResult.Failure(
                "Talep numaras\u0131 olu\u015fturulamad\u0131.");
        }

        workItem.RequestDescription = workItem.RequestDescription.Trim();
        workItem.Department = workItem.Department.Trim();
        workItem.CreatedByUserId = currentUserId.GetValueOrDefault();
        workItem.WorkflowStatus = WorkflowStatus.Submitted;
        workItem.CurrentStatus = WorkflowStatusDescriptions.GetDescription(
            WorkflowStatus.Submitted);
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
        WorkItem? workItem = await _workItemRepository.GetByIdAsync(id);
        ServiceResult<WorkItem> access =
            AuthorizeRead(workItem, currentUserId);

        if (!access.IsSuccess)
        {
            return access;
        }

        return WorkflowStatusPolicy.CanProjectManagerEdit(
            workItem!.WorkflowStatus)
            ? ServiceResult<WorkItem>.Success(workItem)
            : ServiceResult<WorkItem>.Failure(EditStatusMessage);
    }

    public async Task<ServiceResult> UpdateAsync(
        int id,
        WorkItem changes,
        int? currentUserId)
    {
        if (!IsValidPriority(changes.Priority))
        {
            return ServiceResult.Failure(InvalidPriorityMessage);
        }

        WorkItem? workItem = await _workItemRepository.GetByIdAsync(id);
        if (workItem == null)
        {
            return ServiceResult.NotFound(NotFoundMessage);
        }

        if (IsForbidden(workItem, currentUserId))
        {
            return ServiceResult.Forbidden(ForbiddenMessage);
        }

        if (!WorkflowStatusPolicy.CanProjectManagerEdit(
                workItem.WorkflowStatus))
        {
            return ServiceResult.Failure(EditStatusMessage);
        }

        workItem.RequestDescription = changes.RequestDescription.Trim();
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
        WorkItem? workItem =
            await _workItemRepository.GetByIdAsNoTrackingAsync(id);
        ServiceResult<WorkItem> access =
            AuthorizeRead(workItem, currentUserId);

        if (!access.IsSuccess)
        {
            return access;
        }

        return WorkflowStatusPolicy.CanProjectManagerDelete(
            workItem!.WorkflowStatus)
            ? ServiceResult<WorkItem>.Success(workItem)
            : ServiceResult<WorkItem>.Failure(DeleteStatusMessage);
    }

    public async Task<ServiceResult> DeleteAsync(
        int id,
        int? currentUserId)
    {
        WorkItem? workItem = await _workItemRepository.GetByIdAsync(id);
        if (workItem == null)
        {
            return ServiceResult.NotFound(NotFoundMessage);
        }

        if (IsForbidden(workItem, currentUserId))
        {
            return ServiceResult.Forbidden(ForbiddenMessage);
        }

        if (!WorkflowStatusPolicy.CanProjectManagerDelete(
                workItem.WorkflowStatus))
        {
            return ServiceResult.Failure(DeleteStatusMessage);
        }

        _workItemRepository.Remove(workItem);
        await _workItemRepository.SaveChangesAsync();
        return ServiceResult.Success();
    }

    private static ServiceResult<WorkItem> AuthorizeRead(
        WorkItem? workItem,
        int? currentUserId)
    {
        if (workItem == null)
        {
            return ServiceResult<WorkItem>.NotFound(NotFoundMessage);
        }

        return IsForbidden(workItem, currentUserId)
            ? ServiceResult<WorkItem>.Forbidden(ForbiddenMessage)
            : ServiceResult<WorkItem>.Success(workItem);
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

    private static bool IsValidPriority(RequestPriority priority)
    {
        return Enum.IsDefined(typeof(RequestPriority), priority);
    }
}
