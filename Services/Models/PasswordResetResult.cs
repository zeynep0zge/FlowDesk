namespace FlowDesk.Services.Models
{
    public sealed class PasswordResetRequestResult
    {
        public string Email { get; init; } = string.Empty;
    }

    public sealed class PasswordResetCodeResult
    {
        public string Email { get; init; } = string.Empty;

        public string ResetSessionToken { get; init; } = string.Empty;

        public IReadOnlyList<PasswordResetError> Errors { get; init; } =
            [];
    }

    public sealed class PasswordResetCompletionResult
    {
        public IReadOnlyList<PasswordResetError> Errors { get; init; } =
            [];
    }

    public sealed record PasswordResetError(
        string Key,
        string Message);
}
