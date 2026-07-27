namespace FlowDesk.Services.Models
{
    public sealed class AccountAuthenticationResult
    {
        public string? ControllerName { get; init; }

        public string? ActionName { get; init; }

        public string? ReturnUrl { get; init; }

        public string Email { get; init; } = string.Empty;

        public bool UseFreshLoginModel { get; init; }

        public IReadOnlyList<AccountAuthenticationError> Errors { get; init; } =
            [];
    }

    public sealed record AccountAuthenticationError(
        string Key,
        string Message);
}
