# Submission Checklist and Next Steps

This document maps the recruitment assignment requirements to this project and gives the safest final submission procedure.

## Short answer

Git alone is not enough. The evaluator expects an accessible GitHub/GitLab repository containing the complete application, database setup files, tests, README, demo credentials, and an environment-variable example. This project already contains those application pieces, but you still need to create or use a repository whose root is **this folder** and perform a final clean run-through.

## Requirement audit

| Requirement | Current status | Evidence / action |
| --- | --- | --- |
| GitHub/GitLab repository link | **Needs final setup** | The current Git root is `E:\`, and its remote points to an unrelated GPU repository. Do not use that repository for this project. Create a repository rooted at `E:\job_project\asp.net` or upload this folder to a new repository. |
| Complete frontend code | Ready | `frontend/` contains the Next.js, React, TypeScript, validation, and API integration code. |
| Complete backend/API code | Ready | `backend/AssignmentSystem.Api/` contains the ASP.NET Core API, authentication, authorization, validation, Swagger, migrations, seed logic, and uploads. |
| PostgreSQL database files | Ready | `backend/AssignmentSystem.Api/Data/Migrations/`, `database/create-database.sql`, and `backend/AssignmentSystem.Api/Services/SeedData.cs` are included. |
| README | Ready | `README.md` documents features, structure, setup, credentials, assumptions, and limitations. |
| Demo credentials | Ready | Admin, Teacher, and Student credentials are listed in `README.md` and seeded by `SeedData.cs`. |
| Environment configuration | Ready | `frontend/.env.example` is present. Do not commit real deployment secrets. The development values in `appsettings.json` must be replaced for deployment. |
| Easy local setup | Ready, verify once | Follow the exact commands in `README.md`; start PostgreSQL before starting the API. |
| JWT and role authorization | Ready | Implemented in `Program.cs` and endpoint authorization/ownership checks. |
| Unit tests | Ready | `dotnet test AssignmentSystem.sln` passes 13 tests when the project-local .NET SDK variables are set. |
| Frontend production build | Verify in a clean process | Stop old Next.js processes before `npm run build`; multiple processes sharing `.next` can make the build hang. |

## Important Git warning

From this folder, `git rev-parse --show-toplevel` currently reports `E:\`. That means commands such as `git add .` can see unrelated directories elsewhere on the drive. The existing remote also points to an unrelated repository. Do not commit or push from that Git context.

Use a new repository for this project. The safest option is to create an empty repository on GitHub or GitLab first, then run the following commands from this folder:

```powershell
Set-Location E:\job_project\asp.net

# Confirm this is the project folder before initializing Git.
Get-ChildItem README.md, AssignmentSystem.sln, backend, frontend, database

git init
git branch -M main
git add .
git status --short
git commit -m "Complete assignment management system"

# Replace this URL with the new, empty repository URL.
git remote add origin https://github.com/<your-account>/<new-repository>.git
git push -u origin main
```

If `git remote add origin` says that `origin` already exists, inspect it first:

```powershell
git remote -v
```

Only if it is the new project repository should you keep it. Otherwise change it explicitly:

```powershell
git remote set-url origin https://github.com/<your-account>/<new-repository>.git
```

Before pushing, check that the staged file list contains only this project. It should include files under `backend/`, `frontend/`, `database/`, plus the solution, README, and documentation. It should not include unrelated folders such as `GPU_Programming_Essentials`, `xampp`, or other directories under `E:\`.

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

Expected result: all tests pass. The current project has 13 business-rule tests.

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
4. Initialize/push a **new project-specific Git repository**.
5. Test the pushed repository URL from a clean view.
6. Submit that URL through the provided form.

