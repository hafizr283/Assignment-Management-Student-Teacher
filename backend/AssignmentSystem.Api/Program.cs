using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using AssignmentSystem.Api.Data;
using AssignmentSystem.Api.DTOs;
using AssignmentSystem.Api.Models;
using AssignmentSystem.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
    ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.FromMinutes(1)
});
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "Assignment System API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
});
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p.WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
Directory.CreateDirectory(uploadsPath);
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    context.Response.StatusCode = exception is InvalidOperationException ? 400 : 500;
    var code = exception?.Message.Contains("deadline", StringComparison.OrdinalIgnoreCase) == true ? "SUBMISSION_CLOSED" : "REQUEST_FAILED";
    await context.Response.WriteAsJsonAsync(new { data = (object?)null, error = new { code, message = exception?.Message ?? "Unexpected server error." } });
}));
app.UseSwagger(); app.UseSwaggerUI(); app.UseStaticFiles(new StaticFileOptions { FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath), RequestPath = "/uploads" }); app.UseCors("frontend"); app.UseAuthentication(); app.UseAuthorization();

app.MapPost("/api/auth/login", async (LoginRequest request, AppDbContext db, IConfiguration config) =>
{
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == request.Email.ToLower());
    if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return Results.Unauthorized();
    var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role.ToString()) };
    var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims, expires: DateTime.UtcNow.AddMinutes(60), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), user = new { user.Id, user.Name, user.Email, role = user.Role.ToString() } });
});

app.MapPost("/api/uploads", async (IFormFile file, HttpRequest request) =>
{
    var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".zip", ".jpg", ".jpeg", ".png" };
    var extension = Path.GetExtension(file.FileName);
    if (!allowed.Contains(extension)) return Results.BadRequest(new { error = "Allowed file types: pdf, docx, zip, jpg, png." });
    if (file.Length == 0 || file.Length > 10 * 1024 * 1024) return Results.BadRequest(new { error = "File must be between 1 byte and 10 MB." });
    var safeName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    await using var stream = File.Create(Path.Combine(uploadsPath, safeName)); await file.CopyToAsync(stream);
    return Results.Ok(new { fileUrl = $"{request.Scheme}://{request.Host}/uploads/{safeName}" });
}).DisableAntiforgery().RequireAuthorization(p => p.RequireRole("Student"));

