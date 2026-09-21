using System.Text;
using System.Text.RegularExpressions;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

/// <summary>Conservative English pilot scope restriction, not general entailment or tenant authorization.</summary>
public static class CitedScopeClaimPolicy
{
    public const string Version = "cited-scope-restriction-v1";
    private static readonly Regex OutsideScope = new(
        @"\b(legally\s+compliant|legal\s+(compliance|approval)|claim\s+input\s+tax|input[\s-]+tax\s+(eligibility|claim))\b|" +
        @"\b(foreign|another|other)\s+(organization|organisation|tenant)\b|" +
        @"\b(secrets?|passwords?|api[\s_-]*keys?|access[\s_-]*tokens?)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking,
        TimeSpan.FromMilliseconds(100));

    public static bool RequiresRefusal(string question)
    {
        var normalized = string.Concat(question.Normalize(NormalizationForm.FormKC).Where(c =>
            System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format));
        return OutsideScope.IsMatch(normalized);
    }

    public static void Validate(string question, CitedProposalValidation proposal)
    {
        if (RequiresRefusal(question) && (proposal.Status != CitedAnswerStatus.InsufficientEvidence ||
            proposal.ReasonCode != "unsupported_scope" || proposal.Claims.Count != 0))
            throw new CitedWorkflowException("answer_scope_not_supported",
                "This request requires an unsupported-scope refusal.", 502);
    }
}
