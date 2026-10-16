export type ProblemDetails = {
  title?: string; status?: number; detail?: string; code?: string;
  errors?: Record<string, string[]>; traceId?: string;
};
const messages: Record<string, string> = {
  INVALID_CREDENTIALS: "The email or password is incorrect. Please try again. If you have tried several times, wait 15 minutes or reset your password.",
  AUTHENTICATION_REQUIRED: "Your session has ended. Please sign in again.",
  FORBIDDEN: "You do not have access to this area.",
  EMAIL_NOT_VERIFIED: "Verify your email before continuing.",
  EMAIL_ALREADY_EXISTS: "This email is already registered. Sign in or reset your password.",
  EMAIL_VERIFICATION_REQUIRED: "This email is awaiting verification. Continue verification or request a new email. Your original password is unchanged.",
  INVALID_TOKEN: "This verification or reset code is invalid, expired, or already used.",
  VALIDATION_ERROR: "Please check the highlighted fields and try again.",
  CSRF_VALIDATION_FAILED: "Your security check expired. Please try again.",
  RATE_LIMITED: "Too many requests. Please wait before trying again.",
  IDENTITY_EMAIL_UNAVAILABLE: "We could not send your verification message. Please try registering again later.",
  INVALID_FUNDING_AMOUNT: "Funding amount must be between LKR 100.00 and LKR 1,000,000.00.",
  DAILY_FUNDING_LIMIT_EXCEEDED: "You have reached your daily funding limit of 20 operations today.",
  WALLET_LIMIT_EXCEEDED: "This funding would cause your wallet balance to exceed the maximum cap of LKR 5,000,000.00.",
  IDEMPOTENCY_CONFLICT: "This idempotency key was previously submitted with different details.",
  OPERATION_IN_PROGRESS: "A funding request is already in progress. Please retry in a few moments.",
  PRICE_UNAVAILABLE: "The simulated gold price is temporarily unavailable. Please try again later.",
  PRICE_STALE: "The simulated gold price is temporarily stale. Please refresh and try again later.",
  PRICE_CHANGED: "The simulated gold price changed. Review the current price and confirm again.",
  INSUFFICIENT_SIMULATED_FUNDS: "Your simulated wallet balance is insufficient for this purchase.",
  ACTIVE_GOAL_ALREADY_EXISTS: "You already have a savings goal. Refresh to view it.",
  GOLD_QUANTITY_ZERO: "This amount would result in zero simulated gold. Increase the amount and try again.",
};
export function parseProblem(value: unknown): ProblemDetails {
  if (!value || typeof value !== "object") return {};
  const source = value as Record<string, unknown>;
  const result: ProblemDetails = {};
  for (const key of ["title", "detail", "code", "traceId"] as const) {
    if (typeof source[key] === "string") result[key] = source[key];
  }
  if (typeof source.status === "number") result.status = source.status;
  if (source.errors && typeof source.errors === "object") {
    result.errors = Object.fromEntries(Object.entries(source.errors).filter(([, v]) => Array.isArray(v) && v.every(x => typeof x === "string")));
  }
  return result;
}
export class ApiError extends Error {
  status: number;
  problem: ProblemDetails;
  constructor(status: number, problem: ProblemDetails = {}, retryAfter: string | null = null) {
    const seconds = retryAfter && /^\d+$/.test(retryAfter) ? Number(retryAfter) : null;
    super((status >= 500 && problem.code !== "IDENTITY_EMAIL_UNAVAILABLE" ? undefined : (messages[problem.code ?? ""] ?? problem.detail ?? problem.title)) ?? (status === 0 ? "We could not connect. Check your connection and try again." : "We could not complete your request. Please try again later."));
    if (status === 429 && seconds) this.message += ` Try again in ${seconds} seconds.`;
    this.status = status;
    this.problem = problem;
  }
}

// Same-origin /api is forwarded to ASP.NET Core; no business logic runs in Next.js.
const apiBase = (process.env.NEXT_PUBLIC_API_BASE_URL || "/api/v1").replace(/\/$/, "");
export function createApiClient(base = apiBase, fetcher: typeof fetch = (...args) => fetch(...args)) {
  let csrf: Promise<string> | undefined;
  let identityVersion = 0;
  const clearCsrf = () => { csrf = undefined; identityVersion++; };
  async function request<T>(path: string, init?: RequestInit, decode?: (text: string) => T): Promise<T> {
    const requestIdentity = identityVersion;
    let response: Response;
    try {
      response = await fetcher(`${base}${path}`, { ...init, credentials: "include", cache: "no-store", signal: AbortSignal.timeout(20000) });
    } catch { throw new ApiError(0); }
    if (!response.ok) {
      const problem = parseProblem(await response.json().catch(() => null));
      if (response.status === 401 && requestIdentity === identityVersion) {
        clearCsrf();
        if (problem.code === "AUTHENTICATION_REQUIRED" && typeof window !== "undefined") window.dispatchEvent(new Event("nidhi:session-expired"));
      }
      throw new ApiError(response.status, problem, response.headers.get("Retry-After"));
    }
    if (response.status === 204) return undefined as T;
    try { return decode ? decode(await response.text()) : await response.json() as T; } catch { throw new ApiError(502); }
  }
  function token() {
    csrf ??= request<{ token: string }>("/auth/antiforgery").then(result => {
      if (!result.token) throw new ApiError(502);
      return result.token;
    }).catch(error => { clearCsrf(); throw error; });
    return csrf;
  }
  async function post<T>(path: string, body?: unknown, headers?: Record<string, string>, decode?: (text: string) => T): Promise<T> {
    for (let attempt = 0; ; attempt++) {
      try {
        const result = await request<T>(path, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": await token(),
            ...headers,
          },
          body: body === undefined ? undefined : JSON.stringify(body),
        }, decode);
        if (["/auth/login", "/auth/logout", "/auth/reset-password"].includes(path)) clearCsrf();
        return result;
      } catch (error) {
        // Only the explicit middleware rejection is replayable: it occurs before mutation.
        if (attempt === 0 && error instanceof ApiError && error.status === 400 && error.problem.code === "CSRF_VALIDATION_FAILED") { clearCsrf(); continue; }
        throw error;
      }
    }
  }
  return { get: <T>(path: string, decode?: (text: string) => T) => request<T>(path, undefined, decode), post, clearCsrf };
}
export const api = createApiClient();
