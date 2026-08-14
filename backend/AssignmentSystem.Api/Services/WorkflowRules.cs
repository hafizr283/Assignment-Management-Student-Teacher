using AssignmentSystem.Api.Models;

namespace AssignmentSystem.Api.Services;

public static class WorkflowRules
{
    public static bool IsLate(Assignment assignment, DateTime nowUtc) => nowUtc > assignment.DeadlineUtc;

    public static void EnsureAnswerProvided(string? answer, string? fileUrl)
    {
        if (string.IsNullOrWhiteSpace(answer) && string.IsNullOrWhiteSpace(fileUrl))
            throw new WorkflowValidationException("Provide an answer or file URL.");
    }

    public static void EnsureCanSubmit(Assignment assignment, DateTime nowUtc)
    {
        if (assignment.Status != AssignmentStatus.Published)
            throw new WorkflowValidationException("Only published assignments accept submissions.");
        if (assignment.IsArchived)
            throw new WorkflowValidationException("This assignment is archived.");
        if (IsLate(assignment, nowUtc) && !assignment.AllowLateSubmission)
            throw new WorkflowValidationException("The assignment deadline has passed.");
    }

    public static void EnsureFutureDeadline(DateTime deadlineUtc, DateTime nowUtc)
    {
        if (deadlineUtc.ToUniversalTime() <= nowUtc.ToUniversalTime())
            throw new WorkflowValidationException("Deadline must be in the future.");
    }

    public static void EnsureValidAssignmentUpdate(
        Assignment assignment,
        DateTime deadlineUtc,
        int maximumMarks,
        int courseId,
        int subjectId,
        bool hasSubmissions,
        int? highestAwardedMarks,
        DateTime nowUtc)
    {
        EnsureFutureDeadline(deadlineUtc, nowUtc);

        if (hasSubmissions && (assignment.CourseId != courseId || assignment.SubjectId != subjectId))
            throw new WorkflowValidationException("The course and subject cannot be changed after students have submitted work.");

        if (highestAwardedMarks.HasValue && maximumMarks < highestAwardedMarks.Value)
            throw new WorkflowValidationException($"Maximum marks cannot be lower than the existing grade of {highestAwardedMarks.Value}.");
    }

    public static void EnsureCanUpdate(Assignment assignment, DateTime nowUtc, SubmissionStatus currentStatus)
    {
        if (assignment.Status != AssignmentStatus.Published || assignment.IsArchived)
            throw new WorkflowValidationException("This assignment is not accepting submissions.");
        var revisionWasRequested = currentStatus is SubmissionStatus.NeedsRevision or SubmissionStatus.Returned;
        if (!revisionWasRequested && IsLate(assignment, nowUtc) && !assignment.AllowLateSubmission)
            throw new WorkflowValidationException("The assignment deadline has passed.");
        if (!assignment.AllowUpdates)
            throw new WorkflowValidationException("Submission updates are disabled for this assignment.");
        if (currentStatus is SubmissionStatus.Graded or SubmissionStatus.Reviewed)
            throw new WorkflowValidationException("Graded submissions must be reopened before editing.");
    }

    public static void EnsureValidGrade(Assignment assignment, int marks)
    {
        if (marks < 0 || marks > assignment.MaximumMarks)
            throw new WorkflowValidationException($"Marks must be between 0 and {assignment.MaximumMarks}.");
    }

    public static SubmissionStatus StatusForSubmission(Assignment assignment, DateTime nowUtc) =>
        IsLate(assignment, nowUtc) ? SubmissionStatus.Late : SubmissionStatus.Submitted;
}

public sealed class WorkflowValidationException(string message) : InvalidOperationException(message);
