import { ClerkProvider } from '@clerk/nextjs';
import './styles.css';
import './resilience.css';
import './membership.css';
import ApiResilience from './ApiResilience';
import MembershipEntryBar from './MembershipEntryBar';

export const metadata = {
  title: 'FijiLaw AI',
  description: 'Supervised AI legal assistance for Fiji'
};

export const viewport = {
  themeColor: '#0E2A47',
  colorScheme: 'light'
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const app = <ApiResilience><MembershipEntryBar />{children}</ApiResilience>;
  const clerkEnabled = Boolean(process.env.NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY);

  return <html lang="en"><body>{clerkEnabled ? <ClerkProvider>{app}</ClerkProvider> : app}</body></html>;
}
