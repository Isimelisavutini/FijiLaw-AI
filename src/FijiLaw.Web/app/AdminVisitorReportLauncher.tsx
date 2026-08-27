'use client';

import { useEffect, useState } from 'react';
import { usePathname } from 'next/navigation';
import { API_BASE, fetchWithTimeout } from '../lib/api';

export default function AdminVisitorReportLauncher(){
  const pathname=usePathname();
  const[isAdmin,setIsAdmin]=useState(false);

  useEffect(()=>{
    if(!pathname.startsWith('/dashboard')){setIsAdmin(false);return}
    const token=sessionStorage.getItem('fijilaw_access_token');
    if(!token){setIsAdmin(false);return}
    void (async()=>{
      try{
        const response=await fetchWithTimeout(`${API_BASE}/api/membership/me`,{headers:{Authorization:`Bearer ${token}`},cache:'no-store'},8000);
        if(!response.ok){setIsAdmin(false);return}
        const body=await response.json();
        setIsAdmin(Boolean(body.roles?.includes('platform_admin')));
      }catch{setIsAdmin(false)}
    })();
  },[pathname]);

  if(!isAdmin||pathname==='/dashboard/visitor-report')return null;
  return <a href="/dashboard/visitor-report" style={{position:'fixed',right:22,bottom:22,zIndex:80,background:'#E5A93C',color:'#081B2D',padding:'12px 16px',borderRadius:10,fontWeight:800,textDecoration:'none',boxShadow:'0 12px 28px rgba(8,27,45,.24)'}}>Print Visitor Report</a>;
}
