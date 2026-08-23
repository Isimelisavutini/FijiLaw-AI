'use client';

import { useEffect, useMemo, useState } from 'react';
import { API_BASE, fetchWithTimeout } from '../../lib/api';

type Plan={code:string;name:string;audience:string;monthlyPriceFjd:number|null;annualPriceFjd:number|null;isPaid:boolean;entitlements:string[]};

const fallbackPlans:Plan[]=[
  {code:'free',name:'Free',audience:'citizen',monthlyPriceFjd:0,annualPriceFjd:0,isPaid:false,entitlements:[]},
  {code:'personal_plus',name:'Personal Plus',audience:'citizen',monthlyPriceFjd:20,annualPriceFjd:200,isPaid:true,entitlements:['Dashboard.Access','Cases.Create','Cases.ViewOwn','Documents.Analyse','Documents.Store','Referrals.Request']},
  {code:'lawyer_professional',name:'Lawyer Professional',audience:'lawyer',monthlyPriceFjd:100,annualPriceFjd:1000,isPaid:true,entitlements:['Dashboard.Access','Cases.Manage','Documents.Analyse','Referrals.Manage','Leads.View','Analytics.View']},
  {code:'firm_starter',name:'Law Firm Starter',audience:'law_firm',monthlyPriceFjd:200,annualPriceFjd:2000,isPaid:true,entitlements:['Dashboard.Access','Cases.Manage','Referrals.Manage','Leads.View','Firm.Manage','Analytics.View']},
  {code:'firm_professional',name:'Law Firm Professional',audience:'law_firm',monthlyPriceFjd:350,annualPriceFjd:3500,isPaid:true,entitlements:['Dashboard.Access','Cases.Manage','Referrals.Manage','Leads.Manage','Firm.Manage','FirmUsers.Manage','Analytics.View']},
  {code:'firm_premium',name:'Law Firm Premium',audience:'law_firm',monthlyPriceFjd:600,annualPriceFjd:6000,isPaid:true,entitlements:['Dashboard.Access','Cases.Manage','Referrals.Manage','Leads.Manage','Firm.Manage','FirmUsers.Manage','Analytics.View','Directory.PriorityPlacement']},
  {code:'institutional',name:'Institutional',audience:'institution',monthlyPriceFjd:null,annualPriceFjd:null,isPaid:true,entitlements:['Dashboard.Access']}
];

const includedCredits:Record<string,string>={free:'10 introductory FijiLaw Credits',personal_plus:'100 FijiLaw Credits / month',lawyer_professional:'700 FijiLaw Credits / month',firm_starter:'1,500 FijiLaw Credits / month',firm_professional:'3,500 FijiLaw Credits / month',firm_premium:'7,500 FijiLaw Credits / month',institutional:'5,000 FijiLaw Credits / month default'};
const planDescriptions:Record<string,string>={
  free:'For people who want public legal information, legal-service discovery and an introductory FijiLaw AI credit allowance.',
  personal_plus:'For individuals who need to save matters, keep assessment history and use member document and referral workflows.',
  lawyer_professional:'For individual practitioners who want a professional dashboard, profile management, enquiries, referrals and analytics.',
  firm_starter:'For small law firms that want a firm dashboard, listing, enquiries and core referral tools.',
  firm_professional:'For growing firms that need multiple users, stronger case/lead workflows and enhanced analytics.',
  firm_premium:'For firms that want the full professional toolkit plus enhanced visibility and clearly labelled promotional features.',
  institutional:'For Legal Aid, government, NGOs and justice-sector partners requiring organisation-level access and reporting.'
};

const highlights:Record<string,string[]>={
  free:['Public legal information',includedCredits.free,'Find lawyers and Legal Aid','No paid dashboard'],
  personal_plus:['Paid member dashboard',includedCredits.personal_plus,'Saved legal matters','Document workflows','Referral tracking'],
  lawyer_professional:['Lawyer dashboard',includedCredits.lawyer_professional,'Professional profile','Leads and enquiries','Referral management','Analytics'],
  firm_starter:['Firm dashboard',includedCredits.firm_starter,'Firm listing','Basic leads/referrals','Core analytics'],
  firm_professional:['Everything in Starter',includedCredits.firm_professional,'Multiple practitioners/staff','Lead and case workflows','Enhanced analytics'],
  firm_premium:['Everything in Professional',includedCredits.firm_premium,'Enhanced directory profile','Priority promotional placement','Advanced analytics'],
  institutional:['Organisation dashboard',includedCredits.institutional,'Offices and users','Referral management','Privacy-preserving analytics']
};

