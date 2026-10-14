import type { Metadata } from "next";
import { Manrope } from "next/font/google";
import { AuthProvider } from "@/components/auth/auth-provider";
import "./globals.css";
const manrope = Manrope({ subsets: ["latin"], display: "swap", variable: "--font-manrope" });
export const metadata: Metadata = {
  title: "Nidhi", description: "A thoughtful start to simulated gold micro-savings.", referrer: "no-referrer",
};
export default function RootLayout({ children }: { children: React.ReactNode }) {
  return <html lang="en" className={manrope.variable}><body><a className="skip-link" href="#main-content">Skip to content</a><AuthProvider>{children}</AuthProvider></body></html>;
}
