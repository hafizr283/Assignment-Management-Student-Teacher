import './globals.css';
import type { Metadata, Viewport } from 'next';

export const metadata: Metadata = {
  metadataBase: new URL('https://assignment-management-student-teach.vercel.app'),
  title: {
    default: 'Classroom Hub',
    template: '%s | Classroom Hub'
  },
  description: 'A secure assignment, submission, grading, and feedback workspace for schools and colleges.',
  applicationName: 'Classroom Hub',
  openGraph: {
    type: 'website',
    title: 'Classroom Hub',
    description: 'Assignment and submission management for administrators, teachers, and students.',
    siteName: 'Classroom Hub'
  },
  robots: { index: true, follow: true }
};

export const viewport: Viewport = {
  colorScheme: 'light',
  themeColor: '#176b4c'
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body>{children}</body></html>;
}
