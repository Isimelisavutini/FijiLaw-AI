'use client';

import { useEffect, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError } from '../../lib/api';

type CreditPackage={code:string;name:string;credits:number;priceFjd:number};
type ServicePrice={serviceCode:string;name:string;credits:number;implemented:boolean};
type Wallet={userId:string;balance:number;lifetimePurchased:number;lifetimeGranted:number;lifetimeUsed:number;lastAllowanceKey?:string};
type Transaction={id:string;type:string;status:string;amount:number;balanceBefore:number;balanceAfter:number;serviceCode?:string;createdAt:string};

export default function CreditsPage(){
 const[packages,setPackages]=useState<CreditPackage[]>([]);const[services,setServices]=useState<ServicePrice[]>([]);const[wallet,setWallet]=useState<Wallet|null>(null);const[history,setHistory]=useState<Transaction[]>([]);const[message,setMessage]=useState('');const[busy,setBusy]=useState('');const[demo,setDemo]=useState(false);const[paymentReady,setPaymentReady]=useState(false);const[paymentProvider,setPaymentProvider]=useState('');
 useEffect(()=>{void initialize()},[]);
 async function initialize(){await load();const params=new URLSearchParams(window.location.search);const order=params.get('order');const payment=params.get('payment');if(order&&payment)await verifyPayment(order,payment);}
 async function load(){
  try{
   const catalog=await fetchWithTimeout(`${API_BASE}/api/credits/catalog`,{cache:'no-store'},10000);const body=await catalog.json();setPackages(body.packages??[]);setServices(body.services??[]);setPaymentReady(Boolean(body.paymentCheckoutReady));setPaymentProvider(body.paymentProvider??'');
   const token=sessionStorage.getItem('fijilaw_access_token');if(!token)return;
   const headers={Authorization:`Bearer ${token}`};
   const walletResponse=await fetchWithTimeout(`${API_BASE}/api/credits/wallet`,{headers,cache:'no-store'},10000);
   if(walletResponse.ok){const w=await walletResponse.json();setWallet(w.wallet);setDemo(Boolean(w.demo));}
   const historyResponse=await fetchWithTimeout(`${API_BASE}/api/credits/history`,{headers,cache:'no-store'},10000);if(historyResponse.ok){const h=await historyResponse.json();setHistory(h.items??[]);}
  }catch{setMessage('FijiLaw Credits could not be loaded right now.');}
 }
 async function verifyPayment(orderId:string,returnState:string){
  const token=sessionStorage.getItem('fijilaw_access_token');if(!token){window.location.href='/account?mode=login';return;}
  setMessage('Verifying payment with the payment provider…');
  try{
   const response=await fetchWithTimeout(`${API_BASE}/api/credits/payment/status/${encodeURIComponent(orderId)}`,{headers:{Authorization:`Bearer ${token}`},cache:'no-store'},20000);
   const body=await response.json().catch(()=>({}));
   if(!response.ok)throw new Error(body.error??'Payment could not be verified.');
   if(body.completed)setMessage('Payment confirmed. Your FijiLaw Credits have been added.');
   else if(returnState==='cancelled')setMessage('Payment was cancelled. No credits were added.');
   else if(returnState==='declined')setMessage('Payment was not approved. No credits were added.');
   else setMessage(`Payment status: ${body.status??'pending'}. No credits are added until server-side confirmation succeeds.`);
   await load();window.history.replaceState({},'',window.location.pathname);
  }catch(e){setMessage(e instanceof Error?e.message:'Payment verification failed.');}
 }
 async function buy(packageCode:string){
  const token=sessionStorage.getItem('fijilaw_access_token');if(!token){window.location.href='/account?mode=login';return;}
  setBusy(packageCode);setMessage('');
  try{
   const response=await fetchWithTimeout(`${API_BASE}/api/credits/checkout`,{method:'POST',headers:{'Content-Type':'application/json',Authorization:`Bearer ${token}`},body:JSON.stringify({packageCode})},20000);
   const body=await response.json().catch(()=>({}));
   if(!response.ok)throw new Error(body.error??await readApiError(response,'Credit checkout could not be started.'));
   if(body.checkoutUrl){window.location.href=body.checkoutUrl;return;}
   setMessage(body.simulated?'Demo top-up completed. No money was charged.':'Credit purchase request completed.');await load();
  }catch(e){setMessage(e instanceof Error?e.message:'Credit checkout could not be started.');}finally{setBusy('');}
 }
 return <main style={shell}>
  <div style={top}><a href="/" style={brand}>FijiLaw AI</a><div style={{display:'flex',gap:16}}><a href="/dashboard" style={link}>Dashboard</a><a href="/pricing" style={link}>Plans</a></div></div>
  <p style={eyebrow}>FIJILAW CREDITS</p><h1 style={title}>Your AI usage wallet.</h1><p style={lead}>FijiLaw Credits pay for eligible AI-assisted legal services. They are FijiLaw usage units—not OpenAI API tokens, cash, or cryptocurrency.</p>
  {wallet?<section style={walletCard}><div><span style={goldSmall}>AVAILABLE BALANCE</span><strong style={balance}>{wallet.balance.toLocaleString()}</strong><span style={small}>FijiLaw Credits</span></div><div style={stats}><span><strong>{wallet.lifetimeUsed.toLocaleString()}</strong><small> Used</small></span><span><strong>{wallet.lifetimeGranted.toLocaleString()}</strong><small> Included / granted</small></span><span><strong>{wallet.lifetimePurchased.toLocaleString()}</strong><small> Purchased</small></span></div></section>:<section style={notice}>Sign in to see your wallet balance and purchase credits. <a href="/account?mode=login" style={inlineLink}>Sign in →</a></section>}
  {demo&&<section style={demoBox}><strong>Demo credit store.</strong> Package buttons simulate a top-up for testing. No payment is processed.</section>}
  {!demo&&wallet&&!paymentReady&&<section style={demoBox}><strong>Online checkout setup in progress.</strong> FijiLaw is prepared for Windcave hosted payments, but merchant API credentials are not configured yet. Your existing included credits remain usable.</section>}
  {!demo&&paymentReady&&<section style={successBox}><strong>Secure hosted checkout ready.</strong> Purchases are processed by {paymentProvider||'the configured payment provider'} and credits are added only after FijiLaw verifies the payment server-side.</section>}
  {message&&<section style={notice}>{message}</section>}
  <section style={{marginTop:48}}><p style={eyebrow}>TOP-UP PACKAGES</p><h2 style={sectionTitle}>Buy more FijiLaw Credits</h2><div style={grid}>{packages.map(p=><article style={card} key={p.code}><span style={small}>{p.name.toUpperCase()}</span><strong style={packageCredits}>{p.credits.toLocaleString()}</strong><span>credits</span><p style={price}>FJD ${p.priceFjd.toFixed(2)}</p><button style={button} disabled={busy===p.code||(!demo&&!paymentReady)} onClick={()=>void buy(p.code)}>{busy===p.code?'Processing…':demo?'Simulate top-up':paymentReady?'Buy credits':'Checkout coming soon'}</button></article>)}</div></section>
  <section style={{marginTop:54}}><p style={eyebrow}>SERVICE PRICING</p><h2 style={sectionTitle}>Know the credit cost before you run AI.</h2><div style={serviceList}>{services.map(s=><div style={serviceRow} key={s.serviceCode}><div><strong>{s.name}</strong><small style={{display:'block',color:'#667684'}}>{s.implemented?'Available now':'Planned service'}</small></div><strong style={{color:'#0E2A47'}}>{s.credits} credits</strong></div>)}</div></section>
  {wallet&&<section style={{marginTop:54}}><p style={eyebrow}>RECENT ACTIVITY</p><h2 style={sectionTitle}>Credit transactions</h2><div style={serviceList}>{history.length?history.slice(0,12).map(t=><div style={serviceRow} key={t.id}><div><strong>{t.type.replaceAll('_',' ')}</strong><small style={{display:'block',color:'#667684'}}>{t.serviceCode?.replaceAll('_',' ')??t.status} · {new Date(t.createdAt).toLocaleString()}</small></div><strong style={{color:t.amount>=0?'#2F7254':'#9A473A'}}>{t.amount>=0?'+':''}{t.amount}</strong></div>):<p style={{padding:20}}>No credit activity yet.</p>}</div></section>}
  <p style={footer}>Credit packages and service prices are FijiLaw commercial usage units. A purchase adds credits only after authoritative server-side payment verification. Failed AI workflows automatically refund reserved credits.</p>
 </main>;
}

