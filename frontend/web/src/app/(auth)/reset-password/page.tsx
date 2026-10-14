import type { Metadata } from "next";
import { AuthForm } from "@/components/auth/auth-form";
export const metadata: Metadata = { title: "Reset Password | Nidhi" };
export default function Page() { return <AuthForm mode="reset-password" />; }
