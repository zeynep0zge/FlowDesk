using FlowDesk.Common;
using FlowDesk.Models;
using FlowDesk.Repositories.Interfaces;
using FlowDesk.Services.Interfaces;

namespace FlowDesk.Services
{
    public sealed class EmployeeWorkItemService
        : IEmployeeWorkItemService
    {
        private const string ForbiddenMessage =
            "Bu talebe erisim yetkiniz bulunmuyor.";

        private const string NotFoundMessage =
            "Talep bulunamadi.";

        private readonly IWorkItemRepository _workItemRepository;

        public EmployeeWorkItemService(
            IWorkItemRepository workItemRepository)
        {
            _workItemRepository = workItemRepository;
        }

        public async Task<ServiceResult<List<WorkItem>>>
            GetAssignedWorkItemsAsync(int? employeeId)
        {
            if (!IsValidEmployeeId(employeeId))
            {
                return ServiceResult<List<WorkItem>>.Forbidden(
                    ForbiddenMessage);
            }

            List<WorkItem> workItems = await _workItemRepository
                .GetEmployeeAssignedWorkItemsAsync(
                    employeeId.GetValueOrDefault());

            return ServiceResult<List<WorkItem>>.Success(workItems);
        }

        public async Task<ServiceResult<WorkItem>> GetDetailsAsync(
            int workItemId,
            int? employeeId)
        {
            if (!IsValidEmployeeId(employeeId))
            {
                return ServiceResult<WorkItem>.Forbidden(
                    ForbiddenMessage);
            }

            if (workItemId <= 0)
            {
                return ServiceResult<WorkItem>.NotFound(
                    NotFoundMessage);
            }

            WorkItem? workItem = await _workItemRepository
                .GetEmployeeWorkItemByIdAsync(
                    workItemId,
                    employeeId.GetValueOrDefault());

            if (workItem == null)
            {
                return ServiceResult<WorkItem>.NotFound(
                    NotFoundMessage);
            }

            return ServiceResult<WorkItem>.Success(workItem);
        }

        private static bool IsValidEmployeeId(int? employeeId)
        {
            return employeeId.HasValue && employeeId.Value > 0;
        }
    }
}
