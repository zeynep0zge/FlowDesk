using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FlowDesk.CharacterizationTests.Account;

public sealed class RegistrationCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task Register_ValidInput_CreatesUnconfirmedPendingUser()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("register");

        HttpResponseMessage response =
            await RegisterAsync(client, email, AppRoles.ProjectManager);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        ApplicationUser? user = await WithServicesAsync(async services =>
            await services.GetRequiredService<
                    UserManager<ApplicationUser>>()
                .FindByEmailAsync(email));

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(email, user.UserName);
        Assert.Equal("Characterization User", user.FullName);
        Assert.Equal(TestDataSeeder.DefaultDepartment, user.Department);
        Assert.Equal(AppRoles.ProjectManager, user.RequestedRole);
        Assert.False(user.EmailConfirmed);
        Assert.False(user.IsApproved);
    }

    [Fact]
    public async Task Register_ValidInput_DoesNotAssignRequestedIdentityRole()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("register-role");
        await RegisterAsync(client, email, AppRoles.Analyst);

        IList<string> roles = await WithServicesAsync(
            async services =>
            {
                UserManager<ApplicationUser> userManager =
                    services.GetRequiredService<
                        UserManager<ApplicationUser>>();
                ApplicationUser user =
                    (await userManager.FindByEmailAsync(email))!;
                return await userManager.GetRolesAsync(user);
            });

        Assert.Empty(roles);
    }

    [Fact]
    public async Task Register_ValidInput_CreatesVerificationRequestAndSendsEmail()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("register-email");
        await RegisterAsync(client, email, AppRoles.Employee);

        bool requestExists = await WithServicesAsync(async services =>
        {
            AppDbContext context =
                services.GetRequiredService<AppDbContext>();
            int userId = await context.Users
                .Where(x => x.Email == email)
                .Select(x => x.Id)
                .SingleAsync();
            return await context.EmailVerificationRequests
                .AnyAsync(x => x.UserId == userId);
        });

        Assert.True(requestExists);
        Assert.Equal(1, Factory.Email.SendCount);
        Assert.Equal(email, Factory.Email.Messages.Single().Recipient);
        Assert.Contains(
            "Doğrulama",
            Factory.Email.Messages.Single().Subject,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_InvalidModel_DoesNotCreateUser()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("register-invalid");
        Dictionary<string, string> form =
            AccountTestHelper.ValidRegistration(
                email,
                AppRoles.ProjectManager);
        form["FullName"] = "x";

        HttpResponseMessage response =
            await client.PostFormWithAntiforgeryAsync(
                "/Account/Register",
                "/Account/Register",
                form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        bool exists = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .Users.AnyAsync(x => x.Email == email));

        Assert.False(exists);
        Assert.Equal(0, Factory.Email.SendCount);
    }

    [Fact]
    public async Task Register_DepartmentManagerRequestedRole_IsRejected()
    {
        using HttpClient client = CreateClient();
        string email = TestDataSeeder.UniqueEmail("register-forbidden-role");

        HttpResponseMessage response =
            await RegisterAsync(
                client,
                email,
                AppRoles.DepartmentManager);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        bool exists = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .Users.AnyAsync(x => x.Email == email));

        Assert.False(exists);
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string requestedRole)
    {
        return client.PostFormWithAntiforgeryAsync(
            "/Account/Register",
            "/Account/Register",
            AccountTestHelper.ValidRegistration(email, requestedRole));
    }
}