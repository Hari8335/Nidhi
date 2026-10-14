import type { Metadata } from "next";
import { AuthForm } from "@/components/auth/auth-form";
export const metadata: Metadata = { title: "Register | Nidhi" };
export default function Page() { return <AuthForm mode="register" />; }
