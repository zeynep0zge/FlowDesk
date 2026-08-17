using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.Analyst;
using FlowDesk.ViewModels.DepartmentManager;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.Services;

public class AnalystWorkflowService : IAnalystWorkflowService
{
    private const string InvalidActorMessage =
        "Gecerli analist kimligi bulunamadi.";
    private const string NotFoundMessage = "Talep bulunamadı.";

    private readonly IWorkItemRepository _workItemRepository;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AnalystWorkflowValidator _validator = new();

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
                InvalidActorMessage);
        }

        int analystId = currentAnalystId.GetValueOrDefault();
        List<WorkItem> workItems =
            await _workItemRepository.GetAnalystInboxAsync(analystId);
        int returnedCount = await _workItemRepository
            .GetReturnedRequestsCountAsync(analystId);

        AnalystInboxViewModel viewModel = new()
        {
            WorkItems = workItems
                .Select(AnalystWorkflowMapper.ToInboxItem)
                .ToList(),
            ReturnedCount = returnedCount
        };

        return ServiceResult<AnalystInboxViewModel>.Success(viewModel);
    }

    public async Task<ServiceResult<List<AnalystInboxItemViewModel>>>
        GetReturnedRequestsAsync(int? currentAnalystId)
    {
        if (!IsValidUserId(currentAnalystId))
        {
            return ServiceResult<List<AnalystInboxItemViewModel>>
                .Forbidden(InvalidActorMessage);
        }

        List<WorkItem> workItems = await _workItemRepository
            .GetReturnedRequestsAsync(
                currentAnalystId.GetValueOrDefault());
        List<AnalystInboxItemViewModel> viewModels = workItems
            .Select(AnalystWorkflowMapper.ToInboxItem)
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
                InvalidActorMessage);
        }

        string? idError = _validator.ValidateWorkItemId(id);
        if (idError != null)
        {
            return ServiceResult<AnalystReviewViewModel>.Failure(idError);
        }

        WorkItem? workItem = await _workItemRepository
            .GetByIdWithFeedbackAsNoTrackingAsync(id);
        if (workItem == null)
        {
            return ServiceResult<AnalystReviewViewModel>.NotFound(
                NotFoundMessage);
        }

        if (workItem.AnalystId != currentAnalystId.GetValueOrDefault())
        {
            return ServiceResult<AnalystReviewViewModel>.Forbidden(
                "Bu talebe erisim yetkiniz bulunmuyor.");
        }

        if (!WorkflowStatusPolicy.CanAnalystSaveAnalysis(
                workItem.WorkflowStatus))
        {
            return ServiceResult<AnalystReviewViewModel>.Failure(
                "Bu talep analist tarafından düzenlenebilecek aşamada " +
                "değildir.");
        }

        DateTime readAt = DateTime.UtcNow;
        await _workItemRepository.MarkFeedbackMessagesReadAsync(
            workItem.Id,
            currentAnalystId.GetValueOrDefault(),
            readAt);
        foreach (FeedbackMessage message in workItem.FeedbackMessages.Where(
                     message =>
                         message.SenderUserId !=
                             currentAnalystId.GetValueOrDefault() &&
                         !message.IsRead))
        {
            message.IsRead = true;
            message.ReadAt = readAt;
        }

        return ServiceResult<AnalystReviewViewModel>.Success(
            await MapToReviewViewModelAsync(workItem));
    }

    public async Task<ServiceResult> StartReviewAsync(
        int id,
        int? currentAnalystId)
    {
        if (!IsValidUserId(currentAnalystId))
        {
            return ServiceResult.Forbidden(InvalidActorMessage);
        }

        string? idError = _validator.ValidateWorkItemId(id);
        if (idError != null)
        {
            return ServiceResult.Failure(idError);
        }

        int analystId = currentAnalystId.GetValueOrDefault();
        WorkItem? workItem =
            await _workItemRepository.GetByIdAsync(id);
        if (workItem == null)
        {
            return ServiceResult.NotFound(NotFoundMessage);
        }

        if (workItem.WorkflowStatus != WorkflowStatus.Submitted &&
            workItem.AnalystId != analystId)
        {
            return ServiceResult.Forbidden(
                "Bu talebe erisim yetkiniz bulunmuyor.");
        }

        if (workItem.WorkflowStatus == WorkflowStatus.Submitted &&
            workItem.AnalystId.HasValue &&
            workItem.AnalystId != analystId)
        {
            return ServiceResult.Forbidden(
                "Bu talep baska bir analist tarafindan sahiplenilmis.");
        }

        if (!WorkflowStatusPolicy.CanAnalystStartReview(
                workItem.WorkflowStatus))
        {
            return ServiceResult.Failure(
                "Bu talep analist tarafından incelenebilecek aşamada " +
                "değildir.");
        }

        if (workItem.WorkflowStatus == WorkflowStatus.Submitted)
        {
            workItem.AnalystId = analystId;
            workItem.WorkflowStatus = WorkflowStatus.UnderAnalystReview;
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
                InvalidActorMessage);
        }

        string? idError = _validator.ValidateWorkItemId(dto.WorkItemId);
        if (idError != null)
        {
            return ServiceResult<AnalystReviewViewModel>.Failure(idError);
        }

        int analystId = currentAnalystId.GetValueOrDefault();
        WorkItem? workItem = await _workItemRepository
            .GetByIdAsync(dto.WorkItemId);
        if (workItem == null)
        {
            return ServiceResult<AnalystReviewViewModel>.NotFound(
                NotFoundMessage);
        }

        if (workItem.AnalystId != analystId)
        {
            return ServiceResult<AnalystReviewViewModel>.Forbidden(
                "Bu talebi degistirme yetkiniz bulunmuyor.");
        }

        if (!WorkflowStatusPolicy.CanAnalystSaveAnalysis(
                workItem.WorkflowStatus))
        {
            return ServiceResult<AnalystReviewViewModel>.Failure(
                "Bu talep analist tarafından düzenlenebilecek aşamada " +
                "değildir.");
        }

        AnalystWorkflowValidationResult validation =
            _validator.ValidateSave(dto);
        if (!validation.IsValid)
        {
            return ServiceResult<AnalystReviewViewModel>.Failure(
                validation.ErrorMessage!);
        }

        ServiceResult developerResult = await ValidateDeveloperAsync(
            dto.DeveloperId,
            workItem.Department);
        if (!developerResult.IsSuccess)
        {
            return ServiceResult<AnalystReviewViewModel>.Failure(
                developerResult.ErrorMessage!);
        }

        ApplySavedAnalysis(
            workItem,
            dto,
            validation,
            analystId);
        await _workItemRepository.SaveChangesAsync();

        return ServiceResult<AnalystReviewViewModel>.Success(
            await MapToReviewViewModelAsync(workItem));
    }

    public async Task<ServiceResult> SubmitForApprovalAsync(
        SubmitForApprovalDto dto,
        int? currentAnalystId)
    {
        if (!IsValidUserId(currentAnalystId))
        {
            return ServiceResult.Forbidden(InvalidActorMessage);
        }

        int analystId = currentAnalystId.GetValueOrDefault();
        dto.AnalystId = analystId;

        string? idError = _validator.ValidateWorkItemId(dto.WorkItemId);
        if (idError != null)
        {
            return ServiceResult.Failure(idError);
        }

        WorkItem? workItem = await _workItemRepository
            .GetByIdAsync(dto.WorkItemId);
        if (workItem == null)
        {
            return ServiceResult.NotFound(NotFoundMessage);
        }

        if (workItem.AnalystId != analystId)
        {
            return ServiceResult.Forbidden(
                "Bu talebi gonderme yetkiniz bulunmuyor.");
        }

        if (workItem.WorkflowStatus ==
            WorkflowStatus.WaitingManagerApproval)
        {
            return ServiceResult.Failure(
                "Bu talep zaten departman yöneticisi onayına " +
                "gönderilmiş.");
        }

        if (!WorkflowStatusPolicy.CanAnalystSubmitForManagerApproval(
                workItem.WorkflowStatus))
        {
            return ServiceResult.Failure(
                "Bu talep yönetici onayına gönderilebilecek aşamada " +
                "değildir.");
        }

        AnalystWorkflowValidationResult validation =
            _validator.ValidateSubmit(dto);
        if (!validation.IsValid)
        {
            return ServiceResult.Failure(validation.ErrorMessage!);
        }

        ServiceResult developerResult = await ValidateDeveloperAsync(
            dto.DeveloperId,
            workItem.Department);
        if (!developerResult.IsSuccess)
        {
            return developerResult;
        }

        ApplySubmittedAnalysis(
            workItem,
            dto,
            validation,
            analystId);
        await _workItemRepository.SaveChangesAsync();

        return ServiceResult.Success();
    }

    public async Task<ServiceResult<WorkItem>> GetFeedbackAsync(
        int id,
        int? currentAnalystId)
    {
        if (!IsValidUserId(currentAnalystId))
        {
            return ServiceResult<WorkItem>.Forbidden(
                InvalidActorMessage);
        }

        string? idError = _validator.ValidateWorkItemId(id);
        if (idError != null)
        {
            return ServiceResult<WorkItem>.Failure(idError);
        }

        WorkItem? workItem = await _workItemRepository
            .GetByIdAsNoTrackingAsync(id);
        if (workItem == null)
        {
            return ServiceResult<WorkItem>.NotFound(NotFoundMessage);
        }

        if (workItem.AnalystId != currentAnalystId.GetValueOrDefault())
        {
            return ServiceResult<WorkItem>.Forbidden(
                "Bu talebe geri bildirim gönderme yetkiniz bulunmuyor.");
        }

        return ServiceResult<WorkItem>.Success(workItem);
    }

    public async Task<ServiceResult> SendFeedbackAsync(
        SendAnalystFeedbackDto dto,
        int? currentAnalystId)
    {
        if (!IsValidUserId(currentAnalystId))
        {
            return ServiceResult.Forbidden(InvalidActorMessage);
        }

        string? idError = _validator.ValidateWorkItemId(dto.WorkItemId);
        if (idError != null)
        {
            return ServiceResult.Failure(idError);
        }

        WorkItem? workItem = await _workItemRepository
            .GetByIdAsync(dto.WorkItemId);
        if (workItem == null)
        {
            return ServiceResult.NotFound(NotFoundMessage);
        }

        if (workItem.AnalystId != currentAnalystId.GetValueOrDefault())
        {
            return ServiceResult.Forbidden(
                "Bu talebe geri bildirim gönderme yetkiniz bulunmuyor.");
        }

        if (!dto.FeedbackStatus.HasValue ||
            !AnalystFeedbackStatusOptions.Contains(
                dto.FeedbackStatus.Value))
        {
            return ServiceResult.Failure(
                "Geçerli bir geri bildirim durumu seçiniz.");
        }

        string? description = string.IsNullOrWhiteSpace(dto.Description)
            ? null
            : dto.Description.Trim();
        if (description == null)
        {
            return ServiceResult.Failure(
                "Geri bildirim açıklaması zorunludur.");
        }

        if (description.Length > 500)
        {
            return ServiceResult.Failure(
                "Geri bildirim açıklaması en fazla 500 karakter olabilir.");
        }

        DateTime now = DateTime.UtcNow;
        workItem.AnalystFeedbackStatus = dto.FeedbackStatus.Value;
        workItem.AnalystFeedbackDescription = description;
        workItem.AnalystFeedbackAt = now;
        workItem.UpdatedAt = now;
        await _workItemRepository.SaveChangesAsync();
        return ServiceResult.Success();
    }

    public async Task<ServiceResult> SendReviewFeedbackAsync(
        int workItemId,
        string? message,
        int? currentAnalystId)
    {
        if (!IsValidUserId(currentAnalystId))
        {
            return ServiceResult.Forbidden(InvalidActorMessage);
        }

        string? normalizedMessage = NormalizeFeedbackMessage(message);
        if (normalizedMessage == null)
        {
            return ServiceResult.Failure("Mesaj zorunludur.");
        }

        WorkItem? workItem = await _workItemRepository
            .GetByIdAsync(workItemId);
        if (workItem == null)
        {
            return ServiceResult.NotFound(NotFoundMessage);
        }

        int analystId = currentAnalystId.GetValueOrDefault();
        if (workItem.AnalystId != analystId)
        {
            return ServiceResult.Forbidden(
                "Bu talebe mesaj gönderme yetkiniz bulunmuyor.");
        }

        if (!WorkflowStatusPolicy.CanAnalystSaveAnalysis(
                workItem.WorkflowStatus))
        {
            return ServiceResult.Failure(
                "Bu talep inceleme aşamasında değildir.");
        }

        _workItemRepository.AddFeedbackMessage(new FeedbackMessage
        {
            WorkItemId = workItem.Id,
            SenderUserId = analystId,
            Message = normalizedMessage,
            CreatedAt = DateTime.UtcNow
        });
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
            !await _userManager.IsInRoleAsync(developer, AppRoles.Employee) ||
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

    private static void ApplySavedAnalysis(
        WorkItem workItem,
        SaveAnalysisDto dto,
        AnalystWorkflowValidationResult validation,
        int analystId)
    {
        workItem.AnalystId = analystId;
        workItem.DeveloperId = dto.DeveloperId;
        workItem.ReleaseDate = dto.ReleaseDate;
        workItem.BanksoftDeliveryDate = dto.BanksoftDeliveryDate;
        workItem.ExpectedStatus = validation.ExpectedStatus;
        workItem.AnalystNote = validation.AnalystNote;
        workItem.CurrentStatus = validation.CurrentStatus ??
            WorkflowStatusDescriptions.GetDescription(
                WorkflowStatus.UnderAnalystReview);
        workItem.WorkflowStatus = WorkflowStatus.UnderAnalystReview;
        workItem.UpdatedAt = DateTime.UtcNow;
    }

    private static void ApplySubmittedAnalysis(
        WorkItem workItem,
        SubmitForApprovalDto dto,
        AnalystWorkflowValidationResult validation,
        int analystId)
    {
        workItem.AnalystId = analystId;
        workItem.DeveloperId = dto.DeveloperId;
        workItem.ReleaseDate = dto.ReleaseDate;
        workItem.BanksoftDeliveryDate = dto.BanksoftDeliveryDate;
        workItem.ExpectedStatus = validation.ExpectedStatus;
        workItem.AnalystNote = validation.AnalystNote;
        workItem.WorkflowStatus = WorkflowStatus.WaitingManagerApproval;
        workItem.CurrentStatus =
            WorkflowStatusDescriptions.GetDescription(
                WorkflowStatus.WaitingManagerApproval);
        workItem.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<AnalystReviewViewModel>
        MapToReviewViewModelAsync(WorkItem workItem)
    {
        AnalystReviewViewModel viewModel =
            AnalystWorkflowMapper.ToReview(workItem);

        ApplicationUser? analyst = workItem.AnalystId.HasValue
            ? await _userManager.FindByIdAsync(
                workItem.AnalystId.Value.ToString())
            : null;
        viewModel.AnalystDisplayName = GetUserDisplayName(analyst);

        IList<ApplicationUser> employees =
            await _userManager.GetUsersInRoleAsync(AppRoles.Employee);
        viewModel.DeveloperOptions = employees
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
                DisplayText = GetUserDisplayName(user)
            })
            .ToList();

        return viewModel;
    }

    private static string GetUserDisplayName(ApplicationUser? user)
    {
        if (user == null)
        {
            return string.Empty;
        }

        return string.IsNullOrWhiteSpace(user.BusinessCode)
            ? user.FullName
            : $"{user.FullName} \u2014 {user.BusinessCode}";
    }

    private static bool IsValidUserId(int? userId)
    {
        return userId.HasValue && userId.Value > 0;
    }

    private static string? NormalizeFeedbackMessage(string? message)
    {
        string? normalized = string.IsNullOrWhiteSpace(message)
            ? null
            : message.Trim();

        return normalized?.Length <= 2000 ? normalized : null;
    }
}
