import { installPreviewRoleBridge } from "./role-bridge.mjs";

const currentRuleSet = "invoice-po-2.0";
const previewRuleSet = "invoice-po-1.0";
const evaluationPath = /^\/api\/reconciliation-cases\/[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}\/evaluations$/i;

/**
 * The retained fixture cannot run v2. Adapt only its request contract, never its
 * results: Unknown scope keeps its synthetic missing-check warning. No replay,
 * restart, source confirmation, or production import is permitted here.
 */
export function createPreviewComparisonFetch(original, origin) {
  return async (input, init) => {
    const url = new URL(input instanceof Request ? input.url : String(input), origin);
    const method = init?.method ?? (input instanceof Request ? input.method : "GET");
    if (url.origin !== origin || method.toUpperCase() !== "POST" || !evaluationPath.test(url.pathname))
      return original(input, init);

    // Clone before applying init overrides so an unadapted Request stays readable.
    const request = new Request(input instanceof Request ? input.clone() : url.href, init);
    if (request.headers.get("content-type")?.split(";")[0].trim().toLowerCase() !== "application/json")
      return original(input, init);

    let body;
    try { body = await request.clone().json(); }
    catch { return original(input, init); }
    if (!body || Array.isArray(body) || body.ruleSetVersion !== currentRuleSet || Object.hasOwn(body, "orderScope"))
      return original(input, init);

    const headers = new Headers(request.headers);
    headers.delete("content-length"); // The serialized body changes length.
    return original(new Request(request, {
      headers,
      body: JSON.stringify({ ...body, ruleSetVersion: previewRuleSet, orderScope: "Unknown" }),
    }));
  };
}

export function installPreviewFetchBridge(target) {
  // Reset from the existing originalFetch symbol first; HMR must retain role
  // restrictions without stacking old comparison wrappers.
  installPreviewRoleBridge(target);
  target.fetch = createPreviewComparisonFetch(target.fetch, target.location.origin);
}
