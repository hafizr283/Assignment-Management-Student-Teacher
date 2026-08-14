'use client';

import Image from 'next/image';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { z } from 'zod';

const API = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5080/api';
type Role = 'Admin' | 'Teacher' | 'Student';
type User = { id: number; name: string; email: string; role: Role; courseId?: number };
type Assignment = { id: number; title: string; description: string; deadlineUtc: string; maximumMarks: number; status: string; allowUpdates: boolean; allowLateSubmission: boolean; courseId: number; subjectId: number; course: string; subject: string; teacher: string; submission?: { id: number; answer: string; fileUrl?: string; versionNumber: number; isLate: boolean; status: string; marks?: number; feedback?: string } };
type Submission = { id: number; answer: string; fileUrl?: string; assignment: string; assignmentId: number; maximumMarks: number; studentId: number; student: string; versionNumber: number; isLate: boolean; marks?: number; feedback?: string; status: string; updatedAtUtc: string };
type Option = { courseId: number; course: string; subjectId: number; subject: string };
type Catalog = { id: number; name: string; courseId?: number; isActive?: boolean };
type AdminForm = 'assign-teacher' | 'enroll-student' | 'add-user' | 'add-course' | 'add-subject';

class ApiError extends Error {
  constructor(message: string, readonly status: number) {
    super(message);
  }
}

function isImageUrl(url?: string) {
  return !!url && /\.(png|jpe?g)(?:$|[?#])/i.test(url);
}

const loginSchema = z.object({ email: z.string().email(), password: z.string().min(6) });

async function api(path: string, options: RequestInit = {}) {
  const token = localStorage.getItem('assignment_token');
  let response: Response;
  try {
    response = await fetch(`${API}${path}`, {
      ...options,
      headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(options.headers || {}) }
    });
  } catch {
    throw new Error(navigator.onLine ? 'The server could not be reached. Please try again.' : 'You appear to be offline. Check your connection and try again.');
  }
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new ApiError(body.error?.message || body.error || `Request failed (${response.status})`, response.status);
  }
  return response.status === 204 ? null : response.json();
}

async function upload(file: File) {
  const token = localStorage.getItem('assignment_token');
  const form = new FormData(); form.append('file', file);
  let response: Response;
  try {
    response = await fetch(`${API}/uploads`, { method: 'POST', body: form, headers: token ? { Authorization: `Bearer ${token}` } : {} });
  } catch {
    throw new Error(navigator.onLine ? 'The upload service could not be reached. Please try again.' : 'You appear to be offline. Reconnect before uploading.');
  }
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new ApiError(body.error?.message || body.error || 'Upload failed.', response.status);
  return body.fileUrl as string;
}

