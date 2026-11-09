using System.Globalization;
using System.Net.Mail;

namespace PegboardWebSite.Survey;

/// <summary>One page of questions, and where the following page starts.</summary>
public sealed record SurveyStep(IReadOnlyList<SurveyQuestion> Questions, int NextIndex, int StartIndex)
{
    public bool IsEmpty => Questions.Count == 0;
}

/// <summary>What is stored for a completed survey: the visible answers and the ids of the questions shown.</summary>
public sealed record NormalizedResponse(Dictionary<string, List<string>> Answers, List<string> Path);

/// <summary>
/// The survey rules, kept free of HTTP so they can be tested directly (ado:2566).
/// Answers are always a list of option texts or a single typed value per question id.
/// </summary>
public static class SurveyEngine
{
    public const int ShortTextMax = 200;
    public const int TextMax = 2000;
    public const decimal NumberMax = 10000m;

    /// <summary>
    /// Builds the step starting at <paramref name="startIndex"/>. Questions whose condition is
    /// false on the answers so far are skipped. A step ends when it is full, when the header
    /// changes, after a question an end page depends on, or before a question whose condition
    /// depends on a question already on this step (its answer is not known yet).
    /// </summary>
    public static SurveyStep BuildStep(SurveyDefinition def, IReadOnlyDictionary<string, List<string>> answers, int startIndex)
    {
        var qs = def.Questions;
        var step = new List<SurveyQuestion>();
        var gateIds = def.EndPages.Select(e => e.When.Question).ToHashSet(StringComparer.Ordinal);
        var firstShown = -1;
        var i = Math.Max(0, startIndex);

        for (; i < qs.Count; i++)
        {
            var q = qs[i];
            if (step.Count > 0 && q.Header != step[0].Header) break;

            if (q.ShowIf is not null)
            {
                if (step.Any(s => s.Id == q.ShowIf.Question)) break;
                if (!q.ShowIf.Evaluate(answers)) continue;
            }

            if (firstShown < 0) firstShown = i;
            step.Add(q);
            if (step.Count >= def.QuestionsPerStep || gateIds.Contains(q.Id)) { i++; break; }
        }

        return new SurveyStep(step, i, firstShown < 0 ? i : firstShown);
    }

    /// <summary>
    /// Validates the values posted for one step. Returns the accepted answers (blank optional
    /// questions are absent) and a message per question that was refused.
    /// </summary>
    public static (Dictionary<string, List<string>> Accepted, Dictionary<string, string> Errors) ValidateStep(
        IEnumerable<SurveyQuestion> step, Func<string, IReadOnlyList<string>> posted)
    {
        var accepted = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var q in step)
        {
            var raw = posted(q.Id).Select(v => (v ?? "").Trim()).Where(v => v.Length > 0).ToList();
            var error = ValidateAnswer(q, raw, out var value);
            if (error is not null) errors[q.Id] = error;
            else if (value is not null) accepted[q.Id] = value;
        }
        return (accepted, errors);
    }

    private static string? ValidateAnswer(SurveyQuestion q, List<string> raw, out List<string>? value)
    {
        value = null;
        if (raw.Count == 0)
            return q.Optional ? null : (q.IsChoice ? "Please choose an answer." : "Please answer this question.");

        switch (q.Type)
        {
            case QuestionTypes.Single:
                if (raw.Count != 1 || !q.Options.Contains(raw[0])) return "Please choose one of the answers.";
                value = raw;
                return null;

            case QuestionTypes.Multi:
                var distinct = raw.Distinct(StringComparer.Ordinal).ToList();
                if (distinct.Any(v => !q.Options.Contains(v))) return "Please choose from the answers shown.";
                if (q.MaxPicks is int max && distinct.Count > max) return $"Please choose up to {Words(max)}.";
                var exclusive = distinct.FirstOrDefault(q.ExclusiveOptions.Contains);
                if (exclusive is not null && distinct.Count > 1) return $"“{exclusive}” can't be combined with other answers.";
                // Keep the definition's order so stored answers and exports are stable.
                value = q.Options.Where(distinct.Contains).ToList();
                return null;

            case QuestionTypes.Number:
                var text = raw[0].TrimStart('£', '€', '$').Replace(",", "");
                if (raw.Count != 1 || !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var n)
                    || n < 0 || n > NumberMax)
                    return "Please enter a number, like 5 or 4.50.";
                value = new List<string> { n.ToString(CultureInfo.InvariantCulture) };
                return null;

            case QuestionTypes.ShortText:
            case QuestionTypes.Text:
                var limit = q.Type == QuestionTypes.ShortText ? ShortTextMax : TextMax;
                if (raw.Count != 1) return "Please answer this question.";
                if (raw[0].Length > limit) return $"Please keep this under {limit} characters.";
                value = raw;
                return null;
        }
        return "Unsupported question.";
    }

    public static string Words(int n) => n switch
    {
        1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five",
        _ => n.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>The end page whose condition holds, if any (e.g. under 16).</summary>
    public static SurveyEndPage? MatchEndPage(SurveyDefinition def, IReadOnlyDictionary<string, List<string>> answers) =>
        def.EndPages.FirstOrDefault(e => e.When.Evaluate(answers));

    /// <summary>
    /// Re-evaluates every condition in order on the final answers. An answer to a question
    /// that is hidden (or unknown to this definition) is dropped, and a hidden question's
    /// dependants are hidden with it. Path is every question that would be shown.
    /// </summary>
    public static NormalizedResponse Normalize(SurveyDefinition def, IReadOnlyDictionary<string, List<string>> answers)
    {
        var kept = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var path = new List<string>();
        foreach (var q in def.Questions)
        {
            if (q.ShowIf is not null && !q.ShowIf.Evaluate(kept)) continue;
            path.Add(q.Id);
            if (answers.TryGetValue(q.Id, out var v) && v.Count > 0) kept[q.Id] = v;
        }
        return new NormalizedResponse(kept, path);
    }

    /// <summary>Percentage through the survey for the progress bar.</summary>
    public static int Progress(SurveyDefinition def, int index) =>
        def.Questions.Count == 0 ? 0 : Math.Clamp((int)Math.Round(100.0 * index / def.Questions.Count), 0, 100);

    /// <summary>Lower-cased, trimmed email, or null when it is not a plausible address.</summary>
    public static string? NormalizeEmail(string? input)
    {
        var e = (input ?? "").Trim();
        if (e.Length is 0 or > 254 || e.Any(char.IsWhiteSpace)) return null;
        try
        {
            var parsed = new MailAddress(e);
            if (parsed.Address != e) return null;
            var at = e.LastIndexOf('@');
            if (at < 1 || !e[(at + 1)..].Contains('.')) return null;
            return e.ToLowerInvariant();
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
