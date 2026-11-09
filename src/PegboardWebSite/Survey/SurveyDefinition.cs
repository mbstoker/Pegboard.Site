using System.Text.Json.Serialization;

namespace PegboardWebSite.Survey;

/// <summary>
/// A survey as data (ado:2566). Loaded from Survey/Definitions/{id}.json, so wording, options
/// and branching change without a code change. See docs/survey.md for the file format.
/// </summary>
public sealed class SurveyDefinition
{
    public string Id { get; set; } = "";
    public int Version { get; set; }
    public string Title { get; set; } = "";
    public string Opening { get; set; } = "";
    public string AfterSubmission { get; set; } = "";

    /// <summary>The most questions shown on one step.</summary>
    public int QuestionsPerStep { get; set; } = 4;

    /// <summary>A submission completed faster than this (from the opening page) is treated as automated.</summary>
    public int MinSecondsToComplete { get; set; } = 60;

    public List<SurveySection> Sections { get; set; } = new();
    public List<SurveyEndPage> EndPages { get; set; } = new();

    /// <summary>All questions in order, flattened from the sections. Built by <see cref="SurveyCatalog"/>.</summary>
    [JsonIgnore]
    public IReadOnlyList<SurveyQuestion> Questions { get; private set; } = Array.Empty<SurveyQuestion>();

    internal void Flatten()
    {
        var list = new List<SurveyQuestion>();
        foreach (var section in Sections)
        {
            foreach (var q in section.Questions)
            {
                q.Header = section.Header;
                list.Add(q);
            }
        }
        Questions = list;
    }

    public SurveyQuestion? Find(string id) => Questions.FirstOrDefault(q => q.Id == id);

    public int IndexOf(string id)
    {
        for (var i = 0; i < Questions.Count; i++)
            if (Questions[i].Id == id) return i;
        return -1;
    }
}

public sealed class SurveySection
{
    /// <summary>For people editing the file. Not shown to respondents.</summary>
    public string Title { get; set; } = "";

    /// <summary>Line shown at the top of every step in this section, e.g. "Thinking about ...". A change of header starts a new step.</summary>
    public string? Header { get; set; }

    public List<SurveyQuestion> Questions { get; set; } = new();
}

public static class QuestionTypes
{
    public const string Single = "single";
    public const string Multi = "multi";
    public const string Number = "number";
    public const string ShortText = "shortText";
    public const string Text = "text";

    public static readonly string[] All = { Single, Multi, Number, ShortText, Text };
}

public sealed class SurveyQuestion
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = QuestionTypes.Single;
    public string Text { get; set; } = "";
    public string? Hint { get; set; }
    public List<string> Options { get; set; } = new();

    /// <summary>Multi choice only: the most options that can be picked.</summary>
    public int? MaxPicks { get; set; }

    /// <summary>Multi choice only: options that cannot be combined with any other (e.g. "Neither").</summary>
    public List<string> ExclusiveOptions { get; set; } = new();

    public bool Optional { get; set; }
    public SurveyCondition? ShowIf { get; set; }

    [JsonIgnore]
    public string? Header { get; internal set; }

    public bool IsChoice => Type is QuestionTypes.Single or QuestionTypes.Multi;
}

/// <summary>Shown instead of continuing when its condition holds (e.g. under 16). Nothing is stored.</summary>
public sealed class SurveyEndPage
{
    public string Id { get; set; } = "";
    public SurveyCondition When { get; set; } = new();
    public string Text { get; set; } = "";
}

/// <summary>
/// A condition on one earlier answer. Exactly one operator is set. An unanswered question
/// never satisfies a condition, so a question that depends on it stays hidden.
/// </summary>
public sealed class SurveyCondition
{
    public string Question { get; set; } = "";

    /// <summary>Single choice: the answer is this option.</summary>
    [JsonPropertyName("equals")]
    public string? EqualTo { get; set; }

    /// <summary>Single choice: answered, and not this option.</summary>
    [JsonPropertyName("notEquals")]
    public string? NotEqualTo { get; set; }

    /// <summary>Single or multi: an answer is one of these options.</summary>
    public List<string>? AnyOf { get; set; }

    /// <summary>Multi: this option is among the answers.</summary>
    public string? Includes { get; set; }

    /// <summary>Multi: at least this many options were picked.</summary>
    public int? MinCount { get; set; }

    public bool Evaluate(IReadOnlyDictionary<string, List<string>> answers)
    {
        if (!answers.TryGetValue(Question, out var values) || values.Count == 0)
            return false;

        if (EqualTo is not null) return values.Count == 1 && values[0] == EqualTo;
        if (NotEqualTo is not null) return !values.Contains(NotEqualTo);
        if (AnyOf is not null) return values.Any(AnyOf.Contains);
        if (Includes is not null) return values.Contains(Includes);
        if (MinCount is not null) return values.Count >= MinCount.Value;
        return false;
    }

    internal int OperatorCount =>
        (EqualTo is null ? 0 : 1) + (NotEqualTo is null ? 0 : 1) + (AnyOf is null ? 0 : 1)
        + (Includes is null ? 0 : 1) + (MinCount is null ? 0 : 1);

    internal IEnumerable<string> ReferencedOptions()
    {
        if (EqualTo is not null) yield return EqualTo;
        if (NotEqualTo is not null) yield return NotEqualTo;
        if (Includes is not null) yield return Includes;
        if (AnyOf is not null) foreach (var o in AnyOf) yield return o;
    }
}
