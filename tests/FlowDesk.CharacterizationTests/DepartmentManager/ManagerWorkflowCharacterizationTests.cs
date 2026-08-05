using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.DepartmentManager;

public sealed class ManagerWorkflowCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task Approve_WaitingManagerApproval_TransitionsToApproved()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            IDepartmentManagerWorkflowService service =
                services.GetRequiredService<
                    IDepartmentManagerWorkflowService>();

            var result = await service.ApproveRequestAsync(
                new ApproveRequestDto
                {
                    WorkItemId = workItem.Id,
                    DeveloperId = 22,
                    ManagerNote = "Approved in characterization test"
                }, 9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(WorkflowStatus.Approved, workItem.WorkflowStatus);
            Assert.Equal(
                "Departman Yöneticisi Tarafından Onaylandı",
                workItem.CurrentStatus);
            Assert.NotNull(workItem.ApprovedAt);
            Assert.Equal(22, workItem.DeveloperId);
            Assert.Equal(1, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task GetReview_WithSavedAiDraft_ReturnsEditedRequest()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            await InsertAiDraftAsync(
                services,
                workItem.Id,
                "Kayıtlı AI düzenlemesi");

            var service = services.GetRequiredService<
                IDepartmentManagerWorkflowService>();
            var result = await service.GetReviewAsync(workItem.Id, 9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                "Kayıtlı AI düzenlemesi",
                result.Data!.AiEditedRequest);
            Assert.Equal(
                workItem.RequestDescription,
                result.Data.RequestDescription);
        });
    }

    [Fact]
    public async Task GetReview_WithoutAiDraft_ReturnsOriginalRequest()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            var service = services.GetRequiredService<
                IDepartmentManagerWorkflowService>();

            var result = await service.GetReviewAsync(workItem.Id, 9001);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Data!.AiEditedRequest);
            Assert.Equal(
                workItem.RequestDescription,
                result.Data.RequestDescription);
        });
    }

    [Fact]
    public async Task GetSharedExcel_ReturnsScopedApprovedProjectionFromDatabase()
    {
        await WithServicesAsync(async services =>
        {
            ApplicationUser analyst = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("shared-excel-analyst"),
                assignedRole: AppRoles.Analyst,
                businessCode: "ANL-000001",
                fullName: "Can Analist");
            ApplicationUser developer = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("shared-excel-developer"),
                assignedRole: AppRoles.Employee,
                businessCode: "DEV-000001",
                fullName: "Ece Yazılımcı");
            WorkItem older = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Approved,
                analystId: analyst.Id,
                developerId: developer.Id);
            WorkItem newer = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Approved,
                analystId: analyst.Id,
                developerId: developer.Id);
            WorkItem hiddenDepartment =
                await TestDataSeeder.CreateWorkItemAsync(
                    services,
                    WorkflowStatus.Approved,
                    department: "Başka Departman");
            await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);

            older.ReleaseDate = new DateTime(2026, 9, 1);
            older.BanksoftDeliveryDate = new DateTime(2026, 9, 5);
            older.ExpectedStatus = "Canlıya hazır";
            older.CurrentStatus = "Onaylandı";
            older.UpdatedAt = new DateTime(2026, 8, 1);
            newer.UpdatedAt = new DateTime(2026, 8, 2);
            await services.GetRequiredService<AppDbContext>()
                .SaveChangesAsync();
            var service = services.GetRequiredService<
                IDepartmentManagerWorkflowService>();
            var result = await service.GetSharedExcelAsync(9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Data!.Requests.Count);
            Assert.Equal(
                newer.RequestNumber,
                result.Data.Requests[0].RequestNumber);
            Assert.Equal(
                newer.RequestDescription,
                result.Data.Requests[0].RequestDescription);
            var projected = Assert.Single(
                result.Data.Requests,
                item => item.RequestNumber == older.RequestNumber);
            Assert.Equal("Can Analist — ANL-000001",
                projected.AnalystDisplayName);
            Assert.Equal("Ece Yazılımcı — DEV-000001",
                projected.DeveloperDisplayName);
            Assert.Equal(older.ReleaseDate, projected.ReleaseDate);
            Assert.Equal(older.BanksoftDeliveryDate,
                projected.BanksoftDeliveryDate);
            Assert.Equal(older.ExpectedStatus, projected.ExpectedStatus);
            Assert.Equal(older.CurrentStatus, projected.CurrentStatus);
            Assert.Equal(older.RequestDescription,
                projected.RequestDescription);
            Assert.DoesNotContain(
                result.Data.Requests,
                item => item.RequestNumber == hiddenDepartment.RequestNumber);
        });
    }

    [Fact]
    public async Task Approve_WithoutDeveloper_ReturnsFailure()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            var service = services.GetRequiredService<
                IDepartmentManagerWorkflowService>();

            var result = await service.ApproveRequestAsync(
                new ApproveRequestDto { WorkItemId = workItem.Id },
                9001);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                WorkflowStatus.WaitingManagerApproval,
                workItem.WorkflowStatus);
            Assert.Equal(0, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task Approve_IneligibleDeveloper_ReturnsFailure()
    {
        await WithServicesAsync(async services =>
        {
            ApplicationUser developer = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("wrong-department-developer"),
                assignedRole: AppRoles.Employee,
                department: "Bilgi Teknolojileri",
                businessCode: "ENG999999999999999",
                userId: 77);
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            var service = services.GetRequiredService<
                IDepartmentManagerWorkflowService>();

            var result = await service.ApproveRequestAsync(
                new ApproveRequestDto
                {
                    WorkItemId = workItem.Id,
                    DeveloperId = developer.Id
                },
                9001);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                WorkflowStatus.WaitingManagerApproval,
                workItem.WorkflowStatus);
            Assert.Equal(0, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task Approve_UsesSavedAiDraftForExcelRow()
    {
        await WithServicesAsync(async services =>
        {
            ApplicationUser analyst = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("excel-analyst"),
                assignedRole: AppRoles.Analyst,
                businessCode: "ANL999999999999999",
                userId: 33,
                fullName: "Excel Analisti");
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval,
                analystId: analyst.Id);
            workItem.ReleaseDate = new DateTime(2026, 9, 1);
            workItem.BanksoftDeliveryDate = new DateTime(2026, 9, 5);
            workItem.ExpectedStatus = "Canlıya hazır";
            await services.GetRequiredService<AppDbContext>()
                .SaveChangesAsync();
            await InsertAiDraftAsync(
                services,
                workItem.Id,
                "Excel'e yazılacak AI talebi");
            var service = services.GetRequiredService<
                IDepartmentManagerWorkflowService>();

            var result = await service.ApproveRequestAsync(
                new ApproveRequestDto
                {
                    WorkItemId = workItem.Id,
                    DeveloperId = 22
                },
                9001);

            Assert.True(result.IsSuccess);
            var row = Assert.Single(Factory.ApprovedExcel.Rows);
            Assert.Contains("Excel Analisti", row.AnalystDisplayName);
            Assert.NotEmpty(row.DeveloperDisplayName);
            Assert.Equal(workItem.ReleaseDate, row.ReleaseDate);
            Assert.Equal(
                workItem.BanksoftDeliveryDate,
                row.BanksoftDeliveryDate);
            Assert.Equal(workItem.ExpectedStatus, row.ExpectedStatus);
            Assert.Equal(workItem.RequestNumber, row.RequestNumber);
            Assert.Equal("Excel'e yazılacak AI talebi", row.RequestDescription);
        });
    }

    [Fact]
    public async Task ReturnToAnalyst_WithManagerNote_TransitionsAndPersistsNote()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            IDepartmentManagerWorkflowService service =
                services.GetRequiredService<
                    IDepartmentManagerWorkflowService>();

            var result = await service.ReturnToAnalystAsync(
                new ReturnToAnalystDto
                {
                    WorkItemId = workItem.Id,
                    ManagerNote = "Please revise the analysis"
                }, 9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                WorkflowStatus.ReturnedToAnalyst,
                workItem.WorkflowStatus);
            Assert.Equal(
                "Please revise the analysis",
                workItem.ManagerNote);
            Assert.Equal(0, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task Reject_TransitionsWithoutExcelWrite()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            var service = services.GetRequiredService<
                IDepartmentManagerWorkflowService>();

            var result = await service.RejectRequestAsync(
                new RejectRequestDto
                {
                    WorkItemId = workItem.Id,
                    ManagerNote = "Uygun değil"
                },
                9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(WorkflowStatus.Rejected, workItem.WorkflowStatus);
            Assert.Equal(0, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task Approve_ExcelFailure_DoesNotRollbackApproval()
    {
        await WithServicesAsync(async services =>
        {
            Factory.ApprovedExcel.ShouldFail = true;
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            var service = services.GetRequiredService<
                IDepartmentManagerWorkflowService>();

            var result = await service.ApproveRequestAsync(
                new ApproveRequestDto
                {
                    WorkItemId = workItem.Id,
                    DeveloperId = 22
                },
                9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                "Talep onaylandı ancak Excel kaydı tamamlanamadı.",
                result.SuccessMessage);
            Assert.Equal(WorkflowStatus.Approved, workItem.WorkflowStatus);
            Assert.Equal(1, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task ReturnToAnalyst_WithoutManagerNote_ReturnsFailure()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.WaitingManagerApproval);
            IDepartmentManagerWorkflowService service =
                services.GetRequiredService<
                    IDepartmentManagerWorkflowService>();

            var result = await service.ReturnToAnalystAsync(
                new ReturnToAnalystDto
                {
                    WorkItemId = workItem.Id,
                    ManagerNote = " "
                }, 9001);

            Assert.False(result.IsSuccess);
            Assert.Equal(
                WorkflowStatus.WaitingManagerApproval,
                workItem.WorkflowStatus);
            Assert.Null(workItem.ManagerNote);
        });
    }

    [Fact]
    public async Task Approve_InvalidStartingStatus_DoesNotChangeWorkItem()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Submitted);
            IDepartmentManagerWorkflowService service =
                services.GetRequiredService<
                    IDepartmentManagerWorkflowService>();

            var result = await service.ApproveRequestAsync(
                new ApproveRequestDto
                {
                    WorkItemId = workItem.Id,
                    DeveloperId = 22,
                    ManagerNote = "Should not be applied"
                }, 9001);

            Assert.False(result.IsSuccess);
            Assert.Equal(WorkflowStatus.Submitted, workItem.WorkflowStatus);
            Assert.Null(workItem.ManagerNote);
            Assert.Null(workItem.ApprovedAt);
        });
    }

    [Fact]
    public async Task Return_InvalidStartingStatus_DoesNotChangeWorkItem()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
                services,
                WorkflowStatus.Approved);
            IDepartmentManagerWorkflowService service =
                services.GetRequiredService<
                    IDepartmentManagerWorkflowService>();

            var result = await service.ReturnToAnalystAsync(
                new ReturnToAnalystDto
                {
                    WorkItemId = workItem.Id,
                    ManagerNote = "Should not be applied"
                }, 9001);

            Assert.False(result.IsSuccess);
            Assert.Equal(WorkflowStatus.Approved, workItem.WorkflowStatus);
            Assert.Null(workItem.ManagerNote);
        });
    }

    private static async Task InsertAiDraftAsync(
        IServiceProvider services,
        int workItemId,
        string editedRequest)
    {
        AppDbContext context = services.GetRequiredService<AppDbContext>();
        DateTime now = DateTime.UtcNow;
        const string emptyJson = "[]";
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO WorkItemAiDrafts
                (WorkItemId, GeneratedRequest, EditedRequest,
                 AbbreviationsJson, AmbiguitiesJson, UnresolvedTermsJson,
                 ModelName, GeneratedAt, UpdatedAt, RowVersion)
            VALUES
                ({workItemId}, {editedRequest}, {editedRequest},
                 {emptyJson}, {emptyJson}, {emptyJson},
                 {"gemini-test-model"}, {now}, {now}, {new byte[] { 1 }})
            """);
    }
}
