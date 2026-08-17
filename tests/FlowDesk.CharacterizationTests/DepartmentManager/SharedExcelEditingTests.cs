using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.DepartmentManager;

public sealed class SharedExcelEditingTests : DatabaseTestBase
{
    [Fact]
    public async Task EditApprovedWorkItem_AuthorizedManager_UpdatesSqlThenExcel()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await CreateEditableWorkItemAsync(services);
            IDepartmentManagerWorkflowService service = services
                .GetRequiredService<IDepartmentManagerWorkflowService>();

            ServiceResult result = await service.EditApprovedWorkItemAsync(
                CreateValidDto(workItem.Id),
                9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(22, workItem.DeveloperId);
            Assert.Equal(new DateTime(2026, 9, 20), workItem.ReleaseDate);
            Assert.Equal(new DateTime(2026, 9, 18),
                workItem.BanksoftDeliveryDate);
            Assert.Equal("Canlıya hazır", workItem.ExpectedStatus);
            Assert.Equal(AnalystCurrentStatusOptions.DevelopmentInProgress,
                workItem.CurrentStatus);
            Assert.Equal("Güncellenmiş talep metni",
                workItem.RequestDescription);
            Assert.Equal(1, Factory.ApprovedExcel.CallCount);
            Assert.Equal(workItem.RequestNumber,
                Assert.Single(Factory.ApprovedExcel.Rows).RequestNumber);
        });
    }

    [Fact]
    public async Task EditApprovedWorkItem_DifferentDepartment_IsUpdated()
    {
        await WithServicesAsync(async services =>
        {
            string otherDepartment = DepartmentOptions.All.First(
                department => department != TestDataSeeder.DefaultDepartment);
            ApplicationUser developer =
                await TestDataSeeder.CreateUserAsync(
                    services,
                    TestDataSeeder.UniqueEmail("other-department-developer"),
                    assignedRole: AppRoles.Employee,
                    department: otherDepartment,
                    businessCode: "ENG-OTHER-000001");
            WorkItem workItem = await CreateEditableWorkItemAsync(
                services,
                otherDepartment);
            IDepartmentManagerWorkflowService service = services
                .GetRequiredService<IDepartmentManagerWorkflowService>();

            EditApprovedWorkItemDto dto = CreateValidDto(workItem.Id);
            dto.DeveloperId = developer.Id;

            ServiceResult result = await service.EditApprovedWorkItemAsync(
                dto,
                9001);

            Assert.True(result.IsSuccess);
            Assert.Equal("Güncellenmiş talep metni",
                workItem.RequestDescription);
            Assert.Equal(1, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task EditApprovedWorkItem_InvalidEmployee_DoesNotUpdate()
    {
        await WithServicesAsync(async services =>
        {
            ApplicationUser analyst = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("invalid-developer"),
                assignedRole: AppRoles.Analyst,
                businessCode: "ANL-INVALID");
            WorkItem workItem = await CreateEditableWorkItemAsync(services);
            EditApprovedWorkItemDto dto = CreateValidDto(workItem.Id);
            dto.DeveloperId = analyst.Id;
            IDepartmentManagerWorkflowService service = services
                .GetRequiredService<IDepartmentManagerWorkflowService>();

            ServiceResult result = await service.EditApprovedWorkItemAsync(
                dto,
                9001);

            Assert.False(result.IsSuccess);
            Assert.Equal("Characterization test request",
                workItem.RequestDescription);
            Assert.Equal(0, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task EditApprovedWorkItem_IgnoresAnalystAndRequestNumber()
    {
        await WithServicesAsync(async services =>
        {
            ApplicationUser analyst = await TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("immutable-analyst"),
                assignedRole: AppRoles.Analyst,
                businessCode: "ANL-IMMUTABLE");
            WorkItem workItem = await CreateEditableWorkItemAsync(
                services,
                analystId: analyst.Id);
            string requestNumber = workItem.RequestNumber;
            EditApprovedWorkItemDto dto = CreateValidDto(workItem.Id);
            dto.AnalystId = 999999;
            dto.RequestNumber = "TAMPERED";
            IDepartmentManagerWorkflowService service = services
                .GetRequiredService<IDepartmentManagerWorkflowService>();

            ServiceResult result = await service.EditApprovedWorkItemAsync(
                dto,
                9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(analyst.Id, workItem.AnalystId);
            Assert.Equal(requestNumber, workItem.RequestNumber);
        });
    }

    [Fact]
    public async Task EditApprovedWorkItem_ExcelFailure_PreservesSql()
    {
        await WithServicesAsync(async services =>
        {
            Factory.ApprovedExcel.ShouldFail = true;
            WorkItem workItem = await CreateEditableWorkItemAsync(services);
            IDepartmentManagerWorkflowService service = services
                .GetRequiredService<IDepartmentManagerWorkflowService>();

            ServiceResult result = await service.EditApprovedWorkItemAsync(
                CreateValidDto(workItem.Id),
                9001);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                "Değişiklikler kaydedildi ancak ortak Excel güncellenemedi.",
                result.SuccessMessage);
            Assert.Equal("Güncellenmiş talep metni",
                workItem.RequestDescription);
            Assert.Equal(1, Factory.ApprovedExcel.CallCount);
        });
    }

    [Fact]
    public async Task EditApprovedWorkItem_ConcurrencyConflict_DoesNotWriteExcel()
    {
        await WithServicesAsync(async services =>
        {
            WorkItem workItem = await CreateEditableWorkItemAsync(services);
            AppDbContext context = services.GetRequiredService<AppDbContext>();
            await SetRowVersionAsync(context, workItem.Id, [2]);
            IDepartmentManagerWorkflowService service = services
                .GetRequiredService<IDepartmentManagerWorkflowService>();

            ServiceResult result = await service.EditApprovedWorkItemAsync(
                CreateValidDto(workItem.Id),
                9001);

            Assert.True(result.IsConflict);
            Assert.Equal(0, Factory.ApprovedExcel.CallCount);
        });
    }

    private static async Task<WorkItem> CreateEditableWorkItemAsync(
        IServiceProvider services,
        string? department = null,
        int? analystId = null)
    {
        WorkItem workItem = await TestDataSeeder.CreateWorkItemAsync(
            services,
            WorkflowStatus.Approved,
            developerId: 22,
            department: department,
            analystId: analystId);
        AppDbContext context = services.GetRequiredService<AppDbContext>();
        await SetRowVersionAsync(context, workItem.Id, [1]);
        return workItem;
    }

    private static Task<int> SetRowVersionAsync(
        AppDbContext context,
        int workItemId,
        byte[] rowVersion)
    {
        return context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE WorkItems
            SET RowVersion = {rowVersion}
            WHERE Id = {workItemId}
            """);
    }

    private static EditApprovedWorkItemDto CreateValidDto(int workItemId)
    {
        return new EditApprovedWorkItemDto
        {
            WorkItemId = workItemId,
            DeveloperId = 22,
            ReleaseDate = new DateTime(2026, 9, 20),
            BanksoftDeliveryDate = new DateTime(2026, 9, 18),
            ExpectedStatus = "Canlıya hazır",
            CurrentStatus =
                AnalystCurrentStatusOptions.DevelopmentInProgress,
            RequestDescription = "Güncellenmiş talep metni",
            RowVersion = Convert.ToBase64String(new byte[] { 1 })
        };
    }
}
