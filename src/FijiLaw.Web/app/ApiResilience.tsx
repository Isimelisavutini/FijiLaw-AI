'use client';

import { ReactNode, useCallback, useEffect, useRef, useState } from 'react';
import { checkApiHealth } from '../lib/api';

export default function ApiResilience({ children }: { children: ReactNode }) {
  const [online, setOnline] = useState<boolean | null>(null);
  const [checking, setChecking] = useState(false);
  const failures = useRef(0);

  const probe = useCallback(async (showChecking = false) => {
    if (showChecking) setChecking(true);
    try {
      const healthy = await checkApiHealth();
      if (healthy) {
        failures.current = 0;
        setOnline(true);
        return true;
      }

      failures.current += 1;
      if (failures.current >= 2) setOnline(false);
      return false;
    } finally {
      if (showChecking) setChecking(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    let retryTimer: number | undefined;

    async function initialCheck() {
      const healthy = await probe();
      if (!active || healthy) return;
      retryTimer = window.setTimeout(() => {
        if (active) void probe();
      }, 2500);
    }

    void initialCheck();
    const interval = window.setInterval(() => {
      if (active) void probe();
    }, 45000);

    const handleOnline = () => {
      failures.current = 0;
      if (active) void probe();
    };
    window.addEventListener('online', handleOnline);

    return () => {
      active = false;
      window.clearInterval(interval);
      if (retryTimer) window.clearTimeout(retryTimer);
      window.removeEventListener('online', handleOnline);
    };
  }, [probe]);

  return <>
    {online === false && <div role="status" className="serviceNotice">
      <strong>Connection to FijiLaw AI is interrupted.</strong>
      <span>The API will be checked again automatically. Your information is not submitted until a request succeeds.</span>
      <button
        type="button"
        onClick={() => void probe(true)}
        disabled={checking}
        style={{border:'1px solid #9B6A0B',background:'#fff',color:'#0E2A47',borderRadius:7,padding:'6px 10px',fontWeight:800,cursor:checking?'wait':'pointer'}}
      >{checking ? 'Checking…' : 'Retry now'}</button>
    </div>}
    {children}
  </>;
}
