'use client';

import { useEffect, useMemo, useState } from 'react';

type Plan={code:string;name:string;audience:string;monthlyPriceFjd:number|null;annualPriceFjd:number|null;isPaid:boolean;entitlements:string[]};
const apiBase=process.env.NEXT_PUBLIC_API_URL??'https://fijilaw-api-production-production.up.railway.app';

const planDescriptions:Record<string,string>={
  free:'For people who want public legal information, limited AI triage and legal-service discovery without a paid dashboard.',
  personal_plus:'For individuals who need to save matters, keep assessment history and use member document and referral workflows.',
  lawyer_professional:'For individual practitioners who want a professional dashboard, profile management, enquiries, referrals and analytics.',
  firm_starter:'For small law firms that want a firm dashboard, listing, enquiries and core referral tools.',
  firm_professional:'For growing firms that need multiple users, stronger case/lead workflows and enhanced analytics.',
  firm_premium:'For firms that want the full professional toolkit plus enhanced visibility and clearly labelled promotional features.',
  institutional:'For Legal Aid, government, NGOs and justice-sector partners requiring organisation-level access and reporting.'
};

const highlights:Record<string,string[]>={
  free:['Public legal information','Limited AI legal triage','Find lawyers and Legal Aid','No paid dashboard'],
  personal_plus:['Paid member dashboard','Saved legal matters','Assessment history','Document workflows','Referral tracking'],
  lawyer_professional:['Lawyer dashboard','Professional profile','Leads and enquiries','Referral management','Analytics'],
  firm_starter:['Firm dashboard','Firm listing','Basic leads/referrals','Core analytics'],
  firm_professional:['Everything in Starter','Multiple practitioners/staff','Lead and case workflows','Enhanced analytics'],
  firm_premium:['Everything in Professional','Enhanced directory profile','Priority promotional placement','Advanced analytics'],
  institutional:['Organisation dashboard','Offices and users','Referral management','Privacy-preserving analytics']
};

