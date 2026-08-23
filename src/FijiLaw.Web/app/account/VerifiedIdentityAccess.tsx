'use client';

import { SignIn, SignUp, SignedIn, SignedOut } from '@clerk/nextjs';

type Props = {
  mode: 'login' | 'register';
  selectedPlan: string;
  onModeChange: (mode: 'login' | 'register') => void;
};

export default function VerifiedIdentityAccess({ mode, selectedPlan, onModeChange }: Props) {
  const completeUrl = `/auth/complete${selectedPlan ? `?plan=${encodeURIComponent(selectedPlan)}` : ''}`;

  return <main style={{maxWidth:920,margin:'0 auto',padding:'50px 24px 90px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'}}>
    <div style={{display:'flex',justifyContent:'space-between',gap:16,alignItems:'center'}}>
      <a href="/" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>FijiLaw AI</a>
      <a href="/pricing" style={{color:'#173f2b',fontWeight:800,textDecoration:'none'}}>View Pricing</a>
    </div>

    <section style={{marginTop:46,display:'grid',gridTemplateColumns:'minmax(0,1fr) minmax(340px,430px)',gap:42,alignItems:'start'}}>
      <div>
        <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',margin:0}}>VERIFIED MEMBER ACCESS</p>
        <h1 style={{fontFamily:'Georgia,serif',fontSize:'clamp(40px,6vw,62px)',lineHeight:1.02,fontWeight:500,margin:'10px 0 18px'}}>
          {mode === 'register' ? 'Create a verified FijiLaw account.' : 'Sign in securely.'}
        </h1>
        <p style={{color:'#5c6b62',lineHeight:1.7,fontSize:17,maxWidth:560}}>
          Register with Google, Apple, or a Fiji mobile number. FijiLaw only creates the protected member session after the selected identity method has been verified.
        </p>

        <div style={{display:'grid',gap:12,marginTop:28}}>
          <div style={methodCard}><strong>Google account</strong><span style={methodText}>Google authentication plus verified email identity.</span></div>
          <div style={methodCard}><strong>Apple account</strong><span style={methodText}>Sign in with Apple, with verified identity before FijiLaw access.</span></div>
          <div style={methodCard}><strong>Fiji mobile number</strong><span style={methodText}>Use +679 followed by the seven-digit Fiji number and enter the SMS verification code.</span></div>
        </div>

        <div style={{background:'#f4f7f4',border:'1px solid #d5ddd7',borderRadius:14,padding:18,marginTop:24}}>
          <strong>Verification is mandatory.</strong>
          <p style={{margin:'7px 0 0',color:'#5b695f',lineHeight:1.55}}>Unverified accounts cannot open protected dashboards, legal matters, stored documents, referrals, or professional workflows.</p>
        </div>
      </div>

      <div>
        <div style={{display:'flex',gap:8,marginBottom:16}}>
          <button type="button" onClick={()=>onModeChange('login')} style={tab(mode==='login')}>Sign in</button>
          <button type="button" onClick={()=>onModeChange('register')} style={tab(mode==='register')}>Register</button>
        </div>

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

const methodCard={background:'#fff',border:'1px solid #d8e0da',borderRadius:12,padding:'15px 16px',display:'grid',gap:4} as const;
const methodText={fontSize:14,color:'#65736a',lineHeight:1.45} as const;
const primaryLink={display:'inline-block',width:'100%',boxSizing:'border-box',textAlign:'center',background:'#173f2b',color:'#fff',padding:'13px 16px',borderRadius:10,textDecoration:'none',fontWeight:800} as const;
function tab(active:boolean){return {border:'1px solid #b8c4bc',borderRadius:999,padding:'9px 14px',background:active?'#173f2b':'transparent',color:active?'#fff':'#173f2b',fontWeight:700,cursor:'pointer'} as const;}
