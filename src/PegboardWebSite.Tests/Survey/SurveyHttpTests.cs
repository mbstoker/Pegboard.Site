using System.Net;
using System.Text.Json;
using PegboardWebSite.Survey;
using Xunit;

namespace PegboardWebSite.Tests.Survey;

/// <summary>
/// The survey through HTTP (ado:2566): pages render, a completed survey stores one clean row,
/// every abuse rule blocks storage while still showing the thank-you, the tester list stays
/// separate, and the export needs its key. Each test gets its own host (see SurveyApiFactory).
/// </summary>
public sealed class SurveyHttpTests
{
    private static bool RedirectedToThanks(SurveyBrowser b, string survey) =>
        b.Last!.StatusCode == HttpStatusCode.Redirect
        && b.Last.Headers.Location!.OriginalString.StartsWith($"/survey/{survey}/thanks?k=", StringComparison.Ordinal);

    // --- Rendering.

    [Theory]
    [InlineData("organisers", "I&#x27;m asking organisers how they run their club sessions")]
    [InlineData("players", "I&#x27;m asking players what club nights are like for them")]
    public async Task Survey_renders_its_opening_text_first(string id, string opening)
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        var html = await b.Get($"/survey/{id}");
        Assert.Equal(HttpStatusCode.OK, b.Last!.StatusCode);
        Assert.Contains(opening, html);
        Assert.Empty(b.Questions());
        Assert.Contains("name=\"viewport\"", html);
        Assert.Contains("href=\"/survey/privacy\"", html);
    }

    [Fact]
    public async Task Survey_page_has_no_site_navigation_or_calls_to_action()
    {
        using var f = new SurveyApiFactory();
        var html = await f.Browser().Get("/survey/players");
        Assert.DoesNotContain("<nav", html);
        Assert.DoesNotContain("href=\"/features\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("play.epegboard.com", html);
        Assert.DoesNotContain("cloudflareinsights", html);
        // Only one link on the page: the privacy notice.
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "<a "));
    }

    [Fact]
    public async Task Privacy_notice_renders()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        var html = await b.Get("/survey/privacy");
        Assert.Equal(HttpStatusCode.OK, b.Last!.StatusCode);
        Assert.Contains("Mike Stoker", html);
        Assert.Contains("mike.stoker@epegboard.com", html);
        Assert.Contains("How long it is kept", html);
    }

    [Fact]
    public async Task Unknown_survey_is_404()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Get("/survey/nonesuch");
        Assert.Equal(HttpStatusCode.NotFound, b.Last!.StatusCode);
    }

    [Fact]
    public async Task Header_line_shows_on_session_steps()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Get("/survey/organisers");
        await b.Post(_ => null);
        Assert.DoesNotContain("Thinking about your main regular session", b.Html);
        await b.Post(SurveyBrowser.FirstOption); // S1–S4
        Assert.Contains("Thinking about your main regular session over the past four weeks.", b.Html);
        Assert.Equal(new[] { "N1", "N2" }, b.Questions().Select(q => q.Id));
    }

    // --- Completing.

    [Theory]
    [InlineData("organisers")]
    [InlineData("players")]
    public async Task Completing_stores_one_response_with_version_answers_and_path(string id)
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        var shown = await b.Complete(id, q => q.Id == "Y0" ? new[] { "35–49" } : null);

        Assert.True(RedirectedToThanks(b, id), b.Html);
        var r = Assert.Single(f.Store.Responses);
        Assert.Equal(id, r.SurveyId);
        Assert.Equal(1, r.DefinitionVersion);
        Assert.Equal(f.Clock.Now, r.SubmittedUtc);
        Assert.Equal(shown, r.Path);
        Assert.All(r.Answers.Keys, k => Assert.Contains(k, r.Path));
    }

    [Fact]
    public async Task A_stored_response_has_no_name_email_or_ip()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Complete("players", q => q.Id == "Y0" ? new[] { "25–34" } : null);
        var r = Assert.Single(f.Store.Responses);

        // The record type itself has no such field, by name.
        var fields = typeof(SurveyResponseRecord).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();
        Assert.DoesNotContain(fields, n => n.Contains("email") || n.Contains("ip") || n.Contains("name"));
        // And nothing in what was stored looks like an address.
        var json = JsonSerializer.Serialize(r);
        Assert.DoesNotContain("@", json);
        Assert.DoesNotContain("127.0.0.1", json);
    }

    [Fact]
    public async Task Hidden_question_answers_are_never_stored_even_if_posted()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        // One format only, so N3 is hidden; "No" to wider formats, so N3d is hidden.
        // A forged q_N3 / q_N3d rides along on every post.
        await b.Get("/survey/organisers");
        await b.Post(_ => null);
        f.Clock.Advance(TimeSpan.FromMinutes(5));
        for (var i = 0; i < 50 && b.Last!.StatusCode == HttpStatusCode.OK && b.Questions().Count > 0; i++)
        {
            Assert.DoesNotContain(b.Questions(), q => q.Id is "N3" or "N3d");
            await b.Post(q => q.Id == "N3c" ? new[] { "No, we're happy as we are" } : SurveyBrowser.FirstOption(q),
                form =>
                {
                    form.Add(new("q_N3", "We change format during the night"));
                    form.Add(new("q_N3d", "Other"));
                });
        }

        var r = Assert.Single(f.Store.Responses);
        Assert.False(r.Answers.ContainsKey("N3"));
        Assert.False(r.Answers.ContainsKey("N3d"));
        Assert.DoesNotContain("N3", r.Path);
        Assert.Contains("N3c", r.Path);
    }

    [Fact]
    public async Task A_shown_branch_question_is_stored()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Complete("players", q => q.Id switch
        {
            "Y0" => new[] { "16–24" },
            "G1b" => new[] { "Timed rounds of under 10 minutes" },
            _ => null,
        });
        var r = Assert.Single(f.Store.Responses);
        Assert.Contains("G1c", r.Path);
        Assert.True(r.Answers.ContainsKey("G1c"));
    }

    [Fact]
    public async Task Under_16_ends_the_survey_and_stores_nothing()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        var shown = await b.Complete("players", q => q.Id == "Y0" ? new[] { "Under 16" } : null);

        Assert.Equal(new[] { "Y0" }, shown);
        Assert.Equal(HttpStatusCode.OK, b.Last!.StatusCode);
        Assert.Contains("This survey is for players aged 16 and over.", b.Html);
        Assert.DoesNotContain("name=\"state\"", b.Html); // no way to carry on
        Assert.Empty(f.Store.Responses);
        Assert.Empty(f.Store.Testers);
    }

    [Fact]
    public async Task A_fourth_pick_is_refused_and_the_step_shown_again()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Get("/survey/organisers");
        await b.Post(_ => null);
        while (!b.Questions().Any(q => q.Id == "F2"))
            await b.Post(SurveyBrowser.FirstOption);

        await b.Post(q => q.Id == "F2" ? q.Options.Take(4) : SurveyBrowser.FirstOption(q));
        Assert.Equal(HttpStatusCode.OK, b.Last!.StatusCode);
        Assert.Contains("Please choose up to three.", b.Html);
        Assert.Contains(b.Questions(), q => q.Id == "F2");
        Assert.Contains("data-max-picks=\"3\"", b.Html);
    }

    [Fact]
    public async Task Database_down_shows_an_error_not_a_thank_you_and_can_retry()
    {
        using var f = new SurveyApiFactory();
        f.Store.FailSaves = true;
        var b = f.Browser();
        await b.Complete("organisers");

        Assert.Equal(HttpStatusCode.OK, b.Last!.StatusCode);
        Assert.Contains("couldn't be saved", b.Html);
        Assert.Empty(f.Store.Responses);

        f.Store.FailSaves = false;
        await b.Post(SurveyBrowser.FirstOption);
        Assert.True(RedirectedToThanks(b, "organisers"));
        Assert.Single(f.Store.Responses);
    }

    // --- Abuse rules: not stored, but the respondent still sees the thank-you.

    [Fact]
    public async Task A_filled_honeypot_is_not_stored_and_still_thanks()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Get("/survey/organisers");
        await b.Post(_ => null);
        f.Clock.Advance(TimeSpan.FromMinutes(5));
        await b.Post(SurveyBrowser.FirstOption, form =>
        {
            form.RemoveAll(kv => kv.Key == "hp_extra");
            form.Add(new("hp_extra", "http://spam.example"));
        });
        Assert.True(RedirectedToThanks(b, "organisers"));
        Assert.Empty(f.Store.Responses);
    }

    [Fact]
    public async Task A_submit_faster_than_the_minimum_time_is_not_stored_and_still_thanks()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Complete("organisers", waitFirst: false);
        Assert.True(RedirectedToThanks(b, "organisers"));
        Assert.Empty(f.Store.Responses);
    }

    [Fact]
    public async Task Just_over_the_minimum_time_is_stored()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Get("/survey/players");
        await b.Post(_ => null);
        f.Clock.Advance(TimeSpan.FromSeconds(61));
        while (b.Last!.StatusCode == HttpStatusCode.OK && b.Questions().Count > 0)
            await b.Post(q => q.Id == "Y0" ? new[] { "50–64" } : SurveyBrowser.FirstOption(q));
        Assert.Single(f.Store.Responses);
    }

    [Theory]
    [InlineData("")]
    [InlineData("tampered")]
    public async Task A_missing_or_tampered_token_is_not_stored_and_still_thanks(string token)
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Get("/survey/organisers");
        await b.Post(_ => null);
        f.Clock.Advance(TimeSpan.FromMinutes(5));
        var real = b.Hidden("state");
        await b.Post(SurveyBrowser.FirstOption, form =>
        {
            form.RemoveAll(kv => kv.Key == "state");
            form.Add(new("state", token == "tampered" ? real[..^4] + "AAAA" : ""));
        });
        Assert.True(RedirectedToThanks(b, "organisers"));
        Assert.Empty(f.Store.Responses);
    }

    [Fact]
    public async Task A_token_from_the_other_survey_is_refused()
    {
        using var f = new SurveyApiFactory();
        var b = f.Browser();
        await b.Get("/survey/players");
        var playersToken = b.Hidden("state");
        await b.Get("/survey/organisers");
        await b.Post(_ => null, form =>
        {
            form.RemoveAll(kv => kv.Key == "state");
            form.Add(new("state", playersToken));
        });
        Assert.True(RedirectedToThanks(b, "organisers"));
    }

    [Fact]
    public async Task A_burst_over_the_rate_limit_is_not_stored_and_still_thanks()
    {
        using var f = new SurveyApiFactory(); // PermitLimit 3 per 60 minutes
        for (var i = 0; i < 5; i++)
        {
            var b = f.Browser();
            await b.Complete("players", q => q.Id == "Y0" ? new[] { "35–49" } : null);
            Assert.True(RedirectedToThanks(b, "players"));
        }
        Assert.Equal(3, f.Store.Responses.Count);

        // A new window lets the next one through.
        f.Clock.Advance(TimeSpan.FromMinutes(61));
        await f.Browser().Complete("players", q => q.Id == "Y0" ? new[] { "35–49" } : null);
        Assert.Equal(4, f.Store.Responses.Count);
    }

    // --- Tester list.

    private static async Task<SurveyBrowser> AtThanks(SurveyApiFactory f, string survey = "organisers")
    {
        var b = f.Browser();
        await b.Complete(survey, q => q.Id == "Y0" ? new[] { "35–49" } : null);
        await b.Get(b.Last!.Headers.Location!.OriginalString);
        return b;
    }

    private static Task<string> Volunteer(SurveyBrowser b, string email, string? ticket = null) =>
        b.Post(_ => null, form =>
        {
            form.Add(new("email", email));
            form.Add(new("k", ticket ?? b.Hidden("k")));
        });

    [Fact]
    public async Task Thank_you_page_shows_the_after_submission_text_and_an_unticked_email_field()
    {
        using var f = new SurveyApiFactory();
        var b = await AtThanks(f);
        Assert.Contains("Thanks for taking part. If you&#x27;d like to try ePegboard features in development", b.Html);
        Assert.Contains("type=\"email\"", b.Html);
        Assert.DoesNotContain("checked", b.Html);
        // Nothing pre-filled: Razor drops a null value attribute entirely.
        Assert.DoesNotMatch("name=\"email\"[^>]*value=\"[^\"]+\"", b.Html);
    }

    [Fact]
    public async Task Volunteering_stores_a_tester_row_with_no_link_to_the_response()
    {
        using var f = new SurveyApiFactory();
        var b = await AtThanks(f);
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        await Volunteer(b, " Tester@Example.com ");

        Assert.Contains("You're on the tester list", b.Html);
        var t = Assert.Single(f.Store.Testers);
        var r = Assert.Single(f.Store.Responses);
        Assert.Equal("tester@example.com", t.Email);
        Assert.Equal("organisers", t.SurveyId);
        Assert.NotEqual(r.Id, t.Id);
        // Kept to the day, so the time can't line it up with the response.
        Assert.Equal(TimeSpan.Zero, t.CreatedUtc.TimeOfDay);
        // The tester record has no field that could point at a response.
        Assert.Equal(new[] { "CreatedUtc", "Email", "Id", "SurveyId" },
            typeof(SurveyTesterRecord).GetProperties().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        // And the response does not carry the email.
        Assert.DoesNotContain("example.com", JsonSerializer.Serialize(r));
    }

    [Fact]
    public async Task Leaving_the_email_blank_stores_nothing()
    {
        using var f = new SurveyApiFactory();
        var b = await AtThanks(f);
        await Volunteer(b, "   ");
        Assert.Contains("nothing more was saved", b.Html);
        Assert.Empty(f.Store.Testers);
    }

    [Fact]
    public async Task An_invalid_email_is_refused_with_a_message()
    {
        using var f = new SurveyApiFactory();
        var b = await AtThanks(f);
        await Volunteer(b, "not an email");
        Assert.Contains("doesn&#x27;t look like an email address", b.Html);
        Assert.Empty(f.Store.Testers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("forged")]
    public async Task A_tester_post_without_a_valid_ticket_is_not_stored(string ticket)
    {
        using var f = new SurveyApiFactory();
        var b = await AtThanks(f);
        await Volunteer(b, "tester@example.com", ticket);
        Assert.Contains("You're on the tester list", b.Html);
        Assert.Empty(f.Store.Testers);
    }

    [Fact]
    public async Task An_expired_ticket_is_not_stored()
    {
        using var f = new SurveyApiFactory();
        var b = await AtThanks(f);
        f.Clock.Advance(TimeSpan.FromHours(25));
        await Volunteer(b, "tester@example.com");
        Assert.Empty(f.Store.Testers);
    }

    [Fact]
    public async Task A_tester_honeypot_is_not_stored()
    {
        using var f = new SurveyApiFactory();
        var b = await AtThanks(f);
        await b.Post(_ => null, form =>
        {
            form.RemoveAll(kv => kv.Key == "hp_extra");
            form.Add(new("hp_extra", "x"));
            form.Add(new("email", "tester@example.com"));
            form.Add(new("k", b.Hidden("k")));
        });
        Assert.Empty(f.Store.Testers);
    }

    // --- Export.

    [Fact]
    public async Task Export_is_404_when_no_key_is_configured()
    {
        using var f = new SurveyApiFactory();
        var res = await f.CreateClient().GetAsync("/survey/organisers/export.csv");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    [InlineData("test-key-1234x")]
    public async Task Export_refuses_a_missing_or_wrong_key(string? key)
    {
        using var f = new SurveyApiFactory(new() { ["Survey:ExportKey"] = "test-key-1234" });
        var client = f.CreateClient();
        if (key is not null) client.DefaultRequestHeaders.Add(SurveyExport.KeyHeader, key);
        var res = await client.GetAsync("/survey/organisers/export.csv");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Export_with_the_key_has_one_row_per_response_and_one_column_per_question()
    {
        using var f = new SurveyApiFactory(new() { ["Survey:ExportKey"] = "test-key-1234" });
        await f.Browser().Complete("organisers");
        f.Clock.Advance(TimeSpan.FromMinutes(1));
        await f.Browser().Complete("organisers", q => q.Id == "N2" ? q.Options.Take(2) : null);

        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add(SurveyExport.KeyHeader, "test-key-1234");
        var res = await client.GetAsync("/survey/organisers/export.csv");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType?.MediaType);

        var lines = (await res.Content.ReadAsStringAsync()).TrimStart('﻿')
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 responses
        var def = SurveyDefinitionTests.Load("organisers");
        var expectedHeader = new[] { "response_id", "submitted_utc", "definition_version" }
            .Concat(def.Questions.Select(q => q.Id)).Append("path");
        Assert.Equal(string.Join(",", expectedHeader), lines[0]);
        Assert.Contains("Peg board or paddles; Winner stays on", lines[2]);
    }

    [Fact]
    public async Task Export_for_an_unknown_survey_is_404_even_with_the_key()
    {
        using var f = new SurveyApiFactory(new() { ["Survey:ExportKey"] = "test-key-1234" });
        var client = f.CreateClient();
        client.DefaultRequestHeaders.Add(SurveyExport.KeyHeader, "test-key-1234");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/survey/nonesuch/export.csv")).StatusCode);
    }
}
