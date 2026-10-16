"use client";
import { useEffect, useRef, useState } from "react";
import { parseEmailLink, type EmailLink } from "@/lib/auth/email-link";

export function useEmailLink(enabled: boolean) {
  const [link, setLink] = useState<EmailLink | null>(null);
  const consumed = useRef(false);
  useEffect(() => {
    if (!enabled || consumed.current) return;
    consumed.current = true;
    const parsed = parseEmailLink(window.location.hash);
    // Read browser-only credentials once, then remove them from the address bar/history.
    setLink(parsed);
    if (window.location.hash) window.history.replaceState(null, "", window.location.pathname + window.location.search);
  }, [enabled]);
  return link;
}
