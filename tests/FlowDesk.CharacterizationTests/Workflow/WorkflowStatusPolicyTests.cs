using FlowDesk.Models;

namespace FlowDesk.CharacterizationTests.Workflow;

public sealed class WorkflowStatusPolicyTests
{
    public static TheoryData<WorkflowStatus, bool> AnalystStatuses =>
        CreateStatusData(
            WorkflowStatus.Submitted,
            WorkflowStatus.UnderAnalystReview,
            WorkflowStatus.ReturnedToAnalyst);

    public static TheoryData<WorkflowStatus, bool> ManagerApprovalStatuses =>
        CreateStatusData(WorkflowStatus.WaitingManagerApproval);

    public static TheoryData<WorkflowStatus, bool> SubmittedStatuses =>
        CreateStatusData(WorkflowStatus.Submitted);

    [Theory]
    [MemberData(nameof(AnalystStatuses))]
    public void CanAnalystStartReview_ReturnsExpectedResult(
        WorkflowStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusPolicy.CanAnalystStartReview(status));
    }

    [Theory]
    [MemberData(nameof(AnalystStatuses))]
    public void CanAnalystSaveAnalysis_ReturnsExpectedResult(
        WorkflowStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusPolicy.CanAnalystSaveAnalysis(status));
    }

    [Theory]
    [MemberData(nameof(AnalystStatuses))]
    public void CanAnalystSubmitForManagerApproval_ReturnsExpectedResult(
        WorkflowStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusPolicy.CanAnalystSubmitForManagerApproval(status));
    }

    [Theory]
    [MemberData(nameof(ManagerApprovalStatuses))]
    public void CanDepartmentManagerReview_ReturnsExpectedResult(
        WorkflowStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusPolicy.CanDepartmentManagerReview(status));
    }

    [Theory]
    [MemberData(nameof(ManagerApprovalStatuses))]
    public void CanDepartmentManagerApprove_ReturnsExpectedResult(
        WorkflowStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusPolicy.CanDepartmentManagerApprove(status));
    }

    [Theory]
    [MemberData(nameof(ManagerApprovalStatuses))]
    public void CanDepartmentManagerReturnToAnalyst_ReturnsExpectedResult(
        WorkflowStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusPolicy.CanDepartmentManagerReturnToAnalyst(status));
    }

    [Theory]
    [MemberData(nameof(SubmittedStatuses))]
    public void CanProjectManagerEdit_ReturnsExpectedResult(
        WorkflowStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusPolicy.CanProjectManagerEdit(status));
    }

    [Theory]
    [MemberData(nameof(SubmittedStatuses))]
    public void CanProjectManagerDelete_ReturnsExpectedResult(
        WorkflowStatus status,
        bool expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusPolicy.CanProjectManagerDelete(status));
    }

    private static TheoryData<WorkflowStatus, bool> CreateStatusData(
        params WorkflowStatus[] allowedStatuses)
    {
        TheoryData<WorkflowStatus, bool> data = new();

        foreach (WorkflowStatus status in Enum.GetValues<WorkflowStatus>())
        {
            data.Add(status, allowedStatuses.Contains(status));
        }

        return data;
    }
}
