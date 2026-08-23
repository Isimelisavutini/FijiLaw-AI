'use client';

import { useEffect, useState } from 'react';

export default function CompleteVerifiedIdentityPage() {
  const [message,setMessage]=useState('Confirming your verified identity…');
  const [failed,setFailed]=useState(false);

  useEffect(()=>{ void complete(); },[]);

  async function complete(){
    setFailed(false);
    setMessage('Confirming your verified identity…');
    try{
      const params=new URLSearchParams(window.location.search);
      const requestedPlanCode=params.get('plan')||sessionStorage.getItem('fijilaw_selected_plan')||'free';
      const response=await fetch('/api/auth/bridge',{
        method:'POST',
        headers:{'Content-Type':'application/json'},
        cache:'no-store',
        body:JSON.stringify({requestedPlanCode})
      });
      const body=await response.json().catch(()=>({}));
      if(!response.ok) throw new Error(body?.error??'Verified account linking could not be completed.');
      if(!body?.accessToken) throw new Error('The FijiLaw member service did not return a secure session.');

      sessionStorage.setItem('fijilaw_access_token',body.accessToken);
      if(body.phoneNumber) sessionStorage.setItem('fijilaw_member_phone',body.phoneNumber);
      if(body.email && !String(body.email).endsWith('@identity.fijilaw.local')) sessionStorage.setItem('fijilaw_member_email',body.email);
      if(body.primaryIdentifier) sessionStorage.setItem('fijilaw_member_identifier',body.primaryIdentifier);
      if(requestedPlanCode) sessionStorage.setItem('fijilaw_selected_plan',requestedPlanCode);
      setMessage('Verified. Opening your FijiLaw account…');
      window.location.replace('/dashboard');
    }catch(error){
      setFailed(true);
      setMessage(error instanceof Error?error.message:'Verified account linking could not be completed.');
    }
  }

  return <main style={{maxWidth:680,margin:'0 auto',padding:'72px 24px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'}}>
    <a href="/" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>FijiLaw AI</a>
    <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:48}}>SECURE ACCOUNT LINK</p>
    <h1 style={{fontFamily:'Georgia,serif',fontSize:46,fontWeight:500,margin:'8px 0 16px'}}>Finishing your registration.</h1>
    <section role="status" style={{background:failed?'#fff4f1':'#f4f7f4',border:'1px solid #d5ddd7',borderRadius:16,padding:22,lineHeight:1.6}}>
      <strong>{failed?'Account link needs attention.':'Verification in progress.'}</strong>
      <p>{message}</p>
      {failed?<div style={{display:'flex',gap:10,flexWrap:'wrap'}}><button type="button" onClick={()=>void complete()} style={button}>Try again</button><a href="/account?mode=register" style={link}>Return to registration</a></div>:null}
    </section>
  </main>;
}

const button={border:0,borderRadius:10,padding:'11px 15px',background:'#173f2b',color:'#fff',fontWeight:800,cursor:'pointer'} as const;
const link={display:'inline-block',border:'1px solid #b8c4bc',borderRadius:10,padding:'10px 15px',color:'#173f2b',fontWeight:800,textDecoration:'none'} as const;
