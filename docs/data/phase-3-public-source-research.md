# Phase 3 public invoice-source research

**Status:** Web and Reddit research complete; no public source is approved as the UAE claim benchmark  
**Research date:** 2026-08-14  
**Task:** P3-T03

This note evaluates public document-image sources against the Phase 3 source
intake contract. It is an engineering and product-governance assessment, not
legal advice. A source remains ineligible until an authorized reviewer verifies
the applicable terms and privacy treatment for the exact acquired files.

## Decision

No public dataset found in this review independently satisfies the Phase 3
benchmark boundary: real English invoices, document images that can be
processed locally, defensible permission for storage/labeling/processing and
aggregate reporting, privacy clearance, and coverage of UAE-specific fields.

The strongest candidates are:

1. **MIDD**, if the authors provide the original scanned PDFs and resolve the
   contradictory use statements in writing; and
2. **DocILE annotated real documents**, if its access terms permit this
   portfolio evaluation and a privacy review approves a controlled subset.

Neither candidate covers UAE supplier TRNs or establishes UAE performance.
They can at most support a separately named external robustness population.
The primary benchmark still requires organization-owned or expressly permitted
UAE invoices.

## Immediate UAE development asset

The research produced an executable non-claim result: the UAE FTA
field-coverage development pack at
`output/pdf/uae-invoice-development-v0.2.0/README.md`.
It contains nine original, visibly fictional PDFs rather than copied FTA
artwork. The scenarios cover standard full, simplified, zero-rated,
reverse-charge, foreign-currency, discounted multipage, clean scanned, and
rotated/degraded tax invoices. The earlier v0.1.0 pack remains retained as the
initial eight-field UAE development baseline.

The v0.2.0 design adds buyer TRNs when applicable, unit codes, line-level net
and VAT values, AED payable amounts, payment due date and method, discounts,
exchange-rate metadata, and conditional legal statements. It includes full
compliance ground truth, an FTA requirement matrix, a source register, and an
annotation ledger. It passes the structural readiness audit with the expected
synthetic/non-claim blockers. The separate MoF mapping explicitly records that
PDFs and scans are not eInvoices; PINT-AE XML and ASP exchange remain a future
structured-data implementation.

## Candidate assessment

