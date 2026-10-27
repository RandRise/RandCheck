import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { PairUpload } from "./components/pair-upload";

vi.mock("../security/enterprise-provider", () => ({
  useEnterprise: () => ({ session: { organizationId: "org", actorId: "actor" } }),
}));
vi.mock("../security/api-fetch", () => ({ API: "http://test", apiFetch: (url: string, init?: RequestInit) => fetch(url, init) }));

const document = { id: "00000000-0000-4000-8000-000000000001", originalFilename: "Invoice.pdf", status: "Queued" };
function json(value: unknown, status = 200) {
  return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json" } });
}
function setup(fetcher = vi.fn(async () => json(document)), onUploaded = vi.fn(async () => {}), disabled = false) {
  vi.stubGlobal("fetch", fetcher);
  const onBusy = vi.fn();
  render(<PairUpload role="Invoice" disabled={disabled} onUploaded={onUploaded} onBusy={onBusy}/>);
  const input = screen.getByLabelText("Upload invoice") as HTMLInputElement;
  return { fetcher, onUploaded, onBusy, input };
}
const file = () => new File(["invoice bytes"], "Invoice.pdf", { type: "application/pdf" });
beforeEach(() => sessionStorage.clear());
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); });

it("uploads on file choice with no second upload button, and cancellation does nothing", async () => {
  const { input, fetcher, onUploaded } = setup();
  expect(screen.queryByRole("button", { name: "Upload invoice" })).toBeNull();
  fireEvent.change(input, { target: { files: [] } });
  expect(fetcher).not.toHaveBeenCalled();
  fireEvent.change(input, { target: { files: [file()] } });
  await screen.findByText("File uploaded. Its values still need checking.");
  expect(fetcher).toHaveBeenCalledOnce();
  expect(onUploaded).toHaveBeenCalledWith(document);
});

it("locks rapid choices and retries an uncertain upload with the same operation key", async () => {
  const randomUUID = crypto.randomUUID.bind(crypto);
  vi.stubGlobal("crypto", { randomUUID }); // No hashing: the retry must retain the same File object.
  let reject!: (reason: Error) => void;
  const fetcher = vi.fn()
    .mockImplementationOnce(() => new Promise<Response>((_, fail) => { reject = fail; }))
    .mockResolvedValueOnce(json(document));
  const { input } = setup(fetcher);
  act(() => {
    fireEvent.change(input, { target: { files: [file()] } });
    fireEvent.change(input, { target: { files: [file()] } });
  });
  await waitFor(() => expect(fetcher).toHaveBeenCalledOnce());
  expect(input.disabled).toBe(true);
  await act(async () => reject(new Error("Reply lost")));
  fireEvent.click(await screen.findByRole("button", { name: "Retry upload" }));
  await screen.findByText("File uploaded. Its values still need checking.");
  expect(fetcher).toHaveBeenCalledTimes(2);
  const keys = fetcher.mock.calls.map(([, init]) => init.headers["X-Operation-Key"]);
  expect(keys[0]).toBe(keys[1]);
});

it("keeps an accepted file when saving its selection fails and never offers an upload retry", async () => {
  const onUploaded = vi.fn(async () => { throw new Error("Selection failed"); });
  const { input, fetcher } = setup(undefined, onUploaded);
  fireEvent.change(input, { target: { files: [file()] } });
  await screen.findByText(/accepted and is kept in Documents/);
  expect(screen.queryByRole("button", { name: "Retry upload" })).toBeNull();
  expect(fetcher).toHaveBeenCalledOnce();
});

it("requires an explicit separate-upload choice after an operation conflict", async () => {
  const fetcher = vi.fn()
    .mockResolvedValueOnce(json({ code: "operation_source_unavailable", detail: "Original source unavailable" }, 409))
    .mockResolvedValueOnce(json(document));
  const { input } = setup(fetcher);
  fireEvent.change(input, { target: { files: [file()] } });
  const separate = await screen.findByRole("button", { name: "Start a separate upload" });
  expect(fetcher).toHaveBeenCalledOnce();
  expect(screen.queryByRole("button", { name: "Retry upload" })).toBeNull();
  expect(input.disabled).toBe(true);
  fireEvent.change(input, { target: { files: [file()] } });
  await act(async () => {});
  expect(fetcher).toHaveBeenCalledOnce();
  fireEvent.click(separate);
  await screen.findByText("File uploaded. Its values still need checking.");
  const keys = fetcher.mock.calls.map(([, init]) => init.headers["X-Operation-Key"]);
  expect(keys[0]).not.toBe(keys[1]);
});

it("does not upload when the surrounding workflow disables it", async () => {
  const { input, fetcher } = setup(undefined, undefined, true);
  fireEvent.change(input, { target: { files: [file()] } });
  await act(async () => {});
  expect(fetcher).not.toHaveBeenCalled();
});