export default function Home() {
  const [user, setUser] = useState<User | null>(null);
  const [email, setEmail] = useState('student@example.com');
  const [password, setPassword] = useState('Student123!');
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [sessionReady, setSessionReady] = useState(false);
  const [loading, setLoading] = useState(false);
  const [busyAction, setBusyAction] = useState('');
  const [tab, setTab] = useState('assignments');
  const [assignments, setAssignments] = useState<Assignment[]>([]);
  const [submissions, setSubmissions] = useState<Submission[]>([]);
  const [answers, setAnswers] = useState<Record<number, string>>({});
  const [fileUrls, setFileUrls] = useState<Record<number, string>>({});
  const [options, setOptions] = useState<Option[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [courses, setCourses] = useState<Catalog[]>([]);
  const [subjects, setSubjects] = useState<Catalog[]>([]);
  const [newAssignment, setNewAssignment] = useState({ title: '', description: '', deadlineUtc: '', maximumMarks: 20, courseId: 0, subjectId: 0, status: 'Draft', allowUpdates: true, allowLateSubmission: false });
  const [gradingId, setGradingId] = useState<number | null>(null);
  const [gradeDraft, setGradeDraft] = useState({ marks: '', feedback: '' });
  const [deadlineId, setDeadlineId] = useState<number | null>(null);
  const [deadlineValue, setDeadlineValue] = useState('');
  const [adminForm, setAdminForm] = useState<AdminForm | null>(null);
  const [adminDraft, setAdminDraft] = useState({ name: '', email: '', password: '', role: 'Student' as Role, teacherId: '', studentId: '', courseId: '', subjectId: '' });

  const clearSession = useCallback(() => {
    localStorage.removeItem('assignment_token');
    localStorage.removeItem('assignment_user');
    setUser(null);
    setAssignments([]);
    setSubmissions([]);
  }, []);

  const handleError = useCallback((value: unknown, allowSessionReset = true) => {
    const nextError = value instanceof Error ? value : new Error('Something went wrong. Please try again.');
    if (allowSessionReset && nextError instanceof ApiError && nextError.status === 401) {
      clearSession();
      setError('Your session has expired. Sign in again to continue.');
      return;
    }
    if (nextError instanceof ApiError && nextError.status === 403) {
      setError('You do not have permission to complete that action.');
      return;
    }
    setError(nextError.message);
  }, [clearSession]);

  function beginAction(action: string) {
    setBusyAction(action);
    setError('');
    setNotice('');
  }

  const refresh = useCallback(async () => {
    setLoading(true); setError('');
    try {
      setAssignments(await api('/assignments/'));
      if (user?.role === 'Student') setSubmissions(await api('/submissions/me'));
      if (user?.role === 'Teacher' || user?.role === 'Admin') setSubmissions(await api('/submissions'));
      if (user?.role === 'Teacher') {
        const available: Option[] = await api('/assignments/options');
        setOptions(available);
        if (available[0]) setNewAssignment(x => ({ ...x, courseId: available[0].courseId, subjectId: available[0].subjectId }));
      }
      if (user?.role === 'Admin') {
        const [allUsers, allCourses, allSubjects] = await Promise.all([api('/admin/users'), api('/admin/courses'), api('/admin/subjects')]);
        setUsers(allUsers); setCourses(allCourses); setSubjects(allSubjects);
      }
    } catch (e) { handleError(e); }
    finally { setLoading(false); }
  }, [handleError, user]);

  useEffect(() => {
    const bootstrap = window.setTimeout(() => {
      const saved = localStorage.getItem('assignment_user');
      const token = localStorage.getItem('assignment_token');
      if (saved && token) {
        try { setUser(JSON.parse(saved)); }
        catch { clearSession(); }
      } else if (saved || token) {
        clearSession();
      }
      setSessionReady(true);
    }, 0);
    return () => window.clearTimeout(bootstrap);
  }, [clearSession]);
  useEffect(() => {
    if (!user) return;
    const initialRefresh = window.setTimeout(() => void refresh(), 0);
    return () => window.clearTimeout(initialRefresh);
  }, [refresh, user]);

  async function login(event: React.FormEvent) {
    event.preventDefault(); beginAction('login');
    try {
      const body = loginSchema.parse({ email, password });
      const result = await api('/auth/login', { method: 'POST', body: JSON.stringify(body) });
      localStorage.setItem('assignment_token', result.token);
      localStorage.setItem('assignment_user', JSON.stringify(result.user));
      setUser(result.user);
    } catch (e) {
      if (e instanceof z.ZodError) setError(e.issues[0].message);
      else handleError(e, false);
    } finally { setBusyAction(''); }
  }

  function logout() { clearSession(); setError(''); setNotice(''); }

  async function submit(id: number) {
    beginAction(`submit-${id}`);
    try {
      await api(`/assignments/${id}/submissions`, { method: 'POST', body: JSON.stringify({ answer: answers[id] || assignments.find(x => x.id === id)?.submission?.answer || '', fileUrl: fileUrls[id] || assignments.find(x => x.id === id)?.submission?.fileUrl || null }) });
      await refresh(); setAnswers(x => ({ ...x, [id]: '' })); setNotice('Your submission was saved.');
    } catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  function openGrade(item: Submission) {
    setGradingId(item.id);
    setGradeDraft({ marks: item.marks === undefined ? '' : String(item.marks), feedback: item.feedback || '' });
    setError('');
    setNotice('');
  }

  async function grade(event: React.FormEvent, item: Submission) {
    event.preventDefault();
    const marks = Number(gradeDraft.marks);
    if (!Number.isFinite(marks) || marks < 0 || marks > item.maximumMarks) {
      setError(`Enter marks from 0 to ${item.maximumMarks}.`);
      return;
    }
    beginAction(`grade-${item.id}`);
    try {
      await api(`/submissions/${item.id}/grade`, { method: 'POST', body: JSON.stringify({ marks, feedback: gradeDraft.feedback }) });
      await refresh(); setGradingId(null); setNotice(`Grade saved for ${item.student}.`);
    } catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  async function createAssignment(event: React.FormEvent) {
    event.preventDefault(); beginAction('create-assignment');
    try {
      await api('/assignments/', { method: 'POST', body: JSON.stringify({ ...newAssignment, deadlineUtc: new Date(newAssignment.deadlineUtc).toISOString() }) });
      setNewAssignment(x => ({ ...x, title: '', description: '', deadlineUtc: '' })); await refresh(); setNotice('Assignment created successfully.');
    } catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  async function assignmentAction(item: Assignment, action: 'publish' | 'archive') {
    if (action === 'archive' && !window.confirm('Archive this assignment?')) return;
    beginAction(`${action}-${item.id}`);
    try {
      if (action === 'publish') await api(`/assignments/${item.id}/publish`, { method: 'PATCH' });
      if (action === 'archive') await api(`/assignments/${item.id}`, { method: 'DELETE' });
      await refresh(); setNotice(action === 'publish' ? 'Assignment published.' : 'Assignment archived.');
    } catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  function openDeadline(item: Assignment) {
    setDeadlineId(item.id);
    setDeadlineValue(item.deadlineUtc.slice(0, 16));
    setError('');
    setNotice('');
  }

  async function updateDeadline(event: React.FormEvent, item: Assignment) {
    event.preventDefault();
    const deadline = new Date(deadlineValue);
    if (!deadlineValue || Number.isNaN(deadline.getTime())) {
      setError('Choose a valid deadline.');
      return;
    }
    beginAction(`deadline-${item.id}`);
    try {
      await api(`/assignments/${item.id}`, { method: 'PUT', body: JSON.stringify({ title: item.title, description: item.description, deadlineUtc: deadline.toISOString(), maximumMarks: item.maximumMarks, courseId: item.courseId, subjectId: item.subjectId, status: item.status, allowUpdates: item.allowUpdates, allowLateSubmission: item.allowLateSubmission }) });
      await refresh(); setDeadlineId(null); setNotice('Deadline updated.');
    } catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  async function overrideStatus(item: Submission, status: 'NeedsRevision' | 'Late' | 'Submitted') {
    beginAction(`status-${item.id}`);
    try { await api(`/submissions/${item.id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }); await refresh(); setNotice(`Submission marked ${status === 'NeedsRevision' ? 'needs revision' : status.toLowerCase()}.`); }
    catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  function openAdminForm(form: AdminForm) {
    const firstCourse = courses[0];
    const firstSubject = subjects.find(x => x.courseId === firstCourse?.id);
    setAdminForm(form);
    setAdminDraft({
      name: '', email: '', password: '', role: 'Student',
      teacherId: String(users.find(x => x.role === 'Teacher')?.id || ''),
      studentId: String(users.find(x => x.role === 'Student')?.id || ''),
      courseId: String(firstCourse?.id || ''),
      subjectId: String(firstSubject?.id || '')
    });
    setError('');
    setNotice('');
  }

  async function submitAdminForm(event: React.FormEvent) {
    event.preventDefault();
    if (!adminForm) return;
    beginAction(adminForm);
    try {
      if (adminForm === 'assign-teacher') {
        await api('/admin/teacher-assignments', { method: 'POST', body: JSON.stringify({ teacherId: Number(adminDraft.teacherId), courseId: Number(adminDraft.courseId), subjectId: Number(adminDraft.subjectId) }) });
      } else if (adminForm === 'enroll-student') {
        await api('/admin/enrollments', { method: 'POST', body: JSON.stringify({ studentId: Number(adminDraft.studentId), courseId: Number(adminDraft.courseId) }) });
      } else if (adminForm === 'add-user') {
        await api('/admin/users', { method: 'POST', body: JSON.stringify({ name: adminDraft.name, email: adminDraft.email, password: adminDraft.password, role: adminDraft.role, courseId: adminDraft.role === 'Student' ? Number(adminDraft.courseId) : null }) });
      } else if (adminForm === 'add-course') {
        await api('/admin/courses', { method: 'POST', body: JSON.stringify({ name: adminDraft.name }) });
      } else {
        await api('/admin/subjects', { method: 'POST', body: JSON.stringify({ name: adminDraft.name, courseId: Number(adminDraft.courseId) }) });
      }
      const successMessage = adminForm === 'assign-teacher' ? 'Teacher assignment saved.' : adminForm === 'enroll-student' ? 'Student enrollment saved.' : adminForm === 'add-user' ? 'User added.' : adminForm === 'add-course' ? 'Course added.' : 'Subject added.';
      await refresh(); setAdminForm(null); setNotice(successMessage);
    } catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  async function uploadForAssignment(id: number, file?: File) {
    if (!file) return; beginAction(`upload-${id}`);
    try { const fileUrl = await upload(file); setFileUrls(x => ({ ...x, [id]: fileUrl })); setNotice(`${file.name} uploaded and ready to submit.`); }
    catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  async function deactivateUser(id: number) {
    if (!window.confirm('Deactivate this user?')) return;
    beginAction(`deactivate-${id}`);
    try { await api(`/admin/users/${id}/deactivate`, { method: 'PATCH' }); await refresh(); setNotice('User deactivated.'); } catch (e) { handleError(e); } finally { setBusyAction(''); }
  }

  const published = useMemo(() => assignments.filter(x => x.status === 'Published').length, [assignments]);
  const teachers = useMemo(() => users.filter(x => x.role === 'Teacher'), [users]);
  const students = useMemo(() => users.filter(x => x.role === 'Student'), [users]);
  const adminSubjects = useMemo(() => subjects.filter(x => x.courseId === Number(adminDraft.courseId)), [adminDraft.courseId, subjects]);

  if (!sessionReady) return <main className="login-shell"><p className="loading-state" role="status">Loading Classroom Hub...</p></main>;

  if (!user) return <main className="login-shell"><section className="login-card" aria-labelledby="login-title">
    <div className="brand-mark" aria-hidden="true">CH</div><p className="eyebrow">ASSIGNMENT MANAGEMENT</p><h1 id="login-title">Make learning visible.</h1>
    <p className="muted">One calm place for classes, assignments, submissions, and feedback.</p>
    <form onSubmit={login}><label htmlFor="login-email">Email</label><input id="login-email" value={email} onChange={e => setEmail(e.target.value)} type="email" autoComplete="email" required /><label htmlFor="login-password">Password</label><input id="login-password" value={password} onChange={e => setPassword(e.target.value)} type="password" autoComplete="current-password" required />{error && <p className="error" role="alert">{error}</p>}<button className="primary" disabled={busyAction === 'login'}>{busyAction === 'login' ? 'Signing in...' : 'Sign in'}</button></form>
    <p className="hint">Admin: admin@example.com / Admin123!<br />Teacher: teacher@example.com / Teacher123!<br />Student: student@example.com / Student123!</p>
  </section></main>;

  return <main className="app-shell">
    <aside aria-label="Account and navigation">
      <div className="brand"><span className="brand-mark small" aria-hidden="true">CH</span><span>Classroom Hub</span></div>
      <div className="profile"><div className="avatar" aria-hidden="true">{user.name.split(' ').map(x => x[0]).join('').slice(0, 2)}</div><div><strong>{user.name}</strong><small>{user.role}</small></div></div>
      <nav aria-label="Main navigation">
        <button type="button" className={tab === 'assignments' ? 'active' : ''} aria-current={tab === 'assignments' ? 'page' : undefined} onClick={() => setTab('assignments')}>Assignments</button>
        {user.role !== 'Student' && <button type="button" className={tab === 'submissions' ? 'active' : ''} aria-current={tab === 'submissions' ? 'page' : undefined} onClick={() => setTab('submissions')}>Submissions</button>}
        {user.role === 'Admin' && <button type="button" className={tab === 'manage' ? 'active' : ''} aria-current={tab === 'manage' ? 'page' : undefined} onClick={() => setTab('manage')}>Manage</button>}
      </nav>
      <button type="button" className="logout" onClick={logout}>Sign out</button>
    </aside>
    <section className="content" aria-busy={loading}>
      <header><div><p className="eyebrow">{user.role.toUpperCase()} SPACE</p><h1>{tab === 'submissions' ? 'Review submissions' : tab === 'manage' ? 'Administration' : 'Assignments'}</h1></div><button type="button" className="icon-btn" onClick={refresh} aria-label={loading ? 'Refreshing data' : 'Refresh data'} title="Refresh data" disabled={loading}>&#8635;</button></header>
      <div className="message-region" aria-live="polite" aria-atomic="true">{loading && <div className="alert info">Refreshing data...</div>}{notice && <div className="alert success">{notice}</div>}</div>
      {error && <div className="alert error" role="alert">{error}<button type="button" className="alert-action" onClick={refresh}>Try again</button></div>}

      {tab === 'assignments' && <><div className="stats"><div><span>Visible assignments</span><strong>{assignments.length}</strong></div><div><span>Published</span><strong>{published}</strong></div><div><span>Past due</span><strong>{assignments.filter(a => new Date(a.deadlineUtc) < new Date()).length}</strong></div></div>
        {user.role === 'Teacher' && <form className="create-form" onSubmit={createAssignment}>
          <h2>Create assignment</h2>
          {!options.length && !loading && <p className="form-help">You need an assigned course and subject before you can create an assignment.</p>}
          <div className="form-grid">
            <label htmlFor="assignment-title">Title<input id="assignment-title" required value={newAssignment.title} onChange={e => setNewAssignment({ ...newAssignment, title: e.target.value })} /></label>
            <label htmlFor="assignment-deadline">Deadline<input id="assignment-deadline" required type="datetime-local" value={newAssignment.deadlineUtc} onChange={e => setNewAssignment({ ...newAssignment, deadlineUtc: e.target.value })} /></label>
            <label htmlFor="assignment-marks">Maximum marks<input id="assignment-marks" required type="number" min="1" value={newAssignment.maximumMarks} onChange={e => setNewAssignment({ ...newAssignment, maximumMarks: Number(e.target.value) })} /></label>
            <label htmlFor="assignment-course">Course and subject<select id="assignment-course" required disabled={!options.length} value={`${newAssignment.courseId}:${newAssignment.subjectId}`} onChange={e => { const [courseId, subjectId] = e.target.value.split(':').map(Number); setNewAssignment({ ...newAssignment, courseId, subjectId }); }}>{!options.length && <option value="0:0">No options available</option>}{options.map(o => <option key={`${o.courseId}:${o.subjectId}`} value={`${o.courseId}:${o.subjectId}`}>{o.course} &middot; {o.subject}</option>)}</select></label>
          </div>
          <label htmlFor="assignment-description">Description<textarea id="assignment-description" required value={newAssignment.description} onChange={e => setNewAssignment({ ...newAssignment, description: e.target.value })} /></label>
          <div className="form-actions"><label htmlFor="assignment-status">Initial status<select id="assignment-status" value={newAssignment.status} onChange={e => setNewAssignment({ ...newAssignment, status: e.target.value })}><option value="Draft">Draft</option><option value="Published">Publish now</option></select></label><label className="check"><input type="checkbox" checked={newAssignment.allowUpdates} onChange={e => setNewAssignment({ ...newAssignment, allowUpdates: e.target.checked })} /> Allow resubmission</label><label className="check"><input type="checkbox" checked={newAssignment.allowLateSubmission} onChange={e => setNewAssignment({ ...newAssignment, allowLateSubmission: e.target.checked })} /> Allow late submission</label><button className="primary" disabled={busyAction === 'create-assignment' || !options.length}>{busyAction === 'create-assignment' ? 'Saving...' : 'Save assignment'}</button></div>
        </form>}
        {!loading && !assignments.length && <div className="empty"><h2>No assignments yet</h2><p>{user.role === 'Teacher' ? 'Create the first assignment when your course and subject are ready.' : user.role === 'Student' ? 'Published assignments for your course will appear here.' : 'Assignments will appear here after teachers create them.'}</p></div>}
        <div className="assignment-grid">{assignments.map(a => {
          const pastDue = new Date(a.deadlineUtc) < new Date();
          const cannotSubmit = (!a.allowUpdates && !!a.submission) || (pastDue && !a.allowLateSubmission);
          const assignmentBusy = busyAction.endsWith(`-${a.id}`);
          return <article className="assignment-card" key={a.id}>
            <div className="card-top"><span className={`pill ${pastDue ? 'late' : a.status.toLowerCase()}`}>{pastDue ? 'Past due' : a.status}</span><time className="date" dateTime={a.deadlineUtc}>Due {new Date(a.deadlineUtc).toLocaleString()}</time></div>
            <h2>{a.title}</h2><p>{a.description}</p>
            <div className="meta"><span>{a.course}</span><span>{a.subject}</span><span>{a.maximumMarks} marks</span>{a.allowLateSubmission && <span>Late allowed</span>}</div>
            {user.role === 'Teacher' && <div className="card-actions">
              {a.status === 'Draft' && <button type="button" className="text-btn" disabled={assignmentBusy} onClick={() => assignmentAction(a, 'publish')}>{busyAction === `publish-${a.id}` ? 'Publishing...' : 'Publish'}</button>}
              <button type="button" className="text-btn" disabled={assignmentBusy} aria-expanded={deadlineId === a.id} onClick={() => deadlineId === a.id ? setDeadlineId(null) : openDeadline(a)}>Extend deadline</button>
              <button type="button" className="danger-btn" disabled={assignmentBusy} onClick={() => assignmentAction(a, 'archive')}>{busyAction === `archive-${a.id}` ? 'Archiving...' : 'Archive'}</button>
              {deadlineId === a.id && <form className="inline-editor" onSubmit={event => updateDeadline(event, a)}>
                <label htmlFor={`deadline-${a.id}`}>New deadline<input id={`deadline-${a.id}`} type="datetime-local" required value={deadlineValue} onChange={event => setDeadlineValue(event.target.value)} /></label>
                <div className="editor-actions"><button className="primary" disabled={assignmentBusy}>{busyAction === `deadline-${a.id}` ? 'Updating...' : 'Save deadline'}</button><button type="button" className="secondary" disabled={assignmentBusy} onClick={() => setDeadlineId(null)}>Cancel</button></div>
              </form>}
            </div>}
            {user.role === 'Student' && <div className="submission-box">
              <label htmlFor={`answer-${a.id}`}>Written answer<textarea id={`answer-${a.id}`} value={answers[a.id] ?? a.submission?.answer ?? ''} onChange={e => setAnswers({ ...answers, [a.id]: e.target.value })} /></label>
              <label htmlFor={`url-${a.id}`}>Attachment URL <span className="optional">(optional)</span><input id={`url-${a.id}`} type="url" placeholder="https://example.com/file" value={fileUrls[a.id] ?? a.submission?.fileUrl ?? ''} onChange={e => setFileUrls({ ...fileUrls, [a.id]: e.target.value })} /></label>
              <label className="file-field" htmlFor={`file-${a.id}`}>Upload file <span className="optional">(PDF, DOCX, ZIP, or image; 10 MB max)</span><input id={`file-${a.id}`} type="file" accept=".pdf,.docx,.zip,.jpg,.jpeg,.png" onChange={e => uploadForAssignment(a.id, e.target.files?.[0])} /></label>
              {busyAction === `upload-${a.id}` && <p className="field-status" role="status">Uploading file...</p>}
              <button type="button" className="primary" disabled={assignmentBusy || cannotSubmit} onClick={() => submit(a.id)}>{busyAction === `submit-${a.id}` ? 'Submitting...' : a.submission ? 'Resubmit' : pastDue ? 'Submit late' : 'Submit answer'}</button>
              {cannotSubmit && <p className="submission-limit">{a.submission && !a.allowUpdates ? 'This assignment does not allow resubmission.' : 'The deadline has passed and late submissions are closed.'}</p>}
              {a.submission && <p className="submission-note">{a.submission.status} &middot; version {a.submission.versionNumber}{a.submission.marks !== undefined ? ` / ${a.submission.marks} of ${a.maximumMarks} marks` : ''}{a.submission.feedback ? ` / ${a.submission.feedback}` : ''}</p>}
            </div>}
          </article>;
        })}</div></>}

      {tab === 'submissions' && <div className="table-wrap"><table><caption className="sr-only">Submissions available for review</caption><thead><tr><th>Student</th><th>Assignment</th><th>Submitted work</th><th>Version</th><th>Marks</th><th>Status</th><th><span className="sr-only">Actions</span></th></tr></thead><tbody>{submissions.map(s => {
        const rowBusy = busyAction.endsWith(`-${s.id}`);
        return <tr key={s.id}>
          <td data-label="Student">{s.student}</td>
          <td data-label="Assignment">{s.assignment}</td>
          <td data-label="Submitted work" className="submission-work">{s.answer ? <p className="submission-answer">{s.answer}</p> : <p className="submission-empty">No written answer.</p>}{s.fileUrl ? <div className="attachment-preview">{isImageUrl(s.fileUrl) && <a href={s.fileUrl} target="_blank" rel="noreferrer"><Image unoptimized width={96} height={72} src={s.fileUrl} alt={`${s.student}'s submitted attachment`} /></a>}<a className="text-link" href={s.fileUrl} target="_blank" rel="noreferrer">{isImageUrl(s.fileUrl) ? 'Open full image' : 'Open attachment'}<span className="sr-only"> in a new tab</span></a></div> : <span className="submission-empty">No attachment</span>}</td>
          <td data-label="Version">v{s.versionNumber}{s.isLate ? ' / late' : ''}</td>
          <td data-label="Marks">{s.marks ?? '-'} / {s.maximumMarks}</td>
          <td data-label="Status"><span className={`pill ${s.status.toLowerCase()}`}>{s.status}</span></td>
          <td data-label="Actions">{user.role === 'Teacher' && <div className="review-tools">
            <div className="row-actions"><button type="button" className="text-btn" disabled={rowBusy} aria-expanded={gradingId === s.id} onClick={() => gradingId === s.id ? setGradingId(null) : openGrade(s)}>Grade</button><button type="button" className="text-btn" disabled={rowBusy} onClick={() => overrideStatus(s, 'NeedsRevision')}>{busyAction === `status-${s.id}` ? 'Updating...' : 'Request revision'}</button></div>
            {gradingId === s.id && <form className="grade-form" onSubmit={event => grade(event, s)}>
              <label htmlFor={`marks-${s.id}`}>Marks<input id={`marks-${s.id}`} type="number" min="0" max={s.maximumMarks} required value={gradeDraft.marks} onChange={event => setGradeDraft({ ...gradeDraft, marks: event.target.value })} /></label>
              <label htmlFor={`feedback-${s.id}`}>Feedback <span className="optional">(optional)</span><textarea id={`feedback-${s.id}`} maxLength={4000} value={gradeDraft.feedback} onChange={event => setGradeDraft({ ...gradeDraft, feedback: event.target.value })} /></label>
              <div className="editor-actions"><button className="primary" disabled={rowBusy}>{busyAction === `grade-${s.id}` ? 'Saving...' : 'Save grade'}</button><button type="button" className="secondary" disabled={rowBusy} onClick={() => setGradingId(null)}>Cancel</button></div>
            </form>}
          </div>}</td>
        </tr>;
      })}</tbody></table>{!loading && !submissions.length && <div className="empty"><h2>No submissions yet</h2><p>Submitted work will appear here when students turn it in.</p></div>}</div>}

      {tab === 'manage' && <>
        <div className="admin-actions"><button type="button" className="primary" disabled={!teachers.length || !courses.length || !subjects.length} aria-expanded={adminForm === 'assign-teacher'} onClick={() => openAdminForm('assign-teacher')}>Assign teacher</button><button type="button" className="primary" disabled={!students.length || !courses.length} aria-expanded={adminForm === 'enroll-student'} onClick={() => openAdminForm('enroll-student')}>Enroll student</button></div>
        {adminForm && <form className="create-form admin-form-panel" onSubmit={submitAdminForm}>
          <h2>{adminForm === 'assign-teacher' ? 'Assign teacher' : adminForm === 'enroll-student' ? 'Enroll student' : adminForm === 'add-user' ? 'Add user' : adminForm === 'add-course' ? 'Add course' : 'Add subject'}</h2>
          <div className="admin-form-grid">
            {adminForm === 'assign-teacher' && <label htmlFor="admin-teacher">Teacher<select id="admin-teacher" required value={adminDraft.teacherId} onChange={event => setAdminDraft({ ...adminDraft, teacherId: event.target.value })}>{teachers.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
            {adminForm === 'enroll-student' && <label htmlFor="admin-student">Student<select id="admin-student" required value={adminDraft.studentId} onChange={event => setAdminDraft({ ...adminDraft, studentId: event.target.value })}>{students.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
            {(adminForm === 'assign-teacher' || adminForm === 'enroll-student' || adminForm === 'add-subject' || (adminForm === 'add-user' && adminDraft.role === 'Student')) && <label htmlFor="admin-course">Course<select id="admin-course" required value={adminDraft.courseId} onChange={event => { const courseId = event.target.value; const subjectId = String(subjects.find(item => item.courseId === Number(courseId))?.id || ''); setAdminDraft({ ...adminDraft, courseId, subjectId }); }}>{courses.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
            {adminForm === 'assign-teacher' && <label htmlFor="admin-subject">Subject<select id="admin-subject" required value={adminDraft.subjectId} onChange={event => setAdminDraft({ ...adminDraft, subjectId: event.target.value })}>{adminSubjects.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select></label>}
            {(adminForm === 'add-user' || adminForm === 'add-course' || adminForm === 'add-subject') && <label htmlFor="admin-name">{adminForm === 'add-user' ? 'Full name' : 'Name'}<input id="admin-name" required maxLength={100} value={adminDraft.name} onChange={event => setAdminDraft({ ...adminDraft, name: event.target.value })} /></label>}
            {adminForm === 'add-user' && <><label htmlFor="admin-email">Email<input id="admin-email" type="email" autoComplete="email" required value={adminDraft.email} onChange={event => setAdminDraft({ ...adminDraft, email: event.target.value })} /></label><label htmlFor="admin-password">Temporary password<input id="admin-password" type="password" autoComplete="new-password" minLength={8} maxLength={128} required value={adminDraft.password} onChange={event => setAdminDraft({ ...adminDraft, password: event.target.value })} /></label><label htmlFor="admin-role">Role<select id="admin-role" value={adminDraft.role} onChange={event => setAdminDraft({ ...adminDraft, role: event.target.value as Role })}><option value="Student">Student</option><option value="Teacher">Teacher</option><option value="Admin">Admin</option></select></label></>}
          </div>
          <div className="editor-actions"><button className="primary" disabled={busyAction === adminForm || (adminForm === 'assign-teacher' && !adminSubjects.length)}>{busyAction === adminForm ? 'Saving...' : 'Save'}</button><button type="button" className="secondary" disabled={busyAction === adminForm} onClick={() => setAdminForm(null)}>Cancel</button></div>
        </form>}
        <div className="manage-grid">
          <section className="manage-card"><div><h2>Users</h2><button type="button" className="text-btn" onClick={() => openAdminForm('add-user')}>Add user</button></div>{users.map(x => <div className="manage-item" key={x.id}><strong>{x.name}</strong><span>{x.role} &middot; {x.email}{x.courseId ? ` / ${courses.find(course => course.id === x.courseId)?.name || `course ${x.courseId}`}` : ''}</span>{x.id !== user.id && <button type="button" className="danger-btn inline" disabled={busyAction === `deactivate-${x.id}`} onClick={() => deactivateUser(x.id)}>{busyAction === `deactivate-${x.id}` ? 'Deactivating...' : 'Deactivate'}</button>}</div>)}{!loading && !users.length && <p className="compact-empty">No users found.</p>}</section>
          <section className="manage-card"><div><h2>Courses</h2><button type="button" className="text-btn" onClick={() => openAdminForm('add-course')}>Add course</button></div>{courses.map(x => <div className="manage-item" key={x.id}><strong>{x.name}</strong><span>ID {x.id}</span></div>)}{!loading && !courses.length && <p className="compact-empty">No courses yet.</p>}</section>
          <section className="manage-card"><div><h2>Subjects</h2><button type="button" className="text-btn" disabled={!courses.length} onClick={() => openAdminForm('add-subject')}>Add subject</button></div>{subjects.map(x => <div className="manage-item" key={x.id}><strong>{x.name}</strong><span>{courses.find(course => course.id === x.courseId)?.name || 'Unassigned course'}</span></div>)}{!loading && !subjects.length && <p className="compact-empty">No subjects yet. Add a course first.</p>}</section>
        </div>
      </>}
    </section>
  </main>;
}
