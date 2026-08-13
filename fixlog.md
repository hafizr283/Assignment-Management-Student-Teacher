# Fix log

## Current expansion status (2026-08-13)

- Completed: upgraded the simple implementation to the requested full workflow while preserving existing data and integer IDs.
- Backend: active users, course-linked subjects, enrollment, teacher assignment, late submissions, archive/publish, versioning, authenticated file upload, grading audit, revision reopening, ownership checks, and DTO responses.
- Frontend: late/file submission, version/status display, teacher publish/archive/deadline/revision actions, and Admin enrollment/teacher-assignment/deactivation actions.
- Verified: backend build has 0 warnings, 13/13 tests pass, Next.js production build passes, migration `202608130002_FullWorkflow` applied, and the all-role smoke test passed.
- Resume: API `5080`, frontend `3000`, portable PostgreSQL data `.postgres-data`; use README commands after restart.

| Issue / possible hang | Fix / status |
| --- | --- |
| Machine had no .NET SDK and default C: drive was low on space | Downloaded a local .NET 8 SDK to `E:\job_project\asp.net\.dotnet-sdk`; run commands with the E: environment variables shown in README. |
| Docker was unavailable | Kept PostgreSQL setup native and documented Docker as optional. |
| Submission could be updated after deadline or when disabled | Centralized `WorkflowRules.EnsureCanUpdate` and applied it in the submission endpoint. |
| Marks could exceed maximum or be negative | `EnsureValidGrade` rejects invalid marks; covered by xUnit tests. |
| Students could submit to another course | API checks the student's course against the assignment course. |
| Teacher could grade another teacher's submission | Backend ownership check returns 403. |
| Startup could appear to hang waiting on database | Added short connection/command timeouts so it fails quickly if PostgreSQL is unavailable; start PostgreSQL before `dotnet run`. |
| No real secrets committed | Development-only placeholders are in `appsettings.json`; deployment values should come from environment variables. |
| Initial migration did not compile because table constraints were placed in the column lambda | Replaced it with a deterministic PostgreSQL SQL migration applied by EF Core. |
| Tests could not find xUnit attributes and EF package versions differed | Added the explicit xUnit namespace and aligned Npgsql/EF packages to 8.0.8. |
| Next.js type check could not find `prop-types` declarations | Added the explicit `@types/prop-types` development dependency. |
| Frontend sends enum names but ASP.NET defaults to numeric enums | Enabled `JsonStringEnumConverter` for role, assignment status, and submission status payloads. |
| PostgreSQL was not installed and C: had no room | Downloaded portable PostgreSQL 16 into `.postgres` on E:, initialized `.postgres-data`, migrated, seeded, and smoke-tested login. |
| Student submission response caused an object-cycle serialization error (`Assignment.Submissions.Assignment...`) | Replaced the EF entity response with a flat submission DTO containing only submission fields and IDs. |
| Expanded specification required late/revision/version/archive workflows | Added migration `202608130002_FullWorkflow`, new domain fields/statuses, API endpoints, and UI controls. |
| Next.js build waited indefinitely | Existing dev server owned `.next`; stopped only its Node processes, built successfully, then restarted it. |
| Graded students could resubmit | Graded/reviewed submissions are locked until the teacher sets `NeedsRevision`. |
| File requirement was metadata-only | Added authenticated multipart upload with type allow-list and 10 MB server limit, served from `/uploads`. |
