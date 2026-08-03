using FlowDesk.Models;
using FlowDesk.Services;
using FlowDesk.ViewModels.Analyst;

namespace FlowDesk.CharacterizationTests.Analyst;

public sealed class AnalystWorkflowMapperTests
{
    [Fact]
    public void ToInboxItem_MapsBasicFields()
    {
        WorkItem workItem = CreateWorkItem();

        AnalystInboxItemViewModel result =
            AnalystWorkflowMapper.ToInboxItem(workItem);

        Assert.Equal(workItem.Id, result.Id);
        Assert.Equal(workItem.RequestNumber, result.RequestNumber);
        Assert.Equal(
            workItem.RequestDescription,
            result.RequestDescription);
        Assert.Equal(workItem.Department, result.Department);
        Assert.Equal(workItem.Priority, result.Priority);
        Assert.Equal(workItem.WorkflowStatus, result.WorkflowStatus);
        Assert.Equal(workItem.CurrentStatus, result.CurrentStatus);
        Assert.Equal(workItem.ManagerNote, result.ManagerNote);
        Assert.Equal(workItem.CreatedAt, result.CreatedAt);
        Assert.Equal(workItem.UpdatedAt, result.UpdatedAt);
    }

    [Fact]
    public void ToReview_MapsBasicFieldsWithoutIdentityEnrichment()
    {
        WorkItem workItem = CreateWorkItem();

        AnalystReviewViewModel result =
            AnalystWorkflowMapper.ToReview(workItem);

        Assert.Equal(workItem.Id, result.Id);
        Assert.Equal(workItem.RequestNumber, result.RequestNumber);
        Assert.Equal(
            workItem.RequestDescription,
            result.RequestDescription);
        Assert.Equal(workItem.Department, result.Department);
        Assert.Equal(workItem.Priority, result.Priority);
        Assert.Equal(workItem.WorkflowStatus, result.WorkflowStatus);
        Assert.Equal(workItem.CreatedAt, result.CreatedAt);
        Assert.Equal(workItem.AnalystId, result.AnalystId);
        Assert.Equal(workItem.DeveloperId, result.DeveloperId);
        Assert.Equal(workItem.ReleaseDate, result.ReleaseDate);
        Assert.Equal(
            workItem.BanksoftDeliveryDate,
            result.BanksoftDeliveryDate);
        Assert.Equal(workItem.ExpectedStatus, result.ExpectedStatus);
        Assert.Equal(workItem.CurrentStatus, result.CurrentStatus);
        Assert.Equal(workItem.AnalystNote, result.AnalystNote);
        Assert.Equal(workItem.ManagerNote, result.ManagerNote);
        Assert.Empty(result.AnalystDisplayName);
        Assert.Empty(result.DeveloperOptions);
    }

    private static WorkItem CreateWorkItem()
    {
        return new WorkItem
        {
            Id = 42,
            RequestNumber = "TLP-TEST",
            RequestDescription = "Mapper test",
            Department = "Test Department",
            Priority = RequestPriority.High,
            WorkflowStatus = WorkflowStatus.ReturnedToAnalyst,
            CurrentStatus = "Returned",
            ManagerNote = "Manager note",
            CreatedAt = new DateTime(2026, 8, 1),
            UpdatedAt = new DateTime(2026, 8, 2),
            AnalystId = 11,
            DeveloperId = 22,
            ReleaseDate = new DateTime(2026, 9, 20),
            BanksoftDeliveryDate = new DateTime(2026, 9, 10),
            ExpectedStatus = "Ready",
            AnalystNote = "Analyst note"
        };
    }
}
