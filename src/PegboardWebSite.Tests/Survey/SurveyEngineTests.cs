using Microsoft.Extensions.Configuration;
using PegboardWebSite.Survey;
using Xunit;

namespace PegboardWebSite.Tests.Survey;

/// <summary>Branching, validation, the age gate and normalisation, on the shipped definitions (ado:2566).</summary>
public sealed class SurveyEngineTests
{
    private static readonly SurveyDefinition Organisers = SurveyDefinitionTests.Load("organisers");
    private static readonly SurveyDefinition Players = SurveyDefinitionTests.Load("players");

    private static Dictionary<string, List<string>> A(params (string Id, string[] Values)[] answers) =>
        answers.ToDictionary(a => a.Id, a => a.Values.ToList());

    private static bool Shown(SurveyDefinition def, string id, Dictionary<string, List<string>> answers) =>
        SurveyEngine.Normalize(def, answers).Path.Contains(id);

    // --- Branching: each showIf rule, both sides.

    [Theory]
    [InlineData(new[] { "Round robin" }, false)]
    [InlineData(new[] { "Round robin", "Ladder" }, true)]
    public void N3_shows_only_when_more_than_one_format(string[] formats, bool shown) =>
        Assert.Equal(shown, Shown(Organisers, "N3", A(("N2", formats))));

    [Theory]
    [InlineData("Yes", true)]
    [InlineData("Maybe", true)]
    [InlineData("No, we're happy as we are", false)]
    public void N3d_shows_only_when_the_club_would_like_more_formats(string n3c, bool shown) =>
        Assert.Equal(shown, Shown(Organisers, "N3d", A(("N3c", new[] { n3c }))));

    [Fact]
    public void N3d_is_hidden_when_N3c_is_unanswered() =>
        Assert.False(Shown(Organisers, "N3d", A()));

    [Theory]
    [InlineData("Yes, on different courts", true)]
    [InlineData("Yes, in separate sessions", true)]
    [InlineData("No, everyone mixes", false)]
    [InlineData("Players sort themselves out by level", false)]
    public void N5b_shows_only_when_levels_are_separated(string n5, bool shown) =>
        Assert.Equal(shown, Shown(Organisers, "N5b", A(("N5", new[] { n5 }))));

    [Theory]
    [InlineData("They have to book", true, true)]
    [InlineData("We ask, but it's optional", false, true)]
    [InlineData("No, they just turn up", false, false)]
    public void B3_and_B4_follow_booking(string b1, bool b3, bool b4)
    {
        var answers = A(("B1", new[] { b1 }));
        Assert.Equal(b3, Shown(Organisers, "B3", answers));
        Assert.Equal(b4, Shown(Organisers, "B4", answers));
    }

    [Theory]
    [InlineData(new[] { "We enter league teams", "We host external competitions or matches" }, true)]
    [InlineData(new[] { "We host external competitions or matches" }, false)]
    public void L2_shows_only_for_league_clubs(string[] l1, bool shown) =>
        Assert.Equal(shown, Shown(Organisers, "L2", A(("L1", l1))));

    [Theory]
    [InlineData("Use it now", true, true, true, false)]
    [InlineData("Used it before but stopped", true, true, false, true)]
    [InlineData("Never", false, false, false, false)]
    public void Software_questions_follow_T1(string t1, bool t2, bool t3, bool t4, bool t5)
    {
        var answers = A(("T1", new[] { t1 }));
        Assert.Equal(t2, Shown(Organisers, "T2", answers));
        Assert.Equal(t3, Shown(Organisers, "T3", answers));
        Assert.Equal(t4, Shown(Organisers, "T4", answers));
        Assert.Equal(t5, Shown(Organisers, "T5", answers));
    }

    [Theory]
    [InlineData("Timed rounds of under 10 minutes", true)]
    [InlineData("Timed rounds of 10 minutes or more", true)]
    [InlineData("It varies", true)]
    [InlineData("Played to a full score (for example 21)", false)]
    [InlineData("Played to a shorter score (for example 11 or 15)", false)]
    [InlineData("Don't know", false)]
    public void G1c_shows_only_for_timed_or_varying_games(string g1b, bool shown) =>
        Assert.Equal(shown, Shown(Players, "G1c", A(("G1b", new[] { g1b }))));

    [Theory]
    [InlineData("Uses one now", true)]
    [InlineData("Used one before", true)]
    [InlineData("Never", false)]
    [InlineData("Don't know", false)]
    public void Player_S2_follows_S1(string s1, bool shown) =>
        Assert.Equal(shown, Shown(Players, "S2", A(("S1", new[] { s1 }))));

