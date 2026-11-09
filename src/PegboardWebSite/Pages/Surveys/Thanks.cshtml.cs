using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PegboardWebSite.Survey;

namespace PegboardWebSite.Pages.Surveys;

/// <summary>
/// /survey/{id}/thanks (ado:2566): the after-submission text and the optional tester email.
/// Every submission lands here, including blocked ones, so a bot can't tell which it was.
/// The tester form needs the ticket issued with this page, so it can't be posted cold.
/// </summary>
public class ThanksModel : PageModel
{
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromHours(24);

    private readonly SurveyCatalog _catalog;
    private readonly SurveyTokens _tokens;
    private readonly SurveySubmissionService _submissions;

    public ThanksModel(SurveyCatalog catalog, SurveyTokens tokens, SurveySubmissionService submissions)
    {
        _catalog = catalog;
        _tokens = tokens;
        _submissions = submissions;
    }

    public enum View { Form, Added, NothingSaved, SaveFailed }

    public SurveyDefinition Definition { get; private set; } = null!;
    public View Mode { get; private set; } = View.Form;
    public string Ticket { get; private set; } = "";
    public string? Email { get; private set; }
    public string? EmailError { get; private set; }

    public IActionResult OnGet(string id, string? k)
    {
        var def = _catalog.Get(id);
        if (def is null) return NotFound();
        Definition = def;
        Ticket = k ?? "";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var def = _catalog.Get(id);
        if (def is null) return NotFound();
        Definition = def;
        Ticket = Request.Form["k"].ToString();
        Email = Request.Form["email"].ToString().Trim();

        if (Email.Length == 0)
        {
            Mode = View.NothingSaved;
            return Page();
        }

        var normalized = SurveyEngine.NormalizeEmail(Email);
        if (normalized is null)
        {
            EmailError = "That doesn't look like an email address. Please check it.";
            return Page();
        }

        var ticket = _tokens.UnprotectTicket(Ticket);
        var ticketAge = ticket is null ? TimeSpan.MaxValue
            : _submissions.Now - new DateTimeOffset(ticket.IssuedUtcTicks, TimeSpan.Zero);
        if (ticket is null || ticket.SurveyId != def.Id || ticket.TicketId == Guid.Empty
            || ticketAge < TimeSpan.Zero || ticketAge > TicketLifetime)
        {
            _submissions.LogBlocked(def.Id, BlockRules.MissingOrTamperedToken);
            Mode = View.Added;
            return Page();
        }
        if (!string.IsNullOrEmpty(Request.Form[IndexModel.HoneypotField]))
        {
            _submissions.LogBlocked(def.Id, BlockRules.Honeypot);
            Mode = View.Added;
            return Page();
        }

        var outcome = await _submissions.AddTesterAsync(ticket, normalized,
            SurveyRateLimiter.ClientKey(HttpContext.Connection.RemoteIpAddress), HttpContext.RequestAborted);
        Mode = outcome == SubmitOutcome.SaveFailed ? View.SaveFailed : View.Added;
        return Page();
    }
}
