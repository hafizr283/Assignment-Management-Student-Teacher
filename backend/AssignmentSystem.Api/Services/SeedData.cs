using AssignmentSystem.Api.Data;
using AssignmentSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Api.Services;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        db.Database.SetCommandTimeout(TimeSpan.FromSeconds(10));
        await db.Database.MigrateAsync();
        if (await db.Users.AnyAsync()) return;

        var course = new Course { Name = "Computer Science - Year 1" };
        var subject = new Subject { Name = "Programming Fundamentals", Course = course };
        var admin = User("System Admin", "admin@example.com", "Admin123!", UserRole.Admin);
        var teacher = User("Nadia Teacher", "teacher@example.com", "Teacher123!", UserRole.Teacher);
        var student = User("Rafi Student", "student@example.com", "Student123!", UserRole.Student);
        student.Course = course;

        db.AddRange(course, subject, admin, teacher, student);
        await db.SaveChangesAsync();
        db.TeacherCourses.Add(new TeacherCourse { TeacherId = teacher.Id, CourseId = course.Id, SubjectId = subject.Id });
        db.Assignments.Add(new Assignment
        {
            Title = "Build a Console Calculator",
            Description = "Create a C# calculator supporting add, subtract, multiply, and divide operations.",
            DeadlineUtc = DateTime.UtcNow.AddDays(7),
            MaximumMarks = 20,
            Status = AssignmentStatus.Published,
            AllowUpdates = true,
            AllowLateSubmission = true,
            TeacherId = teacher.Id,
            CourseId = course.Id,
            SubjectId = subject.Id
        });
        await db.SaveChangesAsync();
    }

    private static User User(string name, string email, string password, UserRole role) => new()
    {
        Name = name,
        Email = email,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        Role = role
    };
}
