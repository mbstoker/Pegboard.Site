using PegboardWebSite.Survey;
using Xunit;

namespace PegboardWebSite.Tests.Survey;

/// <summary>
/// The shipped definitions (ado:2566) load, validate, and carry the question sets in the
/// order of the 2026-11-09 question-set documents. The id lists here are the transcription
/// check: a question dropped, renamed or moved fails this test.
/// </summary>
public sealed class SurveyDefinitionTests
{
    public static string DefinitionsDir => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "PegboardWebSite", "Survey", "Definitions"));

    public static SurveyDefinition Load(string id) =>
        SurveyCatalog.Parse(File.ReadAllText(Path.Combine(DefinitionsDir, id + ".json")));

    [Theory]
    [InlineData("organisers")]
    [InlineData("players")]
    public void Shipped_definition_is_valid(string id)
    {
        var def = Load(id);
        Assert.Equal(id, def.Id);
        Assert.True(def.Version >= 1);
        Assert.Empty(SurveyCatalog.Validate(def));
    }

    [Fact]
    public void Catalog_loads_both_surveys_from_the_folder()
    {
        var catalog = SurveyCatalog.LoadFrom(DefinitionsDir);
        Assert.NotNull(catalog.Get("organisers"));
        Assert.NotNull(catalog.Get("players"));
    }

    [Theory]
    [InlineData("organisers")]
    [InlineData("players")]
    public void Every_condition_refers_to_an_earlier_question(string id)
    {
        var def = Load(id);
        var earlier = new HashSet<string>();
        foreach (var q in def.Questions)
        {
            if (q.ShowIf is not null)
                Assert.True(earlier.Contains(q.ShowIf.Question), $"{q.Id} depends on {q.ShowIf.Question}, which is not earlier");
            earlier.Add(q.Id);
        }
        foreach (var end in def.EndPages)
            Assert.Contains(end.When.Question, earlier);
    }

    [Fact]
    public void Organiser_questions_match_the_question_set_order()
    {
        var expected = new[]
        {
            "S1", "S2", "S3", "S4",
            "N1", "N2", "N3", "N3c", "N3d", "N3b", "N4", "N5", "N5b", "N6",
            "B1", "B2", "B3", "B4",
            "F1a", "F1c", "F1b", "F2", "F3", "F4", "F5", "F6",
            "C1", "C2",
            "K1",
            "L1", "L2",
            "H1",
            "T1", "T2", "T3", "T4", "T5",
            "A1", "A2", "A3",
            "Z1",
        };
        Assert.Equal(expected, Load("organisers").Questions.Select(q => q.Id));
    }

    [Fact]
    public void Player_questions_match_the_question_set_order()
    {
        var expected = new[]
        {
            "Y0", "Y1", "Y2", "Y3", "Y4", "Y5", "Y6", "Y6b",
            "R1", "R2",
            "G1", "G1b", "G1c", "G2", "G3", "G4", "G5", "G6", "G7", "G8",
            "L1", "L2",
            "B1", "B2",
            "S1", "S2",
            "M1", "M2",
            "D1", "D2", "D3",
            "X1", "Z1",
        };
        Assert.Equal(expected, Load("players").Questions.Select(q => q.Id));
    }

    [Fact]
    public void Player_survey_has_the_under_16_end_page()
    {
        var end = Assert.Single(Load("players").EndPages);
        Assert.Equal("Y0", end.When.Question);
        Assert.Equal("Under 16", end.When.EqualTo);
        Assert.Equal("Thanks for your interest. This survey is for players aged 16 and over.", end.Text);
    }

    [Theory]
    [InlineData("organisers", "Thinking about your main regular session over the past four weeks.", "N1", "A3")]
    [InlineData("players", "Thinking about the session you play at most, over the past four weeks.", "R1", "S2")]
    public void Header_line_covers_the_session_questions(string id, string header, string first, string last)
    {
        var def = Load(id);
        var from = def.IndexOf(first);
        var to = def.IndexOf(last);
        for (var i = 0; i < def.Questions.Count; i++)
            Assert.Equal(i >= from && i <= to ? header : null, def.Questions[i].Header);
    }

    [Fact]
    public void Pick_up_to_three_questions_have_the_limit()
    {
        Assert.Equal(3, Load("organisers").Find("F2")!.MaxPicks);
        Assert.Equal(3, Load("organisers").Find("A2")!.MaxPicks);
        Assert.Equal(3, Load("players").Find("D3")!.MaxPicks);
    }

    [Fact]
    public void Opening_text_says_who_runs_it_and_how_long_it_takes()
    {
        Assert.StartsWith("I'm Mike Stoker, a badminton player in Milton Keynes and the developer of ePegboard.", Load("organisers").Opening);
        Assert.Contains("It takes about 8 minutes.", Load("organisers").Opening);
        Assert.Contains("It takes about 7 minutes.", Load("players").Opening);
    }

    // --- The validator's rejecting half: planted bad definitions must fail.

    private const string Minimal = """
        { "id": "t", "version": 1, "opening": "o", "afterSubmission": "a", "sections": [ { "questions": [
          { "id": "A", "type": "single", "text": "a?", "options": [ "Yes", "No" ] },
          { "id": "B", "type": "single", "text": "b?", "options": [ "Yes", "No" ], "showIf": __COND__ }
        ] } ] }
        """;

    [Theory]
    [InlineData("""{ "question": "C", "equals": "Yes" }""", "not an earlier question")]
    [InlineData("""{ "question": "B", "equals": "Yes" }""", "not an earlier question")]
    [InlineData("""{ "question": "A", "equals": "Maybe" }""", "is not an option")]
    [InlineData("""{ "question": "A", "equals": "Yes", "notEquals": "No" }""", "exactly one operator")]
    [InlineData("""{ "question": "A", "minCount": 2 }""", "multi-choice")]
    public void Validator_rejects_a_bad_condition(string condition, string expected)
    {
        var errors = SurveyCatalog.Validate(SurveyCatalog.Parse(Minimal.Replace("__COND__", condition)));
        Assert.Contains(errors, e => e.Contains(expected));
    }

    [Fact]
    public void Validator_accepts_the_minimal_good_definition()
    {
        var def = SurveyCatalog.Parse(Minimal.Replace("__COND__", """{ "question": "A", "equals": "Yes" }"""));
        Assert.Empty(SurveyCatalog.Validate(def));
    }

    [Fact]
    public void An_invalid_file_is_left_out_of_the_catalog_rather_than_failing_startup()
    {
        var dir = Directory.CreateTempSubdirectory("survey-defs-");
        try
        {
            File.WriteAllText(Path.Combine(dir.FullName, "bad.json"), Minimal.Replace("__COND__", """{ "question": "Z", "equals": "Yes" }"""));
            File.WriteAllText(Path.Combine(dir.FullName, "good.json"),
                Minimal.Replace("\"t\"", "\"good\"").Replace("__COND__", """{ "question": "A", "equals": "Yes" }"""));
            var catalog = SurveyCatalog.LoadFrom(dir.FullName);
            Assert.Null(catalog.Get("t"));
            Assert.NotNull(catalog.Get("good"));
        }
        finally
        {
            dir.Delete(true);
        }
    }
}
