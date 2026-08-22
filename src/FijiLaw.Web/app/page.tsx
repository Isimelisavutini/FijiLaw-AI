'use client';

import { FormEvent, useState } from 'react';

type Result = {
  issue: string; facts: string[]; missingInformation: string[];
  authorities: { title: string; provision?: string; sourceUrl?: string }[];
  guidance: string; nextSteps: string[]; riskLevel: string | number;
  humanReviewRequired: boolean; disclaimer: string; correlationId: string;
};

const legalAreas = [
  ['Employment', 'Dismissal, wages, workplace rights'],
  ['Consumer Rights', 'Refunds, faulty goods, misleading conduct'],
  ['Family Law', 'Maintenance, parenting, marriage'],
  ['Land & iTaukei Land', 'Leases, landowners, customary land'],
  ['Criminal Procedure', 'Arrest, bail, charges, court process'],
  ['Domestic Violence', 'Protection and restraining orders']
];

function riskLabel(value: string | number) {
  if (typeof value === 'number') return value <= 0 ? 'Low' : value === 1 ? 'Medium' : 'High';
  if (value === '0') return 'Low';
  return value;
}

export default function Home() {
  const [situation, setSituation] = useState('');
  const [result, setResult] = useState<Result | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  async function submit(e: FormEvent) {
    e.preventDefault(); setLoading(true); setError(''); setResult(null);
    try {
      const base = process.env.NEXT_PUBLIC_API_URL ?? 'https://fijilaw-api-production-production.up.railway.app';
      const response = await fetch(`${base}/api/legal/triage`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ situation, language: 'en' })
      });
      if (!response.ok) throw new Error('The legal triage service could not process this request.');
      setResult(await response.json());
    } catch (e) { setError(e instanceof Error ? e.message : 'Unexpected error'); }
    finally { setLoading(false); }
  }

  return <main className="siteShell">
    <header className="siteHeader">
      <a className="brand" href="#top">FijiLaw AI</a>
      <nav className="navLinks">
        <a href="#legal-help">Legal help</a>
        <a href="#how-it-works">How it works</a>
        <a href="#trust">Trust & safety</a>
      </nav>
      <a className="headerCta" href="#triage">Start assessment</a>
    </header>

    <section className="hero" id="top">
      <div className="heroCopy">
        <p className="eyebrow">ACCESS TO JUSTICE · FIJI</p>
        <h1>Understand your rights. Know your next step.</h1>
        <p className="lead">FijiLaw AI helps you organize a legal problem, find relevant Fiji law, understand what information matters, and know when a qualified lawyer should review your case.</p>
        <div className="heroActions">
          <a className="primaryCta" href="#triage">Tell me what happened</a>
          <a className="secondaryCta" href="#how-it-works">See how it works</a>
        </div>
        <div className="trustStrip">
          <span>Verified Fiji-law sources</span>
          <span>Human review for high-risk matters</span>
          <span>English · iTaukei · Fiji Hindi</span>
        </div>
      </div>
      <aside className="heroCard">
        <span className="cardKicker">AI LEGAL TRIAGE</span>
        <h2>Start with your story.</h2>
        <p>You do not need to know the name of an Act or legal term. Describe the situation in your own words.</p>
        <div className="miniFlow"><span>1</span><p>Explain what happened</p></div>
        <div className="miniFlow"><span>2</span><p>FijiLaw identifies the legal area</p></div>
        <div className="miniFlow"><span>3</span><p>Review verified authorities and next steps</p></div>
      </aside>
    </section>

    <section className="section" id="legal-help">
      <div className="sectionHeading">
        <div><p className="eyebrow">LEGAL HELP</p><h2>Common areas FijiLaw AI can help you navigate.</h2></div>
        <p>Start with a category or simply describe your situation. The system will classify the matter and retrieve relevant Fiji legal sources.</p>
      </div>
      <div className="categoryGrid">
        {legalAreas.map(([title, desc]) => <button type="button" className="categoryCard" key={title} onClick={() => { setSituation(`I need help with ${title}. `); document.getElementById('triage')?.scrollIntoView({ behavior: 'smooth' }); }}>
          <span className="categoryArrow">↗</span><strong>{title}</strong><small>{desc}</small>
        </button>)}
      </div>
    </section>

    <section className="triageSection" id="triage">
      <div className="triageIntro"><p className="eyebrow">TELL ME MY RIGHTS</p><h2>What happened?</h2><p>Describe the important facts in plain language. Include dates, what happened, who was involved, and what outcome you are seeking where possible.</p></div>
      <section className="panel">
        <form onSubmit={submit}>
          <label htmlFor="situation">Your legal situation</label>
          <textarea id="situation" required minLength={10} value={situation} onChange={e => setSituation(e.target.value)} placeholder="Example: My employer terminated me yesterday without giving me a written reason..." />
          <div className="row"><small>Do not include passwords, banking PINs, or unnecessary sensitive information.</small><button className="submitButton" disabled={loading}>{loading ? 'Assessing…' : 'Start legal triage'}</button></div>
        </form>
      </section>
    </section>

    {error && <p className="error">{error}</p>}
    {result && <section className="result">
      <div className="resultHead"><div><p className="eyebrow">INITIAL ASSESSMENT</p><h2>{result.issue}</h2></div><span className="risk">{riskLabel(result.riskLevel)} risk</span></div>
      {result.humanReviewRequired && <div className="warning"><strong>Human legal review recommended.</strong> This matter should not rely on AI alone.</div>}
      <h3>Guidance</h3><p>{result.guidance}</p>
      <h3>Information still needed</h3><ul>{result.missingInformation.map(x => <li key={x}>{x}</li>)}</ul>
      <h3>Next steps</h3><ol>{result.nextSteps.map(x => <li key={x}>{x}</li>)}</ol>
      <h3>Verified authorities</h3>{result.authorities.length ? <ul className="authorityList">{result.authorities.map((a, i) => <li key={`${a.title}-${a.provision}-${i}`}><strong>{a.title}</strong>{a.provision ? ` — ${a.provision}` : ''}{a.sourceUrl ? <a href={a.sourceUrl} target="_blank" rel="noreferrer">View official source ↗</a> : null}</li>)}</ul> : <p>No verified legal authorities are connected yet. The system intentionally does not invent citations.</p>}
      <p className="disclaimer">{result.disclaimer}<br />Reference: {result.correlationId}</p>
    </section>}

    <section className="section howSection" id="how-it-works">
      <div className="sectionHeading"><div><p className="eyebrow">HOW IT WORKS</p><h2>Legal guidance with controlled AI.</h2></div><p>FijiLaw AI is designed to retrieve law first, reason second, and escalate when a matter requires professional review.</p></div>
      <div className="stepsGrid">
        <article><span>01</span><h3>Understand the facts</h3><p>The system organizes your description into legal issues, parties, dates, evidence and missing information.</p></article>
        <article><span>02</span><h3>Retrieve Fiji law</h3><p>Relevant Acts and provisions are matched from a curated and progressively expanding Fiji legal knowledge base.</p></article>
        <article><span>03</span><h3>Assess the next step</h3><p>You receive a structured assessment, verified authorities and a clear indication when human legal review is required.</p></article>
      </div>
    </section>

    <section className="trustSection" id="trust">
      <div><p className="eyebrow light">TRUST & SAFETY</p><h2>Built to assist people, not invent law.</h2></div>
      <div className="trustGrid">
        <article><strong>Verified sources</strong><p>Legal authorities are surfaced with official-source links rather than generated from memory alone.</p></article>
        <article><strong>Human escalation</strong><p>Higher-risk matters can be flagged for review by a qualified legal practitioner.</p></article>
        <article><strong>Traceable assessments</strong><p>Each assessment receives a reference identifier to support future audit and case workflows.</p></article>
        <article><strong>Privacy-first design</strong><p>Users are prompted to avoid unnecessary sensitive information while secure case handling is developed.</p></article>
      </div>
    </section>

    <section className="languageSection">
      <p className="eyebrow">BUILT FOR FIJI</p>
      <h2>Legal access should not depend on where you live or the language you are most comfortable using.</h2>
      <div className="languagePills"><span>English</span><span>iTaukei</span><span>Fiji Hindi</span><span>More languages planned</span></div>
    </section>

    <footer><div><strong>FijiLaw AI</strong><p>AI-assisted access to legal information in Fiji.</p></div><p>Supervised MVP · Not a substitute for a qualified legal practitioner.</p></footer>
  </main>;
}
