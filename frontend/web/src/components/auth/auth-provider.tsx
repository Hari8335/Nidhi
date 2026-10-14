"use client";
import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { api, ApiError } from "@/lib/api/client";
import type { CurrentUser } from "@/types/auth";

type Session = { status: "loading" | "anonymous" } | { status: "authenticated"; user: CurrentUser } | { status: "error"; message: string };
const AuthContext = createContext<{ session: Session; refresh: () => Promise<CurrentUser | null>; clear: () => void } | null>(null);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session>({ status: "loading" });
  const version = useRef(0);
  const clear = useCallback(() => { version.current++; api.clearCsrf(); setSession({ status: "anonymous" }); }, []);
  const refresh = useCallback(async () => {
    const requestVersion = ++version.current;
    try {
      const user = await api.get<CurrentUser>("/auth/me");
      if (requestVersion === version.current) setSession({ status: "authenticated", user });
      return user;
    } catch (error) {
      if (requestVersion === version.current) {
        if (error instanceof ApiError && error.status === 401) setSession({ status: "anonymous" });
        else setSession({ status: "error", message: error instanceof ApiError ? error.message : "Unable to check your session. Please retry." });
      }
      return null;
    }
  }, []);
  useEffect(() => {
    const expire = () => clear();
    const visible = () => { if (document.visibilityState === "visible") void refresh(); };
    const timer = window.setInterval(() => { if (document.visibilityState === "visible") void refresh(); }, 60000);
    window.addEventListener("nidhi:session-expired", expire);
    window.addEventListener("focus", visible);
    document.addEventListener("visibilitychange", visible);
    // This synchronizes with the external cookie session; refresh sets state only after the network settles.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refresh();
    return () => { window.clearInterval(timer); window.removeEventListener("nidhi:session-expired", expire); window.removeEventListener("focus", visible); document.removeEventListener("visibilitychange", visible); };
  }, [clear, refresh]);
  return <AuthContext.Provider value={{ session, refresh, clear }}>{children}</AuthContext.Provider>;
}
export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) throw new Error("AuthProvider is required.");
  return context;
}
