using AssignmentSystem.Api.Models;

namespace AssignmentSystem.Api.Services;

public static class WorkflowRules
{
    public static bool IsLate(Assignment assignment, DateTime nowUtc) => nowUtc > assignment.DeadlineUtc;

    public static void EnsureAnswerProvided(string? answer, string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(answer) && string.IsNullOrWhiteSpace(fileUrl))
            throw new InvalidOperationException("Provide an answer or file URL.");
    }

    public static void EnsureCanSubmit(Assignment assignment, DateTime nowUtc)
    {
        if (assignment.Status != AssignmentStatus.Published)
            throw new InvalidOperationException("Only published assignments accept submissions.");
        if (assignment.IsArchived)
            throw new InvalidOperationException("This assignment is archived.");
        if (IsLate(assignment, nowUtc) && !assignment.AllowLateSubmission)
            throw new InvalidOperationException("The assignment deadline has passed.");
    }

    public static void EnsureCanUpdate(Assignment assignment, DateTime nowUtc, SubmissionStatus currentStatus)
    {
        if (assignment.Status != AssignmentStatus.Published || assignment.IsArchived)
            throw new InvalidOperationException("This assignment is not accepting submissions.");
        var revisionWasRequested = currentStatus is SubmissionStatus.NeedsRevision or SubmissionStatus.Returned;
        if (!revisionWasRequested && IsLate(assignment, nowUtc) && !assignment.AllowLateSubmission)
            throw new InvalidOperationException("The assignment deadline has passed.");
        if (!assignment.AllowUpdates)
            throw new InvalidOperationException("Submission updates are disabled for this assignment.");
        if (currentStatus is SubmissionStatus.Graded or SubmissionStatus.Reviewed)
            throw new InvalidOperationException("Graded submissions must be reopened before editing.");
    }

    public static void EnsureValidGrade(Assignment assignment, int marks)
    {
        if (marks < 0 || marks > assignment.MaximumMarks)
            throw new InvalidOperationException($"Marks must be between 0 and {assignment.MaximumMarks}.");
    }

    public static SubmissionStatus StatusForSubmission(Assignment assignment, DateTime nowUtc) =>
        IsLate(assignment, nowUtc) ? SubmissionStatus.Late : SubmissionStatus.Submitted;
}
