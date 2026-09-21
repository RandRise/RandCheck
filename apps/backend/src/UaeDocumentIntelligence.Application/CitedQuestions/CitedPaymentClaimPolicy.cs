using System.Text;
using System.Text.RegularExpressions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

/// <summary>
/// Conservative English pilot restriction, NOT a general entailment validator.
/// No authoritative payment-event source is integrated. Neither a document stamp
/// nor model-provided labels can authorize payment assertions. Passing this check
/// does not establish semantic support; publication remains disabled.
/// </summary>
public static class CitedPaymentClaimPolicy
{
    public const string Version = "payment-event-restriction-v1";
    private static readonly Regex[] EventLanguage = new[]
    {
        @"\b(paid|unpaid|settled|unsettled|settlement|remitted|remittance|credited|cleared|received)\b",
        @"\bpayment[\s_-]+(date|status|received|completed|made|confirmed|confirmation|receipt|history)\b",
        @"\b(date|status|receipt|confirmation)[\s_-]+of[\s_-]+(the[\s_-]+)?payment\b",
        @"\b(when|whether|has|have|did|was|were)\b[\s\S]*\b(pay|payment|transfer)\b",
        @"\b(transfer|funds|money|payment)\b[\s\S]*\b(arrived|completed|successful|successfully|went through|made|confirmed)\b"
    }.Select(pattern => new Regex(pattern,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100))).ToArray();

    public static void Validate(string question, CitedProposalValidation proposal)
    {
        // No partial answer to a detected event question: even a bare date or
        // 'yes' would otherwise evade a claim-text-only check. Refusal is safe.
        if (proposal.Status == CitedAnswerStatus.InsufficientEvidence) return;
        if (MentionsEvent(question) || proposal.Claims.Any(claim => MentionsEvent(claim.Text)))
            throw new CitedWorkflowException("payment_event_not_verified",
                "Payment-event answers require a verified payment-event source.", 502);
    }

    public static bool RequiresVerifiedEvent(string question) => MentionsEvent(question);

    private static bool MentionsEvent(string text)
    {
        // Compatibility normalization closes simple full-width/zero-width bypasses,
        // not all Unicode, language, paraphrase or prompt-injection attacks.
        var normalized = string.Concat(text.Normalize(NormalizationForm.FormKC)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                System.Globalization.UnicodeCategory.Format));
        return EventLanguage.Any(pattern => pattern.IsMatch(normalized));
    }
}
