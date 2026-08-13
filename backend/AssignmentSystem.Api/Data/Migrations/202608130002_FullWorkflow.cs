using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace AssignmentSystem.Api.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202608130002_FullWorkflow")]
public partial class FullWorkflow : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;
        ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE "Courses" ADD COLUMN IF NOT EXISTS "IsActive" boolean NOT NULL DEFAULT TRUE;
        ALTER TABLE "Subjects" ADD COLUMN IF NOT EXISTS "CourseId" integer NULL;
        ALTER TABLE "Assignments" ADD COLUMN IF NOT EXISTS "AllowLateSubmission" boolean NOT NULL DEFAULT FALSE;
        ALTER TABLE "Assignments" ADD COLUMN IF NOT EXISTS "IsArchived" boolean NOT NULL DEFAULT FALSE;
        ALTER TABLE "Assignments" ADD COLUMN IF NOT EXISTS "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE "Assignments" ADD COLUMN IF NOT EXISTS "UpdatedAtUtc" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
        ALTER TABLE "Submissions" ADD COLUMN IF NOT EXISTS "FileUrl" text NULL;
        ALTER TABLE "Submissions" ADD COLUMN IF NOT EXISTS "VersionNumber" integer NOT NULL DEFAULT 1;
        ALTER TABLE "Submissions" ADD COLUMN IF NOT EXISTS "IsLate" boolean NOT NULL DEFAULT FALSE;
        ALTER TABLE "Submissions" ADD COLUMN IF NOT EXISTS "GradedAtUtc" timestamp with time zone NULL;
        ALTER TABLE "Submissions" ADD COLUMN IF NOT EXISTS "GradedById" integer NULL;
        UPDATE "Subjects" s SET "CourseId" = tc."CourseId"
          FROM "TeacherCourses" tc WHERE tc."SubjectId" = s."Id" AND s."CourseId" IS NULL;
        CREATE INDEX IF NOT EXISTS "IX_Subjects_CourseId" ON "Subjects" ("CourseId");
        CREATE INDEX IF NOT EXISTS "IX_Assignments_CourseId_Status" ON "Assignments" ("CourseId", "Status");
        CREATE INDEX IF NOT EXISTS "IX_Submissions_AssignmentId" ON "Submissions" ("AssignmentId");
        DO $$ BEGIN
          ALTER TABLE "Subjects" ADD CONSTRAINT "FK_Subjects_Courses_CourseId"
            FOREIGN KEY ("CourseId") REFERENCES "Courses" ("Id") ON DELETE SET NULL;
        EXCEPTION WHEN duplicate_object THEN NULL; END $$;
        DO $$ BEGIN
          ALTER TABLE "Submissions" ADD CONSTRAINT "FK_Submissions_Users_GradedById"
            FOREIGN KEY ("GradedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT;
        EXCEPTION WHEN duplicate_object THEN NULL; END $$;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) { }
}
