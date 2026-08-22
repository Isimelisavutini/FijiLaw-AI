import './styles.css';
import './resilience.css';
import './membership.css';
import ApiResilience from './ApiResilience';
import MembershipEntryBar from './MembershipEntryBar';

export const metadata = {
  title: 'FijiLaw AI',
  description: 'Supervised AI legal assistance for Fiji'
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body><ApiResilience><MembershipEntryBar />{children}</ApiResilience></body></html>;
}
