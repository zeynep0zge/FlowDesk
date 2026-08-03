using System.Net;
using FlowDesk.CharacterizationTests.Infrastructure;
using FlowDesk.Constants;

namespace FlowDesk.CharacterizationTests.Account;

public sealed class AccountRateLimitingTests : DatabaseTestBase
{
    [Theory]
    [InlineData("Register")]
    [InlineData("ForgotPassword")]
    [InlineData("ResendEmailVerificationCode")]
    public async Task ProtectedAnonymousPost_OverLimit_Returns429(
        string action)
    {
        using HttpClient client = CreateClient();
        (string formPath, string postPath, Dictionary<string, string> form) =
            RequestFor(action);

        var responses = new List<HttpResponseMessage>();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            responses.Add(await client.PostFormWithAntiforgeryAsync(
                formPath,
                postPath,
                form));
        }

        Assert.All(responses.Take(5), response => Assert.NotEqual(
            HttpStatusCode.TooManyRequests,
            response.StatusCode));
        Assert.Equal(
            HttpStatusCode.TooManyRequests,
            responses[5].StatusCode);
        string body = await responses[5].Content.ReadAsStringAsync();
        Assert.Contains("Çok fazla istek", body, StringComparison.Ordinal);

        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task LoginPost_IsNotCoveredByAccountRateLimitPolicies()
    {
        using HttpClient client = CreateClient();
        Dictionary<string, string> form = new()
        {
            ["Email"] = TestDataSeeder.UniqueEmail("rate-login"),
            ["Password"] = "WrongPassword123!"
        };

        for (int attempt = 0; attempt < 3; attempt++)
        {
            HttpResponseMessage response =
                await client.PostFormWithAntiforgeryAsync(
                    "/Account/Login",
                    "/Account/Login",
                    form);
            Assert.NotEqual(
                HttpStatusCode.TooManyRequests,
                response.StatusCode);
        }
    }

    private static (
        string FormPath,
        string PostPath,
        Dictionary<string, string> Form) RequestFor(string action)
    {
        string email = TestDataSeeder.UniqueEmail($"rate-{action}");
        return action switch
        {
            "Register" => (
                "/Account/Register",
                "/Account/Register",
                AccountTestHelper.ValidRegistration(
                    email,
                    AppRoles.Employee)),
            "ForgotPassword" => (
                "/Account/ForgotPassword",
                "/Account/ForgotPassword",
                new Dictionary<string, string> { ["Email"] = email }),
            _ => (
                $"/Account/VerifyEmailCode?email={Uri.EscapeDataString(email)}",
                "/Account/ResendEmailVerificationCode",
                new Dictionary<string, string> { ["email"] = email })
        };
    }
}
