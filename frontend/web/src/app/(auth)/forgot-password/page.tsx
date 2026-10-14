import type { Metadata } from "next";
import { AuthForm } from "@/components/auth/auth-form";
export const metadata: Metadata = { title: "Forgot Password | Nidhi" };
export default function Page() { return <AuthForm mode="forgot-password" />; }
