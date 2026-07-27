namespace FlowDesk.Services.Models
{
    public sealed class EmailVerificationResult
    {
        public IReadOnlyList<EmailVerificationError> Errors { get; init; } =
            [];
    }

    public sealed class EmailVerificationResendResult
    {
        public string Email { get; init; } = string.Empty;
    }

    public sealed record EmailVerificationError(
        string Key,
        string Message);
}
