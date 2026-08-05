using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using FlowDesk.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace FlowDesk.CharacterizationTests.Authorization;

public sealed class DepartmentManagerAuthorizationTests
    : DatabaseTestBase
{
    private static string OtherDepartment => DepartmentOptions.All
        .First(x => x != TestDataSeeder.DefaultDepartment);

    [Fact]
    public async Task PendingUsers_DifferentDepartmentUser_IsNotListed()
    {
        string email = TestDataSeeder.UniqueEmail("cross-department");
        await WithServicesAsync(services => TestDataSeeder.CreateUserAsync(
            services,
            email,
            emailConfirmed: true,
            isApproved: false,
            requestedRole: AppRoles.Employee,
            department: OtherDepartment));
        using HttpClient client = ManagerClient();

        HttpResponseMessage response =
            await client.GetAsync("/DepartmentManager/PendingUsers");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(email, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApproveUser_DifferentDepartmentUser_IsForbidden()
    {
        ApplicationUser target = await WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("cross-approval"),
                emailConfirmed: true,
                isApproved: false,
                requestedRole: AppRoles.Employee,
                department: OtherDepartment));
        await SeedSameDepartmentPendingUserAsync();
        using HttpClient client = ManagerClient();

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                "/DepartmentManager/PendingUsers",
                $"/DepartmentManager/ApproveUser/{target.Id}",
                new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        bool isApproved = await WithServicesAsync(async services =>
            (await services.GetRequiredService<AppDbContext>()
                .Users.AsNoTracking()
                .SingleAsync(x => x.Id == target.Id)).IsApproved);
        Assert.False(isApproved);
    }

    [Fact]
    public async Task Review_DifferentDepartmentWorkItem_IsNotFound()
    {
        WorkItem target = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            OtherDepartment);
        using HttpClient client = ManagerClient();

        HttpResponseMessage response = await client.GetAsync(
            $"/DepartmentManager/Review/{target.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Approve_DifferentDepartmentWorkItem_DoesNotChangeIt()
    {
        WorkItem target = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            OtherDepartment);
        WorkItem own = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            TestDataSeeder.DefaultDepartment);
        using HttpClient client = ManagerClient();

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/DepartmentManager/Review/{own.Id}",
                $"/DepartmentManager/Approve/{target.Id}",
                new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            WorkflowStatus.WaitingManagerApproval,
            await GetWorkflowStatusAsync(target.Id));
    }

    [Fact]
    public async Task Return_DifferentDepartmentWorkItem_DoesNotChangeIt()
    {
        WorkItem target = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            OtherDepartment);
        WorkItem own = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            TestDataSeeder.DefaultDepartment);
        using HttpClient client = ManagerClient();

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/DepartmentManager/Review/{own.Id}",
                $"/DepartmentManager/ReturnToAnalyst/{target.Id}",
                new Dictionary<string, string>
                {
                    ["ManagerNote"] = "Cross-department attempt"
                });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            WorkflowStatus.WaitingManagerApproval,
            await GetWorkflowStatusAsync(target.Id));
    }

    [Fact]
    public async Task SharedExcel_ProjectManager_IsForbidden()
    {
        using HttpClient client = CreateClient().AuthenticateAs(
            101,
            AppRoles.ProjectManager,
            TestDataSeeder.UniqueEmail("project-manager-shared-excel"));

        HttpResponseMessage response = await client.GetAsync(
            "/DepartmentManager/SharedExcel");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ApproveUser_ExistingDifferentRole_IsRejected()
    {
        ApplicationUser target = await WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("existing-different-role"),
                emailConfirmed: true,
                isApproved: false,
                requestedRole: AppRoles.Employee,
                assignedRole: AppRoles.ProjectManager));
        using HttpClient client = ManagerClient();

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                "/DepartmentManager/PendingUsers",
                $"/DepartmentManager/ApproveUser/{target.Id}",
                new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var state = await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = (await userManager.FindByIdAsync(
                target.Id.ToString()))!;
            return new
            {
                user.IsApproved,
                Roles = await userManager.GetRolesAsync(user)
            };
        });
        Assert.False(state.IsApproved);
        Assert.Single(state.Roles);
        Assert.Equal(AppRoles.ProjectManager, state.Roles[0]);
    }

    [Fact]
    public async Task ApproveUser_ExistingRequestedRole_DoesNotDuplicateRole()
    {
        ApplicationUser target = await WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                TestDataSeeder.UniqueEmail("existing-same-role"),
                emailConfirmed: true,
                isApproved: false,
                requestedRole: AppRoles.Employee,
                assignedRole: AppRoles.Employee));
        using HttpClient client = ManagerClient();

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                "/DepartmentManager/PendingUsers",
                $"/DepartmentManager/ApproveUser/{target.Id}",
                new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var state = await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser user = (await userManager.FindByIdAsync(
                target.Id.ToString()))!;
            return new
            {
                user.IsApproved,
                Roles = await userManager.GetRolesAsync(user)
            };
        });
        Assert.True(state.IsApproved);
        Assert.Single(state.Roles);
        Assert.Equal(AppRoles.Employee, state.Roles[0]);
    }

    [Fact]
    public async Task InvalidManagerDepartment_AccountAndWorkflowFailClosed()
    {
        const int managerId = 7000;
        await WithServicesAsync(services => TestDataSeeder.CreateUserAsync(
            services,
            TestDataSeeder.UniqueEmail("invalid-department-manager"),
            assignedRole: AppRoles.DepartmentManager,
            department: "Invalid Department",
            userId: managerId));

        var results = await WithServicesAsync(async services =>
        {
            var account = await services
                .GetRequiredService<IAccountApprovalService>()
                .GetPendingUsersAsync(managerId);
            var workflow = await services
                .GetRequiredService<IDepartmentManagerWorkflowService>()
                .GetInboxAsync(managerId);
            return (account, workflow);
        });

        Assert.True(results.account.IsForbidden);
        Assert.True(results.workflow.IsForbidden);
    }

    private HttpClient ManagerClient()
    {
        return CreateClient().AuthenticateAs(
            9001,
            AppRoles.DepartmentManager,
            TestDataSeeder.UniqueEmail("manager-scope"));
    }

    private Task<ApplicationUser> SeedSameDepartmentPendingUserAsync()
    {
        return WithServicesAsync(services => TestDataSeeder.CreateUserAsync(
            services,
            TestDataSeeder.UniqueEmail("same-department-pending"),
            emailConfirmed: true,
            isApproved: false,
            requestedRole: AppRoles.Employee));
    }

    private Task<WorkItem> CreateWorkItemAsync(
        WorkflowStatus status,
        string department)
    {
        return WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                status,
                department: department));
    }

    private Task<WorkflowStatus> GetWorkflowStatusAsync(int id)
    {
        return WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => x.WorkflowStatus)
                .SingleAsync());
    }
}
