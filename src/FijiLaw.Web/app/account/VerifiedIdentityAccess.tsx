'use client';

import { SignIn, SignUp, SignedIn, SignedOut } from '@clerk/nextjs';

type Props = {
  mode: 'login' | 'register';
  selectedPlan: string;
  onModeChange: (mode: 'login' | 'register') => void;
};

export default function VerifiedIdentityAccess({ mode, selectedPlan, onModeChange }: Props) {
  const completeUrl = `/auth/complete${selectedPlan ? `?plan=${encodeURIComponent(selectedPlan)}` : ''}`;

  return <main style={{maxWidth:980,margin:'0 auto',padding:'50px 24px 90px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'}}>
    <div style={{display:'flex',justifyContent:'space-between',gap:16,alignItems:'center'}}>
      <a href="/" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>FijiLaw AI</a>
      <a href="/pricing" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>View Pricing</a>
    </div>

    <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:46}}>VERIFIED MEMBER ACCESS</p>
    <h1 style={{fontFamily:'Georgia,serif',fontSize:'clamp(40px,6vw,60px)',lineHeight:1.05,fontWeight:500,margin:'10px 0 18px'}}>
      {mode === 'register' ? 'Create your verified FijiLaw account.' : 'Sign in securely.'}
    </h1>
    <p style={{color:'#5c6b62',lineHeight:1.7,fontSize:17,maxWidth:760}}>
      Registration is free. Public legal access remains available without a paid dashboard. Paid memberships unlock dashboards, saved legal matters and professional workflows.
    </p>

    {mode==='register'&&<section style={pricingBox}>
      <strong style={{fontSize:17}}>Review pricing before registering.</strong>
      <p style={{margin:'7px 0 12px',color:'#5b695f',lineHeight:1.6}}>You can create a free account, or choose a paid membership for dashboard access. Creating an account does not automatically charge you.</p>
      {selectedPlan?<p style={{margin:'0 0 12px',fontSize:13}}><strong>Selected plan:</strong> {selectedPlan.replaceAll('_',' ')}</p>:null}
      <a href="/pricing" style={{color:'#173f2b',fontWeight:800}}>Compare membership plans →</a>
    </section>}

    <div style={{display:'flex',gap:8,margin:'28px 0 18px'}}>
      <button type="button" onClick={()=>onModeChange('login')} style={tab(mode==='login')}>Sign in</button>
      <button type="button" onClick={()=>onModeChange('register')} style={tab(mode==='register')}>Register</button>
    </div>

    <section style={{display:'grid',gridTemplateColumns:'minmax(0,1fr) minmax(340px,430px)',gap:38,alignItems:'start'}}>
      <div>
        {mode==='register'?<>
          <div style={{display:'flex',justifyContent:'space-between',gap:16,alignItems:'flex-start',flexWrap:'wrap'}}>
            <div><p style={eyebrow}>CHOOSE HOW TO REGISTER</p><h2 style={methodHeading}>Every FijiLaw account must be verified.</h2></div>
            <span style={verifiedBadge}>Verification required</span>
          </div>
          <p style={lead}>Choose the identity method that is easiest for you. FijiLaw only creates protected member access after the selected method has been verified.</p>
          <div style={{display:'grid',gap:10,marginTop:20}}>
            <div style={methodCard}><span style={methodMark}>G</span><span><strong>Google account</strong><small style={methodText}>Google authentication using a verified email identity.</small></span></div>
            <div style={methodCard}><span style={methodMark}>A</span><span><strong>Apple account</strong><small style={methodText}>Sign in with Apple and complete identity verification before FijiLaw access.</small></span></div>
            <div style={methodCard}><span style={{...methodMark,fontSize:12}}>+679</span><span><strong>Fiji mobile number</strong><small style={methodText}>Enter a Fiji mobile number and confirm the SMS verification code.</small></span></div>
            <div style={methodCard}><span style={methodMark}>@</span><span><strong>Email account</strong><small style={methodText}>Create an account with email and verify the address before protected features are enabled.</small></span></div>
          </div>
          <div style={verificationBox}>
            <strong>Verification is mandatory.</strong>
            <p style={{margin:'7px 0 0',color:'#5b695f',lineHeight:1.55}}>Unverified users cannot open protected dashboards, saved legal matters, stored documents, referrals or professional workflows.</p>
          </div>
        </>:<>
          <p style={eyebrow}>SECURE SIGN IN</p>
          <h2 style={methodHeading}>Use the same verified identity you registered with.</h2>
          <p style={lead}>Google, Apple, mobile and verified email identities can be used to return to the same FijiLaw member account.</p>
        </>}
      </div>

      <div>
        <SignedIn>
          <section style={{background:'#fff',border:'1px solid #d5ddd7',borderRadius:18,padding:24}}>
            <h2 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:28,marginTop:0}}>Identity verified.</h2>
            <p style={{color:'#5c6b62',lineHeight:1.6}}>Complete the secure FijiLaw account link to continue.</p>
            <a href={completeUrl} style={primaryLink}>Continue to FijiLaw</a>
          </section>
        </SignedIn>

        <SignedOut>
          <div style={{display:'flex',justifyContent:'center'}}>
            {mode === 'register'
              ? <SignUp routing="hash" forceRedirectUrl={completeUrl} signInUrl="/account?mode=login" />
              : <SignIn routing="hash" forceRedirectUrl={completeUrl} signUpUrl="/account?mode=register" />}
          </div>
        </SignedOut>

        {selectedPlan ? <p style={{fontSize:13,color:'#6a776f',marginTop:16,textAlign:'center'}}>Selected membership: <strong>{selectedPlan.replaceAll('_',' ')}</strong>. Registration itself does not charge you.</p> : null}
      </div>
    </section>
  </main>;
}

const pricingBox={background:'#f4f7f4',border:'1px solid #d5ddd7',borderRadius:14,padding:18,margin:'24px 0 0'} as const;
const eyebrow={letterSpacing:'.12em',fontSize:11,fontWeight:800,color:'#587063',margin:'0 0 7px'} as const;
const methodHeading={fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,margin:0} as const;
const lead={color:'#5c6b62',lineHeight:1.65,margin:'14px 0 0'} as const;
const verifiedBadge={background:'#eaf3ec',border:'1px solid #bfd3c4',color:'#234b34',padding:'7px 10px',borderRadius:999,fontSize:12,fontWeight:800} as const;
const methodCard={background:'#fff',border:'1px solid #d8e0da',borderRadius:12,padding:'14px 15px',display:'grid',gridTemplateColumns:'48px 1fr',alignItems:'center',gap:12} as const;
const methodMark={width:40,height:40,borderRadius:10,border:'1px solid #c8d3cb',display:'grid',placeItems:'center',fontWeight:900,color:'#173f2b'} as const;
const methodText={display:'block',fontSize:13,color:'#65736a',lineHeight:1.45,marginTop:3,fontWeight:500} as const;
const verificationBox={background:'#f4f7f4',border:'1px solid #d5ddd7',borderRadius:14,padding:18,marginTop:20} as const;
const primaryLink={display:'inline-block',width:'100%',boxSizing:'border-box',textAlign:'center',background:'#173f2b',color:'#fff',padding:'13px 16px',borderRadius:10,textDecoration:'none',fontWeight:800} as const;
function tab(active:boolean){return {border:'1px solid #b8c4bc',borderRadius:999,padding:'9px 14px',background:active?'#173f2b':'transparent',color:active?'#fff':'#173f2b',fontWeight:700,cursor:'pointer'} as const;}
