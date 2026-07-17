namespace FlowDesk.ViewModels.DepartmentManager
{
    public class ManagerInboxViewModel
    {
        public List<ManagerInboxItemViewModel> Requests { get; set; } = new();

        public int TotalCount => Requests.Count;
    }
}