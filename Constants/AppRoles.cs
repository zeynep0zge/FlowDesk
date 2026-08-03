namespace FlowDesk.Constants
{
    public static class AppRoles
    {
        public const string ProjectManager = "ProjectManager";
        public const string Analyst = "Analyst";
        public const string DepartmentManager = "DepartmentManager";
        public const string Employee = "Employee";

        public static readonly string[] All =
        {
            ProjectManager,
            Analyst,
            DepartmentManager,
            Employee
        };

        public static readonly string[] SelfRegistrable =
        {
            ProjectManager,
            Analyst,
            Employee
        };

        public static bool IsSelfRegistrable(string? role)
        {
            return Array.Exists(
                SelfRegistrable,
                allowedRole => string.Equals(
                    allowedRole,
                    role,
                    StringComparison.Ordinal));
        }

        public static string GetDisplayName(string? role)
        {
            return role switch
            {
                ProjectManager => "İş Birimi",
                Analyst => "Analist",
                DepartmentManager => "Departman Yöneticisi",
                Employee => "Yazılımcı",
                _ => role ?? string.Empty
            };
        }

        public static string? GetBusinessCodePrefix(string? role)
        {
            return role switch
            {
                ProjectManager => "ISB",
                Analyst => "ANL",
                DepartmentManager => "DYN",
                Employee => "ENG",
                _ => null
            };
        }
    }
}
