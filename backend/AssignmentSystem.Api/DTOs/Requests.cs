using System.ComponentModel.DataAnnotations;
using AssignmentSystem.Api.Models;

namespace AssignmentSystem.Api.DTOs;

public record LoginRequest([Required, EmailAddress] string Email, [Required] string Password);

public record UserRequest(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    UserRole Role,
    int? CourseId,
    bool IsActive = true);

public record UserUpdateRequest(
    [Required, StringLength(100)] string Name,
    [Required, EmailAddress] string Email,
    [MinLength(8)] string? Password,
    UserRole Role,
    int? CourseId,
    bool IsActive = true);

public record CatalogRequest([Required, StringLength(100)] string Name);
public record SubjectRequest([Required, StringLength(100)] string Name, int CourseId);
public record TeacherAssignmentRequest(int TeacherId, int CourseId, int SubjectId);
public record EnrollmentRequest(int StudentId, int CourseId);

public record AssignmentRequest(
    [Required, StringLength(200)] string Title,
    [Required, StringLength(10000)] string Description,
    DateTime DeadlineUtc,
    [Range(1, 1000)] int MaximumMarks,
    int CourseId,
    int SubjectId,
    AssignmentStatus Status,
    bool AllowUpdates = true,
    bool AllowLateSubmission = false);

public record SubmissionRequest(
    [StringLength(10000)] string? Answer,
    [Url, StringLength(500)] string? FileUrl);

public record GradeRequest(
    [Range(0, 1000)] int Marks,
    [StringLength(4000)] string? Feedback);

public record SubmissionStatusRequest(SubmissionStatus Status);

public record AssignmentSubmissionResponse(int Id, string Answer, string? FileUrl, int VersionNumber, bool IsLate, string Status, int? Marks, string? Feedback, DateTime UpdatedAtUtc);
public record AssignmentResponse(int Id, string Title, string Description, DateTime DeadlineUtc, int MaximumMarks, string Status, bool AllowUpdates, bool AllowLateSubmission, bool IsArchived, int TeacherId, int CourseId, int SubjectId, string Course, string Subject, string Teacher, AssignmentSubmissionResponse? Submission);
public record SubmissionResponse(int Id, int AssignmentId, string Assignment, int MaximumMarks, int StudentId, string Student, string Answer, string? FileUrl, int VersionNumber, bool IsLate, DateTime SubmittedAtUtc, DateTime UpdatedAtUtc, int? Marks, string? Feedback, string Status, DateTime? GradedAtUtc, string? GradedBy);
