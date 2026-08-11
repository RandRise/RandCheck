namespace UaeDocumentIntelligence.Domain.Common;

internal static class Guard
{
    public static string Required(string? value, string parameter, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", parameter);
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength) throw new ArgumentOutOfRangeException(parameter, $"Maximum length is {maxLength}.");
        return trimmed;
    }

    public static void Positive(int value, string parameter)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(parameter, "Value must be positive.");
    }
}
