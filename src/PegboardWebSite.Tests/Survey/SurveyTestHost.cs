using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PegboardWebSite.Survey;

namespace PegboardWebSite.Tests.Survey;

/// <summary>Clock the tests move by hand, for the minimum-time and rate-limit rules.</summary>
public sealed class ManualClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 9, 18, 30, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>In-memory store. Holds exactly what would be written, so tests assert the store, not the page.</summary>
public sealed class FakeSurveyStore : ISurveyStore
{
    public List<SurveyResponseRecord> Responses { get; } = new();
    public List<SurveyTesterRecord> Testers { get; } = new();
    public bool FailSaves { get; set; }

    public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task SaveResponseAsync(SurveyResponseRecord response, CancellationToken ct = default)
    {
        if (FailSaves) throw new InvalidOperationException("database down");
        // Same contract as the Npgsql store's ON CONFLICT (id) DO NOTHING.
        lock (Responses)
            if (Responses.All(r => r.Id != response.Id)) Responses.Add(response);
        return Task.CompletedTask;
    }

    /// <summary>
    /// When set, each tester save waits here until this many saves have arrived (or 5 seconds
    /// pass), so concurrent requests are all past the in-memory checks before any row is written.
    /// </summary>
    public int? HoldTesterSavesUntil { get; set; }
    private readonly TaskCompletionSource _testerGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _testerArrivals;
    public int TesterSaveArrivals => Volatile.Read(ref _testerArrivals);

    public async Task SaveTesterAsync(SurveyTesterRecord tester, CancellationToken ct = default)
    {
        if (HoldTesterSavesUntil is int hold)
        {
            if (Interlocked.Increment(ref _testerArrivals) >= hold) _testerGate.TrySetResult();
            await Task.WhenAny(_testerGate.Task, Task.Delay(TimeSpan.FromSeconds(5), ct));
        }
        SaveTester(tester);
    }

    private void SaveTester(SurveyTesterRecord tester)
    {
        if (FailSaves) throw new InvalidOperationException("database down");
        // Same contract as the Npgsql store: primary key on id and unique (lower(email), survey_id),
        // both ON CONFLICT DO NOTHING.
        lock (Testers)
        {
            var conflict = Testers.Any(t => t.Id == tester.Id
                || (t.SurveyId == tester.SurveyId
                    && string.Equals(t.Email, tester.Email, StringComparison.OrdinalIgnoreCase)));
            if (!conflict) Testers.Add(tester);
        }
    }

    public Task<IReadOnlyList<SurveyResponseRecord>> ListResponsesAsync(string surveyId, CancellationToken ct = default)
    {
        if (FailSaves) throw new InvalidOperationException("database down");
        return Task.FromResult<IReadOnlyList<SurveyResponseRecord>>(Responses.Where(r => r.SurveyId == surveyId).ToList());
    }
}

/// <summary>
/// Hermetic host for the survey pages: fake store, manual clock, no database. One per test,
/// because the rate limiter is per process and every TestServer request has the same (empty) IP.
/// </summary>
public sealed class SurveyApiFactory : WebApplicationFactory<Program>
{
    public FakeSurveyStore Store { get; } = new();
    public ManualClock Clock { get; } = new();
    private readonly Dictionary<string, string?> _config;

    public SurveyApiFactory(Dictionary<string, string?>? config = null)
    {
        _config = new Dictionary<string, string?>
        {
            ["ConnectionStrings:PegboardDb"] = string.Empty,
            ["Survey:RateLimit:PermitLimit"] = "3",
            ["Survey:RateLimit:WindowMinutes"] = "60",
        };
        if (config is not null)
            foreach (var (k, v) in config) _config[k] = v;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var webProjectDir = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "PegboardWebSite"));
        if (Directory.Exists(webProjectDir)) builder.UseContentRoot(webProjectDir);

        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(_config));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISurveyStore>();
            services.AddSingleton<ISurveyStore>(Store);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    public SurveyBrowser Browser() =>
        new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }), Clock);
}

/// <summary>A question as rendered on a step: its id, input type and option values.</summary>
public sealed record RenderedQuestion(string Id, string InputType, List<string> Options);

/// <summary>Drives the survey like a respondent: reads each step's form and posts answers back.</summary>
public sealed class SurveyBrowser
{
    private readonly HttpClient _client;
    private readonly ManualClock _clock;

