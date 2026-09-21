using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Infrastructure.Services;

public sealed class PrivateServiceCredential
{
    public string KeyId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}

internal static partial class PrivateServiceCredentials
{
    // Fixed operation mapping: request content never chooses a credential.
    internal static bool TryApply(HttpRequestMessage request, HttpClient client, AiServiceOptions options)
    {
        var path = request.RequestUri?.OriginalString;
        var credential = path switch
        {
            "/internal/extractions" => options.ExtractionCredential,
            "/internal/cited/chunks" or "/internal/cited/lexical-plan" => options.CitedCredential,
            "/internal/cited/answer-prepare" or "/internal/cited/answer-proposal" => options.AnswerCredential,
            _ => null
        };
        if (request.Method != HttpMethod.Post || credential is null ||
            request.Headers.Contains("X-Worker-Token") || request.Headers.Contains("X-Service-Key-Id")) return false;
        if (options.AuthenticationMode == "Development")
        {
            if (string.IsNullOrWhiteSpace(options.WorkerToken) || options.WorkerToken.Any(char.IsControl)) return false;
            return request.Headers.TryAddWithoutValidation("X-Worker-Token", options.WorkerToken);
        }
        if (options.AuthenticationMode != "ServiceKeys" || !SafeOrigin(client.BaseAddress) ||
            !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var configured) || !SafeOrigin(configured) ||
            configured != client.BaseAddress || string.IsNullOrEmpty(credential.KeyId) ||
            string.IsNullOrEmpty(credential.Token) || !KeyIdPattern().IsMatch(credential.KeyId) ||
            !TokenPattern().IsMatch(credential.Token)) return false;
        request.Headers.Add("X-Service-Key-Id", credential.KeyId);
        request.Headers.Add("X-Worker-Token", credential.Token);
        return true;
    }

    private static bool SafeOrigin(Uri? uri) => uri is { IsAbsoluteUri: true } &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) && uri.AbsolutePath == "/";

    [GeneratedRegex("\\A[A-Za-z0-9_-]{1,64}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyIdPattern();
    [GeneratedRegex("\\A[A-Za-z0-9_-]{43,128}\\z", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
