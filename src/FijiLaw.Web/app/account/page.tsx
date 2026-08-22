'use client';

import { FormEvent, useState } from 'react';

const apiBase = process.env.NEXT_PUBLIC_API_URL ?? 'https://fijilaw-api-production-production.up.railway.app';

export default function AccountPage() {
  const [mode,setMode]=useState<'login'|'register'>('login');
  const [email,setEmail]=useState(''); const [password,setPassword]=useState(''); const [displayName,setDisplayName]=useState('');
  const [message,setMessage]=useState(''); const [loading,setLoading]=useState(false);

  async function submit(e:FormEvent){
    e.preventDefault(); setLoading(true); setMessage('');
    try{
      const path=mode==='login'?'/api/auth/login':'/api/auth/register';
      const response=await fetch(`${apiBase}${path}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(mode==='login'?{email,password}:{email,password,displayName})});
      const body=await response.json().catch(()=>({}));
      if(!response.ok) throw new Error(body.error??body.detail??'Sign in could not be completed.');
      sessionStorage.setItem('fijilaw_access_token',body.accessToken);
      sessionStorage.setItem('fijilaw_member_email',body.email);
      window.location.href='/dashboard';
    }catch(e){setMessage(e instanceof Error?e.message:'Sign in could not be completed.');}
    finally{setLoading(false);}
  }

  return <main style={{maxWidth:720,margin:'0 auto',padding:'64px 24px',fontFamily:'Inter,system-ui,sans-serif'}}>
    <a href="/" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>FijiLaw AI</a>
    <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:48}}>MEMBER ACCESS</p>
    <h1 style={{fontFamily:'Georgia,serif',fontSize:52,fontWeight:500,margin:'8px 0 16px'}}>{mode==='login'?'Sign in to FijiLaw.':'Create your FijiLaw account.'}</h1>
    <p style={{color:'#5c6b62',lineHeight:1.6}}>Free accounts can access public services. Paid memberships unlock the FijiLaw dashboard and persistent legal workflows.</p>
    <div style={{display:'flex',gap:8,margin:'28px 0'}}><button onClick={()=>setMode('login')} style={tab(mode==='login')}>Sign in</button><button onClick={()=>setMode('register')} style={tab(mode==='register')}>Register</button></div>
    <form onSubmit={submit} style={{background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:28}}>
      {mode==='register'&&<><label style={label}>Name</label><input style={input} value={displayName} onChange={e=>setDisplayName(e.target.value)} placeholder="Your name"/></>}
      <label style={label}>Email</label><input style={input} type="email" required value={email} onChange={e=>setEmail(e.target.value)} placeholder="you@example.com"/>
      <label style={label}>Password</label><input style={input} type="password" required minLength={10} value={password} onChange={e=>setPassword(e.target.value)} placeholder="At least 10 characters"/>
      {message&&<p style={{background:'#fff0f0',padding:12,borderRadius:8}}>{message}</p>}
      <button disabled={loading} style={{width:'100%',border:0,borderRadius:10,padding:14,background:'#173f2b',color:'#fff',fontWeight:800,cursor:'pointer'}}>{loading?'Please wait…':mode==='login'?'Sign in':'Create account'}</button>
    </form>
    <p style={{fontSize:13,color:'#6a776f',marginTop:18}}>Passwords are never stored in plain text. Paid dashboard access is enforced by the API, not only by the browser.</p>
  </main>;
}

const label={display:'block',fontWeight:700,margin:'14px 0 8px'} as const;
const input={width:'100%',padding:'13px 14px',border:'1px solid #bdc8c0',borderRadius:10,fontSize:16,marginBottom:8,boxSizing:'border-box'} as const;
function tab(active:boolean){return {border:'1px solid #b8c4bc',borderRadius:999,padding:'9px 14px',background:active?'#173f2b':'transparent',color:active?'#fff':'#173f2b',fontWeight:700,cursor:'pointer'} as const;}
