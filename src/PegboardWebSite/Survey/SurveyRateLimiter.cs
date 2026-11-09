using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace PegboardWebSite.Survey;

/// <summary>
/// In-process fixed-window limit per client network on survey submissions (ado:2566; does not
/// wait on the site-wide ado:1393 work). The address is held only in memory for the window and
/// is never stored or logged. A restart clears it, which is acceptable for a survey.
///
/// A permit is charged once per admission (a survey run, or a tester ticket): a respondent
/// retrying the same submission through a database outage is never throttled out of it.
/// </summary>
public sealed class SurveyRateLimiter
{
    public const int DefaultPermitLimit = 40;
    private static readonly TimeSpan AdmissionMemory = TimeSpan.FromHours(25);

    private readonly TimeProvider _clock;
    private readonly int _permits;
    private readonly TimeSpan _window;
    private readonly ConcurrentDictionary<string, Window> _windows = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _admitted = new();
    private long _lastSweepTicks;

    private sealed class Window
    {
        public DateTimeOffset Start;
        public int Count;
    }

    public SurveyRateLimiter(TimeProvider clock, IConfiguration config)
    {
        _clock = clock;
        _permits = int.TryParse(config["Survey:RateLimit:PermitLimit"], out var p) && p > 0 ? p : DefaultPermitLimit;
        _window = TimeSpan.FromMinutes(
            double.TryParse(config["Survey:RateLimit:WindowMinutes"], System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var m) && m > 0 ? m : 60);
    }

    public int PermitLimit => _permits;

    /// <summary>
    /// Admits <paramref name="admissionId"/> for <paramref name="key"/>. An id admitted before
    /// is admitted again without being charged; a new one takes a permit from the window and
    /// is refused once the window's permits are used up.
    /// </summary>
    public bool TryAdmit(string key, Guid admissionId)
    {
        var now = _clock.GetUtcNow();
        Sweep(now);
        if (_admitted.ContainsKey(admissionId)) return true;

        var w = _windows.GetOrAdd(key, _ => new Window { Start = now });
        lock (w)
        {
            if (now - w.Start >= _window)
            {
                w.Start = now;
                w.Count = 0;
            }
            if (w.Count >= _permits) return false;
            w.Count++;
        }
        _admitted[admissionId] = now;
        return true;
    }

    /// <summary>
    /// The limiter key for a client address. IPv6 clients are grouped by their /64, the block a
    /// single household or device is usually given, so rotating within it doesn't reset the limit.
    /// </summary>
    public static string ClientKey(IPAddress? address)
    {
        if (address is null) return "";
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();
        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    private void Sweep(DateTimeOffset now)
    {
        var last = Interlocked.Read(ref _lastSweepTicks);
        if (now.UtcTicks - last < _window.Ticks) return;
        if (Interlocked.CompareExchange(ref _lastSweepTicks, now.UtcTicks, last) != last) return;
        foreach (var (key, w) in _windows)
            if (now - w.Start >= _window) _windows.TryRemove(key, out _);
        foreach (var (id, at) in _admitted)
            if (now - at >= AdmissionMemory) _admitted.TryRemove(id, out _);
    }
}
