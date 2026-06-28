namespace PegboardWebSite.Services;

/// <summary>
/// A currency the marketing site can display prices in, with the full per-tier price set.
///
/// All AUD figures are PROVISIONAL / indicative pending Mike's sign-off — round local
/// price points near a straight FX of the GBP tiers (~1.9x). Confirm before AUD is
/// advertised as a real billing currency. See Strategy/commercial/pricing-decision.md
/// ("Regional/PPP pricing" — parked as "later"). GBP is the canonical billing currency.
/// </summary>
public sealed record PriceSet(
    string Code,
    string Symbol,
    int ProMonth,
    int ProYear,
    int PremiumMonth,
    int PremiumYear,
    int DesktopYear,
    bool Indicative)
{
    public static readonly PriceSet Gbp =
        new("GBP", "£", ProMonth: 8, ProYear: 80, PremiumMonth: 15, PremiumYear: 150, DesktopYear: 60, Indicative: false);

    public static readonly PriceSet Aud =
        new("AUD", "A$", ProMonth: 15, ProYear: 150, PremiumMonth: 29, PremiumYear: 290, DesktopYear: 115, Indicative: true);
}

/// <summary>
/// Resolves which currency to show a visitor, server-side, with no GeoIP dependency.
///
/// Precedence:
///   1. <c>?ccy=aud|gbp</c> query param — explicit choice; persisted as a cookie so it
///      sticks across pages. This is the deterministic path outreach links use.
///   2. The <c>ccy</c> cookie — a returning visitor's last choice.
///   3. <c>Accept-Language</c> containing a <c>-AU</c>/<c>-NZ</c> region tag — the
///      best-effort "auto" for organic Antipodean traffic. Always overridable via the
///      on-page toggle, so a wrong guess is one click to fix.
///   4. Default GBP.
/// </summary>
public static class CurrencyResolver
{
    public const string CookieName = "ccy";
    private const string ItemsKey = "__PriceSet";

    /// <summary>Resolve once per request and cache on HttpContext.Items (idempotent).</summary>
    public static PriceSet Get(HttpContext ctx)
    {
        if (ctx.Items.TryGetValue(ItemsKey, out var cached) && cached is PriceSet ps)
            return ps;

        var resolved = Resolve(ctx);
        ctx.Items[ItemsKey] = resolved;
        return resolved;
    }

    private static PriceSet Resolve(HttpContext ctx)
    {
        // 1. Explicit choice via ?ccy= — persist it so it sticks across pages.
        var q = ctx.Request.Query[CookieName].ToString();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var picked = Match(q);
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.Cookies.Append(CookieName, picked.Code, new CookieOptions
                {
                    MaxAge = TimeSpan.FromDays(365),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = ctx.Request.IsHttps
                });
            }
            return picked;
        }

        // 2. Returning visitor's saved choice.
        if (ctx.Request.Cookies.TryGetValue(CookieName, out var c) && !string.IsNullOrWhiteSpace(c))
            return Match(c);

        // 3. Best-effort auto: Australian / New Zealand browsers default to AUD.
        var lang = ctx.Request.Headers.AcceptLanguage.ToString();
        if (lang.Contains("-AU", StringComparison.OrdinalIgnoreCase) ||
            lang.Contains("-NZ", StringComparison.OrdinalIgnoreCase))
            return PriceSet.Aud;

        // 4. Default.
        return PriceSet.Gbp;
    }

    private static PriceSet Match(string code) => code.Trim().ToUpperInvariant() switch
    {
        "AUD" or "AU" => PriceSet.Aud,
        _ => PriceSet.Gbp
    };
}
