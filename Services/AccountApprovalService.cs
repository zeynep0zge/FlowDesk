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

        public AccountApprovalService(
            UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IReadOnlyList<PendingUserViewModel>>
            GetPendingUsersAsync()
        {
            return await _userManager.Users
                .AsNoTracking()
                .Where(user =>
                    user.EmailConfirmed &&
                    !user.IsApproved &&
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
        }

        public async Task<ServiceResult> ApproveUserAsync(int userId)
        {
            ApplicationUser? user =
                await _userManager.FindByIdAsync(userId.ToString());

            if (user == null)
            {
                return ServiceResult.NotFound(
                    "Kullanıcı bulunamadı.");
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

            bool alreadyInRole = await _userManager.IsInRoleAsync(
                user,
                user.RequestedRole!);

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