    public SurveyBrowser(HttpClient client, ManualClock clock)
    {
        _client = client;
        _clock = clock;
    }

    public HttpClient Client => _client;
    public string Html { get; private set; } = "";
    public HttpResponseMessage? Last { get; private set; }
    public string Path { get; private set; } = "";

    public async Task<string> Get(string path)
    {
        Path = path;
        Last = await _client.GetAsync(path);
        Html = await Last.Content.ReadAsStringAsync();
        return Html;
    }

    public string Hidden(string name)
    {
        var m = Regex.Match(Html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"");
        if (!m.Success) m = Regex.Match(Html, $"value=\"([^\"]*)\"[^>]*name=\"{Regex.Escape(name)}\"");
        return m.Success ? WebUtility.HtmlDecode(m.Groups[1].Value) : "";
    }

    public List<RenderedQuestion> Questions()
    {
        var ids = Regex.Matches(Html, "data-question=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        return ids.Select(id =>
        {
            var inputs = Regex.Matches(Html, $"<(input|textarea)[^>]*name=\"q_{Regex.Escape(id)}\"[^>]*>");
            var type = "text";
            var options = new List<string>();
            foreach (Match input in inputs)
            {
                var tag = input.Value;
                if (input.Groups[1].Value == "textarea") { type = "textarea"; continue; }
                var t = Regex.Match(tag, "type=\"([^\"]+)\"").Groups[1].Value;
                type = t;
                if (t is "radio" or "checkbox")
                    options.Add(WebUtility.HtmlDecode(Regex.Match(tag, "value=\"([^\"]*)\"").Groups[1].Value));
            }
            return new RenderedQuestion(id, type, options);
        }).ToList();
    }

    /// <summary>Posts the current form. Answers come from <paramref name="answer"/>; null means leave it blank.</summary>
    public async Task<string> Post(Func<RenderedQuestion, IEnumerable<string>?> answer,
        Action<List<KeyValuePair<string, string>>>? tweak = null)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", Hidden("__RequestVerificationToken")),
            new("state", Hidden("state")),
            new("hp_extra", ""),
        };
        foreach (var q in Questions())
            foreach (var v in answer(q) ?? Array.Empty<string>())
                form.Add(new("q_" + q.Id, v));
        tweak?.Invoke(form);
        LastForm = form;

        Last = await _client.PostAsync(Path, new FormUrlEncodedContent(form));
        Html = await Last.Content.ReadAsStringAsync();
        return Html;
    }

    /// <summary>The exact form fields last posted.</summary>
    public List<KeyValuePair<string, string>> LastForm { get; private set; } = new();

    /// <summary>Posts the last form again, byte for byte: a double click, a lost redirect or a retry.</summary>
    public async Task<string> Replay()
    {
        Last = await _client.PostAsync(Path, new FormUrlEncodedContent(LastForm));
        Html = await Last.Content.ReadAsStringAsync();
        return Html;
    }

    /// <summary>Default respondent: first option everywhere, a number, a few words of text.</summary>
    public static IEnumerable<string> FirstOption(RenderedQuestion q) => q.InputType switch
    {
        "radio" or "checkbox" => new[] { q.Options[0] },
        "textarea" => new[] { "More courts." },
        _ => q.Id == "C2" ? new[] { "5" } : new[] { "Some words" },
    };

    /// <summary>
    /// Answers every step with <paramref name="answer"/> (falling back to <see cref="FirstOption"/>),
    /// waiting past the minimum time first. Stops at the redirect, an end page, or an error.
    /// </summary>
    public async Task<List<string>> Complete(string surveyId,
        Func<RenderedQuestion, IEnumerable<string>?>? answer = null, bool waitFirst = true)
    {
        var shown = new List<string>();
        await Get($"/survey/{surveyId}");
        await Post(_ => null); // Start
        if (waitFirst) _clock.Advance(TimeSpan.FromMinutes(5));
        for (var guard = 0; guard < 50; guard++)
        {
            if (Last!.StatusCode != HttpStatusCode.OK) return shown;
            var qs = Questions();
            if (qs.Count == 0) return shown;
            shown.AddRange(qs.Select(q => q.Id));
            await Post(q => answer?.Invoke(q) ?? FirstOption(q));
            if (Html.Contains("class=\"survey-error\"") || Html.Contains("class=\"survey-alert\"")) return shown;
        }
        throw new InvalidOperationException("Survey did not finish.");
    }
}
