namespace FlowDesk.Services.Models
{
    public sealed class AccountRegistrationResult
    {
        public string Email { get; init; } = string.Empty;

        public bool EmailSent { get; init; }

        public IReadOnlyList<AccountRegistrationError> Errors { get; init; } =
            [];
    }

    public sealed record AccountRegistrationError(
        string Key,
        string Message);
}
