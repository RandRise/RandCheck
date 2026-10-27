"""Bounded line OCR refinement with source-coordinate preservation."""
import re
from collections import Counter

from .arabic_text import has_arabic


def tokens(data):
    return [i for i, text in enumerate(data["text"]) if str(text).strip()]


def needs_retry(data, indices):
    text = " ".join(str(data["text"][i]) for i in indices).rstrip(" \u061c\u200e\u200f")
    return (any(has_arabic(str(data["text"][i])) for i in indices)
            and (text.endswith(":") or any("|" in str(data["text"][i]) for i in indices)
                 or any(re.search(r"[A-Za-z]", str(data["text"][i]))
                        and float(data["conf"][i]) < 90 for i in indices)))


def stable_values(baseline, candidate):
    """A retry must preserve observed numbers and protected Latin tokens."""
    original = tokens(baseline)
    proposed = tokens(candidate)
    if not original or not proposed:
        return False
    before = " ".join(str(baseline["text"][i]) for i in original)
    after = " ".join(str(candidate["text"][i]) for i in proposed)
    # Preserve numeric runs exactly, including precision, separators and zeros.
    numbers = r"[0-9]+(?:[-.,/][0-9]+)*"
    if Counter(re.findall(numbers, before)) != Counter(re.findall(numbers, after)):
        return False
    for i in original:
        word = str(baseline["text"][i]).strip("\u061c\u200e\u200f")
        if re.search(r"[A-Za-z]", word) and (
            re.search(r"[0-9]", word) or float(baseline["conf"][i]) >= 90
        ) and word not in after.split():
            return False
    return True


def prefer_arabic(baseline, candidate):
    """Accept a language retry only with better confidence and stable identifiers."""
    if not stable_values(baseline, candidate):
        return False
    original, proposed = tokens(baseline), tokens(candidate)
    def mean(data, indices):
        return sum(float(data["conf"][i]) for i in indices) / len(indices)
    return (mean(candidate, proposed) > mean(baseline, original)
            and sum(has_arabic(str(candidate["text"][i])) for i in proposed)
            > sum(has_arabic(str(baseline["text"][i])) for i in original))


def refine_lines(image, baseline, read_line, check_deadline):
    """Use existing OCR line boxes; decline overlapping/large layouts unchanged.

    read_line receives (cropped_image, arabic_only). Returned coordinates are
    translated back to the page before canonical text/evidence is constructed.
    """
    grouped = {}
    for i in tokens(baseline):
        key = tuple(baseline[name][i] for name in ("block_num", "par_num", "line_num"))
        grouped.setdefault(key, []).append(i)
    if not grouped or len(grouped) > 80:
        return baseline
    if not any(needs_retry(baseline, indices) for indices in grouped.values()):
        return baseline
    rows = []
    for indices in grouped.values():
        top = min(int(baseline["top"][i]) for i in indices)
        bottom = max(int(baseline["top"][i]) + int(baseline["height"][i]) for i in indices)
        rows.append((top, bottom, indices))
    rows.sort()
    if any(rows[i][1] > rows[i+1][0] for i in range(len(rows)-1)):
        return baseline
    output = {key: [] for key in ("text", "conf", "left", "top", "width", "height",
                                  "block_num", "par_num", "line_num")}
    for n, (top, bottom, original) in enumerate(rows):
        check_deadline()
        if not needs_retry(baseline, original):
            for i in original:
                for key in output:
                    output[key].append(n+1 if key == "line_num" else
                                       1 if key in {"block_num", "par_num"} else baseline[key][i])
            continue
        padding = max(4, round((bottom-top) * .35))
        y0 = max(0, top-padding, rows[n-1][1] if n else 0)
        y1 = min(image.height, bottom+padding, rows[n+1][0] if n+1 < len(rows) else image.height)
        crop = image.crop((0, y0, image.width, y1))
        selected = read_line(crop, False)
        indices = tokens(selected)
        if not indices:
            return baseline
        if (any(has_arabic(str(selected["text"][i])) for i in indices)
                and any(re.search(r"[A-Za-z]", str(selected["text"][i]))
                        and float(selected["conf"][i]) < 90 for i in indices)):
            check_deadline()
            candidate = read_line(crop, True)
            if prefer_arabic(selected, candidate):
                selected = candidate
        original_data = {key: [baseline[key][i] for i in original] for key in output}
        if not stable_values(original_data, selected):
            selected = original_data
            y0 = 0
        for i in tokens(selected):
            for key in output:
                value = selected[key][i] if key not in {"block_num", "par_num", "line_num"} else 1
                if key == "top":
                    value = int(value) + y0
                elif key == "line_num":
                    value = n + 1
                output[key].append(value)
    return output
