'use client';

import { FormEvent, useEffect, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError } from '../../lib/api';
import styles from './dashboard.module.css';

type Posture={
 registeredUsers:number;pendingUsers:number;suspendedUsers:number;unverifiedUsers:number;
 activeSessions:number;usersWithActiveSessions:number;eventsLast24Hours:number;eventsInPeriod:number;activeAdministrators:number;
};
type EventCount={eventType:string;count:number};
type DailyCount={date:string;count:number};
type AuditEvent={
 id:string;eventType:string;reason?:string|null;createdAt:string;targetUserId?:string|null;
 targetEmail?:string|null;targetDisplayName?:string|null;actorUserId?:string|null;actorEmail?:string|null;actorDisplayName?:string|null;
};
type SecurityResponse={
 posture:Posture;days:number;topEvents:EventCount[];daily:DailyCount[];eventTypes:string[];
 audit:{items:AuditEvent[];page:number;pageSize:number;total:number};
};

export default function AdminSecurityPanel(){
 const[data,setData]=useState<SecurityResponse|null>(null);const[loading,setLoading]=useState(true);
 const[error,setError]=useState('');const[query,setQuery]=useState('');const[eventType,setEventType]=useState('');
 const[days,setDays]=useState(30);const[page,setPage]=useState(1);
 useEffect(()=>{void load(1,30,'','')},[]);

 function token(){return sessionStorage.getItem('fijilaw_access_token')??''}
 async function load(nextPage=page,nextDays=days,nextEventType=eventType,nextQuery=query){
  setLoading(true);setError('');
  try{
   const params=new URLSearchParams({page:String(nextPage),pageSize:'50',days:String(nextDays)});
   if(nextEventType)params.set('eventType',nextEventType);if(nextQuery.trim())params.set('q',nextQuery.trim());
   const response=await fetchWithTimeout(`${API_BASE}/api/admin/security?${params}`,{headers:{Authorization:`Bearer ${token()}`},cache:'no-store'},15000);
   if(response.status===401){sessionStorage.removeItem('fijilaw_access_token');window.location.href='/account?mode=login';return}
   if(response.status===403)throw new Error('Active, verified System Administrator access is required.');
   if(!response.ok)throw new Error(await readApiError(response,'Security and audit data could not be loaded.'));
   const body:SecurityResponse=await response.json();setData(body);setPage(body.audit.page);setDays(body.days);
  }catch(e){setError(e instanceof Error?e.message:'Security and audit data could not be loaded.')}finally{setLoading(false)}
 }
 async function search(event:FormEvent){event.preventDefault();setPage(1);await load(1,days,eventType,query)}
 function changeDays(value:number){setDays(value);setPage(1);void load(1,value,eventType,query)}
 function changeType(value:string){setEventType(value);setPage(1);void load(1,days,value,query)}
 function clearFilters(){setQuery('');setEventType('');setPage(1);void load(1,days,'','')}
 const maxDaily=Math.max(1,...(data?.daily.map(item=>item.count)??[1]));
 const totalPages=Math.max(1,Math.ceil((data?.audit.total??0)/50));

 return <div className={styles.stack} style={{gap:16}}>
  <section className={styles.hero} style={{marginBottom:0}}>
   <div><p className={styles.eyebrow} style={{color:'#F4D28A'}}>SECURITY OPERATIONS</p><h2>Platform security and audit trail</h2><p>Review account posture, active sessions and recorded administrative events. Authentication secrets, credentials and session tokens are never returned to this dashboard.</p></div>
   <span className={styles.heroTag}>{data?.posture.activeAdministrators??'—'} active administrator{data?.posture.activeAdministrators===1?'':'s'}</span>
  </section>

  {error&&<section role="alert" style={errorBox}><strong>Security report unavailable.</strong><p style={{margin:'5px 0 0'}}>{error}</p><button className={styles.secondary} style={{marginTop:10}} onClick={()=>void load()}>Retry</button></section>}

  <div style={postureGrid}>
   <PostureCard label="Pending approval" value={data?.posture.pendingUsers} note="Accounts awaiting your decision" tone={data?.posture.pendingUsers?'warning':'neutral'}/>
   <PostureCard label="Active sessions" value={data?.posture.activeSessions} note={data?`${data.posture.usersWithActiveSessions} signed-in users`:'Loading session posture'} tone="neutral"/>
   <PostureCard label="Suspended users" value={data?.posture.suspendedUsers} note="Access blocked and sessions revoked" tone={data?.posture.suspendedUsers?'danger':'neutral'}/>
   <PostureCard label="Unverified identities" value={data?.posture.unverifiedUsers} note="Pending or active accounts not verified" tone={data?.posture.unverifiedUsers?'warning':'neutral'}/>
   <PostureCard label="Events in 24 hours" value={data?.posture.eventsLast24Hours} note={data?`${data.posture.eventsInPeriod} in selected period`:'Loading audit activity'} tone="neutral"/>
  </div>

  <div className={styles.grid}>
   <section className={styles.card}>
    <div style={sectionHeading}><div><p className={styles.eyebrow}>AUDIT ACTIVITY</p><h3 style={{fontSize:20,marginTop:5}}>Events by day</h3></div><select aria-label="Audit time range" value={days} onChange={event=>changeDays(Number(event.target.value))} style={select}>{[7,14,30,60,90].map(value=><option key={value} value={value}>Last {value} days</option>)}</select></div>
    {loading&&!data?<p>Loading audit activity…</p>:<div style={chart}>{data?.daily.map(item=><div key={item.date} title={`${item.date}: ${item.count} events`} style={barColumn}><div style={{...bar,height:`${Math.max(4,(item.count/maxDaily)*100)}%`}}/><span>{days<=14?item.date.slice(5):item.date.slice(8)}</span></div>)}</div>}
   </section>
   <section className={styles.card}>
    <p className={styles.eyebrow}>MOST FREQUENT</p><h3 style={{fontSize:20,marginTop:5}}>Event categories</h3>
    <div style={{display:'grid',gap:9,marginTop:14}}>{data?.topEvents.map(item=><div key={item.eventType} style={eventCountRow}><span>{eventLabel(item.eventType)}</span><strong>{item.count.toLocaleString()}</strong></div>)}{!loading&&data?.topEvents.length===0&&<div className={styles.empty}>No audit events in this period.</div>}</div>
   </section>
  </div>

  <section className={styles.card}>
   <div style={sectionHeading}><div><p className={styles.eyebrow}>AUDIT EVENT LOG</p><h3 style={{fontSize:20,marginTop:5}}>Security and membership audit</h3><p>Showing recorded account and administrator actions. Use Users to change access or revoke sessions.</p></div><span style={recordCount}>{data?.audit.total?.toLocaleString()??'—'} matching records</span></div>
   <form onSubmit={search} style={filterRow}>
    <label style={srOnly} htmlFor="audit-search">Search audit history</label>
    <input id="audit-search" value={query} onChange={event=>setQuery(event.target.value)} placeholder="Search user, actor, event or reason" style={searchInput}/>
    <label style={srOnly} htmlFor="event-type">Event type</label>
    <select id="event-type" value={eventType} onChange={event=>changeType(event.target.value)} style={select}><option value="">All event types</option>{data?.eventTypes.map(type=><option key={type} value={type}>{eventLabel(type)}</option>)}</select>
    <button className={styles.secondary} disabled={loading}>{loading?'Loading…':'Search'}</button>
    <button type="button" className={styles.secondary} onClick={clearFilters}>Clear</button>
   </form>
   <div style={{overflowX:'auto',marginTop:14}}>
    <table style={table}><thead><tr><th style={th}>Time</th><th style={th}>Event</th><th style={th}>Target account</th><th style={th}>Actor</th><th style={th}>Reason</th></tr></thead>
     <tbody>{data?.audit.items.map(item=><tr key={item.id}><td style={td}><time>{new Date(item.createdAt).toLocaleString()}</time></td><td style={td}><span style={securityBadge(item.eventType)}>{eventLabel(item.eventType)}</span></td><td style={td}><strong>{item.targetDisplayName||item.targetEmail||'System event'}</strong>{item.targetDisplayName&&item.targetEmail&&<small style={subText}>{item.targetEmail}</small>}</td><td style={td}>{item.actorDisplayName||item.actorEmail||'System'}{item.actorDisplayName&&item.actorEmail&&<small style={subText}>{item.actorEmail}</small>}</td><td style={{...td,maxWidth:420,color:'#566674'}}>{item.reason||'No reason recorded.'}</td></tr>)}</tbody>
    </table>
   </div>
   {!loading&&data?.audit.items.length===0&&<div className={styles.empty}>No audit events match these filters.</div>}
   {(data?.audit.total??0)>0&&<div style={{...sectionHeading,alignItems:'center',marginTop:16}}><span style={pageText}>Page {page} of {totalPages}</span><div style={{display:'flex',gap:7}}><button className={styles.secondary} disabled={loading||page<=1} onClick={()=>{const next=page-1;setPage(next);void load(next)}}>Previous</button><button className={styles.secondary} disabled={loading||page>=totalPages} onClick={()=>{const next=page+1;setPage(next);void load(next)}}>Next</button></div></div>}
  </section>
 </div>;
}

