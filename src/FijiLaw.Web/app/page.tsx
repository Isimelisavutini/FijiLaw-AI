'use client';

import { FormEvent, useState } from 'react';

type Result = {
  issue: string; facts: string[]; missingInformation: string[];
  authorities: { title: string; provision?: string; sourceUrl?: string }[];
  guidance: string; nextSteps: string[]; riskLevel: string;
  humanReviewRequired: boolean; disclaimer: string; correlationId: string;
};

export default function Home() {
  const [situation, setSituation] = useState('');
  const [result, setResult] = useState<Result | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  async function submit(e: FormEvent) {
    e.preventDefault(); setLoading(true); setError(''); setResult(null);
    try {
      const base = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000';
      const response = await fetch(`${base}/api/legal/triage`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ situation, language: 'en' })
      });
      if (!response.ok) throw new Error('The legal triage service could not process this request.');
      setResult(await response.json());
    } catch (e) { setError(e instanceof Error ? e.message : 'Unexpected error'); }
    finally { setLoading(false); }
  }

  return <main className="shell">
    <header><span className="brand">FijiLaw AI</span><span className="badge">Supervised MVP</span></header>
    <section className="hero">
      <p className="eyebrow">ACCESS TO JUSTICE · FIJI</p>
      <h1>Understand your legal situation.</h1>
      <p className="lead">Describe what happened. FijiLaw AI will organize the issue, identify information still needed, and prepare safe next steps.</p>
    </section>
    <section className="panel">
      <form onSubmit={submit}>
        <label htmlFor="situation">What happened?</label>
        <textarea id="situation" required minLength={10} value={situation} onChange={e => setSituation(e.target.value)} placeholder="Example: My employer terminated me yesterday without giving me a written reason..." />
        <div className="row"><small>Do not include passwords, banking PINs, or unnecessary sensitive information.</small><button disabled={loading}>{loading ? 'Assessing…' : 'Start legal triage'}</button></div>
      </form>
    </section>
    {error && <p className="error">{error}</p>}
    {result && <section className="result">
      <div className="resultHead"><div><p className="eyebrow">INITIAL ASSESSMENT</p><h2>{result.issue}</h2></div><span className="risk">{result.riskLevel} risk</span></div>
      {result.humanReviewRequired && <div className="warning"><strong>Human legal review recommended.</strong> This matter should not rely on AI alone.</div>}
      <h3>Guidance</h3><p>{result.guidance}</p>
      <h3>Information still needed</h3><ul>{result.missingInformation.map(x => <li key={x}>{x}</li>)}</ul>
      <h3>Next steps</h3><ol>{result.nextSteps.map(x => <li key={x}>{x}</li>)}</ol>
      <h3>Verified authorities</h3>{result.authorities.length ? <ul>{result.authorities.map(a => <li key={a.title}>{a.title}{a.provision ? ` — ${a.provision}` : ''}</li>)}</ul> : <p>No verified legal authorities are connected yet. The system intentionally does not invent citations.</p>}
      <p className="disclaimer">{result.disclaimer}<br />Reference: {result.correlationId}</p>
    </section>}
  </main>;
}
