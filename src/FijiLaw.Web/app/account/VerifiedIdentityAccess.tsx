'use client';

import { SignIn, SignUp, SignedIn, SignedOut } from '@clerk/nextjs';

type Props = {
  mode: 'login' | 'register';
  selectedPlan: string;
  onModeChange: (mode: 'login' | 'register') => void;
};

const clerkAppearance = {
  variables: {
    colorPrimary: '#0E2A47',
    colorForeground: '#081B2D',
    colorBackground: '#FFFFFF',
    colorInputBackground: '#FFFFFF',
    colorInputText: '#081B2D',
    borderRadius: '10px'
  },
  elements: {
    card: 'shadow-none border border-slate-200',
    formButtonPrimary: 'bg-[#E5A93C] hover:bg-[#D69A2F] text-[#081B2D] font-bold',
    footerActionLink: 'text-[#0E2A47] font-bold',
    socialButtonsBlockButton: 'border-slate-300 text-[#081B2D]',
    formFieldInput: 'border-slate-300 focus:border-[#E5A93C] focus:ring-[#E5A93C]'
  }
} as const;

export default function VerifiedIdentityAccess({ mode, selectedPlan, onModeChange }: Props) {
  const completeUrl = `/auth/complete${selectedPlan ? `?plan=${encodeURIComponent(selectedPlan)}` : ''}`;

  return <main style={shell}>
    <div style={topbar}>
      <a href="/" style={brand}>FijiLaw AI</a>
      <a href="/pricing" style={topLink}>View Pricing</a>
    </div>

    <section style={heroBand}>
      <div>
        <p style={goldEyebrow}>VERIFIED MEMBER ACCESS</p>
        <h1 style={title}>{mode === 'register' ? 'Create your verified FijiLaw account.' : 'Sign in securely.'}</h1>
        <p style={heroLead}>Your verified identity connects to FijiLaw membership, dashboards, FijiLaw Credits and professional workflows.</p>
      </div>
      <div style={pillar} aria-hidden="true" />
    </section>

    {mode==='register'&&<section style={pricingBox}>
      <strong style={{fontSize:17}}>Review pricing before registering.</strong>
      <p style={bodyText}>Registration itself is free. Choose a paid membership only if you want dashboard and professional features. Creating an account does not automatically charge you.</p>
      {selectedPlan?<p style={{margin:'0 0 12px',fontSize:13}}><strong>Selected plan:</strong> {selectedPlan.replaceAll('_',' ')}</p>:null}
      <a href="/pricing" style={goldLink}>Compare membership plans →</a>
    </section>}

    <div style={tabs}>
      <button type="button" onClick={()=>onModeChange('login')} style={tab(mode==='login')}>Sign in</button>
      <button type="button" onClick={()=>onModeChange('register')} style={tab(mode==='register')}>Register</button>
    </div>

    <section style={identityGrid}>
      <div>
        {mode==='register'?<>
          <div style={{display:'flex',justifyContent:'space-between',gap:16,alignItems:'flex-start',flexWrap:'wrap'}}>
            <div><p style={eyebrow}>CHOOSE HOW TO REGISTER</p><h2 style={methodHeading}>Every FijiLaw account must be verified.</h2></div>
            <span style={verifiedBadge}>Verification required</span>
          </div>
          <p style={lead}>Choose the identity method that is easiest for you. Protected FijiLaw access is created only after Clerk confirms the selected identity method.</p>
          <div style={{display:'grid',gap:10,marginTop:20}}>
            <div style={methodCard}><span style={methodMark}>G</span><span><strong>Google account</strong><small style={methodText}>Google authentication using a verified email identity.</small></span></div>
            <div style={methodCard}><span style={methodMark}>A</span><span><strong>Apple account</strong><small style={methodText}>Sign in with Apple using a verified identity.</small></span></div>
            <div style={methodCard}><span style={{...methodMark,fontSize:12}}>+679</span><span><strong>Fiji mobile number</strong><small style={methodText}>Enter a Fiji mobile number and confirm the SMS verification code.</small></span></div>
            <div style={methodCard}><span style={methodMark}>@</span><span><strong>Email account</strong><small style={methodText}>Create an account with email and complete Clerk email verification.</small></span></div>
          </div>
          <div style={verificationBox}>
            <strong>Verification is mandatory.</strong>
            <p style={bodyText}>Unverified users cannot be linked to protected FijiLaw dashboards, saved legal matters, stored documents, referrals or professional workflows.</p>
          </div>
        </>:<>
          <p style={eyebrow}>SECURE SIGN IN</p>
          <h2 style={methodHeading}>Use the same verified identity you registered with.</h2>
          <p style={lead}>Google, Apple, Fiji mobile and verified email identities can return to the same FijiLaw member account.</p>
        </>}
      </div>

      <div>
        <SignedIn>
          <section style={verifiedPanel}>
            <p style={goldEyebrow}>IDENTITY VERIFIED</p>
            <h2 style={panelHeading}>Continue to FijiLaw.</h2>
            <p style={bodyText}>Your Clerk identity is verified. FijiLaw will now link it to the internal membership, role, subscription and credit system.</p>
            <a href={completeUrl} style={primaryLink}>Continue to FijiLaw</a>
          </section>
        </SignedIn>

        <SignedOut>
          <div style={{display:'flex',justifyContent:'center'}}>
            {mode === 'register'
              ? <SignUp routing="hash" forceRedirectUrl={completeUrl} signInUrl="/account?mode=login" appearance={clerkAppearance} />
              : <SignIn routing="hash" forceRedirectUrl={completeUrl} signUpUrl="/account?mode=register" appearance={clerkAppearance} />}
          </div>
        </SignedOut>

        {selectedPlan ? <p style={{fontSize:13,color:'#667684',marginTop:16,textAlign:'center'}}>Selected membership: <strong>{selectedPlan.replaceAll('_',' ')}</strong>. Registration itself does not charge you.</p> : null}
      </div>
    </section>
  </main>;
}

