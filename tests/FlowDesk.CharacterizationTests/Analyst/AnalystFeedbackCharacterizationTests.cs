using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;

namespace FlowDesk.CharacterizationTests.Analyst;

public sealed class AnalystFeedbackCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public void FeedbackOptions_UseExistingWorkflowStatusesAndRequiredLabels()
    {
        Assert.Equal(
            new[]
            {
                "İncelemede",
                "Yöneticiye gönderildi",
                "Onaylandı",
                "Reddedildi",
                "Ek bilgi gerekli",
                "İade Et"
            },
            AnalystFeedbackStatusOptions.All
                .Select(option => option.Value)
                .ToArray());
    }

    [Theory]
    [InlineData(WorkflowStatus.UnderAnalystReview)]
    [InlineData(WorkflowStatus.WaitingManagerApproval)]
    [InlineData(WorkflowStatus.Approved)]
    [InlineData(WorkflowStatus.Rejected)]
    [InlineData(WorkflowStatus.ReturnedToAnalyst)]
    public async Task SendFeedback_AllowedStatus_PersistsLatestFeedback(
        WorkflowStatus feedbackStatus)
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Approved,
                analystId: 11);
            WorkflowStatus originalWorkflowStatus = workItem.WorkflowStatus;
            IAnalystWorkflowService service = services
                .GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SendFeedbackAsync(
                new SendAnalystFeedbackDto
                {
                    WorkItemId = workItem.Id,
                    FeedbackStatus = feedbackStatus,
                    Description = "  İş Birimine kısa açıklama.  "
                },
                11);

            Assert.True(result.IsSuccess);
            Assert.Equal(feedbackStatus, workItem.AnalystFeedbackStatus);
            Assert.Equal("İş Birimine kısa açıklama.",
                workItem.AnalystFeedbackDescription);
            Assert.NotNull(workItem.AnalystFeedbackAt);
            Assert.Equal(originalWorkflowStatus, workItem.WorkflowStatus);
        });
    }

    [Fact]
    public async Task SendFeedback_ReturnLabel_DoesNotChangeWorkflowStatus()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                createdByUserId: 101,
                analystId: 11);
            IAnalystWorkflowService service = services
                .GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SendFeedbackAsync(
                new SendAnalystFeedbackDto
                {
                    WorkItemId = workItem.Id,
                    FeedbackStatus =
                        WorkflowStatus.ReturnedToBusinessUnit,
                    Description = "Eksik gereksinimler tamamlanmalı."
                },
                11);

            Assert.True(result.IsSuccess);
            Assert.Equal(WorkflowStatus.UnderAnalystReview,
                workItem.WorkflowStatus);
            Assert.Equal(WorkflowStatus.ReturnedToBusinessUnit,
                workItem.AnalystFeedbackStatus);
            Assert.Equal("Eksik gereksinimler tamamlanmalı.",
                workItem.AnalystFeedbackDescription);
            Assert.NotNull(workItem.AnalystFeedbackAt);
            Assert.NotNull(workItem.UpdatedAt);

            IProjectManagerWorkItemService projectManagerService = services
                .GetRequiredService<IProjectManagerWorkItemService>();
            var ownerResult = await projectManagerService
                .GetDetailsAsync(workItem.Id, 101);
            var otherUserResult = await projectManagerService
                .GetDetailsAsync(workItem.Id, 102);

            Assert.True(ownerResult.IsSuccess);
            Assert.Equal(WorkflowStatus.UnderAnalystReview,
                ownerResult.Data!.WorkflowStatus);
            Assert.Equal("Eksik gereksinimler tamamlanmalı.",
                ownerResult.Data.AnalystFeedbackDescription);
            Assert.True(otherUserResult.IsForbidden);
        });
    }

    [Fact]
    public async Task SendFeedback_ApprovedWorkflow_RemainsApproved()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Approved,
                analystId: 11);
            IAnalystWorkflowService service = services
                .GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SendFeedbackAsync(
                new SendAnalystFeedbackDto
                {
                    WorkItemId = workItem.Id,
                    FeedbackStatus =
                        WorkflowStatus.ReturnedToBusinessUnit,
                    Description = "Geçersiz iade denemesi."
                },
                11);

            Assert.True(result.IsSuccess);
            Assert.Equal(WorkflowStatus.Approved,
                workItem.WorkflowStatus);
            Assert.Equal(WorkflowStatus.ReturnedToBusinessUnit,
                workItem.AnalystFeedbackStatus);
        });
    }

    [Fact]
    public async Task ReturnedToBusinessUnit_AppearsWithoutReviewAction()
    {
        WorkItem workItem = await WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.ReturnedToBusinessUnit,
                analystId: 11));
        using HttpClient client = CreateClient().AuthenticateAs(
            11,
            AppRoles.Analyst,
            TestDataSeeder.UniqueEmail("returning-analyst"));

        string html = await client.GetStringAsync("/Analyst/Inbox");

        Assert.Contains(workItem.RequestNumber, html);
        Assert.Contains("İade Edildi", html);
        Assert.DoesNotContain("İncelemeye Devam Et", html);
        Assert.False(WorkflowStatusPolicy.CanAnalystSaveAnalysis(
            WorkflowStatus.ReturnedToBusinessUnit));

        await WithServicesAsync(async services =>
        {
            var inbox = await services
                .GetRequiredService<IAnalystWorkflowService>()
                .GetInboxAsync(11);

            Assert.True(inbox.IsSuccess);
            Assert.Equal(0, inbox.Data!.ReviewingCount);
        });
    }

    [Fact]
    public async Task SendFeedback_DifferentAnalyst_IsForbidden()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service = services
                .GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SendFeedbackAsync(
                ValidDto(workItem.Id),
                12);

            Assert.True(result.IsForbidden);
            Assert.Null(workItem.AnalystFeedbackStatus);
            Assert.Null(workItem.AnalystFeedbackDescription);
            Assert.Null(workItem.AnalystFeedbackAt);
        });
    }

    [Fact]
    public async Task SendFeedback_NonFeedbackWorkflowStatus_IsRejected()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            SendAnalystFeedbackDto dto = ValidDto(workItem.Id);
            dto.FeedbackStatus = WorkflowStatus.Submitted;
            IAnalystWorkflowService service = services
                .GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SendFeedbackAsync(dto, 11);

            Assert.False(result.IsSuccess);
            Assert.Null(workItem.AnalystFeedbackStatus);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendFeedback_EmptyDescription_IsRejected(
        string description)
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            SendAnalystFeedbackDto dto = ValidDto(workItem.Id);
            dto.Description = description;
            IAnalystWorkflowService service = services
                .GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SendFeedbackAsync(dto, 11);

            Assert.False(result.IsSuccess);
            Assert.Null(workItem.AnalystFeedbackStatus);
        });
    }

    [Fact]
    public async Task SendFeedback_DescriptionOver500Characters_IsRejected()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            SendAnalystFeedbackDto dto = ValidDto(workItem.Id);
            dto.Description = new string('x', 501);
            IAnalystWorkflowService service = services
                .GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SendFeedbackAsync(dto, 11);

            Assert.False(result.IsSuccess);
            Assert.Null(workItem.AnalystFeedbackDescription);
        });
    }

    [Fact]
    public async Task AnalystInbox_IncludesAssignedApprovedRequest()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Approved,
                analystId: 11);
            IWorkItemRepository repository = services
                .GetRequiredService<IWorkItemRepository>();

            List<WorkItem> workItems = await repository
                .GetAnalystInboxAsync(11);

            Assert.Contains(workItems, item => item.Id == workItem.Id);
        });
    }

    [Fact]
    public async Task ProjectManagerDetails_OnlyOwnerReadsLatestFeedback()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Approved,
                createdByUserId: 101,
                analystId: 11);
            IAnalystWorkflowService analystService = services
                .GetRequiredService<IAnalystWorkflowService>();
            Assert.True((await analystService.SendFeedbackAsync(
                ValidDto(workItem.Id),
                11)).IsSuccess);
            IProjectManagerWorkItemService projectManagerService = services
                .GetRequiredService<IProjectManagerWorkItemService>();

            var ownerResult = await projectManagerService
                .GetDetailsAsync(workItem.Id, 101);
            var otherUserResult = await projectManagerService
                .GetDetailsAsync(workItem.Id, 102);

            Assert.True(ownerResult.IsSuccess);
            Assert.Equal(WorkflowStatus.UnderAnalystReview,
                ownerResult.Data!.AnalystFeedbackStatus);
            Assert.Equal("Kısa geri bildirim.",
                ownerResult.Data.AnalystFeedbackDescription);
            Assert.NotNull(ownerResult.Data.AnalystFeedbackAt);
            Assert.True(otherUserResult.IsForbidden);
        });
    }

    private static SendAnalystFeedbackDto ValidDto(int workItemId)
    {
        return new SendAnalystFeedbackDto
        {
            WorkItemId = workItemId,
            FeedbackStatus = WorkflowStatus.UnderAnalystReview,
            Description = "Kısa geri bildirim."
        };
    }
}
