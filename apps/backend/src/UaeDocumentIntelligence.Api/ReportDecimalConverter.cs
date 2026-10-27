using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UaeDocumentIntelligence.Api;

/// <summary>Report amounts cross JavaScript as invariant text, preserving all decimal digits.</summary>
internal sealed class ReportDecimalConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
        throw new NotSupportedException("Report decimal conversion is output-only.");

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
