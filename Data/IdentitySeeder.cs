using FlowDesk.Constants;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;

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