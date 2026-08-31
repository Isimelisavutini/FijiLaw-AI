'use client';

import { FormEvent, useEffect, useState } from 'react';
import { API_BASE, fetchWithTimeout, readApiError } from '../../lib/api';
import styles from './dashboard.module.css';

type Role={code:string;name:string;description?:string|null};
type User={
 id:string;email:string;displayName?:string|null;emailVerified:boolean;identityVerified:boolean;
 status:'pending'|'active'|'suspended'|string;roles:string[];planCode:string;subscriptionStatus:string;
 activeSessions:number;createdAt:string;updatedAt:string;
};
type AuditEvent={id:string;eventType:string;reason?:string|null;createdAt:string;actorEmail?:string|null;actorDisplayName?:string|null};
type UsersResponse={items:User[];roles:Role[];page:number;pageSize:number;total:number};

export default function AdminUsersPanel(){
 const[users,setUsers]=useState<User[]>([]);const[roles,setRoles]=useState<Role[]>([]);const[query,setQuery]=useState('');
 const[loading,setLoading]=useState(true);const[error,setError]=useState('');const[notice,setNotice]=useState('');
 const[busyId,setBusyId]=useState('');const[editingId,setEditingId]=useState('');const[roleDraft,setRoleDraft]=useState<string[]>([]);
 const[auditUser,setAuditUser]=useState<User|null>(null);const[audit,setAudit]=useState<AuditEvent[]>([]);const[auditLoading,setAuditLoading]=useState(false);
 useEffect(()=>{void loadUsers('')},[]);

 function token(){return sessionStorage.getItem('fijilaw_access_token')??''}
 async function adminFetch(path:string,init:RequestInit={}){
  const response=await fetchWithTimeout(`${API_BASE}${path}`,{...init,headers:{Authorization:`Bearer ${token()}`,'Content-Type':'application/json',...(init.headers??{})},cache:'no-store'},15000);
  if(response.status===401){sessionStorage.removeItem('fijilaw_access_token');window.location.href='/account?mode=login';throw new Error('Your session expired.')}
  if(response.status===403)throw new Error('System Administrator access is required.');
  if(!response.ok)throw new Error(await readApiError(response,'The administrator request could not be completed.'));
  return response;
 }
 async function loadUsers(search=query){
  setLoading(true);setError('');
  try{const response=await adminFetch(`/api/admin/users?pageSize=100&q=${encodeURIComponent(search)}`);const body:UsersResponse=await response.json();setUsers(body.items);setRoles(body.roles)}
  catch(e){setError(e instanceof Error?e.message:'Users could not be loaded.')}finally{setLoading(false)}
 }
 async function search(e:FormEvent){e.preventDefault();await loadUsers(query)}
 async function changeStatus(user:User,status:'active'|'suspended'){
  const action=status==='active'?(user.status==='pending'?'approve':'reactivate'):'suspend';
  if(!window.confirm(`Are you sure you want to ${action} ${user.email}?${status==='suspended'?' All active sessions will be revoked.':''}`))return;
  setBusyId(user.id);setError('');setNotice('');
  try{await adminFetch(`/api/admin/users/${user.id}/status`,{method:'PUT',body:JSON.stringify({status})});setNotice(`${user.email} was ${action}d.`);await loadUsers(query)}
  catch(e){setError(e instanceof Error?e.message:'Account status could not be updated.')}finally{setBusyId('')}
 }
 async function revokeSessions(user:User){
  if(!window.confirm(`Revoke every active session for ${user.email}? The user will need to sign in again.`))return;
  setBusyId(user.id);setError('');setNotice('');
  try{const response=await adminFetch(`/api/admin/users/${user.id}/sessions/revoke`,{method:'POST',body:JSON.stringify({})});const body=await response.json();setNotice(`${body.revokedSessions} session(s) revoked for ${user.email}.`);await loadUsers(query)}
  catch(e){setError(e instanceof Error?e.message:'Sessions could not be revoked.')}finally{setBusyId('')}
 }
 function editRoles(user:User){setEditingId(user.id);setRoleDraft([...user.roles]);setAuditUser(null)}
 function toggleRole(code:string){setRoleDraft(current=>current.includes(code)?current.filter(role=>role!==code):[...current,code])}
 async function saveRoles(user:User){
  if(!window.confirm(`Replace roles for ${user.email} with: ${roleDraft.join(', ')||'no roles'}?`))return;
  setBusyId(user.id);setError('');setNotice('');
  try{await adminFetch(`/api/admin/users/${user.id}/roles`,{method:'PUT',body:JSON.stringify({roles:roleDraft})});setNotice(`Roles updated for ${user.email}.`);setEditingId('');await loadUsers(query)}
  catch(e){setError(e instanceof Error?e.message:'Roles could not be updated.')}finally{setBusyId('')}
 }
 async function viewAudit(user:User){
  setAuditUser(user);setEditingId('');setAuditLoading(true);setError('');
  try{const response=await adminFetch(`/api/admin/users/${user.id}/audit?limit=100`);const body=await response.json();setAudit(body.items??[])}
  catch(e){setError(e instanceof Error?e.message:'Audit history could not be loaded.');setAudit([])}finally{setAuditLoading(false)}
 }
 const pending=users.filter(user=>user.status==='pending').length;
 return <div className={styles.stack} style={{gap:16}}>
  <section className={styles.card}>
   <div style={headerRow}><div><p className={styles.eyebrow}>SYSTEM ADMINISTRATOR CONTROL PLANE</p><h3 style={{fontSize:22,marginTop:6}}>Accounts and access</h3><p>New registrations remain pending until you approve them. Approval requires a verified identity.</p></div><div style={summaryBox}><strong>{pending}</strong><span>awaiting approval</span></div></div>
   <form onSubmit={search} style={searchRow}><label htmlFor="user-search" style={srOnly}>Search users</label><input id="user-search" value={query} onChange={event=>setQuery(event.target.value)} placeholder="Search by name or email" style={searchInput}/><button className={styles.secondary} disabled={loading}>{loading?'Loading…':'Search'}</button><button type="button" className={styles.secondary} onClick={()=>{setQuery('');void loadUsers('')}}>Clear</button></form>
   {error&&<p role="alert" style={errorBox}>{error}</p>}{notice&&<p role="status" style={noticeBox}>{notice}</p>}
  </section>

  <section className={styles.card}>
   <div style={{overflowX:'auto'}}>
    <table style={table}><thead><tr><th style={th}>Account</th><th style={th}>Status</th><th style={th}>Identity</th><th style={th}>Roles</th><th style={th}>Plan</th><th style={th}>Sessions</th><th style={th}>Actions</th></tr></thead>
     <tbody>{users.map(user=><tr key={user.id}><td style={td}><strong>{user.displayName||'Unnamed account'}</strong><small style={subText}>{user.email}</small><small style={subText}>Created {new Date(user.createdAt).toLocaleDateString()}</small></td><td style={td}><span style={statusBadge(user.status)}>{user.status}</span></td><td style={td}>{user.identityVerified?<span style={verified}>Verified</span>:<span style={unverified}>Not verified</span>}</td><td style={td}><div style={tagWrap}>{user.roles.map(role=><span key={role} style={tag}>{role.replaceAll('_',' ')}</span>)}</div></td><td style={td}><span>{user.planCode.replaceAll('_',' ')}</span><small style={subText}>{user.subscriptionStatus}</small></td><td style={td}>{user.activeSessions}</td><td style={td}><div style={actions}>{user.status==='pending'&&<button disabled={busyId===user.id||!user.identityVerified} title={!user.identityVerified?'Identity verification is required before approval.':undefined} style={primarySmall} onClick={()=>void changeStatus(user,'active')}>Approve</button>}{user.status==='suspended'?<button disabled={busyId===user.id||!user.identityVerified} className={styles.secondary} onClick={()=>void changeStatus(user,'active')}>Reactivate</button>:user.status==='active'&&<button disabled={busyId===user.id} style={dangerSmall} onClick={()=>void changeStatus(user,'suspended')}>Suspend</button>}<button disabled={busyId===user.id||user.activeSessions===0} className={styles.secondary} onClick={()=>void revokeSessions(user)}>Revoke sessions</button><button disabled={busyId===user.id} className={styles.secondary} onClick={()=>editRoles(user)}>Roles</button><button disabled={busyId===user.id} className={styles.secondary} onClick={()=>void viewAudit(user)}>Audit</button></div></td></tr>)}</tbody>
    </table>
   </div>
   {!loading&&users.length===0&&<div className={styles.empty}>No accounts match this search.</div>}
  </section>

  {editingId&&(()=>{const user=users.find(item=>item.id===editingId);return user?<section className={styles.card}><div style={headerRow}><div><p className={styles.eyebrow}>ROLE ASSIGNMENT</p><h3 style={{marginTop:6}}>{user.email}</h3><p>Role changes are enforced by the API and recorded in the audit history.</p></div><button className={styles.secondary} onClick={()=>setEditingId('')}>Close</button></div><div style={roleGrid}>{roles.map(role=><label key={role.code} style={roleOption}><input type="checkbox" checked={roleDraft.includes(role.code)} onChange={()=>toggleRole(role.code)}/><span><strong>{role.name}</strong><small style={subText}>{role.description||role.code}</small></span></label>)}</div><div style={actions}><button style={primarySmall} disabled={busyId===user.id} onClick={()=>void saveRoles(user)}>Save roles</button><button className={styles.secondary} onClick={()=>setEditingId('')}>Cancel</button></div></section>:null})()}

  {auditUser&&<section className={styles.card}><div style={headerRow}><div><p className={styles.eyebrow}>IMMUTABLE ACCESS HISTORY</p><h3 style={{marginTop:6}}>Audit history for {auditUser.email}</h3></div><button className={styles.secondary} onClick={()=>setAuditUser(null)}>Close</button></div>{auditLoading?<p>Loading audit history…</p>:audit.length===0?<div className={styles.empty}>No audit events recorded.</div>:<div>{audit.map(event=><div key={event.id} style={auditRow}><div><strong>{event.eventType.replaceAll('_',' ')}</strong><p style={{margin:'4px 0'}}>{event.reason||'No reason supplied.'}</p><small style={subText}>Actor: {event.actorDisplayName||event.actorEmail||'System'}</small></div><time style={auditTime}>{new Date(event.createdAt).toLocaleString()}</time></div>)}</div>}</section>}
 </div>;
}

