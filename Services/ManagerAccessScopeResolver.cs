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

    public ManagerAccessScopeResolver(
        UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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

        return ServiceResult<ManagerAccessScope>.Success(
            new ManagerAccessScope(
                DepartmentOptions.AllDepartments,
                true));
    }
}
