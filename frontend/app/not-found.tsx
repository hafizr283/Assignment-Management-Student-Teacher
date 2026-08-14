import Link from 'next/link';

export default function NotFound() {
  return (
    <main className="login-shell">
      <section className="login-card" aria-labelledby="not-found-title">
        <div className="brand-mark" aria-hidden="true">CH</div>
        <p className="eyebrow">PAGE NOT FOUND</p>
        <h1 id="not-found-title">This page is not available.</h1>
        <p className="muted">The address may be incorrect, or the page may have moved.</p>
        <Link className="primary" href="/" style={{ display: 'inline-block', marginTop: 16, textDecoration: 'none' }}>
          Return to Classroom Hub
        </Link>
      </section>
    </main>
  );
}
