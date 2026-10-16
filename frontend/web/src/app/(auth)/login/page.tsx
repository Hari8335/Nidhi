import type { Metadata } from "next";
import { AuthForm } from "@/components/auth/auth-form";
export const metadata: Metadata = { title: "Login | Nidhi" };
export default async function Page({ searchParams }: { searchParams: Promise<{ reason?: string; returnTo?: string }> }) {
  const params = await searchParams;
  return <AuthForm mode="login" expired={params.reason === "session-expired"} returnTo={typeof params.returnTo === "string" ? params.returnTo : null} />;
}
