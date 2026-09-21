import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { fetchOriginal, SourceDownload } from "./source-download";

const id = "12345678-1234-1234-1234-123456789abc";
const path = `/api/documents/${id}/source`;
const grant = { url: path, accessToken: "temporary-credential" };

describe("original file access", () => {
  afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });
  it("initiates an attachment download and revokes its temporary blob URL", async () => {
    const create = vi.fn(() => "blob:local-preview"); const revoke = vi.fn();
    const BrowserUrl = URL;
    vi.stubGlobal("URL", class extends BrowserUrl { static createObjectURL = create; static revokeObjectURL = revoke; });
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(Response.json(grant)).mockResolvedValueOnce(new Response("original bytes")));
    let savedName = ""; let savedUrl = "";
    const clicked = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(function (this: HTMLAnchorElement) { savedName = this.download; savedUrl = this.href; });
    vi.useFakeTimers(); render(<SourceDownload documentId={id}/>);
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Download original" })); });
    expect(clicked).toHaveBeenCalledOnce(); expect(create).toHaveBeenCalledOnce();
    expect(savedName).toBe(`source-${id.replaceAll("-", "")}.bin`); expect(savedUrl).toBe("blob:local-preview");
    expect(screen.getByRole("status").textContent).toContain("download started");
    vi.advanceTimersByTime(1000); expect(revoke).toHaveBeenCalledWith("blob:local-preview");
  });
  it("requests a new grant and sends it only in the header", async () => {
    const fetcher = vi.fn().mockResolvedValueOnce(Response.json(grant)).mockResolvedValueOnce(new Response("original bytes", {
      headers: { "Content-Disposition": `attachment; filename=source-${id.replaceAll("-", "")}.txt` },
    }));
    const file = await fetchOriginal(id, new AbortController().signal, fetcher);
    // jsdom's Blob lacks text(); read the same bytes through its browser FileReader.
    const text = await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result));
      reader.onerror = () => reject(reader.error);
      reader.readAsText(file.blob);
    });
    expect(text).toBe("original bytes");
    expect(file.filename).toBe(`source-${id.replaceAll("-", "")}.txt`);
    expect(fetcher.mock.calls[0][1].method).toBe("POST");
    expect(fetcher.mock.calls[1][1].headers).toEqual({ "X-Document-Access": grant.accessToken });
    expect(fetcher.mock.calls.map(call => call[0]).join()).not.toContain(grant.accessToken);
  });
  it("refuses a grant URL pointing elsewhere without sending the credential", async () => {
    const fetcher = vi.fn().mockResolvedValue(Response.json({ ...grant, url: "https://untrusted.invalid/api/file" }));
    await expect(fetchOriginal(id, new AbortController().signal, fetcher)).rejects.toThrow("temporarily unavailable");
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
  it.each([404, 422, 503])("never creates a download from a rejected response (%s)", async status => {
    const fetcher = vi.fn().mockResolvedValueOnce(Response.json(grant)).mockResolvedValueOnce(new Response("PRIVATE failure", { status }));
    await expect(fetchOriginal(id, new AbortController().signal, fetcher)).rejects.not.toThrow("PRIVATE");
  });
  it("cancels before redemption when selection or session is abandoned", async () => {
    const abort = new AbortController();
    const fetcher = vi.fn().mockImplementation(async () => { abort.abort(); return Response.json(grant); });
    await expect(fetchOriginal(id, abort.signal, fetcher)).rejects.toThrow();
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
  it("shows screening failure and allows a fresh retry", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(Response.json(grant)).mockResolvedValueOnce(new Response("", { status: 422 })));
    render(<SourceDownload documentId={id}/>);
    fireEvent.click(screen.getByRole("button", { name: "Download original" }));
    expect((await screen.findByRole("status")).textContent).toContain("did not pass malware screening");
    expect(screen.getByRole("button", { name: "Download original" }).hasAttribute("disabled")).toBe(false);
  });
});
