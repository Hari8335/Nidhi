import { ApplicationShell } from "@/components/layout/application-shell";
export default function Layout({ children }: { children: React.ReactNode }) { return <ApplicationShell role="CUSTOMER">{children}</ApplicationShell>; }
