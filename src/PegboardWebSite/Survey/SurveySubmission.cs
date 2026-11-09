namespace PegboardWebSite.Survey;

public enum SubmitOutcome
{
    Stored,
    /// <summary>An abuse rule fired. The respondent sees the normal thank-you; nothing is stored.</summary>
    Blocked,
    /// <summary>An end page applies (e.g. under 16). Nothing is stored.</summary>
    Ended,
    /// <summary>The database refused the write. The respondent is told and can retry.</summary>
    SaveFailed,
}

public static class BlockRules
{
    public const string Honeypot = "honeypot";
    public const string MissingOrTamperedToken = "missing-or-tampered-token";
    public const string TooFast = "too-fast";
    public const string RateLimit = "rate-limit";
}

/// <summary>
/// The final-submit rules (ado:2566): minimum time from the protected opening timestamp,
/// per-IP rate limit, end pages, dropping hidden answers, then the write. Honeypot and token
/// checks happen on every step, in the page, through <see cref="LogBlocked"/>.
/// </summary>
public sealed class SurveySubmissionService
{
    private readonly ISurveyStore _store;
    private readonly SurveyRateLimiter _limiter;
    private readonly TimeProvider _clock;
    private readonly ILogger<SurveySubmissionService> _logger;

    public SurveySubmissionService(ISurveyStore store, SurveyRateLimiter limiter, TimeProvider clock,
        ILogger<SurveySubmissionService> logger)
    {
        _store = store;
        _limiter = limiter;
        _clock = clock;
        _logger = logger;
    }

    public DateTimeOffset Now => _clock.GetUtcNow();

    /// <summary>Logs a blocked submission by rule. The client IP is never logged.</summary>
    public void LogBlocked(string surveyId, string rule) =>
        _logger.LogWarning("Survey {SurveyId} submission blocked by rule {Rule}; not stored.", surveyId, rule);

    public async Task<SubmitOutcome> SubmitAsync(SurveyDefinition def, SurveyState state, string clientIp, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var started = new DateTimeOffset(state.StartedUtcTicks, TimeSpan.Zero);
        if (state.StartedUtcTicks <= 0 || now - started < TimeSpan.FromSeconds(def.MinSecondsToComplete))
        {
            LogBlocked(def.Id, BlockRules.TooFast);
            return SubmitOutcome.Blocked;
        }

        if (!_limiter.TryAcquire("response:" + clientIp))
        {
            LogBlocked(def.Id, BlockRules.RateLimit);
            return SubmitOutcome.Blocked;
        }

        if (SurveyEngine.MatchEndPage(def, state.Answers) is not null)
            return SubmitOutcome.Ended;

        var normalized = SurveyEngine.Normalize(def, state.Answers);
        var record = new SurveyResponseRecord(Guid.NewGuid(), def.Id, def.Version, now, normalized.Answers, normalized.Path);
        try
        {
            await _store.SaveResponseAsync(record, ct);
            return SubmitOutcome.Stored;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Survey {SurveyId} response could not be saved.", def.Id);
            return SubmitOutcome.SaveFailed;
        }
    }

    /// <summary>
    /// Adds an email to the tester list. Blank stores nothing. The row carries the survey it
    /// came from and the day, never a response id.
    /// </summary>
    public async Task<SubmitOutcome> AddTesterAsync(string surveyId, string email, string clientIp, CancellationToken ct = default)
    {
        if (!_limiter.TryAcquire("tester:" + clientIp))
        {
            LogBlocked(surveyId, BlockRules.RateLimit);
            return SubmitOutcome.Blocked;
        }

        var now = _clock.GetUtcNow();
        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        try
        {
            await _store.SaveTesterAsync(new SurveyTesterRecord(Guid.NewGuid(), email, surveyId, day), ct);
            return SubmitOutcome.Stored;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Survey {SurveyId} tester signup could not be saved.", surveyId);
            return SubmitOutcome.SaveFailed;
        }
    }
}
