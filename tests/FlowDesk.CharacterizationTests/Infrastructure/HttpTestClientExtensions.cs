using System.Net;
using System.Text.RegularExpressions;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public static class HttpTestClientExtensions
{
    private static readonly Regex AntiforgeryTokenPattern = new(
        "<input[^>]*name=\"__RequestVerificationToken\"" +
        "[^>]*value=\"([^\"]+)\"",
        RegexOptions.Compiled |
        RegexOptions.CultureInvariant |
        RegexOptions.IgnoreCase);

    public static HttpClient AuthenticateAs(
        this HttpClient client,
        int userId,
        string role,
        string email)
    {
        client.DefaultRequestHeaders.Remove(
            TestAuthenticationHandler.UserIdHeader);
        client.DefaultRequestHeaders.Remove(
            TestAuthenticationHandler.RoleHeader);
        client.DefaultRequestHeaders.Remove(
            TestAuthenticationHandler.EmailHeader);

        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.UserIdHeader,
            userId.ToString());
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.RoleHeader,
            role);
        client.DefaultRequestHeaders.Add(
            TestAuthenticationHandler.EmailHeader,
            email);

        return client;
    }

    public static async Task<HttpResponseMessage> PostFormWithAntiforgeryAsync(
        this HttpClient client,
        string formPath,
        string postPath,
        IReadOnlyDictionary<string, string> formValues)
    {
        HttpResponseMessage getResponse = await client.GetAsync(formPath);
        getResponse.EnsureSuccessStatusCode();

        string html = await getResponse.Content.ReadAsStringAsync();
        Match match = AntiforgeryTokenPattern.Match(html);

        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"Antiforgery token bulunamadı: {formPath}");
        }

        Dictionary<string, string> values =
            new(formValues, StringComparer.Ordinal)
            {
                ["__RequestVerificationToken"] =
                    WebUtility.HtmlDecode(match.Groups[1].Value)
            };

        return await client.PostAsync(
            postPath,
            new FormUrlEncodedContent(values));
    }
}