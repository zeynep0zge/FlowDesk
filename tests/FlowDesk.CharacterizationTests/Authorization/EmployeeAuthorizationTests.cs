using System.Net;
using System.Reflection;
using System.Text.Json;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Controllers;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.Authorization;

public sealed class EmployeeAuthorizationTests : DatabaseTestBase
{
    [Fact]
    public async Task Index_OnlyListsCurrentEmployeesAssignedWorkItems()
    {
        WorkItem own = await CreateWorkItemAsync(developerId: 22);
        WorkItem other = await CreateWorkItemAsync(developerId: 23);
        WorkItem unassigned = await CreateWorkItemAsync(developerId: null);
        using HttpClient client = EmployeeClient(22);

        HttpResponseMessage response =
            await client.GetAsync("/Employee/Index");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(own.RequestNumber, body);
        Assert.DoesNotContain(other.RequestNumber, body);
        Assert.DoesNotContain(unassigned.RequestNumber, body);
    }

    [Theory]
    [InlineData(WorkflowStatus.UnderAnalystReview)]
    [InlineData(WorkflowStatus.ReturnedToAnalyst)]
    [InlineData(WorkflowStatus.WaitingManagerApproval)]
    public async Task Index_NonApprovedAssignedWorkItem_IsNotVisible(
        WorkflowStatus status)
    {
        WorkItem workItem = await CreateWorkItemAsync(22, status);
        using HttpClient client = EmployeeClient(22);

        HttpResponseMessage response =
            await client.GetAsync("/Employee/Index");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(workItem.RequestNumber, body);
    }

    [Fact]
    public async Task Details_NonApprovedAssignedWorkItem_IsNotFound()
    {
        WorkItem workItem = await CreateWorkItemAsync(
            22,
            WorkflowStatus.WaitingManagerApproval);
        using HttpClient client = EmployeeClient(22);

        HttpResponseMessage response = await client.GetAsync(
            $"/Employee/Details/{workItem.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Details_CurrentEmployeesWorkItem_IsVisibleAndReadOnly()
    {
        WorkItem own = await CreateWorkItemAsync(developerId: 22);
        using HttpClient client = EmployeeClient(22);

        HttpResponseMessage response = await client.GetAsync(
            $"/Employee/Details/{own.Id}");
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(own.RequestNumber, body);
        Assert.DoesNotContain(
            "type=\"text\"",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "<textarea",
            body,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DeveloperId", body);
        Assert.DoesNotContain("AnalystId", body);
        Assert.DoesNotContain("chatbot", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Details_OtherEmployeesWorkItem_IsNotFound()
    {
        WorkItem other = await CreateWorkItemAsync(developerId: 23);
        using HttpClient client = EmployeeClient(22);

        HttpResponseMessage response = await client.GetAsync(
            $"/Employee/Details/{other.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task InvalidNameIdentifier_DoesNotReturnAssignedItems()
    {
        await CreateWorkItemAsync(developerId: 22);
        using HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserIdHeader,
            "0");
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeader,
            AppRoles.Employee);
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.EmailHeader,
            TestDataSeeder.UniqueEmail("invalid-employee-claim"));

        HttpResponseMessage response =
            await client.GetAsync("/Employee/Index");

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessEmployeeEndpoints()
    {
        using HttpClient client = CreateClient();

        HttpResponseMessage index =
            await client.GetAsync("/Employee/Index");
        HttpResponseMessage details =
            await client.GetAsync("/Employee/Details/1");

        Assert.NotEqual(HttpStatusCode.OK, index.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, details.StatusCode);
    }

    [Fact]
    public async Task WrongRole_CannotAccessEmployeeEndpoints()
    {
        using HttpClient client = CreateClient().AuthenticateAs(
            22,
            AppRoles.Analyst,
            TestDataSeeder.UniqueEmail("wrong-employee-role"));

        HttpResponseMessage index =
            await client.GetAsync("/Employee/Index");
        HttpResponseMessage details =
            await client.GetAsync("/Employee/Details/1");

        Assert.NotEqual(HttpStatusCode.OK, index.StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, details.StatusCode);
    }

    [Fact]
    public void EmployeeController_HasNoDataChangingPostActions()
    {
        MethodInfo[] actions = typeof(EmployeeController).GetMethods(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(actions, method =>
            method.GetCustomAttributes<HttpPostAttribute>().Any());
        Assert.DoesNotContain(actions, method =>
            method.Name is "Edit" or "StartWork" or "Complete");
    }

    [Fact]
    public async Task IndexAndDetails_DoNotChangeWorkItem()
    {
        WorkItem own = await CreateWorkItemAsync(developerId: 22);
        string before = await GetWorkItemJsonAsync(own.Id);
        using HttpClient client = EmployeeClient(22);

        HttpResponseMessage index =
            await client.GetAsync("/Employee/Index");
        HttpResponseMessage details = await client.GetAsync(
            $"/Employee/Details/{own.Id}");
        string after = await GetWorkItemJsonAsync(own.Id);

        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Equal(HttpStatusCode.OK, details.StatusCode);
        Assert.Equal(before, after);
    }

    private HttpClient EmployeeClient(int userId)
    {
        return CreateClient().AuthenticateAs(
            userId,
            AppRoles.Employee,
            TestDataSeeder.UniqueEmail($"employee-{userId}"));
    }

    private Task<WorkItem> CreateWorkItemAsync(
        int? developerId,
        WorkflowStatus status = WorkflowStatus.Approved)
    {
        return WithServicesAsync(services =>
            TestDataSeeder.CreateWorkItemAsync(
                services,
                status,
                developerId: developerId));
    }

    private Task<string> GetWorkItemJsonAsync(int id)
    {
        return WithServicesAsync(async services =>
        {
            WorkItem workItem = await services
                .GetRequiredService<AppDbContext>()
                .WorkItems.AsNoTracking()
                .SingleAsync(x => x.Id == id);

            return JsonSerializer.Serialize(workItem);
        });
    }
}
