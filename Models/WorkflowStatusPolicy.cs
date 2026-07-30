namespace FlowDesk.Models;

public static class WorkflowStatusPolicy
{
    public static bool CanAnalystStartReview(WorkflowStatus status)
    {
        return IsAnalystEditable(status);
    }

    public static bool CanAnalystSaveAnalysis(WorkflowStatus status)
    {
        return IsAnalystEditable(status);
    }

    public static bool CanAnalystSubmitForManagerApproval(
        WorkflowStatus status)
    {
        return IsAnalystEditable(status);
    }

    public static bool CanDepartmentManagerReview(WorkflowStatus status)
    {
        return status == WorkflowStatus.WaitingManagerApproval;
    }

    public static bool CanDepartmentManagerApprove(WorkflowStatus status)
    {
        return status == WorkflowStatus.WaitingManagerApproval;
    }

    public static bool CanDepartmentManagerReturnToAnalyst(
        WorkflowStatus status)
    {
        return status == WorkflowStatus.WaitingManagerApproval;
    }

    public static bool CanDepartmentManagerExport(WorkflowStatus status)
    {
        return status == WorkflowStatus.Approved;
    }

    public static bool CanProjectManagerEdit(WorkflowStatus status)
    {
        return status == WorkflowStatus.Submitted;
    }

    public static bool CanProjectManagerDelete(WorkflowStatus status)
    {
        return status == WorkflowStatus.Submitted;
    }

    private static bool IsAnalystEditable(WorkflowStatus status)
    {
        return status is WorkflowStatus.Submitted
            or WorkflowStatus.UnderAnalystReview
            or WorkflowStatus.ReturnedToAnalyst;
    }
}