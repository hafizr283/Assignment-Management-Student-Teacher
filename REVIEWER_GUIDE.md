# Reviewer Guide

## Project description

The Assignment and Submission Management System is a role-based web application for managing the complete academic assignment workflow. Administrators configure users, courses, subjects, student enrollment, and teacher responsibilities. Teachers create and publish assignments, control deadlines and submission rules, review student work, request revisions, and provide marks and feedback. Students only see assignments for their enrolled course and can submit written answers or supported files, track submission versions, and view grading results.

The application uses a Next.js frontend, an ASP.NET Core 8 API, PostgreSQL with Entity Framework Core migrations, JWT authentication, role and ownership authorization, server-side validation, and automated workflow tests.

## Live application and source

- Live application: https://assignment-management-student-teach.vercel.app/
- Source repository: https://github.com/hafizr283/Assignment-Management-Student-Teacher

## Demo accounts

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@example.com` | `Admin123!` |
| Teacher | `teacher@example.com` | `Teacher123!` |
| Student | `student@example.com` | `Student123!` |

## How the application works

1. The Admin prepares the academic structure by creating users, courses, and subjects. The Admin enrolls students in courses and assigns teachers to course and subject combinations.
2. A Teacher creates an assignment for an assigned course and subject. The assignment can remain a draft or be published immediately. The Teacher defines its deadline, maximum marks, late-submission policy, and resubmission policy.
3. A Student signs in and sees only published assignments belonging to the student's enrolled course. The Student submits a written answer, an attachment URL, an uploaded file, or a combination of these.
4. The API validates the deadline, assignment status, course access, file rules, and resubmission rules before saving the work. Each accepted resubmission increases the version number.
5. The Teacher reviews submitted answers and attachments, assigns marks within the assignment maximum, adds feedback, or requests a revision.
6. The Student sees the latest status, version, marks, and feedback. If revision is requested and updates are allowed, the Student can correct and resubmit the work.

## Recommended reviewer walkthrough

### 1. Review the Admin workflow

Sign in with the Admin account and open **Manage**.

- Inspect the seeded users, courses, and subjects.
- Select **Add user** to view the validated user form.
- Select **Assign teacher** to see course and subject-based teacher assignment.
- Select **Enroll student** to see course enrollment.
- Avoid deactivating the three seeded demo accounts, because the other walkthrough steps use them.

### 2. Review the Teacher workflow

Sign out and sign in with the Teacher account.

- Open **Assignments**.
- Create a draft assignment with a future deadline and select the available course and subject.
- Publish the assignment.
- Use **Extend deadline** to review the inline deadline form.
- Open **Submissions** to review student answers, attachments, versions, late status, and grading controls.

For a shared live demo, use an identifiable title such as `Reviewer Test Assignment` so the record can be recognized later.

### 3. Review the Student workflow

Sign out and sign in with the Student account.

- Confirm that only published assignments for the student's course are visible.
- Enter a written answer or upload an allowed file.
- Submit the work and confirm the success message, submission status, and version number.
- Resubmit if the assignment allows updates and verify that the version number increases.

### 4. Complete grading and revision

Sign back in as the Teacher.

- Open **Submissions** and select **Grade**.
- Enter marks within the assignment maximum and add feedback.
- Confirm that the submission status changes to graded.
- Select **Request revision** to reopen the submission.

Sign in again as the Student and confirm that the feedback is visible and the submission can be updated when the assignment rules allow it.

## Important business rules

- Authentication uses a 60-minute JWT. Expired, invalid, inactive-account, and permission states return clear responses.
- Students can only access assignments for their active enrolled course.
- Teachers can only manage assignments and submissions for their assigned course and subject combinations.
- Draft or archived assignments do not accept submissions.
- Late submissions are rejected unless the Teacher explicitly allows them.
- Resubmission is rejected when updates are disabled or after grading, unless the Teacher requests a revision.
- Marks must be between zero and the assignment's maximum marks.
- An assignment's course or subject cannot be changed after submissions exist.
- Maximum marks cannot be reduced below a mark already awarded.
- Uploads accept PDF, DOCX, ZIP, JPG/JPEG, and PNG files up to 10 MB. The API checks both file metadata and file signatures.

## Job requirement coverage

| Requirement area | Implementation |
| --- | --- |
| Role-based authentication | JWT login for Admin, Teacher, and Student roles, with BCrypt password hashes. |
| Authorization | Server-side role, ownership, active-account, course, and teacher-assignment checks. |
| Admin management | Users, courses, subjects, student enrollment, and teacher course/subject assignment. |
| Assignment management | Create, draft, publish, update deadline, archive, late submission, and update controls. |
| Student submissions | Written answers, uploaded files, attachment URLs, late status, and versioned resubmission. |
| Teacher review | Submission list, submitted content, attachments, marks, feedback, and revision requests. |
| Database | PostgreSQL, Entity Framework Core models, migrations, relationships, and seed data. |
| Validation and errors | Client-side form constraints plus structured API validation and workflow errors. |
| API documentation | Swagger UI is available from the backend `/swagger` route. |
| Testing | 26 automated backend tests cover workflow, authorization, validation, and upload rules. |
| Responsive UX | Keyboard focus, mobile navigation, responsive forms/tables, and loading, empty, error, and success states. |

## Source code map

- `frontend/app/page.tsx`: role-based interface and API integration.
- `frontend/app/globals.css`: responsive and accessible presentation.
- `backend/AssignmentSystem.Api/Program.cs`: API endpoints, authentication, authorization, uploads, and error handling.
- `backend/AssignmentSystem.Api/Models/Entities.cs`: database entities and workflow statuses.
- `backend/AssignmentSystem.Api/Services/WorkflowRules.cs`: assignment, submission, revision, and grading rules.
- `backend/AssignmentSystem.Api/Data/Migrations/`: PostgreSQL schema migrations.
- `backend/AssignmentSystem.Tests/`: automated business-rule, validation, and upload tests.

## Verification commands

From the repository root:

```powershell
dotnet test backend/AssignmentSystem.Tests/AssignmentSystem.Tests.csproj -c Release
```

From `frontend`:

```powershell
npm install
npm run lint
npm run build
```

Current verified result: 26 backend tests pass, frontend lint passes with zero warnings, the production build passes, and the production npm audit reports zero vulnerabilities.

## Demo limitations

- Uploaded files are stored on the API host filesystem. Render storage is ephemeral, so files may be removed after a service restart or redeployment.
- Notifications, plagiarism detection, antivirus scanning, refresh tokens, and pagination are outside the recruitment assignment scope.
- The live demo is shared. Reviewers should use clearly named test records and avoid deactivating seeded accounts.
