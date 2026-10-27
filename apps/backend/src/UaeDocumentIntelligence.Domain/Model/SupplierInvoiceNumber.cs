using System.Globalization;
using System.Text.RegularExpressions;

namespace UaeDocumentIntelligence.Domain.Model;

public static partial class SupplierInvoiceNumber
{
    public static bool TryParse(string value, out decimal number)
    {
        number = 0;
        if (value is null || !Syntax().IsMatch(value)) return false;
        var normalized = value.Replace(",", "", StringComparison.Ordinal);
        if (normalized.Split('.')[0].Length > 18) return false;
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out number);
    }

    public static bool IsPersistableTolerance(decimal value) => value >= 0 &&
        value <= 999999999999.999999m && ((decimal.GetBits(value)[3] >> 16) & 255) <= 6;

    [GeneratedRegex("\\A(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\\.[0-9]{1,6})?\\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex Syntax();
}
