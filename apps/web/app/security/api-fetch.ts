export const API = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080";
type Transport = (input: string | URL | Request, init?: RequestInit) => Promise<Response>;
let transport: Transport | undefined;
export function installTransport(next: Transport) {
  transport = next;
  return () => { if (transport === next) transport = undefined; };
}
// Unwrapped component tests and the explicit local-development mode use anonymous requests.
// The root provider never mounts enterprise pages before installing the authenticated transport.
export const apiFetch: typeof fetch = (input, init) => transport ? transport(input, init) : fetch(input, init);

export function secureTransport(api: string, token: () => Promise<string>, invalid: () => void,
  lifetime: AbortSignal, fetcher: typeof fetch = fetch): Transport {
  const origin = new URL(api);
  if (origin.username || origin.password || origin.search || origin.hash || origin.pathname !== "/" ||
      (origin.protocol !== "https:" && !(origin.protocol === "http:" && ["localhost", "127.0.0.1", "[::1]"].includes(origin.hostname))))
    throw new Error("Invalid API origin");
  return async (input, init = {}) => {
    if (input instanceof Request) throw new Error("Use an API URL and explicit request options");
    const url = new URL(String(input), origin);
    if (url.origin !== origin.origin || !url.pathname.startsWith("/api/") || url.username || url.password || url.hash)
      throw new Error("Request is outside the configured API");
    const signal = init.signal ? AbortSignal.any([init.signal, lifetime]) : lifetime;
    signal.throwIfAborted();
    let accessToken: string;
    try { accessToken = await token(); } catch { signal.throwIfAborted(); invalid(); throw new Error("Please sign in again."); }
    signal.throwIfAborted();
    const headers = new Headers(init.headers);
    headers.set("Authorization", `Bearer ${accessToken}`);
    const response = await fetcher(url.href, { ...init, headers, signal, credentials: "omit", redirect: "error", cache: "no-store" });
    signal.throwIfAborted();
    if (response.status === 401 || response.status === 403) invalid();
    return response;
  };
}
