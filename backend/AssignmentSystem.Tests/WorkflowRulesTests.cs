using AssignmentSystem.Api.Models;
using AssignmentSystem.Api.Services;
using Xunit;

namespace AssignmentSystem.Tests;

public class WorkflowRulesTests
{
    private static Assignment Assignment(DateTime deadline, AssignmentStatus status = AssignmentStatus.Published, bool updates = true) => new()
    {
        Title = "Test", Description = "Test", DeadlineUtc = deadline, MaximumMarks = 20,
        Status = status, AllowUpdates = updates
    };

    [Fact]
    public void Submit_AfterDeadline_IsRejected() =>
        Assert.Throws<InvalidOperationException>(() => WorkflowRules.EnsureCanSubmit(Assignment(DateTime.UtcNow.AddMinutes(-1)), DateTime.UtcNow));

    [Fact]
    public void Submit_ToDraft_IsRejected() =>
        Assert.Throws<InvalidOperationException>(() => WorkflowRules.EnsureCanSubmit(Assignment(DateTime.UtcNow.AddDays(1), AssignmentStatus.Draft), DateTime.UtcNow));

    [Fact]
    public void Update_WhenDisabled_IsRejected() =>
        Assert.Throws<InvalidOperationException>(() => WorkflowRules.EnsureCanUpdate(Assignment(DateTime.UtcNow.AddDays(1), updates: false), DateTime.UtcNow, SubmissionStatus.Submitted));

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void Grade_OutsideRange_IsRejected(int marks) =>
        Assert.Throws<InvalidOperationException>(() => WorkflowRules.EnsureValidGrade(Assignment(DateTime.UtcNow), marks));

    [Fact]
    public void Grade_WithinRange_IsAccepted() =>
        WorkflowRules.EnsureValidGrade(Assignment(DateTime.UtcNow), 20);

    [Fact]
    public void Teacher_CannotManageAnotherTeachersAssignment() =>
        Assert.False(AuthorizationRules.CanTeacherManageAssignment(10, 11));

    [Fact]
    public void Student_CannotAccessAnotherCourse() =>
        Assert.False(AuthorizationRules.CanStudentAccessCourse(2, 3));

    [Fact]
    public void DeadlineBoundary_IsInclusive() =>
        WorkflowRules.EnsureCanSubmit(Assignment(new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc)), new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void LateSubmission_IsAllowedWhenConfigured()
    {
        var assignment = Assignment(DateTime.UtcNow.AddMinutes(-1));
        assignment.AllowLateSubmission = true;
        WorkflowRules.EnsureCanSubmit(assignment, DateTime.UtcNow);
    }

    [Fact]
    public void EmptySubmission_IsRejected() =>
        Assert.Throws<InvalidOperationException>(() => WorkflowRules.EnsureAnswerProvided("", null));

    [Fact]
    public void GradedSubmission_IsLockedUntilRevision() =>
        Assert.Throws<InvalidOperationException>(() => WorkflowRules.EnsureCanUpdate(Assignment(DateTime.UtcNow.AddDays(1)), DateTime.UtcNow, SubmissionStatus.Graded));

    [Fact]
    public void RevisionSubmission_CanBeUpdated() =>
        WorkflowRules.EnsureCanUpdate(Assignment(DateTime.UtcNow.AddDays(1)), DateTime.UtcNow, SubmissionStatus.NeedsRevision);
}
