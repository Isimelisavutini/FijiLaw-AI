'use client';

import { FormEvent, useEffect, useState } from 'react';

const apiBase = process.env.NEXT_PUBLIC_API_URL ?? 'https://fijilaw-api-production-production.up.railway.app';

export default function AccountPage() {
  const [mode,setMode]=useState<'login'|'register'>('login');
  const [selectedPlan,setSelectedPlan]=useState('');
  const [email,setEmail]=useState(''); const [password,setPassword]=useState(''); const [displayName,setDisplayName]=useState('');
  const [message,setMessage]=useState(''); const [loading,setLoading]=useState(false); const [registered,setRegistered]=useState(false);

  useEffect(()=>{
    const params=new URLSearchParams(window.location.search);
    const requestedMode=params.get('mode');
    if(requestedMode==='register') setMode('register');
    if(requestedMode==='login') setMode('login');
    setSelectedPlan(params.get('plan')??'');
  },[]);

  async function requestVerification(targetEmail:string){
    try{
      const response=await fetch(`${apiBase}/api/auth/request-email-verification`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email:targetEmail})});
      const body=await response.json().catch(()=>({}));
      if(!response.ok) throw new Error(body.error??body.detail??'Verification request could not be created.');
      setMessage(body.deliveryConfigured===false
        ? 'Your account was created. Email verification is prepared, but outbound verification email delivery is not enabled yet. You can sign in, but paid dashboard access will remain locked until your email is verified.'
        : 'Verification email sent. Check your inbox and follow the verification link.');
    }catch(e){setMessage(e instanceof Error?e.message:'Verification request could not be created.');}
  }

  async function submit(e:FormEvent){
    e.preventDefault(); setLoading(true); setMessage('');
    try{
      const path=mode==='login'?'/api/auth/login':'/api/auth/register';
      const response=await fetch(`${apiBase}${path}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(mode==='login'?{email,password}:{email,password,displayName})});
      const body=await response.json().catch(()=>({}));
      if(!response.ok) throw new Error(body.error??body.detail??'Account access could not be completed.');
      sessionStorage.setItem('fijilaw_access_token',body.accessToken);
      sessionStorage.setItem('fijilaw_member_email',body.email);
      if(selectedPlan) sessionStorage.setItem('fijilaw_selected_plan',selectedPlan);
      if(mode==='register'){
        setRegistered(true);
        await requestVerification(body.email);
      }else{
        window.location.href='/dashboard';
      }
    }catch(e){setMessage(e instanceof Error?e.message:'Account access could not be completed.');}
    finally{setLoading(false);}
  }

  return <main style={{maxWidth:760,margin:'0 auto',padding:'54px 24px 80px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'}}>
    <div style={{display:'flex',justifyContent:'space-between',gap:16,alignItems:'center'}}><a href="/" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>FijiLaw AI</a><a href="/pricing" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>View Pricing</a></div>
    <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:48}}>MEMBER ACCESS</p>
    <h1 style={{fontFamily:'Georgia,serif',fontSize:52,fontWeight:500,margin:'8px 0 16px'}}>{registered?'Account created.':mode==='login'?'Sign in to FijiLaw.':'Create your FijiLaw account.'}</h1>
    <p style={{color:'#5c6b62',lineHeight:1.6}}>Registration itself is free. Public legal access remains available without a paid dashboard. Paid memberships unlock the FijiLaw dashboard, saved legal matters and professional workflows.</p>

    {mode==='register'&&!registered&&<section style={{background:'#f4f7f4',border:'1px solid #d5ddd7',borderRadius:14,padding:18,margin:'22px 0'}}><strong>Review pricing before registering.</strong><p style={{margin:'6px 0 12px',color:'#5b695f',lineHeight:1.55}}>You can create a free account, or choose a paid membership for dashboard access. Creating an account does not automatically charge you.</p>{selectedPlan?<p style={{margin:'0 0 12px',fontSize:13}}><strong>Selected plan:</strong> {selectedPlan.replaceAll('_',' ')}</p>:null}<a href="/pricing" style={{color:'#173f2b',fontWeight:800}}>Compare membership plans →</a></section>}

    {registered ? <section style={{background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:28,marginTop:28}}>
      <h2 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,marginTop:0}}>Verify your email before using paid features.</h2>
      <p style={{lineHeight:1.7,color:'#53635a'}}>{message}</p>
      {selectedPlan&&<p style={{lineHeight:1.6,color:'#53635a'}}>Your selected plan is <strong>{selectedPlan.replaceAll('_',' ')}</strong>. Subscription activation will occur only after the payment flow is implemented and completed.</p>}
      <div style={{display:'flex',gap:10,flexWrap:'wrap',marginTop:20}}>
        <button onClick={()=>requestVerification(email)} style={primary}>Request verification again</button>
        <a href="/dashboard" style={linkButton}>Continue to dashboard</a>
        <a href="/pricing" style={linkButton}>View membership plans</a>
      </div>
    </section> : <>
      <div style={{display:'flex',gap:8,margin:'28px 0'}}><button onClick={()=>setMode('login')} style={tab(mode==='login')}>Sign in</button><button onClick={()=>setMode('register')} style={tab(mode==='register')}>Register</button></div>
      <form onSubmit={submit} style={{background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:28}}>
        {mode==='register'&&<><label style={label}>Name</label><input style={input} value={displayName} onChange={e=>setDisplayName(e.target.value)} placeholder="Your name"/></>}
        <label style={label}>Email</label><input style={input} type="email" required value={email} onChange={e=>setEmail(e.target.value)} placeholder="you@example.com"/>
        <label style={label}>Password</label><input style={input} type="password" required minLength={10} value={password} onChange={e=>setPassword(e.target.value)} placeholder="At least 10 characters"/>
        {message&&<p style={{background:'#fff0f0',padding:12,borderRadius:8,lineHeight:1.5}}>{message}</p>}
        <button disabled={loading} style={primary}>{loading?'Please wait…':mode==='login'?'Sign in':'Create account'}</button>
      </form>
    </>}
    <p style={{fontSize:13,color:'#6a776f',marginTop:18}}>Passwords are never stored in plain text. Paid dashboard access is enforced by the API, not only by the browser.</p>
  </main>;
}

const label={display:'block',fontWeight:700,margin:'14px 0 8px'} as const;
const input={width:'100%',padding:'13px 14px',border:'1px solid #bdc8c0',borderRadius:10,fontSize:16,marginBottom:8,boxSizing:'border-box'} as const;
const primary={width:'100%',border:0,borderRadius:10,padding:14,background:'#173f2b',color:'#fff',fontWeight:800,cursor:'pointer'} as const;
const linkButton={display:'inline-block',border:'1px solid #b8c4bc',borderRadius:10,padding:'12px 15px',color:'#173f2b',fontWeight:800,textDecoration:'none'} as const;
function tab(active:boolean){return {border:'1px solid #b8c4bc',borderRadius:999,padding:'9px 14px',background:active?'#173f2b':'transparent',color:active?'#fff':'#173f2b',fontWeight:700,cursor:'pointer'} as const;}
