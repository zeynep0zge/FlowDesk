using System.Security.Claims;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.Services.Models;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.Services;

public sealed class AuthenticatedActorContextResolver
    : IAuthenticatedActorContextResolver
{
    private const string InvalidActorMessage =
        "Geçerli ve yetkili kullanıcı bilgisi bulunamadı.";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IManagerAccessScopeResolver
        _managerAccessScopeResolver;

    public AuthenticatedActorContextResolver(
        UserManager<ApplicationUser> userManager,
        IManagerAccessScopeResolver managerAccessScopeResolver)
    {
        _userManager = userManager;
        _managerAccessScopeResolver = managerAccessScopeResolver;
    }

    public async Task<ServiceResult<ActorContext>> ResolveAsync(
        ClaimsPrincipal principal)
    {
        if (principal?.Identity?.IsAuthenticated != true ||
            !int.TryParse(
                principal.FindFirstValue(ClaimTypes.NameIdentifier),
                out int userId) ||
            userId <= 0)
        {
            return Forbidden();
        }

        ApplicationUser? user = await _userManager.FindByIdAsync(
            userId.ToString());
        if (user == null || !user.EmailConfirmed || !user.IsApproved)
        {
            return Forbidden();
        }

        IList<string> roles = await _userManager.GetRolesAsync(user);
        if (roles.Count != 1 ||
            !AppRoles.All.Contains(roles[0], StringComparer.Ordinal))
        {
            return Forbidden();
        }

        string role = roles[0];
        if (role == AppRoles.DepartmentManager)
        {
            ServiceResult<ManagerAccessScope> scopeResult =
                await _managerAccessScopeResolver.ResolveAsync(user.Id);
            if (!scopeResult.IsSuccess || scopeResult.Data == null)
            {
                return Forbidden();
            }

            return ServiceResult<ActorContext>.Success(
                new ActorContext(
                    user.Id,
                    role,
                    scopeResult.Data.Department,
                    scopeResult.Data.CanAccessAllDepartments));
        }

        return ServiceResult<ActorContext>.Success(
            new ActorContext(
                user.Id,
                role,
                user.Department?.Trim(),
                false));
    }

    private static ServiceResult<ActorContext> Forbidden()
    {
        return ServiceResult<ActorContext>.Forbidden(
            InvalidActorMessage);
    }
}
