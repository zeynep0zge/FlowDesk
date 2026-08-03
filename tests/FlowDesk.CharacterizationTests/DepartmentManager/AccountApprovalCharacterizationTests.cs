using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services;
using FlowDesk.Services.Interfaces;
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
                user.BusinessCode,
                isInRole = await userManager.IsInRoleAsync(
                    user,
                    AppRoles.Analyst)
            };
        });

        Assert.True(state.IsApproved);
        Assert.True(state.isInRole);
        Assert.Matches(@"^ANL-\d{8}-\d{4}$", state.BusinessCode!);
    }

    [Theory]
    [InlineData(AppRoles.Employee, "ENG")]
    [InlineData(AppRoles.ProjectManager, "ISB")]
    public async Task ApproveUser_AssignsRoleSpecificBusinessCode(
        string requestedRole,
        string expectedPrefix)
    {
        ApplicationUser pendingUser = await SeedPendingUserAsync(
            TestDataSeeder.UniqueEmail("approval-business-code"),
            requestedRole);

        await ApproveAsync(ManagerClient(), pendingUser.Id);

        string? businessCode = await WithServicesAsync(async services =>
            (await services.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByIdAsync(pendingUser.Id.ToString()))!.BusinessCode);

        Assert.Matches(
            $@"^{expectedPrefix}-\d{{8}}-\d{{4}}$",
            businessCode!);
    }

    [Fact]
    public async Task ApproveUser_ExistingBusinessCode_DoesNotReplaceIt()
    {
        const string existingCode = "ANL-29072026-4321";
        ApplicationUser pendingUser = await WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("approval-existing-code"),
                emailConfirmed: true,
                isApproved: false,
                requestedRole: AppRoles.Analyst,
                businessCode: existingCode));

        await ApproveAsync(ManagerClient(), pendingUser.Id);

        string? businessCode = await WithServicesAsync(async services =>
            (await services.GetRequiredService<UserManager<ApplicationUser>>()
                .FindByIdAsync(pendingUser.Id.ToString()))!.BusinessCode);

        Assert.Equal(existingCode, businessCode);
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

    [Fact]
    public async Task ApproveUser_CodeGenerationFailure_RollsBackRoleAndState()
    {
        ApplicationUser pendingUser = await SeedPendingUserAsync(
            TestDataSeeder.UniqueEmail("approval-rollback"),
            AppRoles.Analyst);

        var result = await WithServicesAsync(async services =>
        {
            var service = new AccountApprovalService(
                services.GetRequiredService<
                    UserManager<ApplicationUser>>(),
                new ThrowingIdentifierGenerator(),
                services.GetRequiredService<AppDbContext>(),
                services.GetRequiredService<
                    IManagerAccessScopeResolver>());

            return await service.ApproveUserAsync(pendingUser.Id, 9001);
        });

        Assert.False(result.IsSuccess);

        var state = await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = (await userManager.FindByIdAsync(
                pendingUser.Id.ToString()))!;
            return new
            {
                user.IsApproved,
                user.BusinessCode,
                IsInRole = await userManager.IsInRoleAsync(
                    user,
                    AppRoles.Analyst)
            };
        });

        Assert.False(state.IsApproved);
        Assert.Null(state.BusinessCode);
        Assert.False(state.IsInRole);
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

    private sealed class ThrowingIdentifierGenerator
        : IIdentifierGenerator
    {
        public Task<string> GenerateUserCodeAsync(string role)
        {
            throw new InvalidOperationException(
                "Controlled identifier failure.");
        }

        public Task<string> GenerateWorkItemCodeAsync()
        {
            return Task.FromResult("unused");
        }
    }
}