    [Fact]
    public void Moving_and_finding_questions_follow_their_gates()
    {
        Assert.False(Shown(Players, "M2", A(("M1", new[] { "No" }))));
        Assert.True(Shown(Players, "M2", A(("M1", new[] { "Left one" }))));
        Assert.False(Shown(Players, "D2", A(("D1", new[] { "No" }))));
        Assert.True(Shown(Players, "D3", A(("D1", new[] { "Yes" }))));
    }

    // --- Hidden answers are never stored.

    [Fact]
    public void Normalize_drops_the_answer_to_a_hidden_question()
    {
        var answers = A(("N2", new[] { "Round robin" }), ("N3", new[] { "We change format during the night" }),
                        ("N3c", new[] { "No, we're happy as we are" }), ("N3d", new[] { "Other" }));
        var result = SurveyEngine.Normalize(Organisers, answers);
        Assert.False(result.Answers.ContainsKey("N3"));
        Assert.False(result.Answers.ContainsKey("N3d"));
        Assert.DoesNotContain("N3", result.Path);
        Assert.DoesNotContain("N3d", result.Path);
        Assert.Equal(new[] { "Round robin" }, result.Answers["N2"]);
    }

    [Fact]
    public void Normalize_keeps_a_shown_question_and_records_it_in_the_path()
    {
        var answers = A(("N3c", new[] { "Maybe" }), ("N3d", new[] { "Other" }));
        var result = SurveyEngine.Normalize(Organisers, answers);
        Assert.Equal(new[] { "Other" }, result.Answers["N3d"]);
        Assert.Contains("N3d", result.Path);
    }

    [Fact]
    public void Normalize_drops_answers_to_ids_the_definition_does_not_have()
    {
        var result = SurveyEngine.Normalize(Organisers, A(("NOPE", new[] { "x" }), ("S1", new[] { "UK" })));
        Assert.False(result.Answers.ContainsKey("NOPE"));
        Assert.True(result.Answers.ContainsKey("S1"));
    }

    // --- Steps.

    [Fact]
    public void A_question_never_shares_a_step_with_the_question_it_depends_on()
    {
        // N2 and N3 are in the same section; N3 must wait for N2's answer.
        var start = Organisers.IndexOf("N1");
        var step = SurveyEngine.BuildStep(Organisers, A(), start);
        Assert.Equal(new[] { "N1", "N2" }, step.Questions.Select(q => q.Id));

        var next = SurveyEngine.BuildStep(Organisers, A(("N2", new[] { "Round robin" })), step.NextIndex);
        Assert.Equal("N3c", next.Questions[0].Id); // N3 skipped: one format only
    }

    [Fact]
    public void The_age_question_is_alone_on_the_first_step()
    {
        var step = SurveyEngine.BuildStep(Players, A(), 0);
        Assert.Equal(new[] { "Y0" }, step.Questions.Select(q => q.Id));
    }

    [Fact]
    public void A_change_of_header_starts_a_new_step()
    {
        var step = SurveyEngine.BuildStep(Organisers, A(), 0);
        Assert.Equal(new[] { "S1", "S2", "S3", "S4" }, step.Questions.Select(q => q.Id));
        Assert.All(step.Questions, q => Assert.Null(q.Header));
        Assert.Equal("N1", SurveyEngine.BuildStep(Organisers, A(), step.NextIndex).Questions[0].Id);
    }

    [Fact]
    public void Steps_hold_at_most_the_configured_number_of_questions()
    {
        var i = 0;
        var answers = A();
        while (true)
        {
            var step = SurveyEngine.BuildStep(Organisers, answers, i);
            if (step.IsEmpty) break;
            Assert.InRange(step.Questions.Count, 1, Organisers.QuestionsPerStep);
            i = step.NextIndex;
        }
    }

    // --- Age gate.

    [Fact]
    public void Under_16_matches_the_end_page()
    {
        Assert.NotNull(SurveyEngine.MatchEndPage(Players, A(("Y0", new[] { "Under 16" }))));
        Assert.Null(SurveyEngine.MatchEndPage(Players, A(("Y0", new[] { "16–24" }))));
    }

    // --- Validation.

    private static (Dictionary<string, List<string>> Accepted, Dictionary<string, string> Errors) Validate(
        SurveyDefinition def, string id, params string[] values) =>
        SurveyEngine.ValidateStep(new[] { def.Find(id)! }, q => q == id ? values : Array.Empty<string>());

    [Fact]
    public void A_pick_up_to_three_question_refuses_a_fourth_choice()
    {
        var (accepted, errors) = Validate(Organisers, "F2", "Word of mouth", "Facebook", "Club website", "The venue");
        Assert.Equal("Please choose up to three.", errors["F2"]);
        Assert.Empty(accepted);
    }

