namespace Restaurant_Aqa_Framework.Core;

public enum CleanupDisposition { DeleteOwnGuests, RetainSubmitted, RetainClosed, RetainUncertainSubmission }

public static class GroupLifecycle
{
    public static CleanupDisposition Decide(bool submissionConfirmed, string status, bool submissionAttempted = false)
    {
        if (submissionConfirmed || status == "Submitted") return CleanupDisposition.RetainSubmitted;
        if (submissionAttempted) return CleanupDisposition.RetainUncertainSubmission;
        return status switch
        {
            "Open" => CleanupDisposition.DeleteOwnGuests,
            "Closed" => CleanupDisposition.RetainClosed,
            _ => throw new InvalidOperationException("Unknown group status: refusing cleanup."),
        };
    }
}
