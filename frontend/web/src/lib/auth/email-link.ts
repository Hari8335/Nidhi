export type EmailLink = { userId: string; token: string };

export function parseEmailLink(fragment: string): EmailLink | null {
  const values = new URLSearchParams(fragment.replace(/^#/, ""));
  const userId = values.get("userId") ?? "";
  const token = values.get("token") ?? "";
  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(userId) || !token || token.length > 4096) return null;
  return { userId, token };
}
