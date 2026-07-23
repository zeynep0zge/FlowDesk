using FlowDesk.Constants;

namespace FlowDesk.ViewModels.DepartmentManager
{
    public class PendingUserViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string RequestedRole { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }

        public string RequestedRoleDisplayName =>
            AppRoles.GetDisplayName(RequestedRole);
    }
}
