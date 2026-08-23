'use client';

import { Show, UserButton } from '@clerk/nextjs';
import { usePathname } from 'next/navigation';

export default function MembershipEntryBar() {
  const pathname = usePathname();
  const clerkEnabled = Boolean(process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY);
  if (pathname !== '/') return null;

  const signedOutActions = <>
    <a className="membershipPricingLink" href="/pricing">View Pricing</a>
    <a className="membershipSignInLink" href="/account?mode=login">Sign In</a>
    <a className="membershipRegisterLink" href="/account?mode=register">Register</a>
  </>;

  return <aside className="membershipEntryBar" aria-label="FijiLaw membership">
    <div className="membershipEntryCopy">
      <strong>Need to save cases and use the member dashboard?</strong>
      <span>Public legal help remains free. Paid plans unlock dashboards, saved matters, document workflows and professional tools.</span>
    </div>
    <div className="membershipEntryActions">
      {clerkEnabled ? <>
        <Show when="signed-out">{signedOutActions}</Show>
        <Show when="signed-in">
          <a className="membershipPricingLink" href="/dashboard">Dashboard</a>
          <UserButton afterSignOutUrl="/" />
        </Show>
      </> : signedOutActions}
    </div>
  </aside>;
}
