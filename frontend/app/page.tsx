'use client';

import { useEffect, useMemo, useState } from 'react';
import { z } from 'zod';

const API = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5080/api';
type Role = 'Admin' | 'Teacher' | 'Student';
type User = { id: number; name: string; email: string; role: Role; courseId?: number };
type Assignment = { id: number; title: string; description: string; deadlineUtc: string; maximumMarks: number; status: string; allowUpdates: boolean; allowLateSubmission: boolean; courseId: number; subjectId: number; course: string; subject: string; teacher: string; submission?: { id: number; answer: string; fileUrl?: string; versionNumber: number; isLate: boolean; status: string; marks?: number; feedback?: string } };
type Submission = { id: number; answer: string; fileUrl?: string; assignment: string; assignmentId: number; maximumMarks: number; studentId: number; student: string; versionNumber: number; isLate: boolean; marks?: number; feedback?: string; status: string; updatedAtUtc: string };
type Option = { courseId: number; course: string; subjectId: number; subject: string };
type Catalog = { id: number; name: string; courseId?: number; isActive?: boolean };

function isImageUrl(url?: string) {
  return !!url && /\.(png|jpe?g)(?:$|[?#])/i.test(url);
}

const loginSchema = z.object({ email: z.string().email(), password: z.string().min(6) });

async function api(path: string, options: RequestInit = {}) {
  const token = localStorage.getItem('assignment_token');
  const response = await fetch(`${API}${path}`, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(options.headers || {}) }
  });
  if (!response.ok) {
    const body = await response.json().catch(() => ({}));
    throw new Error(body.error?.message || body.error || `Request failed (${response.status})`);
  }
  return response.status === 204 ? null : response.json();
}

async function upload(file: File) {
  const token = localStorage.getItem('assignment_token');
  const form = new FormData(); form.append('file', file);
  const response = await fetch(`${API}/uploads`, { method: 'POST', body: form, headers: token ? { Authorization: `Bearer ${token}` } : {} });
  const body = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(body.error || 'Upload failed.');
  return body.fileUrl as string;
}

