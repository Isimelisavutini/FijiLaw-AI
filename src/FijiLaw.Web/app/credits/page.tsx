'use client';

import { useEffect, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError } from '../../lib/api';

type CreditPackage={code:string;name:string;credits:number;priceFjd:number};
type ServicePrice={serviceCode:string;name:string;credits:number;implemented:boolean};
type Wallet={userId:string;balance:number;lifetimePurchased:number;lifetimeGranted:number;lifetimeUsed:number;lastAllowanceKey?:string};
type Transaction={id:string;type:string;status:string;amount:number;balanceBefore:number;balanceAfter:number;serviceCode?:string;createdAt:string};

export default function CreditsPage(){
 const[packages,setPackages]=useState<CreditPackage[]>([]);const[services,setServices]=useState<ServicePrice[]>([]);const[wallet,setWallet]=useState<Wallet|null>(null);const[history,setHistory]=useState<Transaction[]>([]);const[message,setMessage]=useState('');const[busy,setBusy]=useState('');const[demo,setDemo]=useState(false);
 useEffect(()=>{void load()},[]);
 async function load(){
  try{
   const catalog=await fetchWithTimeout(`${API_BASE}/api/credits/catalog`,{cache:'no-store'},10000);const body=await catalog.json();setPackages(body.packages??[]);setServices(body.services??[]);
   const token=sessionStorage.getItem('fijilaw_access_token');if(!token)return;
   const headers={Authorization:`Bearer ${token}`};
   const walletResponse=await fetchWithTimeout(`${API_BASE}/api/credits/wallet`,{headers,cache:'no-store'},10000);
   if(walletResponse.ok){const w=await walletResponse.json();setWallet(w.wallet);setDemo(Boolean(w.demo));}
   const historyResponse=await fetchWithTimeout(`${API_BASE}/api/credits/history`,{headers,cache:'no-store'},10000);if(historyResponse.ok){const h=await historyResponse.json();setHistory(h.items??[]);}
  }catch{setMessage('FijiLaw Credits could not be loaded right now.');}
 }
 async function buy(packageCode:string){
  const token=sessionStorage.getItem('fijilaw_access_token');if(!token){window.location.href='/account?mode=login';return;}
  setBusy(packageCode);setMessage('');
  try{
   const response=await fetchWithTimeout(`${API_BASE}/api/credits/checkout`,{method:'POST',headers:{'Content-Type':'application/json',Authorization:`Bearer ${token}`},body:JSON.stringify({packageCode})},15000);
   const body=await response.json().catch(()=>({}));
   if(!response.ok)throw new Error(body.error??await readApiError(response,'Credit checkout could not be started.'));
   setMessage(body.simulated?'Demo top-up completed. No money was charged.':'Credits added successfully.');await load();
  }catch(e){setMessage(e instanceof Error?e.message:'Credit checkout could not be started.');}finally{setBusy('');}
 }
 return <main style={shell}>
  <div style={top}><a href="/" style={brand}>FijiLaw AI</a><div style={{display:'flex',gap:16}}><a href="/dashboard" style={link}>Dashboard</a><a href="/pricing" style={link}>Plans</a></div></div>
  <p style={eyebrow}>FIJILAW CREDITS</p><h1 style={title}>Your AI usage wallet.</h1><p style={lead}>FijiLaw Credits pay for eligible AI-assisted legal services. They are FijiLaw usage units—not OpenAI API tokens, cash, or cryptocurrency.</p>
  {wallet?<section style={walletCard}><div><span style={small}>AVAILABLE BALANCE</span><strong style={balance}>{wallet.balance.toLocaleString()}</strong><span style={small}>FijiLaw Credits</span></div><div style={stats}><span><strong>{wallet.lifetimeUsed.toLocaleString()}</strong><small> Used</small></span><span><strong>{wallet.lifetimeGranted.toLocaleString()}</strong><small> Included / granted</small></span><span><strong>{wallet.lifetimePurchased.toLocaleString()}</strong><small> Purchased</small></span></div></section>:<section style={notice}>Sign in to see your wallet balance and purchase credits. <a href="/account?mode=login">Sign in →</a></section>}
  {demo&&<section style={demoBox}><strong>Demo credit store.</strong> Package buttons simulate a top-up for testing. No payment is processed.</section>}
  {message&&<section style={notice}>{message}</section>}
  <section style={{marginTop:48}}><p style={eyebrow}>TOP-UP PACKAGES</p><h2 style={sectionTitle}>Buy more FijiLaw Credits</h2><div style={grid}>{packages.map(p=><article style={card} key={p.code}><span style={small}>{p.name.toUpperCase()}</span><strong style={packageCredits}>{p.credits.toLocaleString()}</strong><span>credits</span><p style={price}>FJD ${p.priceFjd.toFixed(2)}</p><button style={button} disabled={busy===p.code} onClick={()=>void buy(p.code)}>{busy===p.code?'Processing…':demo?'Simulate top-up':'Buy credits'}</button></article>)}</div></section>
  <section style={{marginTop:54}}><p style={eyebrow}>SERVICE PRICING</p><h2 style={sectionTitle}>Know the credit cost before you run AI.</h2><div style={serviceList}>{services.map(s=><div style={serviceRow} key={s.serviceCode}><div><strong>{s.name}</strong><small style={{display:'block',color:'#6a786f'}}>{s.implemented?'Available now':'Planned service'}</small></div><strong>{s.credits} credits</strong></div>)}</div></section>
  {wallet&&<section style={{marginTop:54}}><p style={eyebrow}>RECENT ACTIVITY</p><h2 style={sectionTitle}>Credit transactions</h2><div style={serviceList}>{history.length?history.slice(0,12).map(t=><div style={serviceRow} key={t.id}><div><strong>{t.type.replaceAll('_',' ')}</strong><small style={{display:'block',color:'#6a786f'}}>{t.serviceCode?.replaceAll('_',' ')??t.status} · {new Date(t.createdAt).toLocaleString()}</small></div><strong style={{color:t.amount>=0?'#1d5b3a':'#7a3f33'}}>{t.amount>=0?'+':''}{t.amount}</strong></div>):<p>No credit activity yet.</p>}</div></section>}
  <p style={footer}>Credit packages and service prices are FijiLaw commercial usage units. A real purchase will only add credits after server-side payment confirmation. Failed AI workflows automatically refund reserved credits.</p>
 </main>;
}

