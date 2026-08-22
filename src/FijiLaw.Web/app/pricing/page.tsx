'use client';

import { useEffect, useState } from 'react';

type Plan={code:string;name:string;audience:string;monthlyPriceFjd:number|null;annualPriceFjd:number|null;isPaid:boolean;entitlements:string[]};
const apiBase=process.env.NEXT_PUBLIC_API_URL??'https://fijilaw-api-production-production.up.railway.app';

export default function PricingPage(){
  const [plans,setPlans]=useState<Plan[]>([]); const [annual,setAnnual]=useState(false);
  useEffect(()=>{fetch(`${apiBase}/api/membership/plans`,{cache:'no-store'}).then(r=>r.json()).then(b=>setPlans(b.items??[])).catch(()=>{});},[]);
  return <main style={{maxWidth:1180,margin:'0 auto',padding:'48px 24px 90px',fontFamily:'Inter,system-ui,sans-serif',color:'#16231c'}}>
    <a href="/" style={{fontWeight:800,color:'#16231c',textDecoration:'none',fontSize:21}}>FijiLaw AI</a>
    <p style={{letterSpacing:'.14em',fontSize:12,fontWeight:800,color:'#587063',marginTop:48}}>MEMBERSHIP</p>
    <h1 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:58,margin:'8px 0 14px'}}>Choose the access level that fits your needs.</h1>
    <p style={{fontSize:18,lineHeight:1.6,color:'#58685e',maxWidth:760}}>Public legal access remains available for free. Paid memberships unlock persistent dashboards, professional workflow tools and higher-value member services.</p>
    <div style={{display:'flex',gap:8,margin:'28px 0'}}><button onClick={()=>setAnnual(false)} style={toggle(!annual)}>Monthly</button><button onClick={()=>setAnnual(true)} style={toggle(annual)}>Annual</button></div>
    <div style={{display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(250px,1fr))',gap:14}}>{plans.map(p=><article key={p.code} style={{background:'#fff',border:p.code==='firm_professional'?'2px solid #173f2b':'1px solid #d5ddd7',borderRadius:18,padding:24,display:'flex',flexDirection:'column',minHeight:320}}><p style={{fontSize:12,fontWeight:800,letterSpacing:'.1em',color:'#64736a'}}>{p.audience.toUpperCase()}</p><h2 style={{fontFamily:'Georgia,serif',fontWeight:500,fontSize:30,margin:'8px 0'}}>{p.name}</h2><p style={{fontSize:32,fontWeight:800,margin:'8px 0'}}>{p.monthlyPriceFjd===null?'Contact us':p.monthlyPriceFjd===0?'Free':`FJD $${annual?p.annualPriceFjd:p.monthlyPriceFjd}`}</p><small style={{color:'#6b786f'}}>{p.monthlyPriceFjd&&p.monthlyPriceFjd>0?(annual?'per year':'per month'):''}</small><ul style={{paddingLeft:18,lineHeight:1.7,color:'#536158'}}>{p.entitlements.slice(0,6).map(x=><li key={x}>{x.replaceAll('.',' ')}</li>)}</ul><a href={p.isPaid?'/account':'/'} style={{marginTop:'auto',background:p.isPaid?'#173f2b':'#edf1ee',color:p.isPaid?'#fff':'#173f2b',padding:'12px 14px',borderRadius:10,textAlign:'center',textDecoration:'none',fontWeight:800}}>{p.isPaid?'Create account / Sign in':'Continue free'}</a></article>)}</div>
    <p style={{fontSize:13,color:'#69766e',marginTop:28}}>Paid placement and promotional features are kept separate from FijiLaw AI legal analysis and legal recommendations.</p>
  </main>;
}
function toggle(active:boolean){return {border:'1px solid #b8c4bc',borderRadius:999,padding:'9px 14px',background:active?'#173f2b':'transparent',color:active?'#fff':'#173f2b',fontWeight:700,cursor:'pointer'} as const;}