const shell={maxWidth:1080,margin:'0 auto',padding:'36px 24px 80px',fontFamily:'Inter,system-ui,sans-serif',color:'#081B2D'} as const;
const top={display:'flex',justifyContent:'space-between',alignItems:'center',borderBottom:'1px solid #CBD5DD',paddingBottom:18} as const;const brand={fontWeight:900,fontSize:21,textDecoration:'none',color:'#0E2A47'} as const;const link={fontWeight:800,textDecoration:'none',color:'#0E2A47'} as const;const eyebrow={marginTop:50,letterSpacing:'.14em',fontSize:12,fontWeight:900,color:'#667684'} as const;const title={fontFamily:'Georgia,serif',fontSize:'clamp(44px,7vw,70px)',fontWeight:500,margin:'8px 0 14px',color:'#0E2A47'} as const;const lead={fontSize:19,lineHeight:1.65,color:'#566674',maxWidth:820} as const;const walletCard={marginTop:30,background:'linear-gradient(135deg,#081B2D,#0E2A47)',color:'#fff',padding:30,borderRadius:20,display:'flex',justifyContent:'space-between',gap:24,alignItems:'end',flexWrap:'wrap',borderBottom:'3px solid #E5A93C',boxShadow:'0 22px 48px rgba(8,27,45,.16)'} as const;const balance={display:'block',fontFamily:'Georgia,serif',fontSize:64,fontWeight:500,lineHeight:1,color:'#fff'} as const;const small={fontSize:11,letterSpacing:'.1em',fontWeight:900,color:'#667684'} as const;const goldSmall={...small,color:'#F4D28A'} as const;const stats={display:'flex',gap:28,flexWrap:'wrap'} as const;const notice={marginTop:24,padding:16,border:'1px solid #CBD5DD',background:'#fff',borderRadius:12,lineHeight:1.6,boxShadow:'0 10px 26px rgba(8,27,45,.05)'} as const;const demoBox={...notice,background:'#fff7e8',border:'1px solid #E5C978'} as const;const successBox={...notice,background:'#edf7f1',border:'1px solid #b8d7c4'} as const;const sectionTitle={fontFamily:'Georgia,serif',fontSize:38,fontWeight:500,margin:'8px 0 20px',color:'#0E2A47'} as const;const grid={display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(180px,1fr))',gap:14} as const;const card={background:'#fff',border:'1px solid #CBD5DD',borderRadius:16,padding:22,boxShadow:'0 10px 26px rgba(8,27,45,.05)',borderTop:'3px solid #E5A93C'} as const;const packageCredits={display:'block',fontFamily:'Georgia,serif',fontSize:42,fontWeight:500,marginTop:20,color:'#0E2A47'} as const;const price={fontSize:20,fontWeight:900,color:'#0E2A47'} as const;const button={width:'100%',border:'1px solid rgba(8,27,45,.08)',background:'#E5A93C',color:'#081B2D',borderRadius:9,padding:'12px 14px',fontWeight:900,cursor:'pointer'} as const;const serviceList={background:'#fff',border:'1px solid #CBD5DD',borderRadius:16,overflow:'hidden',boxShadow:'0 10px 26px rgba(8,27,45,.05)'} as const;const serviceRow={display:'flex',justifyContent:'space-between',gap:20,padding:'16px 20px',borderBottom:'1px solid #E1E7EC',alignItems:'center'} as const;const footer={fontSize:13,color:'#667684',lineHeight:1.7,marginTop:46} as const;const inlineLink={color:'#9B6A0B',fontWeight:900} as const;
