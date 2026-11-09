using System.Collections.Concurrent;

namespace PegboardWebSite.Survey;

/// <summary>
/// In-process fixed-window limit per client IP on survey submissions (ado:2566; does not wait
/// on the site-wide ado:1393 work). The IP is held only in memory for the window and is never
/// stored or logged. A restart clears it, which is acceptable for a survey.
/// </summary>
public sealed class SurveyRateLimiter
{
    private readonly TimeProvider _clock;
    private readonly int _permits;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private long _lastSweepTicks;

    private sealed class Window
    {
        public DateTimeOffset Start;
        public int Count;
    }

    public SurveyRateLimiter(TimeProvider clock, IConfiguration config)
    {
        _clock = clock;
        _permits = int.TryParse(config["Survey:RateLimit:PermitLimit"], out var p) && p > 0 ? p : 10;
        _window = TimeSpan.FromMinutes(
            double.TryParse(config["Survey:RateLimit:WindowMinutes"], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var m) && m > 0 ? m : 60);
    }

    public int PermitLimit => _permits;

    /// <summary>Counts one attempt for <paramref name="key"/>; false once the window's limit is used up.</summary>
    public bool TryAcquire(string key)
    {
        var now = _clock.GetUtcNow();
        Sweep(now);
        var w = _windows.GetOrAdd(key, _ => new Window { Start = now });
        lock (w)
        {
            if (now - w.Start >= _window)
            {
                w.Start = now;
                w.Count = 0;
            }
            w.Count++;
            return w.Count <= _permits;
        }
    }

    private void Sweep(DateTimeOffset now)
    {
        var last = Interlocked.Read(ref _lastSweepTicks);
        if (now.UtcTicks - last < _window.Ticks) return;
        if (Interlocked.CompareExchange(ref _lastSweepTicks, now.UtcTicks, last) != last) return;
        foreach (var (key, w) in _windows)
            if (now - w.Start >= _window) _windows.TryRemove(key, out _);
    }
}
