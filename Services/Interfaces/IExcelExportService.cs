using FlowDesk.ViewModels.DepartmentManager;

namespace FlowDesk.Services.Interfaces
{
    public interface IExcelExportService
    {
        byte[] CreateApprovedRequestExcel(
            ManagerReviewViewModel request);
    }
}