using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Common;
using FlowDesk.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace FlowDesk.CharacterizationTests.Authorization;

public sealed class ManagerScopeEnvironmentTests
{
    [Fact]
    public async Task Production_DepartmentManager_HasGlobalScopeForBothDomains()
    {
        using var factory = new FlowDeskWebApplicationFactory(
            Environments.Production);
        await factory.ResetDatabaseAsync();

        const int globalManagerId = 9200;
        await using (AsyncServiceScope scope =
                     factory.Services.CreateAsyncScope())
        {
            await TestDataSeeder.CreateUserAsync(
                scope.ServiceProvider,
                TestDataSeeder.UniqueEmail("production-global-manager"),
                assignedRole: AppRoles.DepartmentManager,
                department: DepartmentOptions.AllDepartments,
                userId: globalManagerId);
        }

        using HttpClient globalClient = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    BaseAddress = new Uri("https://localhost")
                })
            .AuthenticateAs(
                globalManagerId,
                AppRoles.DepartmentManager,
                TestDataSeeder.UniqueEmail("production-global-client"));
        globalClient.Timeout = TimeSpan.FromSeconds(15);

        HttpResponseMessage inbox = await globalClient.GetAsync(
            "/DepartmentManager/Inbox");
        HttpResponseMessage pendingUsers = await globalClient.GetAsync(
            "/DepartmentManager/PendingUsers");

        Assert.Equal(HttpStatusCode.OK, inbox.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pendingUsers.StatusCode);

        using HttpClient normalClient = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    BaseAddress = new Uri("https://localhost")
                })
            .AuthenticateAs(
                9001,
                AppRoles.DepartmentManager,
                TestDataSeeder.UniqueEmail("production-normal-client"));
        normalClient.Timeout = TimeSpan.FromSeconds(15);

        Assert.Equal(
            HttpStatusCode.OK,
            (await normalClient.GetAsync(
                "/DepartmentManager/Inbox")).StatusCode);
    }
}