var admin = app.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Admin"));
admin.MapGet("/users", async (AppDbContext db) => await db.Users.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Email, role = x.Role.ToString(), x.CourseId, x.IsActive }).ToListAsync());
admin.MapPost("/users", async (UserRequest r, AppDbContext db) =>
{
    if (await db.Users.AnyAsync(x => x.Email == r.Email.ToLower())) return Results.Conflict(new { error = "Email already exists." });
    if (r.Role == UserRole.Student && r.CourseId is null) return Results.BadRequest(new { error = "Student course is required." });
    var user = new User { Name = r.Name, Email = r.Email.ToLower(), PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password), Role = r.Role, CourseId = r.CourseId, IsActive = r.IsActive };
    db.Add(user); await db.SaveChangesAsync(); return Results.Created($"/api/admin/users/{user.Id}", new { user.Id });
});
admin.MapPut("/users/{id:int}", async (int id, UserUpdateRequest r, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id); if (user is null) return Results.NotFound();
    if (await db.Users.AnyAsync(x => x.Id != id && x.Email == r.Email.ToLower())) return Results.Conflict(new { error = "Email already exists." });
    user.Name = r.Name; user.Email = r.Email.ToLower(); user.Role = r.Role; user.CourseId = r.CourseId; user.IsActive = r.IsActive;
    if (!string.IsNullOrWhiteSpace(r.Password)) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password);
    await db.SaveChangesAsync(); return Results.NoContent();
});
admin.MapPatch("/users/{id:int}/deactivate", async (int id, ClaimsPrincipal principal, AppDbContext db) => { if (id == UserId(principal)) return Results.BadRequest(new { error = "Cannot deactivate your own account." }); var u = await db.Users.FindAsync(id); if (u is null) return Results.NotFound(); u.IsActive = false; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapDelete("/users/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db) => { if (id == UserId(principal)) return Results.BadRequest(new { error = "Cannot deactivate your own account." }); var u = await db.Users.FindAsync(id); if (u is null) return Results.NotFound(); u.IsActive = false; await db.SaveChangesAsync(); return Results.NoContent(); });

admin.MapGet("/courses", async (AppDbContext db) => await db.Courses.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync());
admin.MapPost("/courses", async (CatalogRequest r, AppDbContext db) => { var x = new Course { Name = r.Name }; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/admin/courses/{x.Id}", x); });
admin.MapPut("/courses/{id:int}", async (int id, CatalogRequest r, AppDbContext db) => { var x = await db.Courses.FindAsync(id); if (x is null) return Results.NotFound(); x.Name = r.Name; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapDelete("/courses/{id:int}", async (int id, AppDbContext db) => { var x = await db.Courses.FindAsync(id); if (x is null) return Results.NotFound(); x.IsActive = false; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapGet("/subjects", async (AppDbContext db) => await db.Subjects.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, x.Name, x.CourseId, course = x.Course != null ? x.Course.Name : null }).ToListAsync());
admin.MapPost("/subjects", async (SubjectRequest r, AppDbContext db) => { if (!await db.Courses.AnyAsync(x => x.Id == r.CourseId && x.IsActive)) return Results.BadRequest(new { error = "Invalid course." }); var x = new Subject { Name = r.Name, CourseId = r.CourseId }; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/admin/subjects/{x.Id}", x); });
admin.MapPut("/subjects/{id:int}", async (int id, SubjectRequest r, AppDbContext db) => { var x = await db.Subjects.FindAsync(id); if (x is null) return Results.NotFound(); x.Name = r.Name; x.CourseId = r.CourseId; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapDelete("/subjects/{id:int}", async (int id, AppDbContext db) => { if (await db.Assignments.AnyAsync(x => x.SubjectId == id)) return Results.Conflict(new { error = "Subject is used by assignments." }); var x = await db.Subjects.FindAsync(id); if (x is null) return Results.NotFound(); db.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapPost("/teacher-assignments", async (TeacherAssignmentRequest r, AppDbContext db) => { if (!await db.Users.AnyAsync(x => x.Id == r.TeacherId && x.Role == UserRole.Teacher && x.IsActive)) return Results.BadRequest(new { error = "Invalid teacher." }); if (!await db.Subjects.AnyAsync(x => x.Id == r.SubjectId && x.CourseId == r.CourseId)) return Results.BadRequest(new { error = "Subject does not belong to course." }); if (!await db.TeacherCourses.AnyAsync(x => x.TeacherId == r.TeacherId && x.CourseId == r.CourseId && x.SubjectId == r.SubjectId)) { db.Add(new TeacherCourse { TeacherId = r.TeacherId, CourseId = r.CourseId, SubjectId = r.SubjectId }); await db.SaveChangesAsync(); } return Results.NoContent(); });
admin.MapPost("/enrollments", async (EnrollmentRequest r, AppDbContext db) => { var student = await db.Users.FindAsync(r.StudentId); if (student is null || student.Role != UserRole.Student) return Results.BadRequest(new { error = "Invalid student." }); if (!await db.Courses.AnyAsync(x => x.Id == r.CourseId && x.IsActive)) return Results.BadRequest(new { error = "Invalid course." }); student.CourseId = r.CourseId; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapGet("/overview", async (AppDbContext db) => Results.Ok(new { users = await db.Users.CountAsync(), courses = await db.Courses.CountAsync(), subjects = await db.Subjects.CountAsync(), assignments = await db.Assignments.CountAsync(), submissions = await db.Submissions.CountAsync() }));
admin.MapGet("/assignments", async (AppDbContext db) => await AssignmentList(db.Assignments.AsNoTracking(), 0).ToListAsync());
admin.MapGet("/submissions", async (AppDbContext db) => await SubmissionList(db.Submissions.AsNoTracking()).ToListAsync());

var assignments = app.MapGroup("/api/assignments").RequireAuthorization();
assignments.MapGet("/options", async (ClaimsPrincipal p, AppDbContext db) => { if (!p.IsInRole("Teacher")) return Results.Forbid(); var teacherId = UserId(p); return Results.Ok(await db.TeacherCourses.Where(x => x.TeacherId == teacherId).Select(x => new { x.CourseId, course = x.Course.Name, x.SubjectId, subject = x.Subject.Name }).ToListAsync()); });
assignments.MapGet("/", async (ClaimsPrincipal p, AppDbContext db) =>
{
    var id = UserId(p); var query = db.Assignments.AsNoTracking().Where(x => !x.IsArchived);
    query = p.IsInRole("Teacher") ? query.Where(x => x.TeacherId == id) : p.IsInRole("Student") ? query.Where(x => x.Status == AssignmentStatus.Published && x.Course.Students.Any(s => s.Id == id)) : query;
    return Results.Ok(await AssignmentList(query, id).ToListAsync());
});
assignments.MapGet("/{id:int}", async (int id, ClaimsPrincipal p, AppDbContext db) => { var currentUserId = UserId(p); var item = await AssignmentList(db.Assignments.AsNoTracking().Where(x => x.Id == id && !x.IsArchived), currentUserId).SingleOrDefaultAsync(); if (item is null) return Results.NotFound(); if (p.IsInRole("Teacher") && item.TeacherId != currentUserId) return Results.Forbid(); if (p.IsInRole("Student") && item.CourseId != await db.Users.Where(x => x.Id == currentUserId).Select(x => x.CourseId).SingleAsync()) return Results.Forbid(); return Results.Ok(item); });
assignments.MapPost("/", async (AssignmentRequest r, ClaimsPrincipal p, AppDbContext db) => { var teacherId = UserId(p); if (!await Teaches(db, teacherId, r.CourseId, r.SubjectId)) return Results.Forbid(); if (r.DeadlineUtc <= DateTime.UtcNow) return Results.BadRequest(new { error = "Deadline must be in the future." }); var x = MapAssignment(r, teacherId); db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/assignments/{x.Id}", new { x.Id }); }).RequireAuthorization(p => p.RequireRole("Teacher"));
assignments.MapPut("/{id:int}", async (int id, AssignmentRequest r, ClaimsPrincipal p, AppDbContext db) => { var x = await db.Assignments.FindAsync(id); if (x is null) return Results.NotFound(); if (x.TeacherId != UserId(p) || !await Teaches(db, UserId(p), r.CourseId, r.SubjectId)) return Results.Forbid(); ApplyAssignment(x, r); await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Teacher"));
assignments.MapPatch("/{id:int}/publish", async (int id, ClaimsPrincipal p, AppDbContext db) => { var x = await db.Assignments.FindAsync(id); if (x is null) return Results.NotFound(); if (x.TeacherId != UserId(p)) return Results.Forbid(); x.Status = AssignmentStatus.Published; x.IsArchived = false; x.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Teacher"));
assignments.MapDelete("/{id:int}", async (int id, ClaimsPrincipal p, AppDbContext db) => { var x = await db.Assignments.FindAsync(id); if (x is null) return Results.NotFound(); if (x.TeacherId != UserId(p)) return Results.Forbid(); x.IsArchived = true; x.Status = AssignmentStatus.Draft; x.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Teacher"));

app.MapPost("/api/assignments/{assignmentId:int}/submission", Submit).RequireAuthorization(p => p.RequireRole("Student"));
app.MapPost("/api/assignments/{assignmentId:int}/submissions", Submit).RequireAuthorization(p => p.RequireRole("Student"));
app.MapGet("/api/submissions/me", async (ClaimsPrincipal p, int? assignmentId, AppDbContext db) => { var studentId = UserId(p); var q = db.Submissions.AsNoTracking().Where(x => x.StudentId == studentId); if (assignmentId.HasValue) q = q.Where(x => x.AssignmentId == assignmentId); return Results.Ok(await SubmissionList(q).ToListAsync()); }).RequireAuthorization(p => p.RequireRole("Student"));
app.MapGet("/api/submissions", async (ClaimsPrincipal p, AppDbContext db) => { var q = db.Submissions.AsNoTracking(); if (p.IsInRole("Teacher")) { var teacherId = UserId(p); q = q.Where(x => x.Assignment.TeacherId == teacherId); } return Results.Ok(await SubmissionList(q).ToListAsync()); }).RequireAuthorization(p => p.RequireRole("Teacher", "Admin"));
app.MapGet("/api/assignments/{assignmentId:int}/submissions", async (int assignmentId, ClaimsPrincipal p, AppDbContext db) => { var a = await db.Assignments.FindAsync(assignmentId); if (a is null) return Results.NotFound(); if (!p.IsInRole("Admin") && a.TeacherId != UserId(p)) return Results.Forbid(); return Results.Ok(await SubmissionList(db.Submissions.AsNoTracking().Where(x => x.AssignmentId == assignmentId)).ToListAsync()); }).RequireAuthorization(p => p.RequireRole("Teacher", "Admin"));
app.MapPost("/api/submissions/{id:int}/grade", Grade).RequireAuthorization(p => p.RequireRole("Teacher"));
app.MapPut("/api/submissions/{id:int}/grade", Grade).RequireAuthorization(p => p.RequireRole("Teacher"));
app.MapPatch("/api/submissions/{id:int}/status", async (int id, SubmissionStatusRequest r, ClaimsPrincipal p, AppDbContext db) => { var x = await db.Submissions.Include(s => s.Assignment).SingleOrDefaultAsync(s => s.Id == id); if (x is null) return Results.NotFound(); if (x.Assignment.TeacherId != UserId(p)) return Results.Forbid(); if (r.Status is not (SubmissionStatus.NeedsRevision or SubmissionStatus.Late or SubmissionStatus.Submitted)) return Results.BadRequest(new { error = "Unsupported status override." }); x.Status = r.Status; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Teacher"));

static async Task<IResult> Submit(int assignmentId, SubmissionRequest r, ClaimsPrincipal p, AppDbContext db)
{
    WorkflowRules.EnsureAnswerProvided(r.Answer, r.FileUrl); var studentId = UserId(p); var assignment = await db.Assignments.FindAsync(assignmentId); if (assignment is null) return Results.NotFound();
    var courseId = await db.Users.Where(x => x.Id == studentId).Select(x => x.CourseId).SingleOrDefaultAsync(); if (!AuthorizationRules.CanStudentAccessCourse(courseId, assignment.CourseId)) return Results.Forbid();
    var now = DateTime.UtcNow; var x = await db.Submissions.SingleOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);
    if (x is null) { WorkflowRules.EnsureCanSubmit(assignment, now); x = new Submission { AssignmentId = assignmentId, StudentId = studentId, Answer = r.Answer ?? string.Empty, FileUrl = r.FileUrl, SubmittedAtUtc = now, UpdatedAtUtc = now, IsLate = WorkflowRules.IsLate(assignment, now), Status = WorkflowRules.StatusForSubmission(assignment, now) }; db.Add(x); }
    else { WorkflowRules.EnsureCanUpdate(assignment, now, x.Status); x.Answer = r.Answer ?? string.Empty; x.FileUrl = r.FileUrl; x.UpdatedAtUtc = now; x.VersionNumber++; x.IsLate = WorkflowRules.IsLate(assignment, now); x.Status = WorkflowRules.StatusForSubmission(assignment, now); x.Marks = null; x.Feedback = null; x.GradedAtUtc = null; x.GradedById = null; }
    await db.SaveChangesAsync(); return Results.Ok(new { x.Id, x.AssignmentId, x.StudentId, x.Answer, x.FileUrl, x.VersionNumber, x.IsLate, status = x.Status.ToString(), x.SubmittedAtUtc, x.UpdatedAtUtc });
}
static async Task<IResult> Grade(int id, GradeRequest r, ClaimsPrincipal p, AppDbContext db) { var x = await db.Submissions.Include(s => s.Assignment).SingleOrDefaultAsync(s => s.Id == id); if (x is null) return Results.NotFound(); if (x.Assignment.TeacherId != UserId(p)) return Results.Forbid(); WorkflowRules.EnsureValidGrade(x.Assignment, r.Marks); x.Marks = r.Marks; x.Feedback = r.Feedback; x.Status = SubmissionStatus.Graded; x.GradedAtUtc = DateTime.UtcNow; x.GradedById = UserId(p); await db.SaveChangesAsync(); return Results.NoContent(); }
static IQueryable<AssignmentResponse> AssignmentList(IQueryable<Assignment> q, int currentUserId) => q.OrderByDescending(x => x.DeadlineUtc).Select(x => new AssignmentResponse(x.Id, x.Title, x.Description, x.DeadlineUtc, x.MaximumMarks, x.Status.ToString(), x.AllowUpdates, x.AllowLateSubmission, x.IsArchived, x.TeacherId, x.CourseId, x.SubjectId, x.Course.Name, x.Subject.Name, x.Teacher.Name, x.Submissions.Where(s => s.StudentId == currentUserId).Select(s => new AssignmentSubmissionResponse(s.Id, s.Answer, s.FileUrl, s.VersionNumber, s.IsLate, s.Status.ToString(), s.Marks, s.Feedback, s.UpdatedAtUtc)).FirstOrDefault()));
static IQueryable<SubmissionResponse> SubmissionList(IQueryable<Submission> q) => q.OrderByDescending(x => x.UpdatedAtUtc).Select(x => new SubmissionResponse(x.Id, x.AssignmentId, x.Assignment.Title, x.Assignment.MaximumMarks, x.StudentId, x.Student.Name, x.Answer, x.FileUrl, x.VersionNumber, x.IsLate, x.SubmittedAtUtc, x.UpdatedAtUtc, x.Marks, x.Feedback, x.Status.ToString(), x.GradedAtUtc, x.GradedBy != null ? x.GradedBy.Name : null));
static int UserId(ClaimsPrincipal p) => int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
static Task<bool> Teaches(AppDbContext db, int teacherId, int courseId, int subjectId) => db.TeacherCourses.AnyAsync(x => x.TeacherId == teacherId && x.CourseId == courseId && x.SubjectId == subjectId);
static Assignment MapAssignment(AssignmentRequest r, int teacherId) => new() { Title = r.Title, Description = r.Description, DeadlineUtc = r.DeadlineUtc.ToUniversalTime(), MaximumMarks = r.MaximumMarks, CourseId = r.CourseId, SubjectId = r.SubjectId, Status = r.Status, AllowUpdates = r.AllowUpdates, AllowLateSubmission = r.AllowLateSubmission, TeacherId = teacherId };
static void ApplyAssignment(Assignment x, AssignmentRequest r) { x.Title = r.Title; x.Description = r.Description; x.DeadlineUtc = r.DeadlineUtc.ToUniversalTime(); x.MaximumMarks = r.MaximumMarks; x.CourseId = r.CourseId; x.SubjectId = r.SubjectId; x.Status = r.Status; x.AllowUpdates = r.AllowUpdates; x.AllowLateSubmission = r.AllowLateSubmission; x.UpdatedAtUtc = DateTime.UtcNow; }

await using (var scope = app.Services.CreateAsyncScope()) await SeedData.InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
app.Run();
public partial class Program;
