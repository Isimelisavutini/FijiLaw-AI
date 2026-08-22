import './styles.css';
import ApiResilience from './ApiResilience';

export const metadata = {
  title: 'FijiLaw AI',
  description: 'Supervised AI legal assistance for Fiji'
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body><ApiResilience>{children}</ApiResilience></body></html>;
}
