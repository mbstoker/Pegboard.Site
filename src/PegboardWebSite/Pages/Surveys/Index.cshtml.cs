using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PegboardWebSite.Survey;

namespace PegboardWebSite.Pages.Surveys;

/// <summary>
/// /survey/{id} (ado:2566): opening text, then the steps, then a redirect to the
/// after-submission page. All state between steps rides in one Data-Protection token.
/// </summary>
public class IndexModel : PageModel
{
    public const string StateField = "state";
    public const string HoneypotField = "hp_extra";
    public const string FieldPrefix = "q_";

    private readonly SurveyCatalog _catalog;
    private readonly SurveyTokens _tokens;
    private readonly SurveySubmissionService _submissions;

    public IndexModel(SurveyCatalog catalog, SurveyTokens tokens, SurveySubmissionService submissions)
    {
        _catalog = catalog;
        _tokens = tokens;
        _submissions = submissions;
    }

    public enum View { Opening, Step, EndPage }

    public SurveyDefinition Definition { get; private set; } = null!;
    public View Mode { get; private set; }
    public IReadOnlyList<SurveyQuestion> Questions { get; private set; } = Array.Empty<SurveyQuestion>();
    public string? Header { get; private set; }
    public int ProgressPercent { get; private set; }
    public string StateToken { get; private set; } = "";
    public string? EndText { get; private set; }
    public bool SaveFailed { get; private set; }
    public Dictionary<string, string> Errors { get; private set; } = new();
    private Dictionary<string, List<string>> _values = new();

    public IReadOnlyList<string> ValuesFor(string questionId) =>
        _values.TryGetValue(questionId, out var v) ? v : Array.Empty<string>();

    public IActionResult OnGet(string id)
    {
        var def = _catalog.Get(id);
        if (def is null) return NotFound();
        Definition = def;
        Mode = View.Opening;
        StateToken = _tokens.Protect(new SurveyState
        {
            SurveyId = def.Id,
            StartedUtcTicks = _submissions.Now.UtcTicks,
        });
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var def = _catalog.Get(id);
        if (def is null) return NotFound();
        Definition = def;

        var state = _tokens.UnprotectState(Request.Form[StateField]);
        if (state is null || state.SurveyId != def.Id)
            return Blocked(def, BlockRules.MissingOrTamperedToken);
        if (!string.IsNullOrEmpty(Request.Form[HoneypotField]))
            return Blocked(def, BlockRules.Honeypot);

        int nextIndex;
        if (state.Current is null)
        {
            // Posted from the opening text: show the first step.
            nextIndex = 0;
        }
        else
        {
            var start = Math.Max(0, def.IndexOf(state.Current));
            var step = SurveyEngine.BuildStep(def, state.Answers, start);
            var (accepted, errors) = SurveyEngine.ValidateStep(step.Questions,
                qid => Request.Form[FieldPrefix + qid].Select(v => v ?? "").ToList());

            if (errors.Count > 0)
            {
                Errors = errors;
                _values = step.Questions.ToDictionary(q => q.Id,
                    q => Request.Form[FieldPrefix + q.Id].Select(v => v ?? "").ToList());
                return ShowStep(def, state, step);
            }

            // Replace this step's answers: a question left blank this time is cleared.
            foreach (var q in step.Questions) state.Answers.Remove(q.Id);
            foreach (var (qid, v) in accepted) state.Answers[qid] = v;

            var end = SurveyEngine.MatchEndPage(def, state.Answers);
            if (end is not null)
            {
                Mode = View.EndPage;
                EndText = end.Text;
                return Page();
            }
            nextIndex = step.NextIndex;
        }

        var next = SurveyEngine.BuildStep(def, state.Answers, nextIndex);
        if (!next.IsEmpty)
            return ShowStep(def, state, next);

        var outcome = await _submissions.SubmitAsync(def, state, RequestHelper.GetClientIp(HttpContext), HttpContext.RequestAborted);
        switch (outcome)
        {
            case SubmitOutcome.Ended:
                Mode = View.EndPage;
                EndText = SurveyEngine.MatchEndPage(def, state.Answers)?.Text;
                return Page();
            case SubmitOutcome.SaveFailed:
                // Keep everything and show the last step again with a retry message.
                SaveFailed = true;
                var lastStart = LastStepStart(def, state);
                var last = SurveyEngine.BuildStep(def, state.Answers, lastStart);
                _values = state.Answers;
                return ShowStep(def, state, last);
            default:
                return Thanks(def);
        }
    }

    private IActionResult Blocked(SurveyDefinition def, string rule)
    {
        _submissions.LogBlocked(def.Id, rule);
        return Thanks(def);
    }

    private IActionResult Thanks(SurveyDefinition def)
    {
        var ticket = _tokens.Protect(new TesterTicket { SurveyId = def.Id, IssuedUtcTicks = _submissions.Now.UtcTicks });
        return Redirect($"/survey/{def.Id}/thanks?k={Uri.EscapeDataString(ticket)}");
    }

    private IActionResult ShowStep(SurveyDefinition def, SurveyState state, SurveyStep step)
    {
        Mode = View.Step;
        Questions = step.Questions;
        Header = step.Questions[0].Header;
        ProgressPercent = SurveyEngine.Progress(def, step.StartIndex);
        state.Current = step.Questions[0].Id;
        if (_values.Count == 0) _values = state.Answers;
        StateToken = _tokens.Protect(state);
        return Page();
    }

    /// <summary>Start index of the final step, found by walking the steps from the beginning.</summary>
    private static int LastStepStart(SurveyDefinition def, SurveyState state)
    {
        var start = 0;
        var i = 0;
        while (true)
        {
            var step = SurveyEngine.BuildStep(def, state.Answers, i);
            if (step.IsEmpty) return start;
            start = step.StartIndex;
            i = step.NextIndex;
        }
    }
}
