namespace FlowDesk.Common;

public sealed record ManagerAccessScope(
    string Department,
    bool CanAccessAllDepartments);