    [Fact]
    public void A_pick_up_to_three_question_accepts_three()
    {
        var (accepted, errors) = Validate(Organisers, "F2", "Word of mouth", "Facebook", "Club website");
        Assert.Empty(errors);
        Assert.Equal(3, accepted["F2"].Count);
    }

    [Fact]
    public void An_exclusive_option_cannot_be_combined()
    {
        var (_, errors) = Validate(Organisers, "L1", "Neither", "We enter league teams");
        Assert.True(errors.ContainsKey("L1"));
        Assert.Empty(Validate(Organisers, "L1", "Neither").Errors);
    }

    [Fact]
    public void A_choice_must_be_one_of_the_options()
    {
        Assert.True(Validate(Organisers, "S1", "Narnia").Errors.ContainsKey("S1"));
        Assert.True(Validate(Organisers, "N2", "Round robin", "Made up").Errors.ContainsKey("N2"));
        Assert.True(Validate(Organisers, "S1", "UK", "Ireland").Errors.ContainsKey("S1"));
    }

    [Fact]
    public void A_required_question_must_be_answered_and_an_optional_one_may_be_blank()
    {
        Assert.True(Validate(Organisers, "S1").Errors.ContainsKey("S1"));
        var (accepted, errors) = Validate(Organisers, "Z1");
        Assert.Empty(errors);
        Assert.Empty(accepted);
    }

    [Theory]
    [InlineData("5", "5")]
    [InlineData("4.50", "4.50")]
    [InlineData("£6", "6")]
    [InlineData("€ 7.5", "7.5")]
    [InlineData("4,50", "4.50")]
    [InlineData("4,5", "4.5")]
    public void Number_accepts_a_price(string input, string stored) =>
        Assert.Equal(stored, Validate(Organisers, "C2", input).Accepted["C2"][0]);

    [Theory]
    [InlineData("five")]
    [InlineData("-3")]
    [InlineData("99999")]
    [InlineData("1,000")]
    [InlineData("1,000.50")]
    [InlineData("4,5,0")]
    [InlineData("4.")]
    public void Number_refuses_other_input_with_a_friendly_message(string input) =>
        Assert.Equal("Please enter a number, like 5 or 4.50.", Validate(Organisers, "C2", input).Errors["C2"]);

    // --- Rate-limit keys: the client address is grouped, never stored.

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    [InlineData("2001:db8:1:2:aaaa:bbbb:cccc:dddd", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2::1", "2001:db8:1:2::/64")]
    public void Limiter_key_is_the_address_or_its_IPv6_64(string address, string key) =>
        Assert.Equal(key, SurveyRateLimiter.ClientKey(System.Net.IPAddress.Parse(address)));

    [Fact]
    public void Limiter_charges_a_permit_once_per_admission()
    {
        var clock = new ManualClock();
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Survey:RateLimit:PermitLimit"] = "2" }).Build();
        var limiter = new SurveyRateLimiter(clock, config);
        var run = Guid.NewGuid();
        for (var i = 0; i < 5; i++) Assert.True(limiter.TryAdmit("k", run));
        Assert.True(limiter.TryAdmit("k", Guid.NewGuid()));
        Assert.False(limiter.TryAdmit("k", Guid.NewGuid()));
        Assert.True(limiter.TryAdmit("k", run)); // already admitted, still free
        clock.Advance(TimeSpan.FromMinutes(61));
        Assert.True(limiter.TryAdmit("k", Guid.NewGuid()));
    }

    [Fact]
    public void Limiter_default_is_forty_an_hour()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        Assert.Equal(40, new SurveyRateLimiter(new ManualClock(), config).PermitLimit);
    }

    [Fact]
    public void Stored_submit_time_is_rounded_down_to_the_hour() =>
        Assert.Equal(new DateTimeOffset(2026, 11, 9, 18, 0, 0, TimeSpan.Zero),
            SurveySubmissionService.StoredTime(new DateTimeOffset(2026, 11, 9, 18, 59, 59, TimeSpan.Zero)));

    [Fact]
    public void Free_text_has_a_length_limit()
    {
        Assert.True(Validate(Organisers, "A3", new string('x', SurveyEngine.TextMax + 1)).Errors.ContainsKey("A3"));
        Assert.Empty(Validate(Organisers, "A3", new string('x', SurveyEngine.TextMax)).Errors);
    }

    [Theory]
    [InlineData("  Someone@Example.com ", "someone@example.com")]
    [InlineData("not-an-email", null)]
    [InlineData("a@b", null)]
    [InlineData("Name <a@b.com>", null)]
    public void Email_is_normalised_or_refused(string input, string? expected) =>
        Assert.Equal(expected, SurveyEngine.NormalizeEmail(input));
}
