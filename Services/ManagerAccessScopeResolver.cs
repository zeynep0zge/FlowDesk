using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.Services;

public sealed class ManagerAccessScopeResolver
    : IManagerAccessScopeResolver
{
    private const string InvalidScopeMessage =
        "Departman yöneticisi kapsamı geçersiz.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IHostEnvironment _hostEnvironment;

    public ManagerAccessScopeResolver(
        UserManager<ApplicationUser> userManager,
        IHostEnvironment hostEnvironment)
    {
        _userManager = userManager;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<ServiceResult<ManagerAccessScope>> ResolveAsync(
        int? managerUserId)
    {
        if (!managerUserId.HasValue || managerUserId.Value <= 0)
        {
            return ServiceResult<ManagerAccessScope>.Forbidden(
                InvalidScopeMessage);
        }

        ApplicationUser? manager = await _userManager.FindByIdAsync(
            managerUserId.Value.ToString());

        if (manager == null ||
            !await _userManager.IsInRoleAsync(
                manager,
                AppRoles.DepartmentManager))
        {
            return ServiceResult<ManagerAccessScope>.Forbidden(
                InvalidScopeMessage);
        }

        string? department = manager.Department?.Trim();
        bool requestsGlobalScope = string.Equals(
            department,
            DepartmentOptions.AllDepartments,
            StringComparison.Ordinal);

        if (requestsGlobalScope)
        {
            return _hostEnvironment.IsDevelopment()
                ? ServiceResult<ManagerAccessScope>.Success(
                    new ManagerAccessScope(
                        DepartmentOptions.AllDepartments,
                        true))
                : ServiceResult<ManagerAccessScope>.Forbidden(
                    InvalidScopeMessage);
        }

        if (!DepartmentOptions.Contains(department))
        {
            return ServiceResult<ManagerAccessScope>.Forbidden(
                InvalidScopeMessage);
        }

        return ServiceResult<ManagerAccessScope>.Success(
            new ManagerAccessScope(department!, false));
    }
}
