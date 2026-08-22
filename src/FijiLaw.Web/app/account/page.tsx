'use client';

import { FormEvent, useEffect, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError, SERVICE_UNAVAILABLE_MESSAGE } from '../../lib/api';

type MembershipHealth = 'checking' | 'ready' | 'unavailable';

export default function AccountPage() {
  const [mode,setMode]=useState<'login'|'register'>('login');
  const [selectedPlan,setSelectedPlan]=useState('');
  const [email,setEmail]=useState(''); const [password,setPassword]=useState(''); const [displayName,setDisplayName]=useState('');
  const [message,setMessage]=useState(''); const [loading,setLoading]=useState(false); const [registered,setRegistered]=useState(false);
  const [membershipHealth,setMembershipHealth]=useState<MembershipHealth>('checking');

  useEffect(()=>{
    const params=new URLSearchParams(window.location.search);
    const requestedMode=params.get('mode');
    if(requestedMode==='register') setMode('register');
    if(requestedMode==='login') setMode('login');
    setSelectedPlan(params.get('plan')??'');
    void checkMembershipService();
  },[]);

  async function checkMembershipService(){
    setMembershipHealth('checking');
    try{
      const response=await fetchWithTimeout(`${API_BASE}/health`,{cache:'no-store'},8000);
      if(!response.ok){setMembershipHealth('unavailable');return;}
      const body=await response.json();
      setMembershipHealth(body?.membershipAuth==='available'?'ready':'unavailable');
    }catch{setMembershipHealth('unavailable');}
  }

  async function requestVerification(){
    if(membershipHealth!=='ready'){
      setMessage('Email verification is unavailable until the secure membership database is connected.');
      return;
    }
    const token=sessionStorage.getItem('fijilaw_access_token');
    if(!token){setMessage('Please sign in again before requesting a verification email.');return;}
    try{
      const response=await fetchWithTimeout(`${API_BASE}/api/auth/request-email-verification`,{method:'POST',headers:{Authorization:`Bearer ${token}`}},12000);
      if(response.status===401){setMessage('Your sign-in session has expired. Please sign in again.');return;}
      if(!response.ok) throw new Error(await readApiError(response,'Verification request could not be created.'));
      const body=await response.json().catch(()=>({}));
      if(body.alreadyVerified){setMessage('Your email is already verified. You can continue to the dashboard.');return;}
      if(body.deliveryAccepted){setMessage('Verification email accepted for delivery. Check your inbox and follow the verification link.');return;}
      setMessage(body.deliveryConfigured===false
        ? 'Your account is ready, but outbound verification email delivery is not configured yet. Paid dashboard access remains locked until email delivery is enabled and your address is verified.'
        : 'A verification request was created, but the email provider did not accept the message. Please try again later.');
    }catch(e){setMessage(e instanceof Error?e.message:SERVICE_UNAVAILABLE_MESSAGE);}
  }

  async function submit(e:FormEvent){
    e.preventDefault();
    if(membershipHealth!=='ready'){
      setMessage('Secure member registration and sign-in are temporarily unavailable while account storage is being prepared. Public legal help and pricing remain available.');
      return;
    }
    setLoading(true); setMessage('');
    try{
      const path=mode==='login'?'/api/auth/login':'/api/auth/register';
      const payload=mode==='login'
        ? {email,password}
        : {email,password,displayName,requestedPlanCode:selectedPlan||'free'};
      const response=await fetchWithTimeout(`${API_BASE}${path}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload)},15000);
      if(!response.ok) throw new Error(await readApiError(response,mode==='login'?'Sign in could not be completed.':'Account registration could not be completed.'));
      const body=await response.json();
      if(!body?.accessToken||!body?.email) throw new Error('The member service returned an incomplete response. Please try again.');
      sessionStorage.setItem('fijilaw_access_token',body.accessToken);
      sessionStorage.setItem('fijilaw_member_email',body.email);
      if(selectedPlan) sessionStorage.setItem('fijilaw_selected_plan',selectedPlan);
      if(mode==='register'){
        setRegistered(true);
        await requestVerification();
      }else{
        window.location.href='/dashboard';
      }
    }catch(e){setMessage(e instanceof Error?e.message:'Account access could not be completed.');}
    finally{setLoading(false);}
  }

  const unavailable=membershipHealth==='unavailable';
  const actionDisabled=loading||membershipHealth!=='ready';

  return <main style={{maxWidth:760,margin:'0 auto',padding:'54px 24px 80px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'}}>
    <div style={{display:'flex',justifyContent:'space-between',gap:16,alignItems:'center'}}><a href="/" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>FijiLaw AI</a><a href="/pricing" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>View Pricing</a></div>
    <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:48}}>MEMBER ACCESS</p>
    <h1 style={{fontFamily:'Georgia,serif',fontSize:52,fontWeight:500,margin:'8px 0 16px'}}>{registered?'Account created.':mode==='login'?'Sign in to FijiLaw.':'Create your FijiLaw account.'}</h1>
    <p style={{color:'#5c6b62',lineHeight:1.6}}>Registration itself is free. Public legal access remains available without a paid dashboard. Paid memberships unlock the FijiLaw dashboard, saved legal matters and professional workflows.</p>

    {membershipHealth==='checking'&&<section role="status" style={statusBox}><strong>Checking secure member service…</strong><p style={statusText}>FijiLaw AI is confirming that protected account storage is available before accepting credentials.</p></section>}
    {unavailable&&<section role="status" style={warningBox}><strong>Member accounts are temporarily unavailable.</strong><p style={statusText}>The secure membership database is not connected yet, so FijiLaw AI will not accept registration or sign-in credentials. You can continue using public legal tools and review pricing.</p><button type="button" onClick={()=>void checkMembershipService()} style={retryButton}>Retry member service</button></section>}

    {mode==='register'&&!registered&&<section style={{background:'#f4f7f4',border:'1px solid #d5ddd7',borderRadius:14,padding:18,margin:'22px 0'}}><strong>Review pricing before registering.</strong><p style={{margin:'6px 0 12px',color:'#5b695f',lineHeight:1.55}}>You can create a free account, or choose a paid membership for dashboard access. Creating an account does not automatically charge you.</p>{selectedPlan?<p style={{margin:'0 0 12px',fontSize:13}}><strong>Selected plan:</strong> {selectedPlan.replaceAll('_',' ')}</p>:null}<a href="/pricing" style={{color:'#173f2b',fontWeight:800}}>Compare membership plans →</a></section>}

    {registered ? <section style={{background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:28,marginTop:28}}>
      <h2 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,marginTop:0}}>Verify your email before using paid features.</h2>
      <p style={{lineHeight:1.7,color:'#53635a'}}>{message}</p>
      {selectedPlan&&<p style={{lineHeight:1.6,color:'#53635a'}}>Your selected plan is <strong>{selectedPlan.replaceAll('_',' ')}</strong>. This preference has been recorded, but subscription activation occurs only after a completed payment flow.</p>}
      <div style={{display:'flex',gap:10,flexWrap:'wrap',marginTop:20}}>
        <button onClick={()=>void requestVerification()} style={{...primary,width:'auto'}}>Request verification again</button>
        <a href="/dashboard" style={linkButton}>Continue to dashboard</a>
        <a href="/pricing" style={linkButton}>View membership plans</a>
      </div>
    </section> : <>
      <div style={{display:'flex',gap:8,margin:'28px 0'}}><button type="button" onClick={()=>setMode('login')} style={tab(mode==='login')}>Sign in</button><button type="button" onClick={()=>setMode('register')} style={tab(mode==='register')}>Register</button></div>
      <form onSubmit={submit} style={{background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:28}}>
        {mode==='register'&&<><label style={label}>Name</label><input style={input} value={displayName} onChange={e=>setDisplayName(e.target.value)} placeholder="Your name" autoComplete="name" disabled={unavailable}/></>}
        <label style={label}>Email</label><input style={input} type="email" required value={email} onChange={e=>setEmail(e.target.value)} placeholder="you@example.com" autoComplete="email" disabled={unavailable}/>
        <label style={label}>Password</label><input style={input} type="password" required minLength={10} value={password} onChange={e=>setPassword(e.target.value)} placeholder="At least 10 characters" autoComplete={mode==='login'?'current-password':'new-password'} disabled={unavailable}/>
        {message&&<p role="alert" style={{background:'#fff0f0',padding:12,borderRadius:8,lineHeight:1.5}}>{message}</p>}
        <button disabled={actionDisabled} style={{...primary,opacity:actionDisabled?0.58:1,cursor:actionDisabled?'not-allowed':'pointer'}}>{loading?'Please wait…':membershipHealth==='checking'?'Checking member service…':unavailable?'Member service unavailable':mode==='login'?'Sign in':'Create account'}</button>
      </form>
    </>}
    <p style={{fontSize:13,color:'#6a776f',marginTop:18}}>Passwords are never stored in plain text. Paid dashboard access is enforced by the API, not only by the browser.</p>
  </main>;
}

const label={display:'block',fontWeight:700,margin:'14px 0 8px'} as const;
const input={width:'100%',padding:'13px 14px',border:'1px solid #bdc8c0',borderRadius:10,fontSize:16,marginBottom:8,boxSizing:'border-box'} as const;
const primary={width:'100%',border:0,borderRadius:10,padding:14,background:'#173f2b',color:'#fff',fontWeight:800,cursor:'pointer'} as const;
const linkButton={display:'inline-block',border:'1px solid #b8c4bc',borderRadius:10,padding:'12px 15px',color:'#173f2b',fontWeight:800,textDecoration:'none'} as const;
const statusBox={background:'#f4f7f4',border:'1px solid #d5ddd7',borderRadius:12,padding:16,marginTop:20} as const;
const warningBox={background:'#fff8e8',border:'1px solid #ead9ad',borderRadius:12,padding:16,marginTop:20,color:'#624f27'} as const;
const statusText={margin:'6px 0 0',lineHeight:1.55} as const;
const retryButton={marginTop:12,border:'1px solid #ad9a68',background:'transparent',borderRadius:8,padding:'9px 12px',fontWeight:700,cursor:'pointer'} as const;
function tab(active:boolean){return {border:'1px solid #b8c4bc',borderRadius:999,padding:'9px 14px',background:active?'#173f2b':'transparent',color:active?'#fff':'#173f2b',fontWeight:700,cursor:'pointer'} as const;}