export default function PricingPage(){
  const [plans,setPlans]=useState<Plan[]>([]); const [annual,setAnnual]=useState(false); const [loading,setLoading]=useState(true);
  useEffect(()=>{fetch(`${apiBase}/api/membership/plans`,{cache:'no-store'}).then(r=>r.json()).then(b=>setPlans(b.items??[])).catch(()=>{}).finally(()=>setLoading(false));},[]);
  const ordered=useMemo(()=>plans,[plans]);

  return <main style={{maxWidth:1180,margin:'0 auto',padding:'42px 24px 90px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'}}>
    <header style={{display:'flex',justifyContent:'space-between',gap:18,alignItems:'center',borderBottom:'1px solid #d5ddd7',paddingBottom:18}}>
      <a href="/" style={{fontWeight:800,color:'#16231c',textDecoration:'none',fontSize:21}}>FijiLaw AI</a>
      <div style={{display:'flex',gap:10,alignItems:'center'}}><a href="/account?mode=login" style={smallLink}>Sign In</a><a href="/account?mode=register" style={smallCta}>Register</a></div>
    </header>

    <section style={{padding:'58px 0 28px'}}>
      <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',margin:0}}>PRICING & MEMBERSHIP</p>
      <h1 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:'clamp(42px,7vw,68px)',lineHeight:1.02,margin:'10px 0 18px',maxWidth:920}}>Know the price before you create an account.</h1>
      <p style={{fontSize:19,lineHeight:1.65,color:'#58685e',maxWidth:820}}>FijiLaw AI keeps public access available for free. Registration is free, while paid memberships unlock the private dashboard and persistent legal workflows. Choose a plan first, then register or sign in.</p>
      <div style={{display:'flex',gap:10,flexWrap:'wrap',marginTop:22}}><span style={trustPill}>Free public access remains available</span><span style={trustPill}>Dashboard is a paid-member feature</span><span style={trustPill}>Prices shown in Fijian Dollars</span></div>
    </section>

    <section style={{background:'#173f2b',color:'#fff',borderRadius:20,padding:24,display:'grid',gridTemplateColumns:'1.2fr .8fr',gap:28,alignItems:'center',margin:'10px 0 34px'}}>
      <div><p style={{fontSize:12,letterSpacing:'.12em',fontWeight:800,color:'#c9d9cf',margin:'0 0 8px'}}>BEFORE YOU REGISTER</p><h2 style={{fontFamily:'Georgia,serif',fontSize:32,fontWeight:500,margin:'0 0 10px'}}>Choose whether you need a free account or a paid dashboard.</h2><p style={{lineHeight:1.6,color:'#d6e1da',margin:0}}>You can create a free account without paying. If you need saved cases, document history, referrals or professional tools, choose a paid plan below.</p></div>
      <div style={{display:'flex',gap:10,justifyContent:'flex-end',flexWrap:'wrap'}}><a href="/account?mode=register" style={lightCta}>Create Free Account</a><a href="#plans" style={outlineLight}>Compare Paid Plans</a></div>
    </section>

    <div style={{display:'flex',gap:8,margin:'28px 0'}}><button onClick={()=>setAnnual(false)} style={toggle(!annual)}>Monthly</button><button onClick={()=>setAnnual(true)} style={toggle(annual)}>Annual</button></div>

    <section id="plans">
      {loading&&<p style={{color:'#66746b'}}>Loading membership plans…</p>}
      <div style={{display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(250px,1fr))',gap:14}}>{ordered.map(p=>{
        const isPopular=p.code==='firm_professional'; const price=annual?p.annualPriceFjd:p.monthlyPriceFjd;
        return <article key={p.code} style={{background:'#fff',border:isPopular?'2px solid #173f2b':'1px solid #d5ddd7',borderRadius:18,padding:24,display:'flex',flexDirection:'column',minHeight:410,position:'relative'}}>
          {isPopular&&<span style={{position:'absolute',right:18,top:16,fontSize:11,fontWeight:800,letterSpacing:'.08em',background:'#e7efe9',padding:'6px 8px',borderRadius:999}}>POPULAR</span>}
          <p style={{fontSize:12,fontWeight:800,letterSpacing:'.1em',color:'#64736a',marginTop:0}}>{p.audience.toUpperCase()}</p>
          <h2 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,margin:'8px 0'}}>{p.name}</h2>
          <p style={{fontSize:15,lineHeight:1.55,color:'#627168',minHeight:70}}>{planDescriptions[p.code]??'Membership access for FijiLaw AI.'}</p>
          <p style={{fontSize:32,fontWeight:800,margin:'8px 0'}}>{p.monthlyPriceFjd===null?'Contact us':p.monthlyPriceFjd===0?'Free':`FJD $${price}`}</p>
          <small style={{color:'#6b786f'}}>{p.monthlyPriceFjd&&p.monthlyPriceFjd>0?(annual?'per year':'per month'):p.code==='free'?'No payment required':'Custom agreement'}</small>
          <ul style={{paddingLeft:18,lineHeight:1.75,color:'#536158',marginBottom:24}}>{(highlights[p.code]??p.entitlements.slice(0,6).map(x=>x.replaceAll('.',' '))).map(x=><li key={x}>{x}</li>)}</ul>
          <a href={p.code==='free'?'/account?mode=register':p.code==='institutional'?'/account?mode=register':`/account?mode=register&plan=${encodeURIComponent(p.code)}`} style={{marginTop:'auto',background:p.isPaid?'#173f2b':'#edf1ee',color:p.isPaid?'#fff':'#173f2b',padding:'12px 14px',borderRadius:10,textAlign:'center',textDecoration:'none',fontWeight:800}}>{p.code==='free'?'Register Free':p.code==='institutional'?'Contact / Register':'Choose Plan & Register'}</a>
        </article>})}</div>
    </section>

    <section style={{marginTop:42,padding:26,border:'1px solid #d5ddd7',borderRadius:18,background:'#f8faf8'}}>
      <h2 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,margin:'0 0 10px'}}>Important membership notes</h2>
      <ul style={{lineHeight:1.7,color:'#59685f',paddingLeft:20,marginBottom:0}}><li>Registering an account does not automatically create a paid subscription.</li><li>Paid dashboard access is controlled by the active subscription on the server.</li><li>Annual pricing currently reflects approximately two months free compared with paying monthly for 12 months.</li><li>Sponsored placement and advertising features are clearly labelled and kept separate from FijiLaw AI legal analysis and legal recommendations.</li></ul>
    </section>
  </main>;
}

const smallLink={color:'#173f2b',textDecoration:'none',fontWeight:700,padding:'10px 12px'} as const;
const smallCta={background:'#173f2b',color:'#fff',textDecoration:'none',fontWeight:800,padding:'10px 14px',borderRadius:10} as const;
const trustPill={border:'1px solid #b9c5bd',borderRadius:999,padding:'8px 11px',fontSize:12,color:'#55645b'} as const;
const lightCta={background:'#fff',color:'#173f2b',padding:'12px 16px',borderRadius:10,textDecoration:'none',fontWeight:800} as const;
const outlineLight={border:'1px solid rgba(255,255,255,.45)',color:'#fff',padding:'12px 16px',borderRadius:10,textDecoration:'none',fontWeight:800} as const;
function toggle(active:boolean){return {border:'1px solid #b8c4bc',borderRadius:999,padding:'9px 14px',background:active?'#173f2b':'transparent',color:active?'#fff':'#173f2b',fontWeight:700,cursor:'pointer'} as const;}
