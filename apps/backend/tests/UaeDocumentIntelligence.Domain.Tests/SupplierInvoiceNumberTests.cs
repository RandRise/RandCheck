using System.Globalization;
using UaeDocumentIntelligence.Domain.Model;

namespace UaeDocumentIntelligence.Domain.Tests;

public sealed class SupplierInvoiceNumberTests
{
    [Theory]
    [InlineData("0", "0")]
    [InlineData("0001.20", "1.20")]
    [InlineData("1,234,567.000001", "1234567.000001")]
    [InlineData("999999999999999999.999999", "999999999999999999.999999")]
    public void Accepted_values_are_exact_invariant_decimals(string raw, string expected)
    {
        Assert.True(SupplierInvoiceNumber.TryParse(raw, out var actual));
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), actual);
    }

    [Theory]
    [InlineData("1,23")]
    [InlineData("12,3456")]
    [InlineData("1234,567")]
    [InlineData("1.234,56")]
    [InlineData("1 234")]
    [InlineData("١٢٣")]
    [InlineData("۱۲۳")]
    [InlineData("1٫5")]
    [InlineData("1٬234")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1e3")]
    [InlineData(".5")]
    [InlineData("1.")]
    [InlineData("1.0000001")]
    [InlineData("1000000000000000000")]
    [InlineData(" 1")]
    [InlineData("1\n")]
    [InlineData("AED 1")]
    public void Ambiguous_or_out_of_profile_values_are_unavailable(string raw) =>
        Assert.False(SupplierInvoiceNumber.TryParse(raw, out _));

    [Fact]
    public void Current_culture_cannot_change_supported_syntax()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-AE");
            Assert.True(SupplierInvoiceNumber.TryParse("1,234.50", out var number));
            Assert.Equal(1234.5m, number);
            Assert.False(SupplierInvoiceNumber.TryParse("١,٢٣٤.٥٠", out _));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Tolerances_must_fit_existing_storage_without_rounding()
    {
        Assert.True(SupplierInvoiceNumber.IsPersistableTolerance(999999999999.999999m));
        Assert.True(SupplierInvoiceNumber.IsPersistableTolerance(0.000001m));
        Assert.False(SupplierInvoiceNumber.IsPersistableTolerance(1000000000000m));
        Assert.False(SupplierInvoiceNumber.IsPersistableTolerance(0.0000001m));
        Assert.False(SupplierInvoiceNumber.IsPersistableTolerance(-0.01m));
    }
}