const shell={maxWidth:1080,margin:'0 auto',padding:'36px 24px 80px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'} as const;
const top={display:'flex',justifyContent:'space-between',alignItems:'center'} as const;const brand={fontWeight:850,fontSize:21,textDecoration:'none',color:'#173f2b'} as const;const link={fontWeight:750,textDecoration:'none',color:'#173f2b'} as const;const eyebrow={marginTop:50,letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063'} as const;const title={fontFamily:'Georgia,serif',fontSize:'clamp(44px,7vw,70px)',fontWeight:500,margin:'8px 0 14px'} as const;const lead={fontSize:19,lineHeight:1.65,color:'#596a60',maxWidth:820} as const;const walletCard={marginTop:30,background:'#173f2b',color:'#fff',padding:30,borderRadius:22,display:'flex',justifyContent:'space-between',gap:24,alignItems:'end',flexWrap:'wrap'} as const;const balance={display:'block',fontFamily:'Georgia,serif',fontSize:64,fontWeight:500,lineHeight:1} as const;const small={fontSize:11,letterSpacing:'.1em',fontWeight:800} as const;const stats={display:'flex',gap:28,flexWrap:'wrap'} as const;const notice={marginTop:24,padding:16,border:'1px solid #d3ddd6',background:'#fff',borderRadius:12,lineHeight:1.6} as const;const demoBox={...notice,background:'#fff7e6',border:'1px solid #e4d29d'} as const;const sectionTitle={fontFamily:'Georgia,serif',fontSize:38,fontWeight:500,margin:'8px 0 20px'} as const;const grid={display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(180px,1fr))',gap:14} as const;const card={background:'#fff',border:'1px solid #d3ddd6',borderRadius:18,padding:22} as const;const packageCredits={display:'block',fontFamily:'Georgia,serif',fontSize:42,fontWeight:500,marginTop:20} as const;const price={fontSize:20,fontWeight:800} as const;const button={width:'100%',border:0,background:'#173f2b',color:'#fff',borderRadius:10,padding:'12px 14px',fontWeight:800,cursor:'pointer'} as const;const serviceList={background:'#fff',border:'1px solid #d3ddd6',borderRadius:18,overflow:'hidden'} as const;const serviceRow={display:'flex',justifyContent:'space-between',gap:20,padding:'16px 20px',borderBottom:'1px solid #e5eae6',alignItems:'center'} as const;const footer={fontSize:13,color:'#68776e',lineHeight:1.7,marginTop:46} as const;
