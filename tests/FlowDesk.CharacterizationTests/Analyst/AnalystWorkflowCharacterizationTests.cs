using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.DTOs.Analyst;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;

namespace FlowDesk.CharacterizationTests.Analyst;

public sealed class AnalystWorkflowCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task GetReview_DeveloperOptions_ContainOnlyEligibleDepartmentEmployees()
    {
        await WithServicesAsync(async services =>
        {
            string department = DepartmentOptions.All.First(
                value => value != TestDataSeeder.DefaultDepartment);

            ApplicationUser eligible = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("eligible-developer"),
                assignedRole: AppRoles.Employee,
                department: department,
                businessCode: "ENG-29072026-1001",
                fullName: "Eligible Developer");
            ApplicationUser unapproved = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("unapproved-developer"),
                isApproved: false,
                assignedRole: AppRoles.Employee,
                department: department,
                businessCode: "ENG-29072026-1002");
            ApplicationUser unconfirmed = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("unconfirmed-developer"),
                emailConfirmed: false,
                assignedRole: AppRoles.Employee,
                department: department,
                businessCode: "ENG-29072026-1003");
            ApplicationUser wrongRole = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("wrong-role-developer"),
                assignedRole: AppRoles.Analyst,
                department: department,
                businessCode: "ANL-29072026-1004");
            ApplicationUser missingCode = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("missing-code-developer"),
                assignedRole: AppRoles.Employee,
                department: department);
            ApplicationUser otherDepartment =
                await TestDataSeeder.CreateUserAsync(
                    services,
                    TestDataSeeder.UniqueEmail("other-department-developer"),
                    assignedRole: AppRoles.Employee,
                    department: TestDataSeeder.DefaultDepartment,
                    businessCode: "ENG-29072026-1005");

            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                department: department,
                analystId: 11);

            var result = await services
                .GetRequiredService<IAnalystWorkflowService>()
                .GetReviewAsync(workItem.Id, 11);

            Assert.True(result.IsSuccess);
            Assert.Contains(
                result.Data!.DeveloperOptions,
                option => option.Id == eligible.Id &&
                          option.DisplayText.Contains(
                              eligible.BusinessCode!,
                              StringComparison.Ordinal));
            Assert.DoesNotContain(result.Data.DeveloperOptions, option =>
                option.Id == unapproved.Id ||
                option.Id == unconfirmed.Id ||
                option.Id == wrongRole.Id ||
                option.Id == missingCode.Id ||
                option.Id == otherDepartment.Id);
        });
    }

    [Fact]
    public async Task SaveAnalysis_EmployeeWithoutBusinessCode_IsRejectedServerSide()
    {
        await WithServicesAsync(async services =>
        {
            ApplicationUser employee = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("developer-without-code"),
                assignedRole: AppRoles.Employee,
                userId: 5500);
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);

            var result = await services
                .GetRequiredService<IAnalystWorkflowService>()
                .SaveAnalysisAsync(
                    new SaveAnalysisDto
                    {
                        WorkItemId = workItem.Id,
                        DeveloperId = employee.Id
                    },
                    11);

            Assert.False(result.IsSuccess);
            Assert.Null(workItem.DeveloperId);
        });
    }

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
    public async Task ExpectedStatus_Exactly100Characters_IsAcceptedByBothPaths()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem saveItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            WorkItem submitItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();
            string expectedStatus = new('x', 100);

            var saveResult = await service.SaveAnalysisAsync(
                new SaveAnalysisDto
                {
                    WorkItemId = saveItem.Id,
                    ExpectedStatus = expectedStatus
                },
                11);
            SubmitForApprovalDto submitDto = CompleteSubmitDto(
                submitItem.Id);
            submitDto.ExpectedStatus = expectedStatus;
            var submitResult = await service.SubmitForApprovalAsync(
                submitDto,
                11);

            Assert.True(saveResult.IsSuccess);
            Assert.True(submitResult.IsSuccess);
            Assert.Equal(expectedStatus, saveItem.ExpectedStatus);
            Assert.Equal(expectedStatus, submitItem.ExpectedStatus);
        });
    }

    [Fact]
    public async Task ExpectedStatus_101Characters_IsRejectedByBothPaths()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem saveItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            WorkItem submitItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();
            string expectedStatus = new('x', 101);

            var saveResult = await service.SaveAnalysisAsync(
                new SaveAnalysisDto
                {
                    WorkItemId = saveItem.Id,
                    ExpectedStatus = expectedStatus
                },
                11);
            SubmitForApprovalDto submitDto = CompleteSubmitDto(
                submitItem.Id);
            submitDto.ExpectedStatus = expectedStatus;
            var submitResult = await service.SubmitForApprovalAsync(
                submitDto,
                11);

            Assert.False(saveResult.IsSuccess);
            Assert.False(submitResult.IsSuccess);
            Assert.Equal(saveResult.ErrorMessage, submitResult.ErrorMessage);
            Assert.Null(saveItem.ExpectedStatus);
            Assert.Null(submitItem.ExpectedStatus);
        });
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

    [Theory]
    [InlineData(AnalystCurrentStatusOptions.UnderAnalystReview)]
    [InlineData(AnalystCurrentStatusOptions.WaitingForDevelopment)]
    [InlineData(AnalystCurrentStatusOptions.DevelopmentInProgress)]
    [InlineData(AnalystCurrentStatusOptions.WaitingForTest)]
    [InlineData(AnalystCurrentStatusOptions.ReadyForRelease)]
    public async Task SaveAnalysis_AllowedCurrentStatus_IsAccepted(
        string currentStatus)
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SaveAnalysisAsync(
                new SaveAnalysisDto
                {
                    WorkItemId = workItem.Id,
                    AnalystId = 11,
                    CurrentStatus = currentStatus
                },
                11);

            Assert.True(result.IsSuccess);
            Assert.Equal(currentStatus, workItem.CurrentStatus);
        });
    }

    [Fact]
    public async Task SaveAnalysis_FakeCurrentStatus_ReturnsControlledFailure()
    {
        await AssertInvalidCurrentStatusAsync(
            "Sahte Statü",
            "Geçerli bir mevcut statü seçiniz.");
    }

    [Fact]
    public async Task SaveAnalysis_TooLongCurrentStatus_ReturnsControlledFailure()
    {
        await AssertInvalidCurrentStatusAsync(
            new string('x', AnalystCurrentStatusOptions.MaximumLength + 1),
            "Mevcut statü en fazla 100 karakter olabilir.");
    }

    private async Task AssertInvalidCurrentStatusAsync(
        string currentStatus,
        string expectedError)
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.UnderAnalystReview,
                analystId: 11);
            string originalCurrentStatus = workItem.CurrentStatus;
            IAnalystWorkflowService service =
                services.GetRequiredService<IAnalystWorkflowService>();

            var result = await service.SaveAnalysisAsync(
                new SaveAnalysisDto
                {
                    WorkItemId = workItem.Id,
                    AnalystId = 11,
                    CurrentStatus = currentStatus
                },
                11);

            Assert.False(result.IsSuccess);
            Assert.Equal(expectedError, result.ErrorMessage);
            Assert.Equal(originalCurrentStatus, workItem.CurrentStatus);
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
