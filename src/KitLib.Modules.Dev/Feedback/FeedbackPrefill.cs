namespace KitLib.Feedback;

internal readonly record struct FeedbackPrefill(
    string Title,
    string Description,
    /// <summary>Raw crash report captured at the time the prompt was shown, or null.</summary>
    CrashReport? CrashReport = null);
