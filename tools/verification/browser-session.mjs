import { spawn } from "node:child_process";
import { mkdir } from "node:fs/promises";
import path from "node:path";

/** A temporary Edge profile, with bounded CDP calls and explicit process cleanup. */
export async function openBrowser(output) {
  // Edge requires an absolute profile path; relative paths can prevent CDP startup.
  output = path.resolve(output);
  await mkdir(output, { recursive: true });
  const edge = spawn("C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe", [
    "--headless", "--disable-gpu", "--no-first-run", "--remote-debugging-port=0",
    `--user-data-dir=${path.join(output, `edge-${Date.now()}`)}`, "about:blank",
  ], { windowsHide: true, stdio: ["ignore", "ignore", "pipe"] });
  let socket;
  const pending = new Map();
  const errors = [], requests = [], networkFailures = [], responses = [];
  const requestUrls = new Map();
  let sequence = 0;
  try {
    const endpoint = await new Promise((resolve, reject) => {
      const timeout = setTimeout(() => reject(new Error("Edge startup timed out")), 20000);
      edge.once("error", error => { clearTimeout(timeout); reject(error); });
      edge.stderr.on("data", data => {
        const match = String(data).match(/DevTools listening on (ws:\/\/[^\s]+)/);
        if (match) { clearTimeout(timeout); resolve(match[1]); }
      });
    });
    socket = new WebSocket(endpoint);
    await new Promise((resolve, reject) => { socket.onopen = resolve; socket.onerror = reject; });
    socket.onmessage = event => {
      const message = JSON.parse(String(event.data));
      if (message.method === "Runtime.exceptionThrown") errors.push(message.params.exceptionDetails.text);
      if (message.method === "Network.requestWillBeSent") {
        const { url, method } = message.params.request;
        requestUrls.set(message.params.requestId, url);
        if (new URL(url).hostname === "127.0.0.1") requests.push({ url, method });
      }
      if (message.method === "Network.responseReceived" && message.params.response.url.includes("/api/")) {
        responses.push({ url: message.params.response.url, status: message.params.response.status });
      }
      if (message.method === "Network.loadingFailed") {
        const url = requestUrls.get(message.params.requestId);
        if (url?.includes("/api/")) networkFailures.push({ url, error: message.params.errorText, cancelled: !!message.params.canceled, blockedReason: message.params.blockedReason });
      }
      const entry = pending.get(message.id);
      if (entry) { pending.delete(message.id); clearTimeout(entry.timeout); message.error ? entry.reject(message.error) : entry.resolve(message.result); }
    };
    const send = (method, params = {}, sessionId) => new Promise((resolve, reject) => {
      const id = ++sequence;
      const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 30000);
      pending.set(id, { resolve, reject, timeout });
      socket.send(JSON.stringify({ id, method, params, sessionId }));
    });
    const { targetInfos } = await send("Target.getTargets");
    const { sessionId } = await send("Target.attachToTarget", { targetId: targetInfos.find(t => t.type === "page").targetId, flatten: true });
    const call = (method, params) => send(method, params, sessionId);
    for (const domain of ["Page", "Network", "Runtime", "Performance"]) await call(`${domain}.enable`);
    const evaluate = async expression => {
      const result = await call("Runtime.evaluate", { expression, returnByValue: true, awaitPromise: true });
      if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description ?? result.exceptionDetails.text);
      return result.result.value;
    };
    const waitFor = async expression => {
      for (let attempt = 0; attempt < 120; attempt++) {
        if (await evaluate(expression)) return;
        await new Promise(resolve => setTimeout(resolve, 150));
      }
      throw new Error(`Page did not reach expected state: ${expression}`);
    };
    return { call, evaluate, waitFor, errors, requests, networkFailures, responses, close: async () => {
      try { await send("Browser.close"); } finally { socket.close(); edge.kill(); }
    } };
  } catch (error) { socket?.close(); edge.kill(); throw error; }
}
