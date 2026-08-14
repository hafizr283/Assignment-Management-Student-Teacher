using System.ComponentModel.DataAnnotations;
using AssignmentSystem.Api.Models;

namespace AssignmentSystem.Api.DTOs;

public record LoginRequest(
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StringLength(128)] string Password);

public record UserRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StringLength(128, MinimumLength = 8)] string Password,
    [property: EnumDataType(typeof(UserRole))] UserRole Role,
    [property: Range(1, int.MaxValue)]
    int? CourseId,
    bool IsActive = true);

public record UserUpdateRequest(
    [property: Required, StringLength(100)] string Name,
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: StringLength(128, MinimumLength = 8)] string? Password,
    [property: EnumDataType(typeof(UserRole))] UserRole Role,
    [property: Range(1, int.MaxValue)]
    int? CourseId,
    bool IsActive = true);

public record CatalogRequest([property: Required, StringLength(100)] string Name);
public record SubjectRequest([property: Required, StringLength(100)] string Name, [property: Range(1, int.MaxValue)] int CourseId);
public record TeacherAssignmentRequest([property: Range(1, int.MaxValue)] int TeacherId, [property: Range(1, int.MaxValue)] int CourseId, [property: Range(1, int.MaxValue)] int SubjectId);
public record EnrollmentRequest([property: Range(1, int.MaxValue)] int StudentId, [property: Range(1, int.MaxValue)] int CourseId);

public record AssignmentRequest(
    [property: Required, StringLength(200)] string Title,
    [property: Required, StringLength(10000)] string Description,
    DateTime DeadlineUtc,
    [property: Range(1, 1000)] int MaximumMarks,
    [property: Range(1, int.MaxValue)] int CourseId,
    [property: Range(1, int.MaxValue)] int SubjectId,
    [property: EnumDataType(typeof(AssignmentStatus))] AssignmentStatus Status,
    bool AllowUpdates = true,
    bool AllowLateSubmission = false);

public record SubmissionRequest(
    [property: StringLength(10000)] string? Answer,
    [property: HttpUrl, StringLength(500)] string? FileUrl);

public record GradeRequest(
    [property: Range(0, 1000)] int Marks,
    [property: StringLength(4000)] string? Feedback);

public record SubmissionStatusRequest([property: EnumDataType(typeof(SubmissionStatus))] SubmissionStatus Status);

public sealed class HttpUrlAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is null || value is string { Length: 0 })
            return ValidationResult.Success;

        return value is string text &&
               Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? ValidationResult.Success
            : new ValidationResult("File URL must start with http:// or https://.", [validationContext.MemberName!]);
    }
}

public record AssignmentSubmissionResponse(int Id, string Answer, string? FileUrl, int VersionNumber, bool IsLate, string Status, int? Marks, string? Feedback, DateTime UpdatedAtUtc);
public record AssignmentResponse(int Id, string Title, string Description, DateTime DeadlineUtc, int MaximumMarks, string Status, bool AllowUpdates, bool AllowLateSubmission, bool IsArchived, int TeacherId, int CourseId, int SubjectId, string Course, string Subject, string Teacher, AssignmentSubmissionResponse? Submission);
public record SubmissionResponse(int Id, int AssignmentId, string Assignment, int MaximumMarks, int StudentId, string Student, string Answer, string? FileUrl, int VersionNumber, bool IsLate, DateTime SubmittedAtUtc, DateTime UpdatedAtUtc, int? Marks, string? Feedback, string Status, DateTime? GradedAtUtc, string? GradedBy);
