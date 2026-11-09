using System.Collections.Concurrent;

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
/// per-network rate limit, end pages, dropping hidden answers, then the write. Honeypot and
/// token checks happen on every step, in the page, through <see cref="LogBlocked"/>.
/// </summary>
public sealed class SurveySubmissionService
{
    private static readonly TimeSpan UsedTicketMemory = TimeSpan.FromHours(25);

    private readonly ISurveyStore _store;
    private readonly SurveyRateLimiter _limiter;
    private readonly TimeProvider _clock;
    private readonly ILogger<SurveySubmissionService> _logger;
    private readonly ConcurrentDictionary<string, long> _blockedCounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _usedTickets = new();

    public SurveySubmissionService(ISurveyStore store, SurveyRateLimiter limiter, TimeProvider clock,
        ILogger<SurveySubmissionService> logger)
    {
        _store = store;
        _limiter = limiter;
        _clock = clock;
        _logger = logger;
    }

    public DateTimeOffset Now => _clock.GetUtcNow();

    /// <summary>Blocked submissions per rule since the site started.</summary>
    public IReadOnlyDictionary<string, long> BlockedCounts => _blockedCounts;

    /// <summary>Logs a blocked submission by rule, with the running count for that rule. The client address is never logged.</summary>
    public void LogBlocked(string surveyId, string rule)
    {
        var count = _blockedCounts.AddOrUpdate(rule, 1, (_, n) => n + 1);
        _logger.LogWarning("Survey {SurveyId} submission blocked by rule {Rule}; not stored. {Count} blocked by this rule since startup.",
            surveyId, rule, count);
    }

    /// <summary>Submit time as stored: rounded down to the hour, so it can't pin down a respondent's visit.</summary>
    public static DateTimeOffset StoredTime(DateTimeOffset now) =>
        new(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);

    public async Task<SubmitOutcome> SubmitAsync(SurveyDefinition def, SurveyState state, string clientKey, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var started = new DateTimeOffset(state.StartedUtcTicks, TimeSpan.Zero);
        if (state.StartedUtcTicks <= 0 || state.RunId == Guid.Empty
            || now - started < TimeSpan.FromSeconds(def.MinSecondsToComplete))
        {
            LogBlocked(def.Id, BlockRules.TooFast);
            return SubmitOutcome.Blocked;
        }

        // Charged once per run: a retry of the same run (outage, lost redirect) is admitted free.
        if (!_limiter.TryAdmit("response:" + clientKey, state.RunId))
        {
            LogBlocked(def.Id, BlockRules.RateLimit);
            return SubmitOutcome.Blocked;
        }

        if (SurveyEngine.MatchEndPage(def, state.Answers) is not null)
            return SubmitOutcome.Ended;

        var normalized = SurveyEngine.Normalize(def, state.Answers);
        // The run id is the row id and the insert is insert-if-absent, so a repeat stores nothing new.
        var record = new SurveyResponseRecord(state.RunId, def.Id, state.Version, StoredTime(now),
            normalized.Answers, normalized.Path);
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
    /// Adds an email to the tester list. The row carries the survey it came from and the day,
    /// never a response id. One ticket stores at most one email; retries with the same ticket
    /// after a failed save are not charged again.
    /// </summary>
    public async Task<SubmitOutcome> AddTesterAsync(TesterTicket ticket, string email, string clientKey, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        foreach (var (id, at) in _usedTickets)
            if (now - at >= UsedTicketMemory) _usedTickets.TryRemove(id, out _);

        if (_usedTickets.ContainsKey(ticket.TicketId))
            return SubmitOutcome.Stored; // Already on the list from this page; nothing more to store.

        if (!_limiter.TryAdmit("tester:" + clientKey, ticket.TicketId))
        {
            LogBlocked(ticket.SurveyId, BlockRules.RateLimit);
            return SubmitOutcome.Blocked;
        }

        var day = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        try
        {
            // The row id is the ticket id: the store's insert-if-absent makes one ticket one row
            // durably, across concurrent posts and restarts. The in-memory set above is a fast path.
            await _store.SaveTesterAsync(new SurveyTesterRecord(ticket.TicketId, email, ticket.SurveyId, day), ct);
            _usedTickets[ticket.TicketId] = now;
            return SubmitOutcome.Stored;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Survey {SurveyId} tester signup could not be saved.", ticket.SurveyId);
            return SubmitOutcome.SaveFailed;
        }
    }
}
