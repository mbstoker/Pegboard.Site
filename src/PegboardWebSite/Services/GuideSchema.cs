using System.Collections.Generic;
using System.Text.Json;

namespace PegboardWebSite.Services;

/// <summary>
/// Builds JSON-LD structured data for the /guides articles. Article + FAQPage
/// schema are what search engines and AI answer engines lift, so every guide
/// emits both. Kept in one place so each guide page stays focused on content.
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
