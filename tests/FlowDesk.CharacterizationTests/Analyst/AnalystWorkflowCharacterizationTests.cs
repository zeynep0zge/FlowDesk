using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;

namespace FlowDesk.CharacterizationTests.Analyst;

public sealed class AnalystWorkflowCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task StartReview_SubmittedRequest_TransitionsToUnderAnalystReview()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Submitted);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();

            var result = await service.StartReviewAsync(workItem.Id, 11);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                WorkflowStatus.UnderAnalystReview,
                workItem.WorkflowStatus);
            Assert.Equal("Analist İncelemesinde", workItem.CurrentStatus);
        });
    }

    [Fact]
    public async Task SaveAnalysis_ValidInput_PersistsAnalysisFields()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();
            DateTime releaseDate = new(2026, 9, 20);
            DateTime deliveryDate = new(2026, 9, 10);

            var result = await service.SaveAnalysisAsync(
                new SaveAnalysisDto
                {
                    WorkItemId = workItem.Id,
                    AnalystId = 11,
                    DeveloperId = 22,
                    ReleaseDate = releaseDate,
                    BanksoftDeliveryDate = deliveryDate,
                    ExpectedStatus = "Sürüme Hazır",
                    CurrentStatus = "Analist İncelemesinde",
                    AnalystNote = "Test analysis note"
                },
                11);

            Assert.True(result.IsSuccess);
            Assert.Equal(11, workItem.AnalystId);
            Assert.Equal(22, workItem.DeveloperId);
            Assert.Equal(releaseDate, workItem.ReleaseDate);
            Assert.Equal(deliveryDate, workItem.BanksoftDeliveryDate);
            Assert.Equal("Sürüme Hazır", workItem.ExpectedStatus);
            Assert.Equal("Analist İncelemesinde", workItem.CurrentStatus);
            Assert.Equal("Test analysis note", workItem.AnalystNote);
        });
    }

    [Fact]
    public async Task SaveAnalysis_ZeroAnalystId_DoesNotPersistChanges()
    {
        await AssertInvalidSaveDoesNotPersistAsync(
            dto => dto.AnalystId = 0);
    }

    [Fact]
    public async Task SaveAnalysis_ZeroDeveloperId_DoesNotPersistChanges()
    {
        await AssertInvalidSaveDoesNotPersistAsync(
            dto => dto.DeveloperId = 0);
    }

    [Fact]
    public async Task SaveAnalysis_InvalidDateOrder_DoesNotPersistChanges()
    {
        await AssertInvalidSaveDoesNotPersistAsync(dto =>
        {
            dto.ReleaseDate = new DateTime(2026, 9, 10);
            dto.BanksoftDeliveryDate = new DateTime(2026, 9, 11);
        });
    }

    [Fact]
    public async Task SaveAnalysis_TooLongAnalystNote_DoesNotPersistChanges()
    {
        await AssertInvalidSaveDoesNotPersistAsync(
            dto => dto.AnalystNote = new string('x', 1001));
    }

    [Fact]
    public async Task SubmitForApproval_CompleteAnalysis_TransitionsToWaitingManagerApproval()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SubmitForApprovalAsync(
                CompleteSubmitDto(workItem.Id),
                11);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                WorkflowStatus.WaitingManagerApproval,
                workItem.WorkflowStatus);
            Assert.Equal(
                "Departman Yöneticisi Onayı Bekliyor",
                workItem.CurrentStatus);
        });
    }

    [Fact]
    public async Task SubmitForApproval_MissingRequiredFields_ReturnsFailure()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SubmitForApprovalAsync(
                new SubmitForApprovalDto
                {
                    WorkItemId = workItem.Id,
                    AnalystId = 11
                },
                11);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                WorkflowStatus.UnderAnalystReview,
                workItem.WorkflowStatus);
        });
    }

    [Fact]
    public async Task ReturnedRequest_AppearsOnlyInReturnedQuery()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem returned = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.ReturnedToAnalyst,
                analystId: 11);
            WorkItem submitted = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Submitted);
            IWorkItemRepository repository =
                services.GetRequiredService<IWorkItemRepository>();

            List<WorkItem> returnedItems =
                await repository.GetReturnedRequestsAsync(11);
            List<WorkItem> inboxItems =
                await repository.GetAnalystInboxAsync(11);

            Assert.Contains(returnedItems, x => x.Id == returned.Id);
            Assert.DoesNotContain(inboxItems, x => x.Id == returned.Id);
            Assert.Contains(inboxItems, x => x.Id == submitted.Id);
        });
    }

    private async Task AssertInvalidSaveDoesNotPersistAsync(
        Action<SaveAnalysisDto> makeInvalid)
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();
            SaveAnalysisDto dto = new()
            {
                WorkItemId = workItem.Id,
                AnalystId = 11,
                DeveloperId = 22,
                ReleaseDate = new DateTime(2026, 9, 20),
                BanksoftDeliveryDate = new DateTime(2026, 9, 10),
                ExpectedStatus = "Ready",
                CurrentStatus = "Analist İncelemesinde",
                AnalystNote = "Original valid note"
            };
            makeInvalid(dto);

            var result = await service.SaveAnalysisAsync(dto, 11);

            Assert.False(result.IsSuccess);
            Assert.Equal(11, workItem.AnalystId);
            Assert.Null(workItem.DeveloperId);
            Assert.Null(workItem.ReleaseDate);
            Assert.Null(workItem.BanksoftDeliveryDate);
            Assert.Null(workItem.ExpectedStatus);
            Assert.Null(workItem.AnalystNote);
        });
    }

    private static SubmitForApprovalDto CompleteSubmitDto(int workItemId)
    {
        return new SubmitForApprovalDto
        {
            WorkItemId = workItemId,
            AnalystId = 11,
            DeveloperId = 22,
            ReleaseDate = new DateTime(2026, 9, 20),
            BanksoftDeliveryDate = new DateTime(2026, 9, 10),
            ExpectedStatus = "Sürüme Hazır",
            AnalystNote = "Complete analysis"
        };
    }
}