export default function PricingPage(){
  const [plans,setPlans]=useState<Plan[]>(fallbackPlans); const [annual,setAnnual]=useState(false); const [source,setSource]=useState<'api'|'fallback'>('fallback');
  useEffect(()=>{void loadPlans();},[]);
  async function loadPlans(){
    try{const response=await fetchWithTimeout(`${API_BASE}/api/membership/plans`,{cache:'no-store'},8000);if(!response.ok)return;const body=await response.json();if(Array.isArray(body.items)&&body.items.length){setPlans(body.items);setSource('api');}}catch{/* keep reviewed fallback pricing visible */}
  }
  const ordered=useMemo(()=>plans,[plans]);

  return <main style={{maxWidth:1180,margin:'0 auto',padding:'42px 24px 90px',fontFamily:'Inter,system-ui,sans-serif',color:'#081B2D'}}>
    <header style={{display:'flex',justifyContent:'space-between',gap:18,alignItems:'center',borderBottom:'1px solid #CBD5DD',paddingBottom:18}}>
      <a href="/" style={{fontWeight:900,color:'#0E2A47',textDecoration:'none',fontSize:21}}>FijiLaw AI</a>
      <div style={{display:'flex',gap:10,alignItems:'center'}}><a href="/credits" style={smallLink}>AI Credits</a><a href="/account?mode=login" style={smallLink}>Sign In</a><a href="/account?mode=register" style={smallCta}>Register</a></div>
    </header>
    <section style={{padding:'58px 0 28px'}}><p style={{letterSpacing:'.14em',fontSize:12,fontWeight:900,color:'#667684',margin:0}}>PRICING & MEMBERSHIP</p><h1 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:'clamp(42px,7vw,68px)',lineHeight:1.02,margin:'10px 0 18px',maxWidth:920,color:'#0E2A47'}}>Membership plus FijiLaw AI Credits.</h1><p style={{fontSize:19,lineHeight:1.65,color:'#566674',maxWidth:820}}>Registration is free. Paid memberships unlock the private dashboard and include monthly FijiLaw Credits for AI-assisted legal services. Members can buy additional credit top-ups when needed.</p><div style={{display:'flex',gap:10,flexWrap:'wrap',marginTop:22}}><span style={trustPill}>FijiLaw Credits—not OpenAI tokens</span><span style={trustPill}>Dashboard is a paid-member feature</span><span style={trustPill}>Prices shown in Fijian Dollars</span></div></section>
    <section style={{position:'relative',overflow:'hidden',background:'linear-gradient(135deg,#081B2D,#0E2A47)',color:'#fff',borderRadius:20,padding:26,display:'grid',gridTemplateColumns:'1.2fr .8fr',gap:28,alignItems:'center',margin:'10px 0 34px',borderBottom:'3px solid #E5A93C',boxShadow:'0 22px 48px rgba(8,27,45,.15)'}}><div><p style={{fontSize:12,letterSpacing:'.12em',fontWeight:900,color:'#F4D28A',margin:'0 0 8px'}}>HOW AI BILLING WORKS</p><h2 style={{fontFamily:'Georgia,serif',fontSize:32,fontWeight:500,margin:'0 0 10px'}}>Your plan includes credits. AI services deduct a known number of credits.</h2><p style={{lineHeight:1.6,color:'#D2DEE6',margin:0}}>Advanced Legal Triage currently costs 10 credits and document analysis 15 credits. Failed workflows automatically refund reserved credits.</p></div><div style={{display:'flex',gap:10,justifyContent:'flex-end',flexWrap:'wrap'}}><a href="/credits" style={lightCta}>View Credit Store</a><a href="#plans" style={outlineLight}>Compare Plans</a></div></section>
    {source==='fallback'&&<p role="status" style={{background:'#fff7e8',border:'1px solid #e5c978',padding:'12px 14px',borderRadius:10,color:'#71551e'}}>Live plan data is temporarily unavailable. FijiLaw AI is showing the configured pricing catalogue so you can still review membership options.</p>}
    <div style={{display:'flex',gap:8,margin:'28px 0'}}><button onClick={()=>setAnnual(false)} style={toggle(!annual)}>Monthly</button><button onClick={()=>setAnnual(true)} style={toggle(annual)}>Annual</button></div>
    <section id="plans"><div style={{display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(250px,1fr))',gap:14}}>{ordered.map(p=>{const isPopular=p.code==='firm_professional';const price=annual?p.annualPriceFjd:p.monthlyPriceFjd;return <article key={p.code} style={{background:'#fff',border:isPopular?'2px solid #E5A93C':'1px solid #CBD5DD',borderRadius:16,padding:24,display:'flex',flexDirection:'column',minHeight:430,position:'relative',boxShadow:isPopular?'0 18px 42px rgba(8,27,45,.12)':'0 10px 28px rgba(8,27,45,.05)'}}>{isPopular&&<span style={{position:'absolute',right:18,top:16,fontSize:11,fontWeight:900,letterSpacing:'.08em',background:'#E5A93C',color:'#081B2D',padding:'6px 8px',borderRadius:999}}>POPULAR</span>}<p style={{fontSize:12,fontWeight:900,letterSpacing:'.1em',color:'#667684',marginTop:0}}>{p.audience.toUpperCase()}</p><h2 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,margin:'8px 0',color:'#0E2A47'}}>{p.name}</h2><p style={{fontSize:15,lineHeight:1.55,color:'#5A6A78',minHeight:70}}>{planDescriptions[p.code]??'Membership access for FijiLaw AI.'}</p><p style={{fontSize:32,fontWeight:900,margin:'8px 0',color:'#0E2A47'}}>{p.monthlyPriceFjd===null?'Contact us':p.monthlyPriceFjd===0?'Free':`FJD $${price}`}</p><small style={{color:'#6C7A86'}}>{p.monthlyPriceFjd&&p.monthlyPriceFjd>0?(annual?'per year':'per month'):p.code==='free'?'No payment required':'Custom agreement'}</small><ul style={{paddingLeft:18,lineHeight:1.75,color:'#526270',marginBottom:24}}>{(highlights[p.code]??p.entitlements.slice(0,6).map(x=>x.replaceAll('.',' '))).map(x=><li key={x}>{x}</li>)}</ul><a href={p.code==='free'?'/account?mode=register':p.code==='institutional'?'/account?mode=register':`/account?mode=register&plan=${encodeURIComponent(p.code)}`} style={{marginTop:'auto',background:p.isPaid?'#0E2A47':'#EDF2F5',color:p.isPaid?'#fff':'#0E2A47',padding:'12px 14px',borderRadius:9,textAlign:'center',textDecoration:'none',fontWeight:900,borderBottom:p.isPaid?'3px solid #E5A93C':'none'}}>{p.code==='free'?'Register Free':p.code==='institutional'?'Contact / Register':'Choose Plan & Register'}</a></article>})}</div></section>
    <section style={{marginTop:42,padding:26,border:'1px solid #CBD5DD',borderRadius:16,background:'#F7FAFC'}}><h2 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,margin:'0 0 10px',color:'#0E2A47'}}>Important membership and credit notes</h2><ul style={{lineHeight:1.7,color:'#566674',paddingLeft:20,marginBottom:0}}><li>FijiLaw Credits are usage units for FijiLaw AI services; they are not OpenAI API tokens, cash or cryptocurrency.</li><li>Registering an account does not automatically create a paid subscription.</li><li>Real credit top-ups are only granted after server-side payment confirmation.</li><li>Paid dashboard access is controlled by the active subscription on the server.</li><li>Sponsored placement and advertising features are clearly labelled and kept separate from FijiLaw AI legal analysis and neutral legal recommendations.</li></ul></section>
  </main>;
}

const smallLink={color:'#0E2A47',textDecoration:'none',fontWeight:800,padding:'10px 12px'} as const;
const smallCta={background:'#E5A93C',color:'#081B2D',textDecoration:'none',fontWeight:900,padding:'10px 14px',borderRadius:9} as const;
const trustPill={border:'1px solid #C4CFD7',borderRadius:999,padding:'8px 11px',fontSize:12,color:'#526270',background:'#fff'} as const;
const lightCta={background:'#E5A93C',color:'#081B2D',padding:'12px 16px',borderRadius:9,textDecoration:'none',fontWeight:900} as const;
const outlineLight={border:'1px solid rgba(229,169,60,.55)',color:'#F4D28A',padding:'12px 16px',borderRadius:9,textDecoration:'none',fontWeight:900} as const;
function toggle(active:boolean){return {border:'1px solid #B8C4CD',borderRadius:999,padding:'9px 14px',background:active?'#0E2A47':'#fff',color:active?'#fff':'#0E2A47',fontWeight:800,cursor:'pointer',boxShadow:active?'inset 0 -3px 0 #E5A93C':'none'} as const;}
