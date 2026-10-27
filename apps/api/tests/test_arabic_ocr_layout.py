import pytest
from unittest.mock import patch
from PIL import Image

from app.arabic_ocr_layout import needs_retry, prefer_arabic, refine_lines
from app.local_extraction import LocalInvoiceExtractor
from app.extraction import ExtractionError


def data(words, confidence=92, top=10, line=1):
    n = len(words)
    return {"text": words, "conf": [confidence]*n, "left": [30+i*20 for i in range(n)],
            "top": [top]*n, "width": [15]*n, "height": [20]*n,
            "block_num": [1]*n, "par_num": [1]*n, "line_num": [line]*n}


def test_arabic_retry_improves_uncertain_word_without_changing_numbers():
    before = data(["شروط", "الدفع:", "خلال", "30", "log,"], 85)
    after = data(["شروط", "الدفع:", "خلال", "30", "يوماً"], 92)
    assert prefer_arabic(before, after)
    for changed in ("31", "030", "30.00"):
        assert not prefer_arabic(before, data(["شروط", "الدفع:", "خلال", changed, "يوماً"], 95))


def test_incomplete_rtl_line_is_retried_but_clean_values_are_retained():
    assert needs_retry(data(["315.75", "الإجمالي:\u200e"]), [0, 1])
    assert not needs_retry(data(["الإجمالي:", "315.75"]), [0, 1])


@pytest.mark.parametrize("word,confidence", [("INV-007", 60), ("AED", 95), ("Net", 95)])
def test_retry_cannot_erase_latin_identifiers_or_confident_words(word, confidence):
    before = data(["شروط", word, "log,"], confidence)
    after = data(["شروط", "يوماً", "007"] if word == "INV-007" else ["شروط", "يوماً"], 99)
    assert not prefer_arabic(before, after)


def test_line_crop_coordinates_are_translated_to_original_page():
    original = data(["الإجمالي:", "105.00", "|"], top=100)
    calls = []

    def read(crop, arabic):
        calls.append((crop.size, arabic))
        return data(["الإجمالي:", "105.00"], top=7)

    refined = refine_lines(Image.new("L", (400, 250), 255), original, read, lambda: None)
    assert calls == [((400, 34), False)]
    assert refined["top"] == [100, 100]
    page = LocalInvoiceExtractor._page_from_ocr(refined, 1, 0, (400, 250))
    assert page.canonical_text == "الإجمالي: 105.00\n"
    assert page.lines[0].character_boxes[-1] == (.125, .4, .1625, .48)
    field = next(f for f in LocalInvoiceExtractor._fields([page]) if f.key == "total")
    span = field.evidence[0]
    assert page.canonical_text.encode()[span.start_utf8:span.end_utf8].decode() == "105.00"


def test_overlapping_columns_and_empty_retry_retain_original_data():
    original = data(["الإجمالي:", "|"], top=10)
    right = data(["Right"], top=10, line=2)
    overlapping = {key: original[key] + right[key] for key in original}
    def no_read(*args):
        pytest.fail("Clean or overlapping lines must not be re-read")
    assert refine_lines(Image.new("L", (400, 250)), overlapping, no_read, lambda: None) is overlapping
    clean = data(["Invoice", "Number:", "INV-001"])
    assert refine_lines(Image.new("L", (400, 250)), clean, no_read, lambda: None) is clean
    original = data(["الإجمالي:", "|"])
    assert refine_lines(Image.new("L", (400, 250)), original,
                        lambda *args: data([]), lambda: None) is original


def test_deadline_failure_propagates_and_profile_is_explicit():
    def expired():
        raise ExtractionError("provider_timeout", "Expired", retryable=True)
    with pytest.raises(ExtractionError, match="Expired"):
        refine_lines(Image.new("L", (400, 250)), data(["الإجمالي:", "|"]), lambda *args: data([]), expired)
    assert LocalInvoiceExtractor().layout_profile == "block"
    assert LocalInvoiceExtractor().arabic_model == "fast"
    with pytest.raises(ValueError):
        LocalInvoiceExtractor(layout_profile="lines-v1")
    with pytest.raises(ValueError):
        LocalInvoiceExtractor(arabic_model="unpinned")


def test_line_retry_cannot_change_a_number_even_with_higher_confidence():
    original = data(["الإجمالي:", "105.00", "|"], top=100)
    refined = refine_lines(Image.new("L", (400, 250)), original,
                           lambda *args: data(["الإجمالي:", "106.00"], 99), lambda: None)
    assert refined["text"] == original["text"]
    assert refined["top"] == original["top"]


def test_best_model_pin_does_not_accept_fast_or_modified_files(tmp_path, monkeypatch):
    import hashlib
    for name in ("eng", "osd", "ara"):
        (tmp_path / f"{name}.traineddata").write_bytes(name.encode())
    monkeypatch.setenv("OCR_TESSDATA_DIR", str(tmp_path))
    for name in ("ENG", "OSD"):
        monkeypatch.setattr(f"app.local_extraction.{name}_MODEL_SHA256", hashlib.sha256(name.lower().encode()).hexdigest())
    monkeypatch.setattr("app.local_extraction.ARA_BEST_MODEL_SHA256", hashlib.sha256(b"ara").hexdigest())
    LocalInvoiceExtractor._verify_model_hashes("eng+ara", "best")
    with pytest.raises(ExtractionError):
        LocalInvoiceExtractor._verify_model_hashes("eng+ara", "fast")
    (tmp_path / "ara.traineddata").write_bytes(b"modified")
    with pytest.raises(ExtractionError):
        LocalInvoiceExtractor._verify_model_hashes("eng+ara", "best")


def test_page_retries_share_the_outer_deadline():
    extractor = LocalInvoiceExtractor(language="eng+ara", layout_profile="lines-v1")
    seen = []
    def read(image, deadline, **kwargs):
        seen.append((deadline, kwargs.get("psm", 6)))
        return data(["الإجمالي:", "105.00", "|"], top=10)
    with patch.object(extractor, "_ocr_orientation", return_value=0), \
            patch.object(extractor, "_ocr_data", side_effect=read), \
            patch("app.local_extraction.time.monotonic", return_value=100):
        extractor._ocr_image(Image.new("RGB", (400, 250), "white"), 1, deadline_monotonic=105)
    assert seen == [(105, 6), (105, 7)]
