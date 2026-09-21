using System.Security.Cryptography;
using System.Text.Json;

internal sealed record PilotSelection(Guid RunId, string Question, string? RequestKey, Guid[] IndexIds,
    string? PolicyVersion = null);

internal static class QualityCaseSelection
{
    public static PilotSelection ReadFollowup(string caseId, bool low = false, bool lowFixed = false, bool sourceAware = false)
    {
        var bytes = File.ReadAllBytes(sourceAware ? "samples/phase-5/answer-source-v0.4.0/plan.json" :
            lowFixed ? "samples/phase-5/answer-low-fixed-v0.3.1/plan.json" :
            low ? "samples/phase-5/answer-low-v0.3.0/plan.json" : "samples/phase-5/answer-followup-v0.2.0/plan.json");
        if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() !=
            (sourceAware ? "56fda3a2d5317cea14700f00965b4a808dc855e9d1d6934d48b8190cce7c08be" :
            lowFixed ? "9695a66bdc7fc3394525d10731537f8d33c326f9a1cc3b1b00d9479cf9a44632" :
            low ? "ffca5a39729d0f5a57f72212afa54618824322e0e000bbed9183ff5d41ab7321" :
            "f342359d97a8cb176013d4aed01a1833116b0a1fe510a294162586b494b8b752")) throw new FormatException();
        using var document = JsonDocument.Parse(bytes);
        var item = document.RootElement.GetProperty("cases").EnumerateArray()
            .Single(x => x.GetProperty("caseId").GetString() == caseId);
        return new(item.GetProperty("runId").GetGuid(), item.GetProperty("question").GetString()!,
            item.GetProperty("requestKey").GetString(),
            item.GetProperty("indexIds").EnumerateArray().Select(x => x.GetGuid()).ToArray(),
            item.GetProperty("policyVersion").GetString());
    }

    // Prepared through the real local API from the pre-labeled synthetic Phase 4 pack.
    // A changed plan/source/retrieval needs a separately reviewed, versioned manifest.
    internal const string ManifestPath = "outputs/phase-5/live-quality-20260909/prepared-cases.json";
    internal const string ManifestSha256 = "aeeb7c681733fe4f11dce02af2185b1ab53554123630e502697211ce408a2b6a";
    private static readonly string[] Allowed = ["p5-q-007", "p5-q-009", "p5-q-014", "p5-q-016",
        "p5-q-019", "p5-q-022", "p5-q-025", "p5-q-026", "p5-q-028", "p5-q-029"];

    public static PilotSelection Read(string caseId)
    {
        if (!Allowed.Contains(caseId)) throw new FormatException();
        var info = new FileInfo(ManifestPath);
        if (info.Length is < 1 or > 524288) throw new FormatException();
        var bytes = File.ReadAllBytes(info.FullName);
        if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != ManifestSha256) throw new FormatException();
        using var document = JsonDocument.Parse(bytes.AsMemory(bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }) ? 3 : 0));
        var root = document.RootElement;
        if (root.GetProperty("version").GetString() != "live-quality-v0.1.0" ||
            !root.GetProperty("synthetic").GetBoolean() || root.GetProperty("claim_eligible").GetBoolean() ||
            root.GetProperty("paid_api_calls").GetInt32() != 0 || root.GetProperty("cases").GetArrayLength() != 10)
            throw new FormatException();
        var item = root.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("case_id").GetString() == caseId);
        var result = item.GetProperty("result");
        // No-hit refusals stay provider-free; never inject evidence to force a paid answer.
        if (result.GetProperty("stage").GetString() != "EvidenceReady" ||
            result.GetProperty("answerGenerationEnabled").GetBoolean()) throw new FormatException();
        var request = item.GetProperty("request");
        if (request.GetProperty("topK").GetInt32() != 5) throw new FormatException();
        return new(result.GetProperty("retrieval").GetProperty("id").GetGuid(),
            item.GetProperty("question").GetString()!, request.GetProperty("requestKey").GetString()!,
            request.GetProperty("indexIds").EnumerateArray().Select(x => x.GetGuid()).ToArray());
    }
}
