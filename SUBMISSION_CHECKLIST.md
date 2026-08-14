# Submission Checklist and Next Steps

This document maps the recruitment assignment requirements to this project and gives the safest final submission procedure.

## Short answer

The project now has a dedicated GitHub repository rooted at this folder. Before submission, push the final verified changes and confirm the repository and live application are accessible from a signed-out browser.

## Requirement audit

| Requirement | Current status | Evidence / action |
| --- | --- | --- |
| GitHub/GitLab repository link | Ready, push final changes | Git root: `E:\job_project\asp.net`; remote: `https://github.com/hafizr283/Assignment-Management-Student-Teacher`. |
| Complete frontend code | Ready | `frontend/` contains the Next.js, React, TypeScript, validation, and API integration code. |
| Complete backend/API code | Ready | `backend/AssignmentSystem.Api/` contains the ASP.NET Core API, authentication, authorization, validation, Swagger, migrations, seed logic, and uploads. |
| PostgreSQL database files | Ready | `backend/AssignmentSystem.Api/Data/Migrations/`, `database/create-database.sql`, and `backend/AssignmentSystem.Api/Services/SeedData.cs` are included. |
| README | Ready | `README.md` documents features, structure, setup, credentials, assumptions, and limitations. |
| Demo credentials | Ready | Admin, Teacher, and Student credentials are listed in `README.md` and seeded by `SeedData.cs`. |
| Environment configuration | Ready | Frontend and backend examples are in `frontend/.env.example` and `backend/AssignmentSystem.Api/.env.example`. Do not commit real deployment secrets. |
| Easy local setup | Ready, verify once | Follow the exact commands in `README.md`; start PostgreSQL before starting the API. |
| JWT and role authorization | Ready | Implemented in `Program.cs` and endpoint authorization/ownership checks. |
| Unit tests | Re-run after final backend changes | Run `dotnet test AssignmentSystem.sln` with the .NET 8 SDK and confirm every test passes. |
| Frontend production build | Re-run after the dependency upgrade | Run `npm run build` with the committed Next.js version and confirm the main, not-found, and favicon routes are generated. |

## Repository status

Verified on 14 August 2026:

- Git root: `E:\job_project\asp.net`
- Branch: `main`
- Remote: `https://github.com/hafizr283/Assignment-Management-Student-Teacher.git`
- Live application: `https://assignment-management-student-teach.vercel.app/`

Inspect and push the final project changes from this folder:

```powershell
Set-Location E:\job_project\asp.net
git status --short
git diff --check
git add README.md SUBMISSION_CHECKLIST.md backend database frontend AssignmentSystem.sln render.yaml .gitignore
git status --short
git commit -m "Polish assignment management UX and documentation"
git push -u origin main
```

## Final local verification

### 1. Start PostgreSQL

From `E:\job_project\asp.net`:

```powershell
& "$PWD\.postgres\pgsql\bin\pg_ctl.exe" `
  -D "$PWD\.postgres-data" `
  -l "$PWD\.tmp\postgres-server.log" `
  -o '-p 5432' start
```

If PostgreSQL is already running, `pg_ctl` will report that fact; do not start a second instance.

### 2. Run backend tests

```powershell
$env:TEMP = "$PWD\.tmp"
$env:TMP = "$PWD\.tmp"
$env:NUGET_PACKAGES = "$PWD\.packages\nuget"
$env:Path = "$PWD\.dotnet-sdk;$env:Path"

dotnet restore AssignmentSystem.sln
dotnet test AssignmentSystem.sln
```

Expected result: all tests pass.

### 3. Start and inspect the API

```powershell
dotnet run --project backend/AssignmentSystem.Api --launch-profile http
```

Open:

- Swagger: `http://localhost:5080/swagger`
- API base URL: `http://localhost:5080/api`

The API applies migrations and seed data at startup. If login returns a transient database error, PostgreSQL is not running or is not listening on port 5432.

### 4. Start and build the frontend

In a second PowerShell window:

```powershell
Set-Location E:\job_project\asp.net\frontend
$env:npm_config_cache = "$PWD\..\.cache\npm"

npm install
npm run build
npm run dev
```

Open `http://localhost:3000`. Stop old `next dev`, `next start`, or `next build` processes before rebuilding if the build appears to hang or the page returns an unexpected 404.

### 5. Perform the role smoke test

Use the credentials in `README.md`:

1. Log in as Admin. Confirm users, courses, subjects, enrollment, and teacher assignment screens load.
2. Log in as Teacher. Create a draft assignment, publish it, inspect the deadline/options, and view submissions.
3. Log in as Student. Confirm only the student’s course assignments are visible. Submit text and/or an allowed file, then verify status and version.
4. As Teacher, grade the submission and add feedback. Set `NeedsRevision`, then confirm the student can update and resubmit.
5. Confirm a student cannot access another course and a teacher cannot grade another teacher’s assignment.

## What to include in the repository

Include:

- `backend/AssignmentSystem.Api/`
- `backend/AssignmentSystem.Tests/`
- `frontend/`
- `database/create-database.sql`
- `AssignmentSystem.sln`
- `README.md`
- `fixlog.md` and this checklist (useful documentation)
- `frontend/.env.example`
- EF Core migration files and seed data

Do not include generated or machine-local files. The existing `.gitignore` excludes the local SDK, PostgreSQL data, NuGet/npm caches, build output, uploads, `node_modules`, `.next`, and local environment files.

## Before sending the repository link

- Open the repository URL in a private/incognito browser window and confirm it is accessible.
- Confirm the default branch contains both frontend and backend.
- Confirm the README commands work from a fresh clone.
- Confirm no real passwords, API keys, tokens, database dumps containing secrets, or `.env.local` files are committed.
- Confirm the three demo credentials work after a fresh database startup.
- Copy the final repository URL into the recruitment submission form: `https://q-rp.com/c/4CIs`.

## Recommended final order

1. Stop duplicate local API/Next.js processes if present.
2. Start PostgreSQL and run the smoke test.
3. Run `dotnet test` and `npm run build`.
4. Commit and push the final changes to the existing project repository.
5. Test the pushed repository URL from a clean view.
6. Submit that URL through the provided form.
