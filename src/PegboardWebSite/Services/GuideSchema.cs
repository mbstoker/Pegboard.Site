using System.Collections.Generic;
using System.Text.Json;

namespace PegboardWebSite.Services;

/// <summary>
/// Builds JSON-LD structured data for the /guides articles. Kept in one place so
/// each guide page stays focused on content.
///
/// NOTE (2026-08-23): Faq() is retained for the existing 16 guides only - do NOT
/// call it from new articles. Google stopped showing FAQ rich results entirely on
/// 2026-05-07 (restricted to gov/health in Aug 2023, then withdrawn outright) and
/// removed the documentation in June 2026; HowTo is gone on both platforms. A
/// 1,885-page controlled study found JSON-LD moved AI-engine citations by nothing
/// distinguishable from noise, and Google's own AI-features doc states no markup is
/// needed to appear in them. The markup is left on existing pages because unused
/// structured data is harmless and the study covered only already-cited pages.
/// Article is the type still worth emitting. See Operating/articles.md section 8.
/// </summary>
public static class GuideSchema
{
    private const string Logo = "https://www.epegboard.com/Images/epegboard-text-logo.png";

    public static string Article(string headline, string description, string canonicalUrl, string datePublished)
    {
        var obj = new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Article",
            ["headline"] = headline,
            ["description"] = description,
            ["datePublished"] = datePublished,
            ["dateModified"] = datePublished,
            ["mainEntityOfPage"] = canonicalUrl,
            ["author"] = new Dictionary<string, object> { ["@type"] = "Organization", ["name"] = "ePegboard" },
            ["publisher"] = new Dictionary<string, object>
            {
                ["@type"] = "Organization",
                ["name"] = "ePegboard",
                ["logo"] = new Dictionary<string, object> { ["@type"] = "ImageObject", ["url"] = Logo }
            }
        };
        return JsonSerializer.Serialize(obj);
    }

    public static string Faq(IEnumerable<(string Q, string A)> pairs)
    {
        var items = new List<object>();
        foreach (var (q, a) in pairs)
        {
            items.Add(new Dictionary<string, object>
            {
                ["@type"] = "Question",
                ["name"] = q,
                ["acceptedAnswer"] = new Dictionary<string, object> { ["@type"] = "Answer", ["text"] = a }
            });
        }
        return JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = items
        });
    }
}
