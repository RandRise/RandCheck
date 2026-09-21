using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Application.CitedQuestions;

/// <summary>Versioned conservative source screening, not a general injection detector.</summary>
public static class CitedSourceSafety
{
    public const string Version = "cited-source-screen-v1";
    public static bool Suspicious(IEnumerable<string> fragments)
    {
        var text = string.Join("\n", fragments);
        if (text.Length > 8_388_608) return true;
        text = WebUtility.HtmlDecode(text).Normalize(NormalizationForm.FormKC);
        text = Regex.Replace(text, @"\\u([0-9a-fA-F]{4})", match =>
            ((char)Convert.ToUInt16(match.Groups[1].Value, 16)).ToString(), RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));
        var compact = new string(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        string[] markers = ["ignorepreviousinstructions", "ignoreallinstructions", "disregardpreviousinstructions",
            "overridesystem", "revealsystemprompt", "revealsecrets", "revealotherorganizations", "exfiltrate",
            "callhttps", "callhttp", "executetool", "droptable", "forgedcitation", "forgecitation"];
        return markers.Any(compact.Contains) || Regex.IsMatch(text,
            @"(?is)\[\s*(system|developer)\s*\]|<\s*(script|system|developer)\b|\bonerror\s*=|\bjavascript\s*:",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking, TimeSpan.FromMilliseconds(200));
    }

    public static void RequireSafe(IEnumerable<string> fragments)
    {
        if (Suspicious(fragments)) throw new CitedWorkflowException("unsafe_source",
            "The selected evidence contains instruction-like content and requires review.");
    }
}
