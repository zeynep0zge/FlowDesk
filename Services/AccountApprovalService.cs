using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using FlowDesk.ViewModels.DepartmentManager;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.Services
{
    public sealed class AccountApprovalService
        : IAccountApprovalService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IIdentifierGenerator _identifierGenerator;

        public AccountApprovalService(
            UserManager<ApplicationUser> userManager,
            IIdentifierGenerator identifierGenerator)
        {
            _userManager = userManager;
            _identifierGenerator = identifierGenerator;
        }

        public async Task<
            ServiceResult<IReadOnlyList<PendingUserViewModel>>>
            GetPendingUsersAsync(int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await GetManagerAccessScopeAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult<
                    IReadOnlyList<PendingUserViewModel>>.Forbidden(
                        departmentResult.ErrorMessage!);
            }

            ManagerAccessScope managerScope = departmentResult.Data!;

            List<PendingUserViewModel> pendingUsers =
                await _userManager.Users
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

            return ServiceResult<
                IReadOnlyList<PendingUserViewModel>>.Success(
                    pendingUsers);
        }

        public async Task<ServiceResult> ApproveUserAsync(
            int userId,
            int? managerUserId)
        {
            ServiceResult<ManagerAccessScope> departmentResult =
                await GetManagerAccessScopeAsync(managerUserId);

            if (!departmentResult.IsSuccess)
            {
                return ServiceResult.Forbidden(
                    departmentResult.ErrorMessage!);
            }

            ApplicationUser? user =
                await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                return ServiceResult.NotFound(
                    "Kullanıcı bulunamadı.");
            }

            if (!departmentResult.Data!.CanAccessAllDepartments &&
                !string.Equals(
                    user.Department,
                    departmentResult.Data.Department,
                    StringComparison.Ordinal))
            {
                return ServiceResult.Forbidden(
                    "Bu kullaniciyi onaylama yetkiniz bulunmuyor.");
            }

            IList<string> existingRoles =
                await _userManager.GetRolesAsync(user);

            if (existingRoles.Any(role => !string.Equals(
                    role,
                    user.RequestedRole,
                    StringComparison.Ordinal)))
            {
                return ServiceResult.Failure(
                    "Mevcut rol talep edilen rolle uyumlu degil.");
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
                    "Kullanıcının e-posta veya rol talebi " +
                    "onay için uygun değil.");
            }

            IList<string> currentRoles = existingRoles;

            bool alreadyInRole = currentRoles.Any(role => string.Equals(
                role,
                user.RequestedRole,
                StringComparison.Ordinal));

            if (!alreadyInRole)
            {
                IdentityResult roleResult =
                    await _userManager.AddToRoleAsync(
                        user,
                        user.RequestedRole!);

                if (!roleResult.Succeeded)
                {
                    return ServiceResult.Failure(
                        JoinIdentityErrors(roleResult));
                }
            }

            if (string.IsNullOrWhiteSpace(user.BusinessCode))
            {
                try
                {
                    user.BusinessCode =
                        await _identifierGenerator.GenerateUserCodeAsync(
                            user.RequestedRole!);
                }
                catch (Exception exception)
                    when (exception is ArgumentException or
                          InvalidOperationException)
                {
                    return ServiceResult.Failure(
                        "Kullanici kurumsal kodu olusturulamadi.");
                }
            }

            user.IsApproved = true;
            IdentityResult updateResult =
                await _userManager.UpdateAsync(user);

            if (!updateResult.Succeeded)
            {
                return ServiceResult.Failure(
                    JoinIdentityErrors(updateResult));
            }

            return ServiceResult.Success(
                "Kullanıcı hesabı onaylandı ve " +
                "talep edilen rol atandı.");
        }

        private async Task<ServiceResult<ManagerAccessScope>>
            GetManagerAccessScopeAsync(int? managerUserId)
        {
            if (!managerUserId.HasValue || managerUserId.Value <= 0)
            {
                return ServiceResult<ManagerAccessScope>.Forbidden(
                    "Gecerli departman yoneticisi kimligi bulunamadi.");
            }

            ApplicationUser? manager = await _userManager.FindByIdAsync(
                managerUserId.Value.ToString());

            if (manager == null ||
                (!DepartmentOptions.Contains(manager.Department) &&
                 !string.Equals(
                     manager.Department,
                     DepartmentOptions.AllDepartments,
                     StringComparison.Ordinal)) ||
                !await _userManager.IsInRoleAsync(
                    manager,
                    AppRoles.DepartmentManager))
            {
                return ServiceResult<ManagerAccessScope>.Forbidden(
                    "Departman yoneticisi departmani gecersiz.");
            }

            bool canAccessAllDepartments = string.Equals(
                manager.Department,
                DepartmentOptions.AllDepartments,
                StringComparison.Ordinal);

            return ServiceResult<ManagerAccessScope>.Success(
                new ManagerAccessScope(
                    manager.Department!,
                    canAccessAllDepartments));
        }

        private static string JoinIdentityErrors(
            IdentityResult identityResult)
        {
            return string.Join(
                " ",
                identityResult.Errors.Select(
                    error => error.Description));
        }
    }
}
