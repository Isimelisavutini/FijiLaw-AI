'use client';

import { useEffect, useState } from 'react';

export default function CompleteVerifiedIdentityPage() {
  const [message,setMessage]=useState('Confirming your verified identity…');
  const [failed,setFailed]=useState(false);
  const [pendingApproval,setPendingApproval]=useState(false);

  useEffect(()=>{ void complete(); },[]);

  async function complete(){
    setFailed(false);
    setPendingApproval(false);
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
      if(body.approvalRequired){
        sessionStorage.removeItem('fijilaw_access_token');
        setPendingApproval(true);
        setMessage('Your identity is verified and your FijiLaw account is awaiting System Administrator approval. You can sign in after approval.');
        return;
      }
      setMessage('Verified. Opening your FijiLaw account…');
      window.location.replace('/dashboard');
    }catch(error){
      setFailed(true);
      setMessage(error instanceof Error?error.message:'Verified account linking could not be completed.');
    }
  }

  return <main style={shell}>
    <div style={topbar}><a href="/" style={brand}>FijiLaw AI</a><a href="/pricing" style={topLink}>Pricing</a></div>
    <section style={hero}>
      <div>
        <p style={eyebrow}>SECURE ACCOUNT LINK</p>
        <h1 style={title}>Finishing your registration.</h1>
        <p style={lead}>Clerk confirms your identity first. FijiLaw then links that verified identity to the internal membership, role, subscription and FijiLaw Credits system.</p>
      </div>
      <div style={pillar} aria-hidden="true" />
    </section>
    <section role="status" style={failed?errorBox:statusBox}>
      <strong>{failed?'Account link needs attention.':'Verification in progress.'}</strong>
      <p style={{margin:'8px 0 0',lineHeight:1.65,color:'#566674'}}>{message}</p>
      {failed?<div style={{display:'flex',gap:10,flexWrap:'wrap',marginTop:16}}><button type="button" onClick={()=>void complete()} style={button}>Try again</button><a href="/account?mode=register" style={link}>Return to registration</a></div>:null}
      {pendingApproval?<div style={{display:'flex',gap:10,flexWrap:'wrap',marginTop:16}}><a href="/account?mode=login" style={link}>Return to sign in</a><a href="/" style={link}>Public legal help</a></div>:null}
    </section>
  </main>;
}

const shell={maxWidth:760,margin:'0 auto',padding:'42px 24px 90px',fontFamily:'Inter,system-ui,sans-serif',color:'#081B2D'} as const;
const topbar={display:'flex',justifyContent:'space-between',gap:16,alignItems:'center',borderBottom:'1px solid #CBD5DD',paddingBottom:18} as const;
const brand={color:'#0E2A47',fontWeight:900,textDecoration:'none',fontSize:21} as const;
const topLink={color:'#0E2A47',fontWeight:850,textDecoration:'none'} as const;
const hero={position:'relative',overflow:'hidden',display:'grid',gridTemplateColumns:'1fr 90px',gap:24,background:'linear-gradient(135deg,#081B2D,#0E2A47)',borderRadius:20,padding:'36px 32px',marginTop:34,color:'#fff',borderBottom:'3px solid #E5A93C',boxShadow:'0 22px 48px rgba(8,27,45,.16)'} as const;
const eyebrow={letterSpacing:'.15em',fontSize:11,fontWeight:900,color:'#F4D28A',margin:'0 0 8px'} as const;
const title={fontFamily:'Georgia,serif',fontSize:'clamp(38px,6vw,52px)',fontWeight:500,margin:'8px 0 12px',lineHeight:1.05} as const;
const lead={color:'#D4DFE6',lineHeight:1.65,margin:0} as const;
const pillar={alignSelf:'end',justifySelf:'center',width:36,height:120,background:'linear-gradient(90deg,#1B2A36,#42515C,#16232D)',clipPath:'polygon(18% 0,82% 0,100% 100%,0 100%)',boxShadow:'0 0 24px rgba(229,169,60,.18)'} as const;
const statusBox={background:'#F7FAFC',border:'1px solid #CBD5DD',borderRadius:16,padding:22,lineHeight:1.6,marginTop:24,borderLeft:'4px solid #E5A93C'} as const;
const errorBox={background:'#FFF7E8',border:'1px solid #E5C978',borderRadius:16,padding:22,lineHeight:1.6,marginTop:24,borderLeft:'4px solid #C98724'} as const;
const button={border:'1px solid #D69A2F',borderRadius:10,padding:'11px 15px',background:'#E5A93C',color:'#081B2D',fontWeight:900,cursor:'pointer'} as const;
const link={display:'inline-block',border:'1px solid #BCC8D1',borderRadius:10,padding:'10px 15px',color:'#0E2A47',fontWeight:850,textDecoration:'none',background:'#fff'} as const;
