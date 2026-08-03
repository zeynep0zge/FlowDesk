using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;
using FlowDesk.Data;
using FlowDesk.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FlowDesk.CharacterizationTests.Account;

public sealed class PasswordResetCharacterizationTests
    : DatabaseTestBase
{
    [Fact]
    public async Task ForgotPassword_KnownEmail_CreatesHashedRequestAndSendsEmail()
    {
        string email = TestDataSeeder.UniqueEmail("forgot");
        await SeedUserAsync(email);
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await RequestResetAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, Factory.Email.SendCount);
        string code = Factory.Email.GetLatestSixDigitCode(email);

        PasswordResetRequest request = await WithServicesAsync(
            async services =>
                await services.GetRequiredService<AppDbContext>()
                    .PasswordResetRequests.AsNoTracking()
                    .SingleAsync());

        Assert.NotEmpty(request.CodeHash);
        Assert.NotEqual(code, request.CodeHash);
        Assert.Null(request.CompletedAtUtc);
        Assert.False(request.IsInvalidated);
    }

    [Fact]
    public async Task ResetPassword_ValidFlow_ChangesPasswordAndCompletesRequest()
    {
        string email = TestDataSeeder.UniqueEmail("reset-valid");
        await SeedUserAsync(email);
        using HttpClient client = CreateClient();
        string token = await VerifyResetCodeAndGetSessionAsync(client, email);
        const string newPassword = "ChangedPass123!";

        HttpResponseMessage response = await ResetPasswordAsync(
            client,
            email,
            token,
            newPassword);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var result = await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<
                    UserManager<ApplicationUser>>();
            ApplicationUser user =
                (await userManager.FindByEmailAsync(email))!;
            bool passwordWorks =
                await userManager.CheckPasswordAsync(user, newPassword);
            PasswordResetRequest request =
                await services.GetRequiredService<AppDbContext>()
                    .PasswordResetRequests.AsNoTracking()
                    .SingleAsync();
            return new
            {
                passwordWorks,
                request.CompletedAtUtc,
                request.IsInvalidated,
                request.ResetSessionHash
            };
        });

        Assert.True(result.passwordWorks);
        Assert.NotNull(result.CompletedAtUtc);
        Assert.True(result.IsInvalidated);
        Assert.False(string.IsNullOrWhiteSpace(result.ResetSessionHash));
        Assert.NotEqual(token, result.ResetSessionHash);
    }

    [Fact]
    public async Task ResetPassword_UsedSession_CannotBeUsedAgain()
    {
        string email = TestDataSeeder.UniqueEmail("reset-reuse");
        await SeedUserAsync(email);
        using HttpClient client = CreateClient();
        string token = await VerifyResetCodeAndGetSessionAsync(client, email);
        const string firstPassword = "ChangedPass123!";
        const string secondPassword = "AnotherPass456!";
        await ResetPasswordAsync(client, email, token, firstPassword);

        HttpResponseMessage retry = await ResetPasswordAsync(
            client,
            email,
            token,
            secondPassword);

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);

        var state = await WithServicesAsync(async services =>
        {
            UserManager<ApplicationUser> userManager =
                services.GetRequiredService<
                    UserManager<ApplicationUser>>();
            ApplicationUser user =
                (await userManager.FindByEmailAsync(email))!;
            return new
            {
                firstWorks = await userManager.CheckPasswordAsync(
                    user,
                    firstPassword),
                secondWorks = await userManager.CheckPasswordAsync(
                    user,
                    secondPassword)
            };
        });

        Assert.True(state.firstWorks);
        Assert.False(state.secondWorks);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_ReturnsGenericRedirectWithoutCreatingRequest()
    {
        string email = TestDataSeeder.UniqueEmail("unknown");
        using HttpClient client = CreateClient();

        HttpResponseMessage response = await RequestResetAsync(client, email);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/Account/VerifyResetCode",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, Factory.Email.SendCount);

        int requestCount = await WithServicesAsync(async services =>
            await services.GetRequiredService<AppDbContext>()
                .PasswordResetRequests.CountAsync());

        Assert.Equal(0, requestCount);
    }

    [Fact]
    public async Task ResetPassword_CompletionSaveFailure_RollsBackPassword()
    {
        using var factory = new FlowDeskWebApplicationFactory(
            "Testing",
            new FailPasswordResetCompletionInterceptor());
        await factory.ResetDatabaseAsync();
        string email = TestDataSeeder.UniqueEmail("reset-rollback");

        await using (AsyncServiceScope scope =
                     factory.Services.CreateAsyncScope())
        {
            await TestDataSeeder.CreateUserAsync(
                scope.ServiceProvider,
                email,
                emailConfirmed: true,
                isApproved: true,
                assignedRole: AppRoles.ProjectManager);
        }

        using HttpClient client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost")
            });
        client.Timeout = TimeSpan.FromSeconds(15);
        await RequestResetAsync(client, email);
        string code = factory.Email.GetLatestSixDigitCode(email);
        HttpResponseMessage verify =
            await client.PostFormWithAntiforgeryAsync(
                $"/Account/VerifyResetCode?email={Uri.EscapeDataString(email)}",
                "/Account/VerifyResetCode",
                new Dictionary<string, string>
                {
                    ["Email"] = email,
                    ["Code"] = code
                });
        string token = AccountTestHelper.GetQueryValue(
            verify.Headers,
            "token");
        const string newPassword = "RollbackPass123!";

        HttpResponseMessage response = await ResetPasswordAsync(
            client,
            email,
            token,
            newPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using AsyncServiceScope verificationScope =
            factory.Services.CreateAsyncScope();
        UserManager<ApplicationUser> userManager = verificationScope
            .ServiceProvider.GetRequiredService<
                UserManager<ApplicationUser>>();
        ApplicationUser user =
            (await userManager.FindByEmailAsync(email))!;
        PasswordResetRequest request = await verificationScope
            .ServiceProvider.GetRequiredService<AppDbContext>()
            .PasswordResetRequests.AsNoTracking()
            .SingleAsync();

        Assert.True(await userManager.CheckPasswordAsync(
            user,
            TestDataSeeder.DefaultPassword));
        Assert.False(await userManager.CheckPasswordAsync(
            user,
            newPassword));
        Assert.Null(request.CompletedAtUtc);
        Assert.False(request.IsInvalidated);
    }

    private async Task SeedUserAsync(string email)
    {
        await WithServicesAsync(services =>
            TestDataSeeder.CreateUserAsync(
                services,
                email,
                emailConfirmed: true,
                isApproved: true,
                assignedRole: AppRoles.ProjectManager));
    }

    private async Task<string> VerifyResetCodeAndGetSessionAsync(
        HttpClient client,
        string email)
    {
        await RequestResetAsync(client, email);
        string code = Factory.Email.GetLatestSixDigitCode(email);
        string encodedEmail = Uri.EscapeDataString(email);

        HttpResponseMessage verifyResponse =
            await client.PostFormWithAntiforgeryAsync(
                $"/Account/VerifyResetCode?email={encodedEmail}",
                "/Account/VerifyResetCode",
                new Dictionary<string, string>
                {
                    ["Email"] = email,
                    ["Code"] = code
                });

        Assert.Equal(HttpStatusCode.Redirect, verifyResponse.StatusCode);
        return AccountTestHelper.GetQueryValue(
            verifyResponse.Headers,
            "token");
    }

    private static Task<HttpResponseMessage> RequestResetAsync(
        HttpClient client,
        string email)
    {
        return client.PostFormWithAntiforgeryAsync(
            "/Account/ForgotPassword",
            "/Account/ForgotPassword",
            new Dictionary<string, string>
            {
                ["Email"] = email
            });
    }

    private static Task<HttpResponseMessage> ResetPasswordAsync(
        HttpClient client,
        string email,
        string token,
        string newPassword)
    {
        string formPath =
            "/Account/ResetPassword?email=" +
            Uri.EscapeDataString(email) +
            "&token=" +
            Uri.EscapeDataString(token);

        return client.PostFormWithAntiforgeryAsync(
            formPath,
            "/Account/ResetPassword",
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["ResetSessionToken"] = token,
                ["NewPassword"] = newPassword,
                ["ConfirmPassword"] = newPassword
            });
    }
}
