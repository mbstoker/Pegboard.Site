using System.Text.Json;

namespace PegboardWebSite.Survey;

/// <summary>
/// Loads every Survey/Definitions/*.json under the content root once, validates it, and serves
/// it by id. An invalid definition is logged and left out (its URL 404s) rather than taking
/// the marketing site down; the test suite validates the shipped files so that never ships.
/// </summary>
public sealed class SurveyCatalog
{
    public const string DefinitionsFolder = "Survey/Definitions";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly Dictionary<string, SurveyDefinition> _surveys;

    public SurveyCatalog(IEnumerable<SurveyDefinition> definitions)
    {
        _surveys = definitions.ToDictionary(d => d.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<SurveyDefinition> All => _surveys.Values;

    public SurveyDefinition? Get(string? id) =>
        id is not null && _surveys.TryGetValue(id, out var d) ? d : null;

    public static SurveyCatalog LoadFrom(string directory, ILogger? logger = null)
    {
        var list = new List<SurveyDefinition>();
        if (!Directory.Exists(directory))
        {
            logger?.LogError("Survey definitions folder {Folder} not found; no surveys will be served.", directory);
            return new SurveyCatalog(list);
        }

        foreach (var file in Directory.GetFiles(directory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            try
            {
                var def = Parse(File.ReadAllText(file));
                var errors = Validate(def);
                if (errors.Count > 0)
                {
                    logger?.LogError("Survey definition {File} is invalid and will not be served: {Errors}",
                        Path.GetFileName(file), string.Join("; ", errors));
                    continue;
                }
                list.Add(def);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Survey definition {File} could not be read and will not be served.", Path.GetFileName(file));
            }
        }
        return new SurveyCatalog(list);
    }

    public static SurveyDefinition Parse(string json)
    {
        var def = JsonSerializer.Deserialize<SurveyDefinition>(json, JsonOptions)
                  ?? throw new InvalidDataException("Empty survey definition.");
        def.Flatten();
        return def;
    }

    /// <summary>Every problem with a definition, empty when it is valid.</summary>
    public static List<string> Validate(SurveyDefinition def)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(def.Id) || !def.Id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'))
            errors.Add("id must be lower-case letters, digits or '-'");
        if (def.Version < 1) errors.Add("version must be 1 or more");
        if (string.IsNullOrWhiteSpace(def.Opening)) errors.Add("opening text is missing");
        if (string.IsNullOrWhiteSpace(def.AfterSubmission)) errors.Add("afterSubmission text is missing");
        if (def.QuestionsPerStep < 1) errors.Add("questionsPerStep must be 1 or more");
        if (def.Questions.Count == 0) errors.Add("no questions");

        var seen = new Dictionary<string, SurveyQuestion>(StringComparer.Ordinal);
        foreach (var q in def.Questions)
        {
            var where = $"question {q.Id}";

            // A condition may only look backwards: check it before this question joins `seen`,
            // so a question can't depend on itself or on anything after it.
            if (q.ShowIf is not null)
                ValidateCondition(q.ShowIf, seen, $"{where} showIf", errors);

            if (string.IsNullOrWhiteSpace(q.Id)) errors.Add("a question has no id");
            else if (!seen.TryAdd(q.Id, q)) { errors.Add($"{where}: duplicate id"); continue; }

            if (!QuestionTypes.All.Contains(q.Type)) errors.Add($"{where}: unknown type '{q.Type}'");
            if (string.IsNullOrWhiteSpace(q.Text)) errors.Add($"{where}: no text");

            if (q.IsChoice)
            {
                if (q.Options.Count < 2) errors.Add($"{where}: a choice question needs at least two options");
                if (q.Options.Distinct(StringComparer.Ordinal).Count() != q.Options.Count) errors.Add($"{where}: duplicate options");
            }
            else if (q.Options.Count > 0) errors.Add($"{where}: only choice questions have options");

            if (q.MaxPicks is not null && (q.Type != QuestionTypes.Multi || q.MaxPicks < 1))
                errors.Add($"{where}: maxPicks is only for multi choice and must be 1 or more");
            foreach (var x in q.ExclusiveOptions)
                if (q.Type != QuestionTypes.Multi || !q.Options.Contains(x))
                    errors.Add($"{where}: exclusive option '{x}' is not one of its multi-choice options");
        }

        foreach (var end in def.EndPages)
        {
            if (string.IsNullOrWhiteSpace(end.Text)) errors.Add($"end page {end.Id}: no text");
            ValidateCondition(end.When, seen, $"end page {end.Id}", errors);
        }
        return errors;
    }

    private static void ValidateCondition(SurveyCondition c, IReadOnlyDictionary<string, SurveyQuestion> earlier,
        string where, List<string> errors)
    {
        if (c.OperatorCount != 1)
        {
            errors.Add($"{where}: needs exactly one operator");
            return;
        }
        if (!earlier.TryGetValue(c.Question, out var target))
        {
            errors.Add($"{where}: refers to '{c.Question}', which is not an earlier question");
            return;
        }
        if (!target.IsChoice)
        {
            errors.Add($"{where}: refers to '{c.Question}', which is not a choice question");
            return;
        }
        foreach (var option in c.ReferencedOptions())
            if (!target.Options.Contains(option))
                errors.Add($"{where}: '{option}' is not an option of {c.Question}");
        if ((c.Includes is not null || c.MinCount is not null) && target.Type != QuestionTypes.Multi)
            errors.Add($"{where}: includes/minCount need a multi-choice question");
        if ((c.EqualTo is not null || c.NotEqualTo is not null) && target.Type != QuestionTypes.Single)
            errors.Add($"{where}: equals/notEquals need a single-choice question");
    }
}