| Source | What the authoritative source establishes | Fit to the eight-field Phase 3 ontology | Rights/privacy result | Decision |
|---|---|---|---|---|
| [MIDD paper](https://www.mdpi.com/2306-5729/6/7/78) and [Zenodo record](https://zenodo.org/records/5113009) | The paper describes 630 real scanned construction-supplier invoices, four layouts, and a `CC-BY-4.0` dataset license. The public Zenodo deposit currently contains only `IOB.rar` (1.1 MB), not the described 566.4 MB of PDFs. The paper also says the data is for research purposes only. | Published labels directly cover invoice number, invoice date, and grand total. Supplier GST is not a UAE TRN; currency, subtotal, VAT amount, and payment terms are not published labels. Relabeling the images is impossible until the scans are obtained. | The CC BY label and research-only statement conflict, the deposited asset does not contain the document images, and real supplier invoices require confidentiality/privacy review. | **Conditional lead, not usable now.** Ask the authors for the scans, the license that applies to the scans, permission for the required uses, and privacy/redaction evidence. Keep any received bytes outside Git until approved. |
| [DocILE benchmark](https://docile.rossum.ai/), [paper](https://arxiv.org/abs/2302.05658), and [privacy notice](https://docile.rossum.ai/static/information_duty_on_scientific_research.pdf) | 6,680 annotated real business documents plus 100,000 synthetic documents. Access requires a token issued for research purposes. The real documents originate from public US archives and include invoices and orders. | Broad invoice structure and totals are useful, but the source population cannot provide UAE TRN coverage. The eight fields would need fresh independent annotation, and invoices must be separated from orders and other document types. | The public repository's MIT license covers software, not a clear grant over the document images. The privacy notice says names, contact data, payment information, bank details, and other identifiers may appear. | **Conditional external benchmark only.** Obtain the full access terms and explicit permission for this use; use only the annotated real subset; exclude synthetic and non-invoice records; complete privacy review; store outside Git. |
| [CORD](https://github.com/clovaai/cord) | 1,000 released Indonesian shop/restaurant receipts with images, OCR boxes/text, and semantic labels under CC BY 4.0. The maintainers removed store, payment, and other categories because of Indonesian legal issues. | Useful for subtotal, tax, and total OCR behavior. It is receipts rather than invoices and does not cover invoice number, supplier TRN, currency, or payment terms consistently. | The license is explicit, but CC BY does not eliminate privacy or other-rights review. The public release already omits fields important to the invoice ontology. | **Optional non-claim supplemental set.** Report as Indonesian receipt robustness, never as invoice or UAE accuracy. |
| [FATURA](https://arxiv.org/html/2311.11856) and [dataset](https://huggingface.co/datasets/mathieu1256/FATURA2-invoices) | 10,000 English invoice images under CC BY 4.0. The paper confirms that text, logos, and images are generated from 50 templates. | Strong schema coverage and useful deterministic layout variation. | Clear public license and low personal-data risk, but every evaluation image is synthetic. | **Development only.** Suitable for contract and regression testing, never for real-document performance claims. |
| [IDSEM](https://www.nature.com/articles/s41597-022-01885-3) | 75,000 generated electricity invoices using nine templates, with a large structured label set and CC BY distribution. | Rich monetary and invoice metadata, but a Spanish electricity-billing population and template-generated documents do not match the Phase 3 claim boundary. | Publicly licensed but synthetic. | **Development only; no claim use.** |
| [Portuguese invoices and receipts](https://zenodo.org/records/6371710) | 813 real images from a private company with seller, buyer-tax, date, total, tax, and reference annotations. | Several fields map structurally, but the documents are Portuguese and mix invoices with receipts. | Real commercial documents require a source-specific rights and privacy review; the language is outside Phase 3. | **Reject for Phase 3.** Reconsider only in a separately scoped Portuguese evaluation. |
| [SROIE official challenge](https://rrc.cvc.uab.es/?ch=13) | Scanned receipt OCR and key-information extraction benchmark with blurred sensitive fields. Official downloads require registration; no dataset-image license sufficient for this project was verified in this review. | Company, date, address, and total only; receipt rather than invoice. | Third-party mirrors make conflicting license claims, which do not replace original-source terms. | **Reject pending original terms; wrong document type regardless.** |
| [UCSF Industry Documents Library](https://beta.industrydocuments.ucsf.edu/fossilfuel/help/copyright/) and FCC public files | Publicly viewable archives used by DocILE/DeepForm. UCSF states that creators may retain copyright and substantial reproduction can require permission. FCC APIs are public, but uploaded station documents are not thereby established as US-government public-domain works. | Some invoices/order forms exist, but population and field coverage are uncontrolled and not UAE-specific. | Public access is not a redistribution license. Copyright, privacy, and payment-data risks remain document-specific. | **Do not scrape into the benchmark.** Use only through a dataset with verified terms or document-by-document written clearance. |
| Community Hugging Face/Kaggle invoice compilations | Dataset cards often declare CC BY or CC0, but commonly aggregate FATURA, RVL-CDIP, templates, or documents with no demonstrated rights chain. Some provide no origin details at all. | Variable and often classification-only; many are synthetic. | A downstream uploader cannot grant rights they did not receive. Repository metadata alone is insufficient evidence for the source images. | **Reject unless every original asset and license chain is independently verified.** |

## Reddit cross-check

Reddit was used as a discovery channel, not as rights evidence. Threads in
[r/datasets](https://www.reddit.com/r/datasets/comments/1c2a1yu/looking_for_dataset_consisting_of_invoices_and/),
[r/MachineLearning](https://www.reddit.com/r/MachineLearning/comments/i9n2zt/project_you_need_more_than_ocr_parse_the_layout/),
and a [recent receipt-dataset request](https://www.reddit.com/r/datasets/comments/1uak3n8/does_anybody_know_of_any_quality_datasets_that/)
repeatedly point back to CORD, SROIE, public-receipt collections, or private
paid offers. The recent discussion also reports that teams ultimately collected
their own data because generic public sets had too much domain mismatch and
because contributors would not share receipts containing private information.

The Reddit leads produced these additional primary-source checks:

| Reddit-discovered source | Verified primary-source facts | Phase 3 decision |
|---|---|---|
| [Jens Walter's My Receipts](https://github.com/JensWalter/my-receipts) | Personal receipt PDF scans collected worldwide and explicitly dedicated to CC0. The collection is multilingual, unannotated, and consists of receipts rather than invoices. | **Optional non-claim scan robustness.** Perform privacy review, create independent labels, and report separately from invoices. |
| [ExpressExpense SRD](https://expressexpense.com/blog/free-receipt-images-ocr-machine-learning-dataset) | 200 high-resolution English restaurant-receipt images, explicitly offered under MIT with a published archive hash. It is unlabeled and covers receipt fields such as subtotal, tax, and total. | **Optional non-claim English receipt robustness.** It cannot support invoice or UAE accuracy. |
| [Humans in the Loop receipt OCR dataset](https://humansintheloop.org/resources/datasets/free-receipt-ocr-dataset/) | A 192-image, 3,839-box CC0 annotated derivative of ExpressExpense, with receipt-specific classes including date, subtotal, tax, and total. | **Optional annotated receipt supplement.** Retain ExpressExpense provenance and keep its metrics separate. |
| [FuxiJia InvoiceDatasets](https://github.com/FuxiJia/InvoiceDatasets) | Camera-captured Chinese taxi and VAT invoices with keyword/character datasets and train/test splits. The repository contains no license. | **Reject for Phase 3.** Wrong language and jurisdiction, and the document-image rights are not granted. |
| Reddit comments offering paid datasets by direct message | No stable manifest, provenance, license, privacy treatment, or independently reviewable contract was supplied publicly. | **Reject as evidence.** A commercial source could be reconsidered only after normal vendor due diligence and exact-file rights review. |

This cross-check improves the list of development and receipt-stress assets but
does not reveal a real, rights-cleared English UAE invoice collection.

## UAE claim boundary

The [Federal Tax Authority's invoice guidance](https://tax.gov.ae/ftatacsoft5cms/DataFolder/Files/Pdf/2022/Get%20to%20know%20your%20Tax%20Obligations.pdf)
requires UAE tax invoices to carry information including the supplier TRN, a
unique invoice number, issue date, total payable in AED, and VAT payable in
AED. The current product ontology also includes currency, subtotal, and payment
terms. None of the public real-document candidates reviewed here provides a
representative UAE population with that coverage.

The [UAE Ministry of Finance](https://mof.gov.ae/en/about-us/initiatives/einvoicing/)
also distinguishes structured eInvoices from PDFs, images, scanned copies, and
emails. Official eInvoicing examples can validate terminology and field rules,
but they are not a real scanned-document benchmark for this product.

Therefore:

- results on MIDD or DocILE must be named **external English invoice
  robustness**, not UAE invoice accuracy;
- CORD results must be named **Indonesian receipt robustness**;
- FATURA and IDSEM results must be named **synthetic development results**;
- only a rights-cleared UAE invoice population can support UAE-facing claims.

## Recommended source strategy

Use two populations and never merge their headline metrics:

1. **Primary UAE benchmark.** Obtain organization-owned or expressly permitted
   English UAE invoices. Record separate permission for storage, labeling,
   processing, and aggregate-result publication. Redact personal/confidential
   data under an approved policy and keep restricted bytes outside Git.
2. **External robustness benchmark.** Pursue MIDD first because its described
   documents are real scanned invoices; pursue DocILE in parallel as a larger
   layout-diversity option. Admit either only after the exact document-image
   terms and privacy treatment pass the intake contract.

FATURA may expand development coverage immediately without changing the claim
boundary. CORD is optional if receipt OCR stress testing is valuable, but it
must remain a separate report.

## Required clearance questions

For MIDD:

- Are the original 630 scanned PDFs available, and is there a stable manifest?
- Does CC BY 4.0 apply to the scanned document images, not only the IOB
  annotations and article?
- Does the phrase "research purposes only" impose a separate restriction?
- Did the construction firm and suppliers authorize dataset distribution,
  relabeling, automated processing, and publication of aggregate results?
- What personal, tax, bank, or commercially confidential data remains?

For DocILE:

- What exact terms are accepted when the access token is issued?
- Is a public portfolio evaluation within the permitted research purpose?
- May derived labels and aggregate metrics be published?
- Are local processing and approved third-party OCR/provider calls permitted?
- What retention, access-control, deletion, and incident duties apply?

Until these answers are recorded, all permission flags in the source register
must remain false and no candidate may set `claim_eligible: true`.

## Next executable checkpoint

After one image source is cleared, acquire a small pilot into controlled
storage, fingerprint every file, classify invoice versus non-invoice before
provider output is viewed, and run privacy/redaction review. Then calibrate the
eight-field annotation guide. The pilot, not the published dataset size, will
determine feasible strata and the benchmark sample-count target.
