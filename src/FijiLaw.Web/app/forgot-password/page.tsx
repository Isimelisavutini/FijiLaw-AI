'use client';

import { FormEvent, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError, SERVICE_UNAVAILABLE_MESSAGE } from '../../lib/api';

export default function ForgotPasswordPage(){
  const [email,setEmail]=useState(''); const [message,setMessage]=useState(''); const [loading,setLoading]=useState(false); const [submitted,setSubmitted]=useState(false);

  async function submit(e:FormEvent){
    e.preventDefault(); setLoading(true); setMessage(''); setSubmitted(false);
    try{
      const response=await fetchWithTimeout(`${API_BASE}/api/auth/forgot-password`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({email})},12000);
      if(!response.ok) throw new Error(await readApiError(response,'Password reset could not be requested.'));
      const body=await response.json().catch(()=>({}));
      setSubmitted(true);
      setMessage(body.deliveryConfigured===false
        ? 'If an active account exists for that email, a reset request has been created. Email delivery is not configured yet, so no reset message can be sent until the transactional email service is enabled.'
        : 'If an active account exists for that email, password reset instructions have been sent.');
    }catch(e){setMessage(e instanceof Error?e.message:SERVICE_UNAVAILABLE_MESSAGE);}
    finally{setLoading(false);}
  }

  return <main style={shell}>
    <div style={top}><a href="/" style={brand}>FijiLaw AI</a><a href="/account?mode=login" style={link}>Back to Sign In</a></div>
    <p style={eyebrow}>ACCOUNT RECOVERY</p>
    <h1 style={title}>Reset your password.</h1>
    <p style={lead}>Enter the email address used for your FijiLaw account. For privacy, FijiLaw gives the same response whether or not an account exists.</p>
    <form onSubmit={submit} style={card}>
      <label style={label}>Email</label>
      <input type="email" required autoComplete="email" value={email} onChange={e=>setEmail(e.target.value)} placeholder="you@example.com" style={input}/>
      {message&&<p role={submitted?'status':'alert'} style={{...notice,background:submitted?'#edf8f1':'#fff0f0'}}>{message}</p>}
      <button disabled={loading} style={{...button,opacity:loading?.6:1}}>{loading?'Requesting…':'Send reset instructions'}</button>
    </form>
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
const label={display:'block',fontWeight:700,marginBottom:8} as const;
const input={width:'100%',padding:'13px 14px',border:'1px solid #bdc8c0',borderRadius:10,fontSize:16,boxSizing:'border-box',marginBottom:14} as const;
const button={width:'100%',border:0,borderRadius:10,padding:14,background:'#173f2b',color:'#fff',fontWeight:800,cursor:'pointer'} as const;
const notice={padding:12,borderRadius:8,lineHeight:1.5} as const;