export default function Home() {
  const [user, setUser] = useState<User | null>(null);
  const [email, setEmail] = useState('student@example.com');
  const [password, setPassword] = useState('Student123!');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
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

  useEffect(() => {
    const saved = localStorage.getItem('assignment_user');
    if (saved) setUser(JSON.parse(saved));
  }, []);
  useEffect(() => { if (user) refresh(); }, [user]);

  async function refresh() {
    setError('');
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
    } catch (e) { setError((e as Error).message); }
  }

  async function login(event: React.FormEvent) {
    event.preventDefault(); setError('');
    try {
      const body = loginSchema.parse({ email, password });
      const result = await api('/auth/login', { method: 'POST', body: JSON.stringify(body) });
      localStorage.setItem('assignment_token', result.token);
      localStorage.setItem('assignment_user', JSON.stringify(result.user));
      setUser(result.user);
    } catch (e) { setError(e instanceof z.ZodError ? e.issues[0].message : (e as Error).message); }
  }

  function logout() { localStorage.clear(); setUser(null); setAssignments([]); setSubmissions([]); }

  async function submit(id: number) {
    setBusy(true); setError('');
    try {
      await api(`/assignments/${id}/submissions`, { method: 'POST', body: JSON.stringify({ answer: answers[id] || assignments.find(x => x.id === id)?.submission?.answer || '', fileUrl: fileUrls[id] || assignments.find(x => x.id === id)?.submission?.fileUrl || null }) });
      await refresh(); setAnswers({ ...answers, [id]: '' });
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  async function grade(item: Submission) {
    const value = window.prompt(`Marks (0-${item.maximumMarks})`, String(item.marks ?? ''));
    if (value === null) return;
    const feedback = window.prompt('Feedback', item.feedback || '') || '';
    setBusy(true);
    try {
      await api(`/submissions/${item.id}/grade`, { method: 'POST', body: JSON.stringify({ marks: Number(value), feedback }) });
      await refresh();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  async function createAssignment(event: React.FormEvent) {
    event.preventDefault(); setBusy(true); setError('');
    try {
      await api('/assignments/', { method: 'POST', body: JSON.stringify({ ...newAssignment, deadlineUtc: new Date(newAssignment.deadlineUtc).toISOString() }) });
      setNewAssignment(x => ({ ...x, title: '', description: '', deadlineUtc: '' })); await refresh();
    } catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  async function addCatalog(kind: 'courses' | 'subjects') {
    const name = window.prompt(`New ${kind.slice(0, -1)} name`);
    if (!name) return;
    const courseId = kind === 'subjects' ? Number(window.prompt(`Course id (${courses.map(x => `${x.id}: ${x.name}`).join(', ')})`, String(courses[0]?.id || ''))) : undefined;
    try { await api(`/admin/${kind}`, { method: 'POST', body: JSON.stringify(kind === 'subjects' ? { name, courseId } : { name }) }); await refresh(); }
    catch (e) { setError((e as Error).message); }
  }

  async function assignmentAction(item: Assignment, action: 'publish' | 'archive' | 'deadline') {
    try {
      if (action === 'publish') await api(`/assignments/${item.id}/publish`, { method: 'PATCH' });
      if (action === 'archive' && window.confirm('Archive this assignment?')) await api(`/assignments/${item.id}`, { method: 'DELETE' });
      if (action === 'deadline') {
        const value = window.prompt('New deadline (YYYY-MM-DDTHH:mm)', item.deadlineUtc.slice(0, 16)); if (!value) return;
        await api(`/assignments/${item.id}`, { method: 'PUT', body: JSON.stringify({ title: item.title, description: item.description, deadlineUtc: new Date(value).toISOString(), maximumMarks: item.maximumMarks, courseId: item.courseId, subjectId: item.subjectId, status: item.status, allowUpdates: item.allowUpdates, allowLateSubmission: item.allowLateSubmission }) });
      }
      await refresh();
    } catch (e) { setError((e as Error).message); }
  }

  async function overrideStatus(item: Submission, status: 'NeedsRevision' | 'Late' | 'Submitted') {
    try { await api(`/submissions/${item.id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }); await refresh(); }
    catch (e) { setError((e as Error).message); }
  }

  async function assignTeacher() {
    const teachers = users.filter(x => x.role === 'Teacher');
    const teacherId = Number(window.prompt(`Teacher id (${teachers.map(x => `${x.id}: ${x.name}`).join(', ')})`, String(teachers[0]?.id || ''))); if (!teacherId) return;
    const courseId = Number(window.prompt(`Course id (${courses.map(x => `${x.id}: ${x.name}`).join(', ')})`, String(courses[0]?.id || ''))); if (!courseId) return;
    const available = subjects.filter(x => x.courseId === courseId); const subjectId = Number(window.prompt(`Subject id (${available.map(x => `${x.id}: ${x.name}`).join(', ')})`, String(available[0]?.id || ''))); if (!subjectId) return;
    try { await api('/admin/teacher-assignments', { method: 'POST', body: JSON.stringify({ teacherId, courseId, subjectId }) }); await refresh(); } catch (e) { setError((e as Error).message); }
  }

  async function enrollStudent() {
    const students = users.filter(x => x.role === 'Student');
    const studentId = Number(window.prompt(`Student id (${students.map(x => `${x.id}: ${x.name}`).join(', ')})`, String(students[0]?.id || ''))); if (!studentId) return;
    const courseId = Number(window.prompt(`Course id (${courses.map(x => `${x.id}: ${x.name}`).join(', ')})`, String(courses[0]?.id || ''))); if (!courseId) return;
    try { await api('/admin/enrollments', { method: 'POST', body: JSON.stringify({ studentId, courseId }) }); await refresh(); } catch (e) { setError((e as Error).message); }
  }

  async function uploadForAssignment(id: number, file?: File) {
    if (!file) return; setBusy(true);
    try { const fileUrl = await upload(file); setFileUrls(x => ({ ...x, [id]: fileUrl })); }
    catch (e) { setError((e as Error).message); } finally { setBusy(false); }
  }

  async function deactivateUser(id: number) {
    if (!window.confirm('Deactivate this user?')) return;
    try { await api(`/admin/users/${id}/deactivate`, { method: 'PATCH' }); await refresh(); } catch (e) { setError((e as Error).message); }
  }

  async function addUser() {
    const name = window.prompt('Full name'); if (!name) return;
    const email = window.prompt('Email'); if (!email) return;
    const password = window.prompt('Temporary password (6+ characters)'); if (!password) return;
    const role = (window.prompt('Role: Admin, Teacher, or Student', 'Student') || 'Student') as Role;
    const courseId = role === 'Student' ? Number(window.prompt(`Course id (${courses.map(x => `${x.id}: ${x.name}`).join(', ')})`, String(courses[0]?.id || ''))) : null;
    try { await api('/admin/users', { method: 'POST', body: JSON.stringify({ name, email, password, role, courseId }) }); await refresh(); }
    catch (e) { setError((e as Error).message); }
  }

  const published = useMemo(() => assignments.filter(x => x.status === 'Published').length, [assignments]);

  if (!user) return <main className="login-shell"><section className="login-card">
    <div className="brand-mark">CH</div><p className="eyebrow">ASSIGNMENT MANAGEMENT</p><h1>Make learning visible.</h1>
    <p className="muted">One calm place for classes, assignments, submissions, and feedback.</p>
    <form onSubmit={login}><label>Email<input value={email} onChange={e => setEmail(e.target.value)} type="email" /></label><label>Password<input value={password} onChange={e => setPassword(e.target.value)} type="password" /></label>{error && <p className="error">{error}</p>}<button className="primary">Sign in</button></form>
    <p className="hint">Admin: admin@example.com / Admin123!<br />Teacher: teacher@example.com / Teacher123!<br />Student: student@example.com / Student123!</p>
  </section></main>;

  return <main className="app-shell">
    <aside><div className="brand"><span className="brand-mark small">CH</span><span>Classroom Hub</span></div><div className="profile"><div className="avatar">{user.name.split(' ').map(x => x[0]).join('').slice(0, 2)}</div><div><strong>{user.name}</strong><small>{user.role}</small></div></div><nav><button className={tab === 'assignments' ? 'active' : ''} onClick={() => setTab('assignments')}>Assignments</button>{user.role !== 'Student' && <button className={tab === 'submissions' ? 'active' : ''} onClick={() => setTab('submissions')}>Submissions</button>}{user.role === 'Admin' && <button className={tab === 'manage' ? 'active' : ''} onClick={() => setTab('manage')}>Manage</button>}</nav><button className="logout" onClick={logout}>Sign out</button></aside>
    <section className="content"><header><div><p className="eyebrow">{user.role.toUpperCase()} SPACE</p><h1>{tab === 'submissions' ? 'Review submissions' : tab === 'manage' ? 'Administration' : 'Assignments'}</h1></div><button className="icon-btn" onClick={refresh} aria-label="Refresh">↻</button></header>{error && <div className="alert error">{error}</div>}

      {tab === 'assignments' && <><div className="stats"><div><span>Visible assignments</span><strong>{assignments.length}</strong></div><div><span>Published</span><strong>{published}</strong></div><div><span>Past due</span><strong>{assignments.filter(a => new Date(a.deadlineUtc) < new Date()).length}</strong></div></div>
        {user.role === 'Teacher' && <form className="create-form" onSubmit={createAssignment}><h2>Create assignment</h2><div className="form-grid"><input required placeholder="Title" value={newAssignment.title} onChange={e => setNewAssignment({ ...newAssignment, title: e.target.value })} /><input required type="datetime-local" value={newAssignment.deadlineUtc} onChange={e => setNewAssignment({ ...newAssignment, deadlineUtc: e.target.value })} /><input required type="number" min="1" value={newAssignment.maximumMarks} onChange={e => setNewAssignment({ ...newAssignment, maximumMarks: Number(e.target.value) })} /><select value={`${newAssignment.courseId}:${newAssignment.subjectId}`} onChange={e => { const [courseId, subjectId] = e.target.value.split(':').map(Number); setNewAssignment({ ...newAssignment, courseId, subjectId }); }}>{options.map(o => <option key={`${o.courseId}:${o.subjectId}`} value={`${o.courseId}:${o.subjectId}`}>{o.course} · {o.subject}</option>)}</select></div><textarea required placeholder="Description" value={newAssignment.description} onChange={e => setNewAssignment({ ...newAssignment, description: e.target.value })} /><div className="form-actions"><select value={newAssignment.status} onChange={e => setNewAssignment({ ...newAssignment, status: e.target.value })}><option value="Draft">Draft</option><option value="Published">Publish now</option></select><label className="check"><input type="checkbox" checked={newAssignment.allowUpdates} onChange={e => setNewAssignment({ ...newAssignment, allowUpdates: e.target.checked })} /> Resubmission</label><label className="check"><input type="checkbox" checked={newAssignment.allowLateSubmission} onChange={e => setNewAssignment({ ...newAssignment, allowLateSubmission: e.target.checked })} /> Late submission</label><button className="primary" disabled={busy}>Save assignment</button></div></form>}
        <div className="assignment-grid">{assignments.map(a => { const pastDue = new Date(a.deadlineUtc) < new Date(); return <article className="assignment-card" key={a.id}><div className="card-top"><span className={`pill ${pastDue ? 'late' : a.status.toLowerCase()}`}>{pastDue ? 'Past due' : a.status}</span><span className="date">Due {new Date(a.deadlineUtc).toLocaleString()}</span></div><h2>{a.title}</h2><p>{a.description}</p><div className="meta"><span>{a.course}</span><span>{a.subject}</span><span>{a.maximumMarks} marks</span>{a.allowLateSubmission && <span>Late allowed</span>}</div>{user.role === 'Teacher' && <div className="card-actions">{a.status === 'Draft' && <button className="text-btn" onClick={() => assignmentAction(a, 'publish')}>Publish</button>}<button className="text-btn" onClick={() => assignmentAction(a, 'deadline')}>Extend deadline</button><button className="danger-btn" onClick={() => assignmentAction(a, 'archive')}>Archive</button></div>}{user.role === 'Student' && <div className="submission-box"><textarea placeholder="Write your answer" value={answers[a.id] ?? a.submission?.answer ?? ''} onChange={e => setAnswers({ ...answers, [a.id]: e.target.value })} /><input type="url" placeholder="Optional file URL" value={fileUrls[a.id] ?? a.submission?.fileUrl ?? ''} onChange={e => setFileUrls({ ...fileUrls, [a.id]: e.target.value })} /><label className="file-field">Upload file (10 MB max)<input type="file" accept=".pdf,.docx,.zip,.jpg,.jpeg,.png" onChange={e => uploadForAssignment(a.id, e.target.files?.[0])} /></label><button className="primary" disabled={busy || (!a.allowUpdates && !!a.submission) || (pastDue && !a.allowLateSubmission)} onClick={() => submit(a.id)}>{a.submission ? 'Resubmit' : pastDue ? 'Submit late' : 'Submit answer'}</button>{a.submission && <p className="submission-note">{a.submission.status} · version {a.submission.versionNumber}{a.submission.marks !== undefined ? ` · ${a.submission.marks}/${a.maximumMarks}` : ''}{a.submission.feedback ? ` · ${a.submission.feedback}` : ''}</p>}</div>}</article>})}</div></>}

      {tab === 'submissions' && <div className="table-wrap"><table><thead><tr><th>Student</th><th>Assignment</th><th>Submitted work</th><th>Version</th><th>Marks</th><th>Status</th><th></th></tr></thead><tbody>{submissions.map(s => <tr key={s.id}><td>{s.student}</td><td>{s.assignment}</td><td className="submission-work">{s.answer ? <p className="submission-answer">{s.answer}</p> : <p className="submission-empty">No written answer.</p>}{s.fileUrl ? <div className="attachment-preview">{isImageUrl(s.fileUrl) && <a href={s.fileUrl} target="_blank" rel="noreferrer"><img src={s.fileUrl} alt={`${s.student}'s submitted attachment`} /></a>}<a className="text-link" href={s.fileUrl} target="_blank" rel="noreferrer">{isImageUrl(s.fileUrl) ? 'Open full image' : 'Open attachment'}</a></div> : <span className="submission-empty">No attachment</span>}</td><td>v{s.versionNumber}{s.isLate ? ' · late' : ''}</td><td>{s.marks ?? '—'} / {s.maximumMarks}</td><td><span className={`pill ${s.status.toLowerCase()}`}>{s.status}</span></td><td>{user.role === 'Teacher' && <div className="row-actions"><button className="text-btn" disabled={busy} onClick={() => grade(s)}>Grade</button><button className="text-btn" onClick={() => overrideStatus(s, 'NeedsRevision')}>Revision</button></div>}</td></tr>)}</tbody></table>{!submissions.length && <div className="empty">No submissions yet.</div>}</div>}

      {tab === 'manage' && <><div className="admin-actions"><button className="primary" onClick={assignTeacher}>Assign teacher</button><button className="primary" onClick={enrollStudent}>Enroll student</button></div><div className="manage-grid"><section className="manage-card"><div><h2>Users</h2><button className="text-btn" onClick={addUser}>+ Add</button></div>{users.map(x => <p key={x.id}><strong>{x.name}</strong><span>{x.role} · {x.email}{x.courseId ? ` · course ${x.courseId}` : ''}</span>{x.id !== user.id && <button className="danger-btn inline" onClick={() => deactivateUser(x.id)}>Deactivate</button>}</p>)}</section><section className="manage-card"><div><h2>Courses</h2><button className="text-btn" onClick={() => addCatalog('courses')}>+ Add</button></div>{courses.map(x => <p key={x.id}><strong>{x.name}</strong><span>ID {x.id}</span></p>)}</section><section className="manage-card"><div><h2>Subjects</h2><button className="text-btn" onClick={() => addCatalog('subjects')}>+ Add</button></div>{subjects.map(x => <p key={x.id}><strong>{x.name}</strong><span>ID {x.id} · course {x.courseId || 'unassigned'}</span></p>)}</section></div></>}
    </section>
  </main>;
}
