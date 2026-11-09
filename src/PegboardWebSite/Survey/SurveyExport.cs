using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PegboardWebSite.Survey;

/// <summary>
/// CSV of a survey's responses for analysis (ado:2566): one row per response, one column per
/// question id. Served at /survey/{id}/export.csv only with the configured Survey:ExportKey in
/// the X-Survey-Key header; with no key configured the endpoint does not exist (404).
/// </summary>
public static class SurveyExport
{
    public const string KeyHeader = "X-Survey-Key";

    public static string ToCsv(SurveyDefinition def, IEnumerable<SurveyResponseRecord> responses)
    {
        var rows = responses.ToList();
        var ids = def.Questions.Select(q => q.Id).ToList();
        // Ids from an earlier definition version that the current one no longer has.
        ids.AddRange(rows.SelectMany(r => r.Answers.Keys).Distinct(StringComparer.Ordinal)
            .Where(k => !ids.Contains(k)).OrderBy(k => k, StringComparer.Ordinal));

        var sb = new StringBuilder();
        var header = new List<string> { "response_id", "submitted_utc", "definition_version" };
        header.AddRange(ids);
        header.Add("path");
        AppendRow(sb, header);

        foreach (var r in rows)
        {
            var cells = new List<string>
            {
                r.Id.ToString(),
                r.SubmittedUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                r.DefinitionVersion.ToString(CultureInfo.InvariantCulture),
            };
            cells.AddRange(ids.Select(id => r.Answers.TryGetValue(id, out var v) ? string.Join("; ", v) : ""));
            cells.Add(string.Join("; ", r.Path));
            AppendRow(sb, cells);
        }
        return sb.ToString();
    }

    private static void AppendRow(StringBuilder sb, IEnumerable<string> cells)
    {
        sb.Append(string.Join(",", cells.Select(Escape)));
        sb.Append("\r\n");
    }

    private static string Escape(string value)
    {
        // Free text is respondent-written: stop a spreadsheet treating it as a formula.
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
            value = "'" + value;
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    /// <summary>Constant-time comparison of the presented key with the configured one.</summary>
    public static bool KeyMatches(string configured, string? presented)
    {
        if (string.IsNullOrEmpty(presented)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(configured));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(presented));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/survey/{id}/export.csv", async (string id, HttpContext ctx, IConfiguration config,
            SurveyCatalog catalog, ISurveyStore store, ILoggerFactory loggers) =>
        {
            var key = config["Survey:ExportKey"];
            if (string.IsNullOrWhiteSpace(key)) return Results.NotFound();
            if (!KeyMatches(key, ctx.Request.Headers[KeyHeader].FirstOrDefault())) return Results.Unauthorized();

            var def = catalog.Get(id);
            if (def is null) return Results.NotFound();

            IReadOnlyList<SurveyResponseRecord> rows;
            try
            {
                rows = await store.ListResponsesAsync(def.Id, ctx.RequestAborted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                loggers.CreateLogger("SurveyExport").LogError(ex, "Survey {SurveyId} export failed.", def.Id);
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            ctx.Response.Headers.CacheControl = "no-store";
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(ToCsv(def, rows))).ToArray(),
                "text/csv; charset=utf-8", $"survey-{def.Id}.csv");
        });
    }
}
