namespace FlowDesk.Services.Models;

public sealed class ActorContext
{
    internal ActorContext(
        int userId,
        string role,
        string? department,
        bool canAccessAllDepartments)
    {
        UserId = userId;
        Role = role;
        Department = department;
        CanAccessAllDepartments = canAccessAllDepartments;
    }

    public int UserId { get; }

    public string Role { get; }

    public string? Department { get; }

    public bool CanAccessAllDepartments { get; }
}
