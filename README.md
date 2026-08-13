# Assignment & Submission Management System

Full-stack implementation of the recruitment assignment. It supports Admin, Teacher, and Student roles with JWT authentication, PostgreSQL persistence, assignment publishing/archiving, late submissions, versioned resubmission, revision requests, grading, feedback, and file uploads.

## Stack

- Backend: ASP.NET Core 8 minimal API, EF Core, PostgreSQL/Npgsql, JWT, Swagger
- Frontend: Next.js 14, React, TypeScript, Zod validation, responsive CSS
- Tests: xUnit business-rule tests

## Main features

- Admin manages users, courses, subjects, student enrollment, and teacher course/subject assignments.
- Teachers create, edit, publish, archive, and extend assignments; accept late work; grade; and request revision.
- Students see only their course assignments, submit text and/or files, resubmit with version increments, and view status, marks, and feedback.
- BCrypt passwords, 60-minute JWTs, backend ownership checks, restricted CORS, Swagger, migrations, seed data, and consistent handled errors.

## Project structure

`backend/AssignmentSystem.Api` contains the API, entities, migrations, uploads, seed data, and workflow rules. `backend/AssignmentSystem.Tests` covers deadline, late submission, revision, validation, authorization, and grading rules. `frontend` contains the responsive Next.js client.

## Run locally (all files stay on E:)

1. Install PostgreSQL and create a database named `assignment_system` (or edit the connection string in `backend/AssignmentSystem.Api/appsettings.json`). Docker is optional.
2. In PowerShell from this folder:

```powershell
$env:TEMP = "$PWD\.tmp"; $env:TMP = "$PWD\.tmp"; $env:NUGET_PACKAGES = "$PWD\.packages\nuget"
$env:Path = "$PWD\.dotnet-sdk;$env:Path"
dotnet restore AssignmentSystem.sln
dotnet test AssignmentSystem.sln
dotnet run --project backend/AssignmentSystem.Api --launch-profile http
```

The API and Swagger are at http://localhost:5080/swagger.

3. In a second PowerShell window:

```powershell
Set-Location frontend
$env:npm_config_cache = "$PWD\..\.cache\npm"
npm install
npm run dev
```

The frontend is at http://localhost:3000. Copy `.env.example` to `.env.local` if the API URL differs.

The API applies the included migration and seeds demo data on startup. No manual table creation is needed. `database/create-database.sql` is an optional one-command database creation script.

For this workspace, a portable PostgreSQL 16 instance is already installed at `.postgres`, stores data at `.postgres-data`, and runs from E: on port 5432. Start it again with:

```powershell
& "$PWD\.postgres\pgsql\bin\pg_ctl.exe" -D "$PWD\.postgres-data" -l "$PWD\.tmp\postgres-server.log" -o '-p 5432' start
```

## Demo credentials

| Role | Email | Password |
| --- | --- | --- |
| Admin | admin@example.com | Admin123! |
| Teacher | teacher@example.com | Teacher123! |
| Student | student@example.com | Student123! |

## Assumptions and limitations

- A student belongs to one course; a teacher is assigned to course/subject pairs.
- File uploads accept PDF, DOCX, ZIP, JPG/JPEG, and PNG up to 10 MB and are stored locally for this demo.
- The development JWT key and database password are placeholders and must be replaced for deployment.
- The API uses UTC timestamps and rejects late work unless the assignment enables it.
- Integer IDs are retained for compatibility with the initial database; this does not affect ownership/security rules.
- Notifications, plagiarism detection, antivirus scanning, refresh tokens, pagination, and live deployment remain out of scope.
