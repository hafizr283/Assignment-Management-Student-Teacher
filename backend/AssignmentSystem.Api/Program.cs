using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AssignmentSystem.Api.Data;
using AssignmentSystem.Api.DTOs;
using AssignmentSystem.Api.Models;
using AssignmentSystem.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args });
var platformPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(platformPort)) builder.WebHost.UseUrls($"http://0.0.0.0:{platformPort}");
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
var databaseConnection = GetDatabaseConnection(builder.Configuration);
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(databaseConnection));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"], ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), ClockSkew = TimeSpan.FromMinutes(1)
    };
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var idValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idValue, out var userId))
            {
                SetAuthenticationFailure(context.HttpContext, "INVALID_SESSION", "Your session is invalid. Sign in again.");
                context.Fail("The token does not identify a valid user.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId);
            if (user is null || !user.IsActive)
            {
                SetAuthenticationFailure(context.HttpContext, "ACCOUNT_INACTIVE", "This account is inactive. Contact an administrator.");
                context.Fail("The account is inactive.");
                return;
            }

            if (!context.Principal!.IsInRole(user.Role.ToString()))
            {
                SetAuthenticationFailure(context.HttpContext, "SESSION_CHANGED", "Your account permissions changed. Sign in again.");
                context.Fail("The account role has changed.");
            }
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            var expired = context.AuthenticateFailure is SecurityTokenExpiredException;
            var code = context.HttpContext.Items["AuthenticationErrorCode"] as string ?? (expired ? "SESSION_EXPIRED" : "AUTHENTICATION_REQUIRED");
            var message = context.HttpContext.Items["AuthenticationErrorMessage"] as string ?? (expired ? "Your session has expired. Sign in again." : "Sign in to continue.");
            await context.Response.WriteAsJsonAsync(new { data = (object?)null, error = new { code, message } });
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { data = (object?)null, error = new { code = "FORBIDDEN", message = "You do not have permission to perform this action." } });
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<RequestValidationFilter>();
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = UploadRules.MaximumFileBytes + 1024 * 1024);
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});
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
var uploadStagingPath = Path.Combine(app.Environment.ContentRootPath, "upload-staging");
Directory.CreateDirectory(uploadsPath);
Directory.CreateDirectory(uploadStagingPath);
app.UseForwardedHeaders();
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var workflowError = exception is WorkflowValidationException;
    var invalidRequest = exception is BadHttpRequestException or JsonException or InvalidDataException;
    context.Response.StatusCode = workflowError || invalidRequest ? 400 : 500;
    var code = workflowError && exception!.Message.Contains("deadline", StringComparison.OrdinalIgnoreCase)
        ? "SUBMISSION_CLOSED"
        : workflowError ? "WORKFLOW_VALIDATION_FAILED" : invalidRequest ? "INVALID_REQUEST" : "REQUEST_FAILED";
    var message = workflowError ? exception!.Message : invalidRequest ? "The request body or uploaded form is invalid." : "Unexpected server error. Please try again.";
    await context.Response.WriteAsJsonAsync(new { data = (object?)null, error = new { code, message } });
}));
app.UseSwagger(); app.UseSwaggerUI(); app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.XContentTypeOptions = "nosniff";
        context.Context.Response.Headers.CacheControl = "private, max-age=3600";
        var extension = Path.GetExtension(context.File.Name);
        if (!UploadRules.CanDisplayInline(extension))
            context.Context.Response.Headers.ContentDisposition = $"attachment; filename=\"attachment{extension.ToLowerInvariant()}\"";
    }
}); app.UseCors("frontend"); app.UseAuthentication(); app.UseAuthorization();
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/auth/login", async (LoginRequest request, AppDbContext db, IConfiguration config) =>
{
    var normalizedEmail = request.Email.Trim().ToLowerInvariant();
    var user = await db.Users.SingleOrDefaultAsync(x => x.Email == normalizedEmail);
    if (user is null || !user.IsActive || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        return Results.Json(new { data = (object?)null, error = new { code = "INVALID_CREDENTIALS", message = "Email or password is incorrect." } }, statusCode: StatusCodes.Status401Unauthorized);
    var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Name), new Claim(ClaimTypes.Email, user.Email), new Claim(ClaimTypes.Role, user.Role.ToString()) };
    var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"], claims, expires: DateTime.UtcNow.AddMinutes(60), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), user = new { user.Id, user.Name, user.Email, role = user.Role.ToString() } });
}).AddEndpointFilter<RequestValidationFilter>();