const shell={maxWidth:1000,margin:'0 auto',padding:'38px 24px 90px',fontFamily:'Inter,system-ui,sans-serif',color:'#081B2D'} as const;
const topbar={display:'flex',justifyContent:'space-between',gap:16,alignItems:'center',borderBottom:'1px solid #CBD5DD',paddingBottom:18} as const;
const brand={color:'#0E2A47',fontWeight:900,textDecoration:'none',fontSize:21} as const;
const topLink={color:'#0E2A47',fontWeight:850,textDecoration:'none'} as const;
const heroBand={position:'relative',overflow:'hidden',display:'grid',gridTemplateColumns:'1fr 120px',gap:24,background:'linear-gradient(135deg,#081B2D,#0E2A47)',borderRadius:20,padding:'38px 34px',marginTop:34,color:'#fff',borderBottom:'3px solid #E5A93C',boxShadow:'0 22px 48px rgba(8,27,45,.16)'} as const;
const goldEyebrow={letterSpacing:'.15em',fontSize:11,fontWeight:900,color:'#F4D28A',margin:'0 0 8px'} as const;
const title={fontFamily:'Georgia,serif',fontSize:'clamp(38px,6vw,56px)',fontWeight:500,margin:'8px 0 12px',lineHeight:1.05} as const;
const heroLead={color:'#D4DFE6',lineHeight:1.6,margin:0,maxWidth:690} as const;
const pillar={alignSelf:'end',justifySelf:'center',width:46,height:150,background:'linear-gradient(90deg,#1B2A36,#42515C,#16232D)',clipPath:'polygon(18% 0,82% 0,100% 100%,0 100%)',boxShadow:'0 0 24px rgba(229,169,60,.18)'} as const;
const pricingBox={background:'#F7FAFC',border:'1px solid #CBD5DD',borderRadius:14,padding:18,margin:'24px 0 0',borderLeft:'4px solid #E5A93C'} as const;
const bodyText={margin:'7px 0 12px',color:'#566674',lineHeight:1.6} as const;
const goldLink={color:'#9A6A16',fontWeight:850,textDecoration:'none'} as const;
const tabs={display:'flex',gap:8,margin:'28px 0 18px'} as const;
const identityGrid={display:'grid',gridTemplateColumns:'minmax(0,1fr) minmax(340px,430px)',gap:38,alignItems:'start'} as const;
const eyebrow={letterSpacing:'.12em',fontSize:11,fontWeight:900,color:'#667684',margin:'0 0 7px'} as const;
const methodHeading={fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,margin:0,color:'#0E2A47'} as const;
const lead={color:'#566674',lineHeight:1.65,margin:'14px 0 0'} as const;
const verifiedBadge={background:'#FFF3D5',border:'1px solid #E5C978',color:'#6B501B',padding:'7px 10px',borderRadius:999,fontSize:12,fontWeight:900} as const;
const methodCard={background:'#fff',border:'1px solid #CBD5DD',borderRadius:12,padding:'14px 15px',display:'grid',gridTemplateColumns:'48px 1fr',alignItems:'center',gap:12,boxShadow:'0 8px 24px rgba(8,27,45,.04)'} as const;
const methodMark={width:40,height:40,borderRadius:10,border:'1px solid #C3CED6',display:'grid',placeItems:'center',fontWeight:900,color:'#0E2A47',background:'#F8FAFC'} as const;
const methodText={display:'block',fontSize:13,color:'#667684',lineHeight:1.45,marginTop:3,fontWeight:500} as const;
const verificationBox={background:'#F7FAFC',border:'1px solid #CBD5DD',borderRadius:14,padding:18,marginTop:20,borderLeft:'4px solid #E5A93C'} as const;
const verifiedPanel={background:'#fff',border:'1px solid #CBD5DD',borderRadius:18,padding:24,boxShadow:'0 14px 34px rgba(8,27,45,.07)'} as const;
const panelHeading={fontFamily:'Georgia,serif',fontWeight:500,fontSize:28,margin:'0 0 8px',color:'#0E2A47'} as const;
const primaryLink={display:'inline-block',width:'100%',boxSizing:'border-box',textAlign:'center',background:'#E5A93C',color:'#081B2D',padding:'13px 16px',borderRadius:10,textDecoration:'none',fontWeight:900,border:'1px solid #D69A2F'} as const;
function tab(active:boolean){return {border:'1px solid #BCC8D1',borderRadius:999,padding:'9px 14px',background:active?'#0E2A47':'transparent',color:active?'#fff':'#0E2A47',fontWeight:800,cursor:'pointer'} as const;}
