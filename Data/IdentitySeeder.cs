using FlowDesk.Constants;
using FlowDesk.Common;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FlowDesk.Services.Interfaces;

namespace FlowDesk.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedRolesAsync(
            IServiceProvider serviceProvider)
        {
            var roleManager =
                serviceProvider.GetRequiredService<
                    RoleManager<IdentityRole<int>>>();

            foreach (var roleName in AppRoles.All)
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                var result = await roleManager.CreateAsync(
                    new IdentityRole<int>(roleName));

                if (!result.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        result.Errors.Select(x => x.Description));

                    throw new InvalidOperationException(
                        $"{roleName} rolü oluşturulamadı: {errors}");
                }
            }
        }

        public static async Task SeedTestUsersAsync(
            IServiceProvider serviceProvider)
        {
            var userManager =
                serviceProvider.GetRequiredService<
                    UserManager<ApplicationUser>>();

            await CreateUserAsync(
                userManager,
                email: "projectmanager@flowdesk.local",
                fullName: "Test Proje Yöneticisi",
                department: null,
                roleName: AppRoles.ProjectManager);

            await CreateUserAsync(
                userManager,
                email: "analyst@flowdesk.local",
                fullName: "Test Analist",
                department: null,
                roleName: AppRoles.Analyst);

            await CreateUserAsync(
                userManager,
                email: "manager@flowdesk.local",
                fullName: "Test Departman Müdürü",
                department: "Kartlı Sistemler 1",
                roleName: AppRoles.DepartmentManager);

            await EnsureDepartmentAsync(
                userManager,
                "manager@flowdesk.local",
                DepartmentOptions.AllDepartments);

            await CreateUserAsync(
                userManager,
                email: "employee@flowdesk.local",
                fullName: "Test Yaz\u0131l\u0131mc\u0131",
                department: "Kartl\u0131 Sistemler 1",
                roleName: AppRoles.Employee);
        }

        public static async Task BackfillBusinessCodesAsync(
            IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<
                UserManager<ApplicationUser>>();
            var identifierGenerator = serviceProvider.GetRequiredService<
                IIdentifierGenerator>();

            List<ApplicationUser> users = await userManager.Users
                .Where(user => user.BusinessCode == null)
                .OrderBy(user => user.Id)
                .ToListAsync();

            foreach (ApplicationUser user in users)
            {
                string? role = await ResolveBusinessCodeRoleAsync(
                    userManager,
                    user);

                if (role == null)
                {
                    continue;
                }

                user.BusinessCode =
                    await identifierGenerator.GenerateUserCodeAsync(role);

                IdentityResult result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    string errors = string.Join(
                        ", ",
                        result.Errors.Select(error => error.Description));
                    throw new InvalidOperationException(
                        $"Business code backfill failed for user {user.Id}: " +
                        errors);
                }
            }
        }

        private static async Task<string?> ResolveBusinessCodeRoleAsync(
            UserManager<ApplicationUser> userManager,
            ApplicationUser user)
        {
            if (AppRoles.GetBusinessCodePrefix(user.RequestedRole) != null)
            {
                return user.RequestedRole;
            }

            IList<string> roles = await userManager.GetRolesAsync(user);
            string[] supportedRoles = roles
                .Where(role => AppRoles.GetBusinessCodePrefix(role) != null)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return supportedRoles.Length == 1
                ? supportedRoles[0]
                : null;
        }

        private static async Task EnsureDepartmentAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string department)
        {
            ApplicationUser? user = await userManager.FindByEmailAsync(email);
            if (user == null || string.Equals(
                    user.Department,
                    department,
                    StringComparison.Ordinal))
            {
                return;
            }

            user.Department = department;
            IdentityResult result = await userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                string errors = string.Join(
                    ", ",
                    result.Errors.Select(error => error.Description));
                throw new InvalidOperationException(
                    $"{email} department could not be updated: {errors}");
            }
        }

        private static async Task CreateUserAsync(
            UserManager<ApplicationUser> userManager,
            string email,
            string fullName,
            string? department,
            string roleName)
        {
            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    Department = department,
                    EmailConfirmed = true
                };

                var createResult = await userManager.CreateAsync(
                    user,
                    "FlowDesk123!");

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        createResult.Errors.Select(x => x.Description));

                    throw new InvalidOperationException(
                        $"{email} kullanıcısı oluşturulamadı: {errors}");
                }
            }

            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                var roleResult =
                    await userManager.AddToRoleAsync(user, roleName);

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        roleResult.Errors.Select(x => x.Description));

                    throw new InvalidOperationException(
                        $"{email} kullanıcısına rol atanamadı: {errors}");
                }
            }
        }
    }
}
