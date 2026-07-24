using System.Net.Http.Headers;
using Microsoft.AspNetCore.WebUtilities;

namespace FlowDesk.CharacterizationTests.Infrastructure;

public static class AccountTestHelper
{
    public static Dictionary<string, string> ValidRegistration(
        string email,
        string requestedRole)
    {
        return new Dictionary<string, string>
        {
            ["FullName"] = "Characterization User",
            ["Email"] = email,
            ["Department"] = TestDataSeeder.DefaultDepartment,
            ["RequestedRole"] = requestedRole,
            ["Password"] = TestDataSeeder.DefaultPassword,
            ["ConfirmPassword"] = TestDataSeeder.DefaultPassword
        };
    }

    public static string GetQueryValue(
        HttpResponseHeaders headers,
        string key)
    {
        Uri? location = headers.Location;

        if (location == null)
        {
            throw new InvalidOperationException(
                "Beklenen redirect konumu bulunamadı.");
        }

        string locationText = location.OriginalString;
        int queryStart = locationText.IndexOf('?');
        string queryText = queryStart >= 0
            ? locationText[queryStart..]
            : string.Empty;
        var query = QueryHelpers.ParseQuery(queryText);

        if (!query.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                "Redirect üzerinde beklenen değer bulunamadı.");
        }

        return value.ToString();
    }
}