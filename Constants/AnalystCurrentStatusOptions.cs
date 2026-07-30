namespace FlowDesk.Constants;

public static class AnalystCurrentStatusOptions
{
    public const int MaximumLength = 100;

    public const string UnderAnalystReview = "Analist İncelemesinde";
    public const string WaitingForDevelopment = "Geliştirme Bekliyor";
    public const string DevelopmentInProgress = "Geliştirme Devam Ediyor";
    public const string WaitingForTest = "Test Bekliyor";
    public const string ReadyForRelease = "Sürüme Hazır";

    public static IReadOnlyList<string> All { get; } =
    [
        UnderAnalystReview,
        WaitingForDevelopment,
        DevelopmentInProgress,
        WaitingForTest,
        ReadyForRelease
    ];

    public static bool Contains(string value)
    {
        return All.Contains(value, StringComparer.Ordinal);
    }
}