using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.DTOs.DepartmentManager;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;

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
                    ManagerNote = "Approved in characterization test"
                });

            Assert.True(result.IsSuccess);
            Assert.Equal(WorkflowStatus.Approved, workItem.WorkflowStatus);
            Assert.Equal(
                "Departman Yöneticisi Tarafından Onaylandı",
                workItem.CurrentStatus);
            Assert.NotNull(workItem.ApprovedAt);
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
                });

            Assert.True(result.IsSuccess);
            Assert.Equal(
                WorkflowStatus.ReturnedToAnalyst,
                workItem.WorkflowStatus);
            Assert.Equal(
                "Please revise the analysis",
                workItem.ManagerNote);
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
                });

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
                    ManagerNote = "Should not be applied"
                });

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
                });

            Assert.False(result.IsSuccess);
            Assert.Equal(WorkflowStatus.Approved, workItem.WorkflowStatus);
            Assert.Null(workItem.ManagerNote);
        });
    }
}