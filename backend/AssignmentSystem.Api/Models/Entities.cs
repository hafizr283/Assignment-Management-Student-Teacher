namespace AssignmentSystem.Api.Models;

public enum UserRole { Admin, Teacher, Student }
public enum AssignmentStatus { Draft, Published }
public enum SubmissionStatus
{
    Submitted = 0,
    Reviewed = 1, // legacy value retained for existing rows; API treats it as graded
    Returned = 2, // legacy value retained for existing rows; API treats it as needs revision
    Late = 3,
    NeedsRevision = 4,
    Graded = 5
}

public class User
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int? CourseId { get; set; }
    public Course? Course { get; set; }
}

public class Course
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<User> Students { get; set; } = [];
    public ICollection<TeacherCourse> TeacherCourses { get; set; } = [];
}

public class Subject
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int? CourseId { get; set; }
    public Course? Course { get; set; }
    public ICollection<TeacherCourse> TeacherCourses { get; set; } = [];
}

public class TeacherCourse
{
    public int TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
}

public class Assignment
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string Description { get; set; }
    public DateTime DeadlineUtc { get; set; }
    public int MaximumMarks { get; set; }
    public AssignmentStatus Status { get; set; }
    public bool AllowUpdates { get; set; } = true;
    public bool AllowLateSubmission { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public int TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public int CourseId { get; set; }
    public Course Course { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public ICollection<Submission> Submissions { get; set; } = [];
}

public class Submission
{
    public int Id { get; set; }
    public required string Answer { get; set; }
    public string? FileUrl { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public int VersionNumber { get; set; } = 1;
    public bool IsLate { get; set; }
    public int? Marks { get; set; }
    public string? Feedback { get; set; }
    public DateTime? GradedAtUtc { get; set; }
    public int? GradedById { get; set; }
    public User? GradedBy { get; set; }
    public SubmissionStatus Status { get; set; } = SubmissionStatus.Submitted;
    public int AssignmentId { get; set; }
    public Assignment Assignment { get; set; } = null!;
    public int StudentId { get; set; }
    public User Student { get; set; } = null!;
}
