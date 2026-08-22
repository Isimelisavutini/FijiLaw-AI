'use client';

import { FormEvent, useEffect, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError, SERVICE_UNAVAILABLE_MESSAGE } from '../../lib/api';

export default function ResetPasswordPage(){
  const [token,setToken]=useState(''); const [password,setPassword]=useState(''); const [confirm,setConfirm]=useState('');
  const [message,setMessage]=useState(''); const [success,setSuccess]=useState(false); const [loading,setLoading]=useState(false);

  useEffect(()=>{setToken(new URLSearchParams(window.location.search).get('token')?.trim()??'');},[]);

  async function submit(e:FormEvent){
    e.preventDefault(); setMessage(''); setSuccess(false);
    if(password!==confirm){setMessage('The passwords do not match.');return;}
    if(password.length<10){setMessage('Password must be at least 10 characters long.');return;}
    if(!token){setMessage('The password reset link is missing its security token. Request a new reset email.');return;}
    setLoading(true);
    try{
      const response=await fetchWithTimeout(`${API_BASE}/api/auth/reset-password`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token,newPassword:password})},12000);
      if(!response.ok) throw new Error(await readApiError(response,'Password reset could not be completed.'));
      sessionStorage.removeItem('fijilaw_access_token');
      setSuccess(true); setMessage('Your password has been changed. Existing FijiLaw sign-in sessions were revoked for security.');
    }catch(e){setMessage(e instanceof Error?e.message:SERVICE_UNAVAILABLE_MESSAGE);}
    finally{setLoading(false);}
  }

  return <main style={shell}>
    <div style={top}><a href="/" style={brand}>FijiLaw AI</a><a href="/account?mode=login" style={link}>Sign In</a></div>
    <p style={eyebrow}>ACCOUNT RECOVERY</p>
    <h1 style={title}>{success?'Password changed.':'Choose a new password.'}</h1>
    <p style={lead}>Reset links expire after 30 minutes. Completing a password reset signs out existing FijiLaw sessions.</p>
    <form onSubmit={submit} style={card}>
      <label style={label}>New password</label><input type="password" required minLength={10} autoComplete="new-password" value={password} onChange={e=>setPassword(e.target.value)} disabled={success} style={input}/>
      <label style={label}>Confirm new password</label><input type="password" required minLength={10} autoComplete="new-password" value={confirm} onChange={e=>setConfirm(e.target.value)} disabled={success} style={input}/>
      {message&&<p role={success?'status':'alert'} style={{...notice,background:success?'#edf8f1':'#fff0f0'}}>{message}</p>}
      {!success&&<button disabled={loading} style={{...button,opacity:loading?.6:1}}>{loading?'Changing password…':'Change password'}</button>}
    </form>
    {success&&<p style={{marginTop:20}}><a href="/account?mode=login" style={link}>Sign in with your new password →</a></p>}
    {!token&&<p style={{marginTop:20}}><a href="/forgot-password" style={link}>Request a new reset link →</a></p>}
  </main>;
}

const shell={maxWidth:720,margin:'0 auto',padding:'64px 24px 80px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'} as const;
const top={display:'flex',justifyContent:'space-between',gap:16,alignItems:'center'} as const;
const brand={color:'#173f2b',fontWeight:800,textDecoration:'none'} as const;
const link={color:'#173f2b',fontWeight:800,textDecoration:'none'} as const;
const eyebrow={letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:48} as const;
const title={fontFamily:'Georgia,serif',fontWeight:500,fontSize:52,margin:'8px 0 16px'} as const;
const lead={color:'#5c6b62',lineHeight:1.7} as const;
const card={background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:28,marginTop:28} as const;
const label={display:'block',fontWeight:700,margin:'14px 0 8px'} as const;
const input={width:'100%',padding:'13px 14px',border:'1px solid #bdc8c0',borderRadius:10,fontSize:16,boxSizing:'border-box',marginBottom:8} as const;
const button={width:'100%',border:0,borderRadius:10,padding:14,background:'#173f2b',color:'#fff',fontWeight:800,cursor:'pointer',marginTop:12} as const;
const notice={padding:12,borderRadius:8,lineHeight:1.5} as const;
