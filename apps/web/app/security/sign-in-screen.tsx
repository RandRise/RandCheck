"use client";

import "./security.css";

export function SignInScreen({ configured = false, busy = false, message = "", onSignIn, onSignOut }: {
  configured?: boolean; busy?: boolean; message?: string; onSignIn?: () => void; onSignOut?: () => void;
}) {
  return <main className="signin-page">
    <div className="security-wordmark"><span aria-hidden="true">R.</span> RandCheck</div>
    <div className="signin-grid">
      <section className="signin-story">
        <h1>Your document workspace</h1>
        <p className="signin-lead">Review documents, reconcile the details, and trace every decision back to its source.</p>
        <div className="signin-principles">
          <div><b>01</b><p><strong>One trusted identity</strong><span>Use your organization’s Microsoft account.</span></p></div>
          <div><b>02</b><p><strong>Access with purpose</strong><span>Your organization sets what you can view and change.</span></p></div>
          <div><b>03</b><p><strong>A record of important changes</strong><span>Membership and workflow changes leave an audit trail.</span></p></div>
        </div>
      </section>
      <section className="signin-card" aria-labelledby="signin-title">
        <h2 id="signin-title">Sign in to your<br />workspace.</h2>
        <p>Continue with the work account that your administrator has given access.</p>
        <button className="microsoft-button" disabled={!configured || busy} onClick={onSignIn}>
          <span className="microsoft-mark" aria-hidden="true"><i/><i/><i/><i/></span>
          {busy ? "Connecting…" : "Continue with Microsoft"}
        </button>
        {message && <p className="security-alert" role="alert">{message}</p>}
        {onSignOut && <button className="security-button secondary" onClick={onSignOut}>Retry sign-out</button>}
        {!configured && <div className="signin-setup"><strong>Sign-in is being configured</strong><p>Your administrator needs to connect Microsoft Entra before you can sign in.</p></div>}
        <div className="signin-help"><strong>Need access?</strong><p>Ask your organization’s administrator to add your work account. Signing in does not create a membership.</p></div>
        <small>Access is checked on every request.</small>
      </section>
    </div>
    <footer className="signin-footer"><span>RandCheck</span><span>Document review · Reconciliation · Evidence</span></footer>
  </main>;
}