const table={width:'100%',borderCollapse:'collapse' as const,minWidth:1040,fontSize:13};
const th={textAlign:'left' as const,padding:'11px 9px',borderBottom:'1px solid #CBD5DD',color:'#0E2A47',fontSize:11,textTransform:'uppercase' as const,letterSpacing:'.06em'};
const td={padding:'13px 9px',borderBottom:'1px solid #E5EAEE',verticalAlign:'top' as const};
const headerRow={display:'flex',justifyContent:'space-between',gap:20,alignItems:'flex-start',flexWrap:'wrap' as const};
const searchRow={display:'flex',gap:8,marginTop:18,flexWrap:'wrap' as const};
const searchInput={flex:'1 1 280px',border:'1px solid #BFCBD4',borderRadius:9,padding:'10px 12px',fontSize:14};
const summaryBox={display:'grid',background:'#FFF6DE',border:'1px solid #E5C978',borderRadius:12,padding:'12px 18px',minWidth:130};
const subText={display:'block',color:'#667684',fontSize:11,marginTop:4};
const tagWrap={display:'flex',gap:4,flexWrap:'wrap' as const};
const tag={background:'#EDF2F5',color:'#405463',borderRadius:999,padding:'4px 7px',fontSize:10,fontWeight:800};
const actions={display:'flex',gap:6,flexWrap:'wrap' as const};
const primarySmall={border:'1px solid #C88C24',borderRadius:8,padding:'9px 11px',background:'#E5A93C',color:'#081B2D',fontWeight:900,cursor:'pointer'};
const dangerSmall={border:'1px solid #B9463B',borderRadius:8,padding:'9px 11px',background:'#FFF2F0',color:'#8D2E26',fontWeight:900,cursor:'pointer'};
const verified={color:'#276448',background:'#EAF6EF',borderRadius:999,padding:'5px 8px',fontSize:11,fontWeight:900};
const unverified={color:'#785918',background:'#FFF5D9',borderRadius:999,padding:'5px 8px',fontSize:11,fontWeight:900};
const errorBox={background:'#FFF0EE',border:'1px solid #DCA59F',color:'#862D25',padding:12,borderRadius:9};
const noticeBox={background:'#EAF6EF',border:'1px solid #AFCFBC',color:'#285F45',padding:12,borderRadius:9};
const roleGrid={display:'grid',gridTemplateColumns:'repeat(auto-fit,minmax(220px,1fr))',gap:10,margin:'18px 0'};
const roleOption={display:'flex',gap:10,alignItems:'flex-start',border:'1px solid #CBD5DD',borderRadius:10,padding:12,background:'#F9FBFC'};
const auditRow={display:'flex',justifyContent:'space-between',gap:18,borderTop:'1px solid #E1E7EB',padding:'14px 0'};
const auditTime={whiteSpace:'nowrap' as const,color:'#667684',fontSize:11};
const srOnly={position:'absolute' as const,width:1,height:1,padding:0,margin:-1,overflow:'hidden',clip:'rect(0,0,0,0)',whiteSpace:'nowrap' as const,border:0};
function statusBadge(status:string){const palette=status==='active'?{background:'#EAF6EF',color:'#276448'}:status==='pending'?{background:'#FFF5D9',color:'#785918'}:{background:'#FFF0EE',color:'#862D25'};return {...palette,borderRadius:999,padding:'5px 8px',fontSize:11,fontWeight:900,textTransform:'capitalize' as const}}
