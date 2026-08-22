'use client';

import { usePathname } from 'next/navigation';

export default function MembershipEntryBar() {
  const pathname = usePathname();
  if (pathname !== '/') return null;

  return <aside className="membershipEntryBar" aria-label="FijiLaw membership">
    <div className="membershipEntryCopy">
      <strong>Need to save cases and use the member dashboard?</strong>
      <span>Public legal help remains free. Paid plans unlock dashboards, saved matters, document workflows and professional tools.</span>
    </div>
    <div className="membershipEntryActions">
      <a className="membershipPricingLink" href="/pricing">View Pricing</a>
      <a className="membershipSignInLink" href="/account?mode=login">Sign In</a>
      <a className="membershipRegisterLink" href="/pricing?next=register">Register</a>
    </div>
  </aside>;
}
