'use client';

import { FormEvent, useEffect, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError, SERVICE_UNAVAILABLE_MESSAGE } from '../../lib/api';

export default function VerifyEmailPage(){
  const [token,setToken]=useState(''); const [message,setMessage]=useState(''); const [success,setSuccess]=useState(false); const [loading,setLoading]=useState(false);

  useEffect(()=>{
    const params=new URLSearchParams(window.location.search);
    const queryToken=params.get('token');
    if(queryToken)setToken(queryToken);
  },[]);

  async function verify(e:FormEvent){
    e.preventDefault(); setLoading(true); setMessage(''); setSuccess(false);
    try{
      const response=await fetchWithTimeout(`${API_BASE}/api/auth/verify-email`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({token:token.trim()})},12000);
      if(!response.ok) throw new Error(await readApiError(response,'Email verification could not be completed.'));
      setSuccess(true); setMessage('Your email has been verified. You can now continue to your member dashboard.');
    }catch(e){setMessage(e instanceof Error?e.message:SERVICE_UNAVAILABLE_MESSAGE);}
    finally{setLoading(false);}
  }

  return <main style={{maxWidth:720,margin:'0 auto',padding:'64px 24px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'}}>
    <div style={{display:'flex',justifyContent:'space-between',gap:16,alignItems:'center'}}><a href="/" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>FijiLaw AI</a><a href="/account?mode=login" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>Sign In</a></div>
    <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:48}}>EMAIL VERIFICATION</p>
    <h1 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:52,margin:'8px 0 16px'}}>Verify your FijiLaw account.</h1>
    <p style={{color:'#5c6b62',lineHeight:1.7}}>Enter the verification token from your FijiLaw email. Verification is required before paid dashboard features are enabled.</p>
    <form onSubmit={verify} style={{background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:28,marginTop:28}}>
      <label style={{display:'block',fontWeight:700,marginBottom:8}}>Verification token</label>
      <input required value={token} onChange={e=>setToken(e.target.value)} placeholder="Paste verification token" autoComplete="one-time-code" style={{width:'100%',padding:'13px 14px',border:'1px solid #bdc8c0',borderRadius:10,fontSize:16,boxSizing:'border-box',marginBottom:14}}/>
      {message&&<p role={success?'status':'alert'} style={{background:success?'#edf8f1':'#fff0f0',padding:12,borderRadius:8,lineHeight:1.5}}>{message}</p>}
      <button disabled={loading||!token.trim()} style={{width:'100%',border:0,borderRadius:10,padding:14,background:'#173f2b',color:'#fff',fontWeight:800,cursor:loading||!token.trim()?'not-allowed':'pointer',opacity:loading||!token.trim()?.58:1}}>{loading?'Verifying…':'Verify email'}</button>
    </form>
    {success&&<p style={{marginTop:20}}><a href="/dashboard" style={{color:'#173f2b',fontWeight:800}}>Continue to dashboard →</a></p>}
  </main>;
}
