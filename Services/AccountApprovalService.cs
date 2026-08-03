using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.DepartmentManager;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Services;

public sealed class AccountApprovalService : IAccountApprovalService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IIdentifierGenerator _identifierGenerator;
    private readonly AppDbContext _dbContext;
    private readonly IManagerAccessScopeResolver
        _managerAccessScopeResolver;

    public AccountApprovalService(
        UserManager<ApplicationUser> userManager,
        IIdentifierGenerator identifierGenerator,
        AppDbContext dbContext,
        IManagerAccessScopeResolver managerAccessScopeResolver)
    {
        _userManager = userManager;
        _identifierGenerator = identifierGenerator;
        _dbContext = dbContext;
        _managerAccessScopeResolver = managerAccessScopeResolver;
    }

    public async Task<ServiceResult<IReadOnlyList<PendingUserViewModel>>>
        GetPendingUsersAsync(int? managerUserId)
    {
        ServiceResult<ManagerAccessScope> scopeResult =
            await _managerAccessScopeResolver.ResolveAsync(managerUserId);

        if (!scopeResult.IsSuccess)
        {
            return ServiceResult<IReadOnlyList<PendingUserViewModel>>
                .Forbidden(scopeResult.ErrorMessage!);
        }

        ManagerAccessScope managerScope = scopeResult.Data!;
        List<PendingUserViewModel> pendingUsers = await _userManager.Users
            .AsNoTracking()
            .Where(user =>
                user.EmailConfirmed &&
                !user.IsApproved &&
                (managerScope.CanAccessAllDepartments ||
                 user.Department == managerScope.Department) &&
                user.RequestedRole != null &&
                user.RequestedRole != string.Empty)
            .OrderBy(user => user.CreatedAtUtc)
            .Select(user => new PendingUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Department = user.Department ?? string.Empty,
                RequestedRole = user.RequestedRole ?? string.Empty,
                CreatedAtUtc = user.CreatedAtUtc
            })
            .ToListAsync();

        return ServiceResult<IReadOnlyList<PendingUserViewModel>>.Success(
            pendingUsers);
    }

    public async Task<ServiceResult> ApproveUserAsync(
        int userId,
        int? managerUserId)
    {
        ServiceResult<ManagerAccessScope> scopeResult =
            await _managerAccessScopeResolver.ResolveAsync(managerUserId);

        if (!scopeResult.IsSuccess)
        {
            return ServiceResult.Forbidden(scopeResult.ErrorMessage!);
        }

        ApplicationUser? user =
            await _userManager.FindByIdAsync(userId.ToString());

        if (user == null)
        {
            return ServiceResult.NotFound("Kullanıcı bulunamadı.");
        }

        ManagerAccessScope managerScope = scopeResult.Data!;
        if (!managerScope.CanAccessAllDepartments &&
            !string.Equals(
                user.Department,
                managerScope.Department,
                StringComparison.Ordinal))
        {
            return ServiceResult.Forbidden(
                "Bu kullanıcıyı onaylama yetkiniz bulunmuyor.");
        }

        IList<string> existingRoles =
            await _userManager.GetRolesAsync(user);

        if (existingRoles.Any(role => !string.Equals(
                role,
                user.RequestedRole,
                StringComparison.Ordinal)))
        {
            return ServiceResult.Failure(
                "Mevcut rol talep edilen rolle uyumlu değil.");
        }

        if (user.IsApproved)
        {
            return ServiceResult.Success(
                "Bu kullanıcı daha önce onaylanmış.");
        }

        if (!user.EmailConfirmed ||
            !AppRoles.IsSelfRegistrable(user.RequestedRole))
        {
            return ServiceResult.Failure(
                "Kullanıcının e-posta veya rol talebi onay için uygun değil.");
        }

        bool alreadyInRole = existingRoles.Any(role => string.Equals(
            role,
            user.RequestedRole,
            StringComparison.Ordinal));

        try
        {
            return await _dbContext.Database
                .CreateExecutionStrategy()
                .ExecuteAsync(async () =>
                {
                    await using var transaction =
                        await _dbContext.Database.BeginTransactionAsync();

                    if (!alreadyInRole)
                    {
                        IdentityResult roleResult =
                            await _userManager.AddToRoleAsync(
                                user,
                                user.RequestedRole!);

                        if (!roleResult.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            return ServiceResult.Failure(
                                JoinIdentityErrors(roleResult));
                        }
                    }

                    if (string.IsNullOrWhiteSpace(user.BusinessCode))
                    {
                        user.BusinessCode = await _identifierGenerator
                            .GenerateUserCodeAsync(user.RequestedRole!);
                    }

                    user.IsApproved = true;
                    IdentityResult updateResult =
                        await _userManager.UpdateAsync(user);

                    if (!updateResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return ServiceResult.Failure(
                            JoinIdentityErrors(updateResult));
                    }

                    await transaction.CommitAsync();
                    return ServiceResult.Success(
                        "Kullanıcı hesabı onaylandı ve talep edilen rol atandı.");
                });
        }
        catch (Exception exception)
            when (exception is ArgumentException or
                  InvalidOperationException or
                  DbUpdateException)
        {
            return ServiceResult.Failure(
                "Kullanıcı onay işlemi tamamlanamadı.");
        }
    }

    private static string JoinIdentityErrors(IdentityResult identityResult)
    {
        return string.Join(
            " ",
            identityResult.Errors.Select(error => error.Description));
    }
}
