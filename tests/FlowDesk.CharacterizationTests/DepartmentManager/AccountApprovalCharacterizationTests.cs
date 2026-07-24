using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.CharacterizationTests.DepartmentManager;

public sealed class AccountApprovalCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task ApproveUser_ValidPendingUser_AssignsRequestedRoleAndApproves()
    {
        string email = TestDataSeeder.UniqueEmail("approval-valid");
        ApplicationUser pendingUser = await SeedPendingUserAsync(
            email,
            AppRoles.Analyst);
        using HttpClient client = ManagerClient();

        HttpResponseMessage response = await ApproveAsync(
            client,
            pendingUser.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var state = await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<
                    UserManager<ApplicationUser>>();
            ApplicationUser user =
                (await userManager.FindByIdAsync(
                    pendingUser.Id.ToString()))!;
            return new
            {
                user.IsApproved,
                isInRole = await userManager.IsInRoleAsync(
                    user,
                    AppRoles.Analyst)
            };
        });

        Assert.True(state.IsApproved);
        Assert.True(state.isInRole);
    }

    [Fact]
    public async Task ApproveUser_ValidPendingUser_RemovesUserFromPendingList()
    {
        string email = TestDataSeeder.UniqueEmail("approval-list");
        ApplicationUser pendingUser = await SeedPendingUserAsync(
            email,
            AppRoles.Employee);
        using HttpClient client = ManagerClient();
        await ApproveAsync(client, pendingUser.Id);

        HttpResponseMessage response =
            await client.GetAsync("/DepartmentManager/PendingUsers");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(
            email,
            body,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveUser_InvalidRequestedRole_DoesNotApproveUser()
    {
        string email = TestDataSeeder.UniqueEmail("approval-invalid-role");
        ApplicationUser pendingUser = await SeedPendingUserAsync(
            email,
            AppRoles.DepartmentManager);
        using HttpClient client = ManagerClient();

        HttpResponseMessage response = await ApproveAsync(
            client,
            pendingUser.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var state = await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<
                    UserManager<ApplicationUser>>();
            ApplicationUser user =
                (await userManager.FindByIdAsync(
                    pendingUser.Id.ToString()))!;
            return new
            {
                user.IsApproved,
                isInRole = await userManager.IsInRoleAsync(
                    user,
                    AppRoles.DepartmentManager)
            };
        });

        Assert.False(state.IsApproved);
        Assert.False(state.isInRole);
    }

    [Fact]
    public async Task ApproveUser_AlreadyApprovedUser_KeepsApprovedState()
    {
        string email = TestDataSeeder.UniqueEmail("approval-existing");
        ApplicationUser user = await WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                email,
                emailConfirmed: true,
                isApproved: true,
                requestedRole: AppRoles.ProjectManager));
        using HttpClient client = ManagerClient();

        HttpResponseMessage response = await ApproveAsync(
            client,
            user.Id);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        bool isApproved = await WithServicesAsync(async services =>
            (await services.GetRequiredService<
                    UserManager<ApplicationUser>>()
                .FindByIdAsync(user.Id.ToString()))!.IsApproved);

        Assert.True(isApproved);
    }

    private Task<ApplicationUser> SeedPendingUserAsync(
        string email,
        string requestedRole)
    {
        return WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                email,
                emailConfirmed: true,
                isApproved: false,
                requestedRole: requestedRole));
    }

    private HttpClient ManagerClient()
    {
        return CreateClient().AuthenticateAs(
            9001,
            AppRoles.DepartmentManager,
            TestDataSeeder.UniqueEmail("manager"));
    }

    private static Task<HttpResponseMessage> ApproveAsync(
        HttpClient client,
        int userId)
    {
        return client.PostFormWithAntiforgeryAsync(
            "/DepartmentManager/PendingUsers",
            $"/DepartmentManager/ApproveUser/{userId}",
            new Dictionary<string, string>());
    }
}