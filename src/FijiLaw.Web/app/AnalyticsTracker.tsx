'use client';

import { useEffect } from 'react';
import { usePathname } from 'next/navigation';
import { API_BASE } from '../lib/api';

const VISITOR_KEY='fijilaw_analytics_visitor_id';

function visitorId(){
  let id=localStorage.getItem(VISITOR_KEY);
  if(!id){id=crypto.randomUUID();localStorage.setItem(VISITOR_KEY,id)}
  return id;
}

export default function AnalyticsTracker(){
  const pathname=usePathname();
  useEffect(()=>{
    if(!pathname)return;
    const token=sessionStorage.getItem('fijilaw_access_token');
    const headers:Record<string,string>={'Content-Type':'application/json'};
    if(token)headers.Authorization=`Bearer ${token}`;
    const body={visitorId:visitorId(),path:pathname,referrer:document.referrer||null,userAgent:navigator.userAgent};
    fetch(`${API_BASE}/api/analytics/visit`,{method:'POST',headers,body:JSON.stringify(body),keepalive:true}).catch(()=>{});
  },[pathname]);
  return null;
}
