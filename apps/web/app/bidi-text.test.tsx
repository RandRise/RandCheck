import { render } from "@testing-library/react";
import { expect, it } from "vitest";
import { BidiText } from "./bidi-text";

it("keeps ISO dates and identifiers in LTR runs without changing source text", () => {
  const text = "تاريخ الفاتورة: 2026-09-21\nرقم: INV-007\nالإجمالي: 210.50\npayment_terms[0]: خلال 30 يوماً";
  const { container } = render(<pre dir="auto"><BidiText text={text} /></pre>);
  expect(container.textContent).toBe(text);
  expect([...container.querySelectorAll('bdi[dir="ltr"]')].map(x => x.textContent))
    .toEqual(["2026-09-21", "INV-007", "210.50", "payment_terms[0]", "30"]);
});

it("preserves highlights that cross or split isolated runs", () => {
  const text = "تاريخ: 2026-09-21 ورقم INV-007";
  const start = text.indexOf("09"), end = text.indexOf("007") + 2;
  const { container } = render(<BidiText text={text} highlightStart={start} highlightEnd={end} />);
  expect(container.textContent).toBe(text);
  expect([...container.querySelectorAll("mark")].map(x => x.textContent).join(""))
    .toBe(text.slice(start, end));
  expect(container.querySelector("bdi")?.textContent).toBe("2026-09-21");
});

it("isolates an entire English payment phrase within Arabic text", () => {
  const text = "شروط الدفع: Net 45 days\nالإجمالي: 315.75";
  const { container } = render(<pre dir="auto"><BidiText text={text} /></pre>);
  expect(container.textContent).toBe(text);
  expect([...container.querySelectorAll('bdi[dir="ltr"]')].map(x => x.textContent))
    .toEqual(["Net 45 days", "315.75"]);
});
