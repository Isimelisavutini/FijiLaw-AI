'use client';

import { useEffect, useState } from 'react';

const apiBase = process.env.NEXT_PUBLIC_API_URL ?? 'https://fijilaw-api-production-production.up.railway.app';

type Dashboard = { userId:string; email:string; displayName?:string; planCode:string; subscriptionStatus:string; roles:string[]; permissions:string[]; dashboardAccess:boolean; };
type Member = { userId:string; email:string; displayName?:string; emailVerified:boolean; planCode:string; subscriptionStatus:string; roles:string[]; permissions:string[]; };

export default function DashboardPage(){
  const [data,setData]=useState<Dashboard|null>(null); const [member,setMember]=useState<Member|null>(null);
  const [error,setError]=useState(''); const [loading,setLoading]=useState(true); const [upgrade,setUpgrade]=useState(false); const [unverified,setUnverified]=useState(false);

  useEffect(()=>{ void load(); },[]);
  async function load(){
    const token=sessionStorage.getItem('fijilaw_access_token');
    if(!token){window.location.href='/account';return;}
    try{
      const meResponse=await fetch(`${apiBase}/api/membership/me`,{headers:{Authorization:`Bearer ${token}`},cache:'no-store'});
      if(meResponse.status===401){sessionStorage.removeItem('fijilaw_access_token');window.location.href='/account';return;}
      const meBody=await meResponse.json().catch(()=>({}));
      if(!meResponse.ok) throw new Error(meBody.error??meBody.detail??'Member profile could not be loaded.');
      setMember(meBody);

      const response=await fetch(`${apiBase}/api/dashboard`,{headers:{Authorization:`Bearer ${token}`},cache:'no-store'});
      const body=await response.json().catch(()=>({}));
      if(response.status===401){sessionStorage.removeItem('fijilaw_access_token');window.location.href='/account';return;}
      if(response.status===403){
        setUnverified(meBody.emailVerified===false && meBody.planCode!=='free');
        setUpgrade(meBody.planCode==='free' || !meBody.permissions?.includes('Dashboard.Access'));
        setError(body.error??'Your dashboard is currently locked.');
        return;
      }
      if(!response.ok) throw new Error(body.error??body.detail??'Dashboard could not be loaded.');
      setData(body);
    }catch(e){setError(e instanceof Error?e.message:'Dashboard could not be loaded.');}
    finally{setLoading(false);}
  }

  async function requestVerification(){
    if(!member?.email)return;
    try{
      const response=await fetch(`${apiBase}/api/auth/request-email-verification`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:member.email})});
      const body=await response.json().catch(()=>({}));
      setError(body.deliveryConfigured===false
        ? 'Verification was prepared, but outbound verification email delivery is not enabled yet.'
        : body.message??'Verification request created.');
    }catch{setError('Verification request could not be created.');}
  }

  async function logout(){const token=sessionStorage.getItem('fijilaw_access_token'); if(token) await fetch(`${apiBase}/api/auth/logout`,{method:'POST',headers:{Authorization:`Bearer ${token}`}}).catch(()=>{}); sessionStorage.clear(); window.location.href='/';}

  if(loading)return <main style={shell}><h1>Loading dashboard…</h1></main>;

  if(unverified)return <main style={shell}><a href="/" style={brand}>FijiLaw AI</a><p style={eyebrow}>EMAIL VERIFICATION REQUIRED</p><h1 style={title}>Verify your email to unlock paid dashboard access.</h1><p style={lead}>{error}</p><div style={card}><h2>Why this is required</h2><p style={{lineHeight:1.7,color:'#53635a'}}>Paid legal workflows can contain sensitive information. FijiLaw AI requires a verified member email before enabling dashboard features for paid accounts.</p><button onClick={requestVerification} style={ctaButton}>Request verification email</button><a href="/verify-email" style={{...cta,marginLeft:10}}>I have a verification token</a></div><button onClick={logout} style={secondary}>Sign out</button></main>;

  if(upgrade)return <main style={shell}><a href="/" style={brand}>FijiLaw AI</a><p style={eyebrow}>PAID MEMBER DASHBOARD</p><h1 style={title}>Unlock your FijiLaw Dashboard.</h1><p style={lead}>{error}</p><div style={card}><h2>Dashboard features</h2><ul style={{lineHeight:1.8}}><li>Saved legal matters and assessment history</li><li>Document analysis and storage entitlements</li><li>Referral tracking and saved lawyers</li><li>Member billing and plan controls</li></ul><a href="/pricing" style={cta}>View membership plans</a></div><button onClick={logout} style={secondary}>Sign out</button></main>;
  if(error)return <main style={shell}><h1>Dashboard unavailable</h1><p>{error}</p><a href="/pricing">View plans</a></main>;

  return <main style={shell}><div style={{display:'flex',justifyContent:'space-between',alignItems:'center',gap:16}}><a href="/" style={brand}>FijiLaw AI</a><div style={{display:'flex',gap:10,alignItems:'center'}}><a href="/pricing" style={{...secondary,textDecoration:'none',color:'#16231c'}}>Plans</a><button onClick={logout} style={secondary}>Sign out</button></div></div><p style={eyebrow}>MEMBER DASHBOARD</p><h1 style={title}>Welcome{data?.displayName?`, ${data.displayName}`:''}.</h1><p style={lead}>Your current plan is <strong>{data?.planCode.replaceAll('_',' ')}</strong>. Dashboard access is enforced from your active server-side subscription entitlements.</p><div style={{display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(220px,1fr))',gap:14,marginTop:32}}><section style={card}><h3>My Legal Matters</h3><p>Saved case workflows will appear here.</p></section><section style={card}><h3>Documents</h3><p>Paid document analysis and storage controls.</p></section><section style={card}><h3>Referrals</h3><p>Track lawyer and Legal Aid referral activity.</p></section><section style={card}><h3>Membership</h3><p>{data?.subscriptionStatus} · {data?.planCode}</p><p style={{fontSize:13,color:'#66746b'}}>Email verified: {member?.emailVerified?'Yes':'No'}</p></section></div></main>;
}

const shell={maxWidth:1100,margin:'0 auto',padding:'48px 24px 80px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'} as const;
const brand={fontWeight:800,color:'#16231c',textDecoration:'none',fontSize:21} as const;
const eyebrow={letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:48} as const;
const title={fontFamily:'Georgia,serif',fontWeight:500,fontSize:54,margin:'8px 0 14px'} as const;
const lead={fontSize:18,lineHeight:1.6,color:'#58685e',maxWidth:760} as const;
const card={background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:24} as const;
const cta={display:'inline-block',background:'#173f2b',color:'#fff',padding:'12px 16px',borderRadius:10,textDecoration:'none',fontWeight:800,marginTop:10} as const;
const ctaButton={border:0,background:'#173f2b',color:'#fff',padding:'12px 16px',borderRadius:10,fontWeight:800,cursor:'pointer'} as const;
const secondary={border:'1px solid #b9c4bd',background:'transparent',borderRadius:10,padding:'10px 14px',fontWeight:700,cursor:'pointer'} as const;