function PostureCard({label,value,note,tone}:{label:string;value?:number;note:string;tone:'neutral'|'warning'|'danger'}){
 const border=tone==='danger'?'#C85D52':tone==='warning'?'#E5A93C':'#8CA2B3';
 return <section className={styles.kpi} style={{borderTopColor:border}}><span>{label}</span><strong>{value?.toLocaleString()??'—'}</strong><small style={subText}>{note}</small></section>;
}
function eventLabel(value:string){return value.replaceAll('_',' ').replace(/\b\w/g,letter=>letter.toUpperCase())}
function securityBadge(type:string){
 const danger=type.includes('suspend')||type.includes('failed');
 const warning=type.includes('reset')||type.includes('revoked')||type.includes('role');
 return {display:'inline-block',borderRadius:999,padding:'5px 8px',fontSize:10,fontWeight:900,whiteSpace:'nowrap' as const,
  background:danger?'#FFF0EE':warning?'#FFF5D9':'#EAF3F8',color:danger?'#862D25':warning?'#785918':'#315A72'};
}

const postureGrid={display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(165px,1fr))',gap:12};
const sectionHeading={display:'flex',justifyContent:'space-between',gap:16,alignItems:'flex-start',flexWrap:'wrap' as const};
const filterRow={display:'flex',gap:8,flexWrap:'wrap' as const,marginTop:16};
const searchInput={flex:'1 1 290px',border:'1px solid #BFCBD4',borderRadius:9,padding:'10px 12px',fontSize:13};
const select={border:'1px solid #BFCBD4',borderRadius:9,padding:'9px 11px',fontSize:12,background:'#fff',color:'#0E2A47'};
const chart={height:190,display:'flex',alignItems:'flex-end',gap:3,marginTop:22,borderBottom:'1px solid #CBD5DD',padding:'0 2px'};
const barColumn={height:'100%',flex:1,minWidth:3,display:'flex',flexDirection:'column' as const,justifyContent:'flex-end',alignItems:'center',gap:5};
const bar={width:'100%',minHeight:4,background:'linear-gradient(180deg,#E5A93C,#B47716)',borderRadius:'4px 4px 0 0'};
const eventCountRow={display:'flex',justifyContent:'space-between',gap:12,borderBottom:'1px solid #E5EAEE',padding:'9px 0',color:'#405463'};
const recordCount={border:'1px solid #CBD5DD',background:'#F7FAFC',borderRadius:999,padding:'7px 10px',fontSize:11,fontWeight:900,color:'#405463'};
const table={width:'100%',borderCollapse:'collapse' as const,minWidth:940,fontSize:12};
const th={textAlign:'left' as const,padding:'10px 8px',borderBottom:'1px solid #CBD5DD',color:'#0E2A47',fontSize:10,textTransform:'uppercase' as const,letterSpacing:'.06em'};
const td={padding:'12px 8px',borderBottom:'1px solid #E5EAEE',verticalAlign:'top' as const};
const subText={display:'block',color:'#667684',fontSize:10,marginTop:4,lineHeight:1.4};
const pageText={color:'#667684',fontSize:12,fontWeight:800};
const errorBox={background:'#FFF0EE',border:'1px solid #DCA59F',color:'#862D25',padding:14,borderRadius:10};
const srOnly={position:'absolute' as const,width:1,height:1,padding:0,margin:-1,overflow:'hidden',clip:'rect(0,0,0,0)',whiteSpace:'nowrap' as const,border:0};