app.MapPost("/api/uploads", async (IFormFile file, HttpRequest request) =>
{
    if (!UploadRules.TryValidateMetadata(file.FileName, file.Length, out var extension, out var error))
        return Results.BadRequest(new { error });

    await using var source = file.OpenReadStream();
    if (!UploadRules.HasValidContent(extension, source))
        return Results.BadRequest(new { error = "File content does not match the selected file type or the file is damaged." });

    var safeName = $"{Guid.NewGuid():N}{extension}";
    var stagingFile = Path.Combine(uploadStagingPath, $"{Guid.NewGuid():N}.tmp");
    try
    {
        await using (var destination = new FileStream(stagingFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await source.CopyToAsync(destination, request.HttpContext.RequestAborted);
        File.Move(stagingFile, Path.Combine(uploadsPath, safeName));
    }
    finally
    {
        if (File.Exists(stagingFile)) File.Delete(stagingFile);
    }
    return Results.Ok(new { fileUrl = $"{request.Scheme}://{request.Host}/uploads/{safeName}" });
}).DisableAntiforgery().RequireAuthorization(p => p.RequireRole("Student"));

var admin = app.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Admin")).AddEndpointFilter<RequestValidationFilter>();
admin.MapGet("/users", async (AppDbContext db) => await db.Users.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new { x.Id, x.Name, x.Email, role = x.Role.ToString(), x.CourseId, x.IsActive }).ToListAsync());
admin.MapPost("/users", async (UserRequest r, AppDbContext db) =>
{
    var normalizedEmail = r.Email.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(x => x.Email == normalizedEmail)) return Results.Conflict(new { error = "Email already exists." });
    var courseValidation = await ValidateUserCourse(r.Role, r.CourseId, db); if (courseValidation is not null) return courseValidation;
    var user = new User { Name = r.Name.Trim(), Email = normalizedEmail, PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password), Role = r.Role, CourseId = r.Role == UserRole.Student ? r.CourseId : null, IsActive = r.IsActive };
    db.Add(user); await db.SaveChangesAsync(); return Results.Created($"/api/admin/users/{user.Id}", new { user.Id });
});
admin.MapPut("/users/{id:int}", async (int id, UserUpdateRequest r, ClaimsPrincipal principal, AppDbContext db) =>
{
    var user = await db.Users.FindAsync(id); if (user is null) return Results.NotFound();
    if (id == UserId(principal) && (!r.IsActive || r.Role != UserRole.Admin)) return Results.BadRequest(new { error = "You cannot deactivate or remove your own administrator access." });
    var normalizedEmail = r.Email.Trim().ToLowerInvariant();
    if (await db.Users.AnyAsync(x => x.Id != id && x.Email == normalizedEmail)) return Results.Conflict(new { error = "Email already exists." });
    var courseValidation = await ValidateUserCourse(r.Role, r.CourseId, db); if (courseValidation is not null) return courseValidation;
    user.Name = r.Name.Trim(); user.Email = normalizedEmail; user.Role = r.Role; user.CourseId = r.Role == UserRole.Student ? r.CourseId : null; user.IsActive = r.IsActive;
    if (!string.IsNullOrWhiteSpace(r.Password)) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(r.Password);
    await db.SaveChangesAsync(); return Results.NoContent();
});
admin.MapPatch("/users/{id:int}/deactivate", async (int id, ClaimsPrincipal principal, AppDbContext db) => { if (id == UserId(principal)) return Results.BadRequest(new { error = "Cannot deactivate your own account." }); var u = await db.Users.FindAsync(id); if (u is null) return Results.NotFound(); u.IsActive = false; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapDelete("/users/{id:int}", async (int id, ClaimsPrincipal principal, AppDbContext db) => { if (id == UserId(principal)) return Results.BadRequest(new { error = "Cannot deactivate your own account." }); var u = await db.Users.FindAsync(id); if (u is null) return Results.NotFound(); u.IsActive = false; await db.SaveChangesAsync(); return Results.NoContent(); });

admin.MapGet("/courses", async (AppDbContext db) => await db.Courses.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync());
admin.MapPost("/courses", async (CatalogRequest r, AppDbContext db) => { var x = new Course { Name = r.Name }; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/admin/courses/{x.Id}", x); });
admin.MapPut("/courses/{id:int}", async (int id, CatalogRequest r, AppDbContext db) => { var x = await db.Courses.FindAsync(id); if (x is null) return Results.NotFound(); x.Name = r.Name; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapDelete("/courses/{id:int}", async (int id, AppDbContext db) => { var x = await db.Courses.FindAsync(id); if (x is null) return Results.NotFound(); x.IsActive = false; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapGet("/subjects", async (AppDbContext db) => await db.Subjects.AsNoTracking().OrderBy(x => x.Name).ThenBy(x => x.Id).Select(x => new { x.Id, x.Name, x.CourseId, course = x.Course != null ? x.Course.Name : null }).ToListAsync());
admin.MapPost("/subjects", async (SubjectRequest r, AppDbContext db) => { if (!await db.Courses.AnyAsync(x => x.Id == r.CourseId && x.IsActive)) return Results.BadRequest(new { error = "Invalid course." }); var x = new Subject { Name = r.Name, CourseId = r.CourseId }; db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/admin/subjects/{x.Id}", x); });
admin.MapPut("/subjects/{id:int}", async (int id, SubjectRequest r, AppDbContext db) => { var x = await db.Subjects.FindAsync(id); if (x is null) return Results.NotFound(); if (!await db.Courses.AnyAsync(c => c.Id == r.CourseId && c.IsActive)) return Results.BadRequest(new { error = "Invalid course." }); if (await db.Assignments.AnyAsync(a => a.SubjectId == id && a.CourseId != r.CourseId)) return Results.Conflict(new { error = "This subject is used by assignments in its current course." }); x.Name = r.Name; x.CourseId = r.CourseId; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapDelete("/subjects/{id:int}", async (int id, AppDbContext db) => { if (await db.Assignments.AnyAsync(x => x.SubjectId == id)) return Results.Conflict(new { error = "Subject is used by assignments." }); var x = await db.Subjects.FindAsync(id); if (x is null) return Results.NotFound(); db.Remove(x); await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapPost("/teacher-assignments", async (TeacherAssignmentRequest r, AppDbContext db) => { if (!await db.Users.AnyAsync(x => x.Id == r.TeacherId && x.Role == UserRole.Teacher && x.IsActive)) return Results.BadRequest(new { error = "Invalid teacher." }); if (!await db.Courses.AnyAsync(x => x.Id == r.CourseId && x.IsActive)) return Results.BadRequest(new { error = "Invalid course." }); if (!await db.Subjects.AnyAsync(x => x.Id == r.SubjectId && x.CourseId == r.CourseId)) return Results.BadRequest(new { error = "Subject does not belong to course." }); if (!await db.TeacherCourses.AnyAsync(x => x.TeacherId == r.TeacherId && x.CourseId == r.CourseId && x.SubjectId == r.SubjectId)) { db.Add(new TeacherCourse { TeacherId = r.TeacherId, CourseId = r.CourseId, SubjectId = r.SubjectId }); await db.SaveChangesAsync(); } return Results.NoContent(); });
admin.MapPost("/enrollments", async (EnrollmentRequest r, AppDbContext db) => { var student = await db.Users.FindAsync(r.StudentId); if (student is null || student.Role != UserRole.Student || !student.IsActive) return Results.BadRequest(new { error = "Invalid or inactive student." }); if (!await db.Courses.AnyAsync(x => x.Id == r.CourseId && x.IsActive)) return Results.BadRequest(new { error = "Invalid course." }); student.CourseId = r.CourseId; await db.SaveChangesAsync(); return Results.NoContent(); });
admin.MapGet("/overview", async (AppDbContext db) => Results.Ok(new { users = await db.Users.CountAsync(), courses = await db.Courses.CountAsync(), subjects = await db.Subjects.CountAsync(), assignments = await db.Assignments.CountAsync(), submissions = await db.Submissions.CountAsync() }));
admin.MapGet("/assignments", async (AppDbContext db) => await AssignmentList(db.Assignments.AsNoTracking(), 0).ToListAsync());
admin.MapGet("/submissions", async (AppDbContext db) => await SubmissionList(db.Submissions.AsNoTracking()).ToListAsync());

var assignments = app.MapGroup("/api/assignments").RequireAuthorization().AddEndpointFilter<RequestValidationFilter>();
assignments.MapGet("/options", async (ClaimsPrincipal p, AppDbContext db) => { if (!p.IsInRole("Teacher")) return Results.Forbid(); var teacherId = UserId(p); return Results.Ok(await db.TeacherCourses.Where(x => x.TeacherId == teacherId && x.Course.IsActive && x.Subject.CourseId == x.CourseId).OrderBy(x => x.Course.Name).ThenBy(x => x.Subject.Name).ThenBy(x => x.CourseId).ThenBy(x => x.SubjectId).Select(x => new { x.CourseId, course = x.Course.Name, x.SubjectId, subject = x.Subject.Name }).ToListAsync()); });
assignments.MapGet("/", async (ClaimsPrincipal p, AppDbContext db) =>
{
    var id = UserId(p); var query = db.Assignments.AsNoTracking().Where(x => !x.IsArchived);
    query = p.IsInRole("Teacher") ? query.Where(x => x.TeacherId == id) : p.IsInRole("Student") ? query.Where(x => x.Status == AssignmentStatus.Published && x.Course.IsActive && x.Course.Students.Any(s => s.Id == id)) : query;
    return Results.Ok(await AssignmentList(query, id).ToListAsync());
});
assignments.MapGet("/{id:int}", async (int id, ClaimsPrincipal p, AppDbContext db) => { var currentUserId = UserId(p); var item = await AssignmentList(db.Assignments.AsNoTracking().Where(x => x.Id == id && !x.IsArchived), currentUserId).SingleOrDefaultAsync(); if (item is null) return Results.NotFound(); if (p.IsInRole("Teacher") && item.TeacherId != currentUserId) return Results.Forbid(); if (p.IsInRole("Student")) { if (item.Status != AssignmentStatus.Published.ToString()) return Results.NotFound(); var courseId = await db.Users.Where(x => x.Id == currentUserId && x.Course != null && x.Course.IsActive).Select(x => x.CourseId).SingleOrDefaultAsync(); if (item.CourseId != courseId) return Results.Forbid(); } return Results.Ok(item); });
assignments.MapPost("/", async (AssignmentRequest r, ClaimsPrincipal p, AppDbContext db) => { var teacherId = UserId(p); if (!await Teaches(db, teacherId, r.CourseId, r.SubjectId)) return Results.Forbid(); WorkflowRules.EnsureFutureDeadline(r.DeadlineUtc, DateTime.UtcNow); var x = MapAssignment(r, teacherId); db.Add(x); await db.SaveChangesAsync(); return Results.Created($"/api/assignments/{x.Id}", new { x.Id }); }).RequireAuthorization(p => p.RequireRole("Teacher"));
assignments.MapPut("/{id:int}", async (int id, AssignmentRequest r, ClaimsPrincipal p, AppDbContext db) => { var x = await db.Assignments.FindAsync(id); if (x is null) return Results.NotFound(); var teacherId = UserId(p); if (x.TeacherId != teacherId || !await Teaches(db, teacherId, r.CourseId, r.SubjectId)) return Results.Forbid(); var hasSubmissions = await db.Submissions.AnyAsync(s => s.AssignmentId == id); var highestAwardedMarks = await db.Submissions.Where(s => s.AssignmentId == id && s.Marks.HasValue).MaxAsync(s => (int?)s.Marks); WorkflowRules.EnsureValidAssignmentUpdate(x, r.DeadlineUtc, r.MaximumMarks, r.CourseId, r.SubjectId, hasSubmissions, highestAwardedMarks, DateTime.UtcNow); ApplyAssignment(x, r); await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Teacher"));
assignments.MapPatch("/{id:int}/publish", async (int id, ClaimsPrincipal p, AppDbContext db) => { var x = await db.Assignments.FindAsync(id); if (x is null) return Results.NotFound(); var teacherId = UserId(p); if (x.TeacherId != teacherId || !await Teaches(db, teacherId, x.CourseId, x.SubjectId)) return Results.Forbid(); WorkflowRules.EnsureFutureDeadline(x.DeadlineUtc, DateTime.UtcNow); x.Status = AssignmentStatus.Published; x.IsArchived = false; x.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Teacher"));
assignments.MapDelete("/{id:int}", async (int id, ClaimsPrincipal p, AppDbContext db) => { var x = await db.Assignments.FindAsync(id); if (x is null) return Results.NotFound(); if (x.TeacherId != UserId(p)) return Results.Forbid(); x.IsArchived = true; x.Status = AssignmentStatus.Draft; x.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Teacher"));

app.MapPost("/api/assignments/{assignmentId:int}/submission", Submit).RequireAuthorization(p => p.RequireRole("Student")).AddEndpointFilter<RequestValidationFilter>();
app.MapPost("/api/assignments/{assignmentId:int}/submissions", Submit).RequireAuthorization(p => p.RequireRole("Student")).AddEndpointFilter<RequestValidationFilter>();
app.MapGet("/api/submissions/me", async (ClaimsPrincipal p, int? assignmentId, AppDbContext db) => { var studentId = UserId(p); var q = db.Submissions.AsNoTracking().Where(x => x.StudentId == studentId); if (assignmentId.HasValue) q = q.Where(x => x.AssignmentId == assignmentId); return Results.Ok(await SubmissionList(q).ToListAsync()); }).RequireAuthorization(p => p.RequireRole("Student"));
app.MapGet("/api/submissions", async (ClaimsPrincipal p, AppDbContext db) => { var q = db.Submissions.AsNoTracking(); if (p.IsInRole("Teacher")) { var teacherId = UserId(p); q = q.Where(x => x.Assignment.TeacherId == teacherId); } return Results.Ok(await SubmissionList(q).ToListAsync()); }).RequireAuthorization(p => p.RequireRole("Teacher", "Admin"));
app.MapGet("/api/assignments/{assignmentId:int}/submissions", async (int assignmentId, ClaimsPrincipal p, AppDbContext db) => { var a = await db.Assignments.FindAsync(assignmentId); if (a is null) return Results.NotFound(); if (!p.IsInRole("Admin") && a.TeacherId != UserId(p)) return Results.Forbid(); return Results.Ok(await SubmissionList(db.Submissions.AsNoTracking().Where(x => x.AssignmentId == assignmentId)).ToListAsync()); }).RequireAuthorization(p => p.RequireRole("Teacher", "Admin"));
app.MapPost("/api/submissions/{id:int}/grade", Grade).RequireAuthorization(p => p.RequireRole("Teacher")).AddEndpointFilter<RequestValidationFilter>();
app.MapPut("/api/submissions/{id:int}/grade", Grade).RequireAuthorization(p => p.RequireRole("Teacher")).AddEndpointFilter<RequestValidationFilter>();
app.MapPatch("/api/submissions/{id:int}/status", async (int id, SubmissionStatusRequest r, ClaimsPrincipal p, AppDbContext db) => { var x = await db.Submissions.Include(s => s.Assignment).SingleOrDefaultAsync(s => s.Id == id); if (x is null) return Results.NotFound(); if (x.Assignment.TeacherId != UserId(p)) return Results.Forbid(); if (r.Status is not (SubmissionStatus.NeedsRevision or SubmissionStatus.Late or SubmissionStatus.Submitted)) return Results.BadRequest(new { error = "Unsupported status override." }); x.Status = r.Status; await db.SaveChangesAsync(); return Results.NoContent(); }).RequireAuthorization(p => p.RequireRole("Teacher")).AddEndpointFilter<RequestValidationFilter>();

static async Task<IResult> Submit(int assignmentId, SubmissionRequest r, ClaimsPrincipal p, AppDbContext db)
{
    WorkflowRules.EnsureAnswerProvided(r.Answer, r.FileUrl); var studentId = UserId(p); var assignment = await db.Assignments.FindAsync(assignmentId); if (assignment is null) return Results.NotFound();
    var courseId = await db.Users.Where(x => x.Id == studentId && x.Course != null && x.Course.IsActive).Select(x => x.CourseId).SingleOrDefaultAsync(); if (!AuthorizationRules.CanStudentAccessCourse(courseId, assignment.CourseId)) return Results.Forbid();
    var now = DateTime.UtcNow; var x = await db.Submissions.SingleOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentId == studentId);
    if (x is null) { WorkflowRules.EnsureCanSubmit(assignment, now); x = new Submission { AssignmentId = assignmentId, StudentId = studentId, Answer = r.Answer ?? string.Empty, FileUrl = r.FileUrl, SubmittedAtUtc = now, UpdatedAtUtc = now, IsLate = WorkflowRules.IsLate(assignment, now), Status = WorkflowRules.StatusForSubmission(assignment, now) }; db.Add(x); }
    else { WorkflowRules.EnsureCanUpdate(assignment, now, x.Status); x.Answer = r.Answer ?? string.Empty; x.FileUrl = r.FileUrl; x.UpdatedAtUtc = now; x.VersionNumber++; x.IsLate = WorkflowRules.IsLate(assignment, now); x.Status = WorkflowRules.StatusForSubmission(assignment, now); x.Marks = null; x.Feedback = null; x.GradedAtUtc = null; x.GradedById = null; }
    await db.SaveChangesAsync(); return Results.Ok(new { x.Id, x.AssignmentId, x.StudentId, x.Answer, x.FileUrl, x.VersionNumber, x.IsLate, status = x.Status.ToString(), x.SubmittedAtUtc, x.UpdatedAtUtc });
}
static async Task<IResult> Grade(int id, GradeRequest r, ClaimsPrincipal p, AppDbContext db) { var x = await db.Submissions.Include(s => s.Assignment).SingleOrDefaultAsync(s => s.Id == id); if (x is null) return Results.NotFound(); if (x.Assignment.TeacherId != UserId(p)) return Results.Forbid(); WorkflowRules.EnsureValidGrade(x.Assignment, r.Marks); x.Marks = r.Marks; x.Feedback = r.Feedback; x.Status = SubmissionStatus.Graded; x.GradedAtUtc = DateTime.UtcNow; x.GradedById = UserId(p); await db.SaveChangesAsync(); return Results.NoContent(); }
static IQueryable<AssignmentResponse> AssignmentList(IQueryable<Assignment> q, int currentUserId) => q.OrderByDescending(x => x.DeadlineUtc).ThenByDescending(x => x.Id).Select(x => new AssignmentResponse(x.Id, x.Title, x.Description, x.DeadlineUtc, x.MaximumMarks, x.Status.ToString(), x.AllowUpdates, x.AllowLateSubmission, x.IsArchived, x.TeacherId, x.CourseId, x.SubjectId, x.Course.Name, x.Subject.Name, x.Teacher.Name, x.Submissions.Where(s => s.StudentId == currentUserId).Select(s => new AssignmentSubmissionResponse(s.Id, s.Answer, s.FileUrl, s.VersionNumber, s.IsLate, s.Status.ToString(), s.Marks, s.Feedback, s.UpdatedAtUtc)).FirstOrDefault()));
static IQueryable<SubmissionResponse> SubmissionList(IQueryable<Submission> q) => q.OrderByDescending(x => x.UpdatedAtUtc).ThenByDescending(x => x.Id).Select(x => new SubmissionResponse(x.Id, x.AssignmentId, x.Assignment.Title, x.Assignment.MaximumMarks, x.StudentId, x.Student.Name, x.Answer, x.FileUrl, x.VersionNumber, x.IsLate, x.SubmittedAtUtc, x.UpdatedAtUtc, x.Marks, x.Feedback, x.Status.ToString(), x.GradedAtUtc, x.GradedBy != null ? x.GradedBy.Name : null));
static int UserId(ClaimsPrincipal p) => int.Parse(p.FindFirstValue(ClaimTypes.NameIdentifier)!);
static Task<bool> Teaches(AppDbContext db, int teacherId, int courseId, int subjectId) => db.TeacherCourses.AnyAsync(x => x.TeacherId == teacherId && x.Teacher.IsActive && x.CourseId == courseId && x.Course.IsActive && x.SubjectId == subjectId && x.Subject.CourseId == courseId);
static async Task<IResult?> ValidateUserCourse(UserRole role, int? courseId, AppDbContext db)
{
    if (role != UserRole.Student)
        return null;

    if (!courseId.HasValue)
        return Results.BadRequest(new { error = "Student course is required." });

    return await db.Courses.AnyAsync(x => x.Id == courseId.Value && x.IsActive)
        ? null
        : Results.BadRequest(new { error = "Student course must be active." });
}
static void SetAuthenticationFailure(HttpContext context, string code, string message)
{
    context.Items["AuthenticationErrorCode"] = code;
    context.Items["AuthenticationErrorMessage"] = message;
}
static string GetDatabaseConnection(IConfiguration config)
{
    var value = config.GetConnectionString("Default") ?? config["DATABASE_URL"];
    if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("A PostgreSQL connection string is required.");
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql")) return value;
    var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.None);
    var builder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = uri.Host,
        Port = uri.Port > 0 ? uri.Port : 5432,
        Database = uri.AbsolutePath.Trim('/'),
        Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : null,
        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        SslMode = Npgsql.SslMode.Require
    };
    return builder.ConnectionString;
}
static Assignment MapAssignment(AssignmentRequest r, int teacherId) => new() { Title = r.Title, Description = r.Description, DeadlineUtc = r.DeadlineUtc.ToUniversalTime(), MaximumMarks = r.MaximumMarks, CourseId = r.CourseId, SubjectId = r.SubjectId, Status = r.Status, AllowUpdates = r.AllowUpdates, AllowLateSubmission = r.AllowLateSubmission, TeacherId = teacherId };
static void ApplyAssignment(Assignment x, AssignmentRequest r) { x.Title = r.Title; x.Description = r.Description; x.DeadlineUtc = r.DeadlineUtc.ToUniversalTime(); x.MaximumMarks = r.MaximumMarks; x.CourseId = r.CourseId; x.SubjectId = r.SubjectId; x.Status = r.Status; x.AllowUpdates = r.AllowUpdates; x.AllowLateSubmission = r.AllowLateSubmission; x.UpdatedAtUtc = DateTime.UtcNow; }

await using (var scope = app.Services.CreateAsyncScope()) await SeedData.InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
app.Run();
public partial class Program;
