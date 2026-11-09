using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace PegboardWebSite.Survey;

/// <summary>
/// Everything a respondent has done so far, carried between steps in one hidden field.
/// It is protected with ASP.NET Data Protection, so it can be neither read nor altered by the
/// browser: that is what makes the opening-page timestamp trustworthy for the minimum-time
/// rule, and what stops a posted answer to an unshown question from ever entering it.
/// </summary>
public sealed class SurveyState
{
    public string SurveyId { get; set; } = "";
    public long StartedUtcTicks { get; set; }

    /// <summary>Id of the first question on the step being shown; null while the opening text is shown.</summary>
    public string? Current { get; set; }

    public Dictionary<string, List<string>> Answers { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Issued on the after-submission page so the tester form can't be posted cold.</summary>
public sealed class TesterTicket
{
    public string SurveyId { get; set; } = "";
    public long IssuedUtcTicks { get; set; }
}

public sealed class SurveyTokens
{
    private readonly IDataProtector _state;
    private readonly IDataProtector _ticket;

    public SurveyTokens(IDataProtectionProvider provider)
    {
        _state = provider.CreateProtector("PegboardWebSite.Survey.State.v1");
        _ticket = provider.CreateProtector("PegboardWebSite.Survey.TesterTicket.v1");
    }

    public string Protect(SurveyState state) => _state.Protect(JsonSerializer.Serialize(state));
    public string Protect(TesterTicket ticket) => _ticket.Protect(JsonSerializer.Serialize(ticket));

    /// <summary>Null when the token is missing, tampered, or protected with another key.</summary>
    public SurveyState? UnprotectState(string? token) => Unprotect<SurveyState>(_state, token);
    public TesterTicket? UnprotectTicket(string? token) => Unprotect<TesterTicket>(_ticket, token);

    private static T? Unprotect<T>(IDataProtector protector, string? token) where T : class
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(protector.Unprotect(token));
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}
