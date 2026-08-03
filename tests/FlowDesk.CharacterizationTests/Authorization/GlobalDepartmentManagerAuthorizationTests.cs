using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Common;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace FlowDesk.CharacterizationTests.Authorization;

public sealed class GlobalDepartmentManagerAuthorizationTests
    : DatabaseTestBase
{
    public GlobalDepartmentManagerAuthorizationTests()
        : base(Environments.Development)
    {
    }

    private const int GlobalManagerId = 9100;
    private static string OtherDepartment => DepartmentOptions.All
        .First(value => value != TestDataSeeder.DefaultDepartment);

    [Fact]
    public void AllDepartments_IsNotAUserSelectableDepartment()
    {
        Assert.DoesNotContain(
            DepartmentOptions.AllDepartments,
            DepartmentOptions.All);
    }

    [Fact]
    public async Task NormalManager_InboxListsOnlyOwnDepartmentRequests()
    {
        WorkItem own = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            TestDataSeeder.DefaultDepartment);
        WorkItem other = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            OtherDepartment);

        using HttpClient client = ManagerClient(9001);
        string body = await (await client.GetAsync(
            "/DepartmentManager/Inbox")).Content.ReadAsStringAsync();

        Assert.Contains(own.RequestNumber, body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            other.RequestNumber,
            body,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalManager_InboxListsRequestsFromAllDepartments()
    {
        await SeedGlobalManagerAsync();
        WorkItem first = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            TestDataSeeder.DefaultDepartment);
        WorkItem second = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            OtherDepartment);

        using HttpClient client = ManagerClient(GlobalManagerId);
        HttpResponseMessage response =
            await client.GetAsync("/DepartmentManager/Inbox");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(first.RequestNumber, body, StringComparison.Ordinal);
        Assert.Contains(second.RequestNumber, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalManager_CanReviewOtherDepartmentRequest()
    {
        await SeedGlobalManagerAsync();
        WorkItem target = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            OtherDepartment);

        using HttpClient client = ManagerClient(GlobalManagerId);
        HttpResponseMessage response = await client.GetAsync(
            $"/DepartmentManager/Review/{target.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GlobalManager_CanApproveOtherDepartmentRequest()
    {
        await SeedGlobalManagerAsync();
        WorkItem target = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            OtherDepartment);
        using HttpClient client = ManagerClient(GlobalManagerId);

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/DepartmentManager/Review/{target.Id}",
                $"/DepartmentManager/Approve/{target.Id}",
                new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            WorkflowStatus.Approved,
            await GetWorkflowStatusAsync(target.Id));
    }

    [Fact]
    public async Task GlobalManager_CanReturnOtherDepartmentRequest()
    {
        await SeedGlobalManagerAsync();
        WorkItem target = await CreateWorkItemAsync(
            WorkflowStatus.WaitingManagerApproval,
            OtherDepartment);
        using HttpClient client = ManagerClient(GlobalManagerId);

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                $"/DepartmentManager/Review/{target.Id}",
                $"/DepartmentManager/ReturnToAnalyst/{target.Id}",
                new Dictionary<string, string>
                {
                    ["ManagerNote"] = "Global scope return test"
                });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            WorkflowStatus.ReturnedToAnalyst,
            await GetWorkflowStatusAsync(target.Id));
    }

    [Fact]
    public async Task GlobalManager_CanExportApprovedRequestsFromAllDepartments()
    {
        await SeedGlobalManagerAsync();
        WorkItem first = await CreateWorkItemAsync(
            WorkflowStatus.Approved,
            TestDataSeeder.DefaultDepartment);
        WorkItem second = await CreateWorkItemAsync(
            WorkflowStatus.Approved,
            OtherDepartment);
        using HttpClient client = ManagerClient(GlobalManagerId);

        HttpResponseMessage firstResponse = await client.GetAsync(
            $"/DepartmentManager/DownloadExcel/{first.Id}");
        HttpResponseMessage secondResponse = await client.GetAsync(
            $"/DepartmentManager/DownloadExcel/{second.Id}");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            firstResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            secondResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GlobalManager_ApprovedRequestsListsAllDepartments()
    {
        await SeedGlobalManagerAsync();
        WorkItem first = await CreateWorkItemAsync(
            WorkflowStatus.Approved,
            TestDataSeeder.DefaultDepartment);
        WorkItem second = await CreateWorkItemAsync(
            WorkflowStatus.Approved,
            OtherDepartment);
        using HttpClient client = ManagerClient(GlobalManagerId);

        HttpResponseMessage response = await client.GetAsync(
            "/DepartmentManager/ApprovedRequests");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(first.RequestNumber, body, StringComparison.Ordinal);
        Assert.Contains(second.RequestNumber, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalManager_PendingUsersListsAllDepartments()
    {
        await SeedGlobalManagerAsync();
        ApplicationUser first = await CreatePendingUserAsync(
            TestDataSeeder.DefaultDepartment);
        ApplicationUser second = await CreatePendingUserAsync(
            OtherDepartment);
        using HttpClient client = ManagerClient(GlobalManagerId);

        HttpResponseMessage response =
            await client.GetAsync("/DepartmentManager/PendingUsers");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(first.Email!, body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(second.Email!, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GlobalManager_CanApproveOtherDepartmentPendingUser()
    {
        await SeedGlobalManagerAsync();
        ApplicationUser target = await CreatePendingUserAsync(OtherDepartment);
        using HttpClient client = ManagerClient(GlobalManagerId);

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                "/DepartmentManager/PendingUsers",
                $"/DepartmentManager/ApproveUser/{target.Id}",
                new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        bool isApproved = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .Users.AsNoTracking()
                .Where(user => user.Id == target.Id)
                .Select(user => user.IsApproved)
                .SingleAsync());
        Assert.True(isApproved);
    }

    [Fact]
    public async Task DevelopmentSeeder_ExistingTestManager_UpdatesGlobalDepartment()
    {
        await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();
            ApplicationUser existing = await TestDataSeeder.CreateUserAsync(
                services,
                "manager@flowdesk.local",
                assignedRole: AppRoles.DepartmentManager,
                department: TestDataSeeder.DefaultDepartment);

            await IdentitySeeder.SeedTestUsersAsync(services);

            ApplicationUser updated = (await userManager.FindByIdAsync(
                existing.Id.ToString()))!;
            Assert.Equal(
                DepartmentOptions.AllDepartments,
                updated.Department);
        });
    }

    private Task SeedGlobalManagerAsync()
    {
        return WithServicesAsync(services => TestDataSeeder.CreateUserAsync(
            services,
            TestDataSeeder.UniqueEmail("global-manager"),
            assignedRole: AppRoles.DepartmentManager,
            department: DepartmentOptions.AllDepartments,
            userId: GlobalManagerId));
    }

    private Task<ApplicationUser> CreatePendingUserAsync(string department)
    {
        return WithServicesAsync(services => TestDataSeeder.CreateUserAsync(
            services,
            TestDataSeeder.UniqueEmail("global-pending"),
            emailConfirmed: true,
            isApproved: false,
            requestedRole: AppRoles.Employee,
            department: department));
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

    private HttpClient ManagerClient(int userId)
    {
        return CreateClient().AuthenticateAs(
            userId,
            AppRoles.DepartmentManager,
            TestDataSeeder.UniqueEmail("manager-client"));
    }

    private Task<WorkflowStatus> GetWorkflowStatusAsync(int id)
    {
        return WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .WorkItems.AsNoTracking()
                .Where(workItem => workItem.Id == id)
                .Select(workItem => workItem.WorkflowStatus)
                .SingleAsync());
    }
}
