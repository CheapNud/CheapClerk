using CheapClerk.Services;
using Xunit;

namespace CheapClerk.Tests;

public sealed class LlmJsonParserTests
{
    private sealed class ProbeDoc
    {
        public string? SuggestedTitle { get; set; }
        public double Confidence { get; set; }
    }

    private enum ProbeCategory { Unknown, Invoice, Contract }

    private sealed class ProbeTyped
    {
        public ProbeCategory Category { get; set; }
        public double Confidence { get; set; }
    }

    [Fact]
    public void TryParse_JsonInFencedCodeBlock_Parses()
    {
        var raw = """
            ```json
            {"SuggestedTitle": "KBC Woonverzekering", "Confidence": 0.92}
            ```
            """;

        var success = LlmJsonParser.TryParse<ProbeDoc>(raw, out var parsed);

        Assert.True(success);
        Assert.Equal("KBC Woonverzekering", parsed!.SuggestedTitle);
        Assert.Equal(0.92, parsed.Confidence);
    }

    [Fact]
    public void TryParse_JsonSurroundedByProse_Parses()
    {
        var raw = """
            Sure, here is the classification you requested:
            {"SuggestedTitle": "Engie Factuur", "Confidence": 0.75}
            Let me know if you need anything else!
            """;

        var success = LlmJsonParser.TryParse<ProbeDoc>(raw, out var parsed);

        Assert.True(success);
        Assert.Equal("Engie Factuur", parsed!.SuggestedTitle);
        Assert.Equal(0.75, parsed.Confidence);
    }

    [Fact]
    public void TryParse_BareJson_Parses()
    {
        var raw = """{"SuggestedTitle": "Bare JSON", "Confidence": 0.5}""";

        var success = LlmJsonParser.TryParse<ProbeDoc>(raw, out var parsed);

        Assert.True(success);
        Assert.Equal("Bare JSON", parsed!.SuggestedTitle);
        Assert.Equal(0.5, parsed.Confidence);
    }

    [Fact]
    public void TryParse_CamelCaseProperties_BindToPascalCaseMembers()
    {
        var raw = """{"suggestedTitle": "Camel Case Title", "confidence": 0.6}""";

        var success = LlmJsonParser.TryParse<ProbeDoc>(raw, out var parsed);

        Assert.True(success);
        Assert.Equal("Camel Case Title", parsed!.SuggestedTitle);
        Assert.Equal(0.6, parsed.Confidence);
    }

    [Fact]
    public void TryParse_TextWithNoBraces_ReturnsFalse()
    {
        var raw = "I'm sorry, I could not classify this document.";

        var success = LlmJsonParser.TryParse<ProbeDoc>(raw, out var parsed);

        Assert.False(success);
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_MalformedJsonInsideBraces_ReturnsFalse()
    {
        var raw = """{"SuggestedTitle": "Missing closing quote, "Confidence": 0.4}""";

        var success = LlmJsonParser.TryParse<ProbeDoc>(raw, out var parsed);

        Assert.False(success);
        Assert.Null(parsed);
    }

    [Fact]
    public void TryParse_NullOrWhitespace_ReturnsFalse()
    {
        Assert.False(LlmJsonParser.TryParse<ProbeDoc>(null, out var parsedNull));
        Assert.Null(parsedNull);

        Assert.False(LlmJsonParser.TryParse<ProbeDoc>("   ", out var parsedWhitespace));
        Assert.Null(parsedWhitespace);
    }

    [Fact]
    public void TryParse_BindsStringEnumValues()
    {
        var fenced = "```json\n{\"category\":\"Invoice\",\"confidence\":0.9}\n```";

        var ok = LlmJsonParser.TryParse<ProbeTyped>(fenced, out var parsed);

        Assert.True(ok);
        Assert.Equal(ProbeCategory.Invoice, parsed!.Category);
    }
}
