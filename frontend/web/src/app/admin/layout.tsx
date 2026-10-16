import type { Metadata } from "next";
import { ApplicationShell } from "@/components/layout/application-shell";
export const metadata: Metadata = { robots: { index: false, follow: false } };

export default function Layout({ children }: { children: React.ReactNode }) { return <ApplicationShell role="ADMIN">{children}</ApplicationShell>; }
