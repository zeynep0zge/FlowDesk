using FlowDesk.Models;

namespace FlowDesk.CharacterizationTests.Workflow;

public sealed class WorkflowStatusDescriptionsTests
{
    [Theory]
    [InlineData(
        WorkflowStatus.Submitted,
        "Analist İncelemesi Bekliyor")]
    [InlineData(
        WorkflowStatus.UnderAnalystReview,
        "Analist İncelemesinde")]
    [InlineData(
        WorkflowStatus.WaitingManagerApproval,
        "Departman Yöneticisi Onayı Bekliyor")]
    [InlineData(
        WorkflowStatus.ReturnedToAnalyst,
        "Analiste İade Edildi")]
    [InlineData(
        WorkflowStatus.Approved,
        "Departman Yöneticisi Tarafından Onaylandı")]
    [InlineData(WorkflowStatus.Assigned, "Yazılımcıya Atandı")]
    [InlineData(WorkflowStatus.InProgress, "Geliştiriliyor")]
    [InlineData(WorkflowStatus.Completed, "Tamamlandı")]
    [InlineData(WorkflowStatus.Rejected, "Reddedildi")]
    public void GetDescription_ReturnsCentralizedDisplayText(
        WorkflowStatus status,
        string expected)
    {
        Assert.Equal(
            expected,
            WorkflowStatusDescriptions.GetDescription(status));
    }
}