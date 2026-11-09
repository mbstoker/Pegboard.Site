using PegboardWebSite.Survey;
using Xunit;

namespace PegboardWebSite.Tests.Survey;

/// <summary>CSV cell escaping for the survey export (ado:2566): formula injection and RFC 4180 quoting.</summary>
public sealed class SurveyExportTests
{
    private static readonly SurveyDefinition Organisers = SurveyDefinitionTests.Load("organisers");

    /// <summary>The A3 (free text) cell of a one-response export, exactly as written.</summary>
    private static string Csv(string a3) =>
        SurveyExport.ToCsv(Organisers, new[]
        {
            new SurveyResponseRecord(Guid.Empty, "organisers", 1, new DateTimeOffset(2026, 11, 9, 18, 0, 0, TimeSpan.Zero),
                new Dictionary<string, List<string>> { ["A3"] = new() { a3 } }, new List<string> { "A3" }),
        });

    private static string DataRow(string csv) => csv.Split("\r\n")[1];

    [Theory]
    [InlineData("=HYPERLINK(\"x\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public void A_cell_starting_with_a_formula_character_is_prefixed(string text)
    {
        var csv = Csv(text);
        // Prefixed with ' (and quoted where the text itself has a quote), never left bare.
        var expected = "'" + text;
        expected = expected.Contains('"') ? "\"" + expected.Replace("\"", "\"\"") + "\"" : expected;
        Assert.Contains("," + expected + ",", DataRow(csv));
        Assert.DoesNotContain("," + text, DataRow(csv));
    }

    [Fact]
    public void A_cell_with_a_comma_is_quoted()
    {
        var row = DataRow(Csv("Courts, mostly"));
        Assert.Contains(",\"Courts, mostly\",", row);
    }

    [Fact]
    public void A_cell_with_a_newline_and_quotes_is_quoted_and_the_quotes_doubled()
    {
        var csv = Csv("Line one\nsaid \"hello\"");
        Assert.Contains(",\"Line one\nsaid \"\"hello\"\"\",", csv);
        // The embedded newline stays inside the quotes, so it doesn't start a new record.
        Assert.Equal(3, csv.Split("\r\n").Length); // header, row, trailing empty
    }

    [Fact]
    public void A_plain_cell_is_left_alone() =>
        Assert.Contains(",More courts,", DataRow(Csv("More courts")));
}
