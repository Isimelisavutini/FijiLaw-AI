'use client';

import { ReactNode, useEffect, useLayoutEffect, useState } from 'react';
import { checkApiHealth, SERVICE_UNAVAILABLE_MESSAGE } from '../lib/api';

export default function ApiResilience({ children }: { children: ReactNode }) {
  const [online, setOnline] = useState<boolean | null>(null);

  useLayoutEffect(() => {
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (...args) => {
      try {
        return await originalFetch(...args);
      } catch (error) {
        if (error instanceof TypeError) {
          throw new Error(SERVICE_UNAVAILABLE_MESSAGE);
        }
        throw error;
      }
    };

    return () => {
      window.fetch = originalFetch;
    };
  }, []);

  useEffect(() => {
    let active = true;
    checkApiHealth().then(result => active && setOnline(result));
    const timer = window.setInterval(() => {
      checkApiHealth().then(result => active && setOnline(result));
    }, 60000);

    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, []);

  return <>
    {online === false && <div role="status" className="serviceNotice">
      <strong>Legal service temporarily unavailable.</strong>
      <span>Your information has not been submitted. Please try again shortly.</span>
    </div>}
    {children}
  </>;
}
