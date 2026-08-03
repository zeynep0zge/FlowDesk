namespace FlowDesk.Common;

public static class AccountRateLimitPolicies
{
    public const string Register = "account-register";
    public const string ForgotPassword = "account-forgot-password";
    public const string ResendEmailVerification =
        "account-resend-email-verification";
}
