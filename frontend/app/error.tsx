'use client';

export default function ErrorPage({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <main className="login-shell">
      <section className="login-card" aria-labelledby="error-title">
        <div className="brand-mark" aria-hidden="true">CH</div>
        <p className="eyebrow">SOMETHING WENT WRONG</p>
        <h1 id="error-title">We could not load this page.</h1>
        <p className="muted" role="alert">Try again. If the problem continues, return later or contact the system administrator.</p>
        <button className="primary" type="button" onClick={reset}>Try again</button>
      </section>
    </main>
  );
}
