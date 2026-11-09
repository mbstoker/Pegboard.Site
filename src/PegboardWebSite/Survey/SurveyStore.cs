using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace PegboardWebSite.Survey;

/// <summary>A stored response. Deliberately has no name, email or IP field.</summary>
public sealed record SurveyResponseRecord(
    Guid Id,
    string SurveyId,
    int DefinitionVersion,
    DateTimeOffset SubmittedUtc,
    Dictionary<string, List<string>> Answers,
    List<string> Path);

/// <summary>A tester-list entry. Deliberately has no response id; see <see cref="NpgsqlSurveyStore"/>.</summary>
public sealed record SurveyTesterRecord(Guid Id, string Email, string SurveyId, DateTimeOffset CreatedUtc);

/// <summary>Survey persistence. Saves throw on failure so the page can say so instead of thanking falsely.</summary>
public interface ISurveyStore
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task SaveResponseAsync(SurveyResponseRecord response, CancellationToken ct = default);
    Task SaveTesterAsync(SurveyTesterRecord tester, CancellationToken ct = default);
    Task<IReadOnlyList<SurveyResponseRecord>> ListResponsesAsync(string surveyId, CancellationToken ct = default);
}

/// <summary>
/// The two site-owned survey tables in PegboardDb (deploy/sql/survey.sql). Ids are random
/// UUIDs rather than sequences, and a tester row's time is kept to the day, so neither the
/// order nor the time of a tester row lines it up with a response.
/// </summary>
public sealed class NpgsqlSurveyStore : ISurveyStore
{
    // Mirrors deploy/sql/survey.sql; keep the two in step.
    internal const string Ddl =
        "CREATE TABLE IF NOT EXISTS survey_responses (" +
        "  id                 UUID        PRIMARY KEY," +
        "  survey_id          TEXT        NOT NULL," +
        "  definition_version INTEGER     NOT NULL," +
        "  submitted_utc      TIMESTAMPTZ NOT NULL," +
        "  answers            JSONB       NOT NULL," +
        "  path               JSONB       NOT NULL);" +
        "CREATE INDEX IF NOT EXISTS ix_survey_responses_survey ON survey_responses (survey_id, submitted_utc);" +
        "CREATE TABLE IF NOT EXISTS survey_testers (" +
        "  id          UUID        PRIMARY KEY," +
        "  email       TEXT        NOT NULL," +
        "  survey_id   TEXT        NOT NULL," +
        "  created_utc TIMESTAMPTZ NOT NULL);" +
        "CREATE UNIQUE INDEX IF NOT EXISTS ux_survey_testers_email_survey ON survey_testers (email, survey_id);";

    private readonly IConfiguration _config;

    public NpgsqlSurveyStore(IConfiguration config) => _config = config;

    private NpgsqlConnection Open()
    {
        var cs = _config.GetConnectionString("PegboardDb");
        if (string.IsNullOrWhiteSpace(cs)) throw new InvalidOperationException("PegboardDb is not configured.");
        var connection = new NpgsqlConnection(cs);
        connection.Open();
        return connection;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        await using var connection = Open();
        await using var cmd = new NpgsqlCommand(Ddl, connection);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveResponseAsync(SurveyResponseRecord r, CancellationToken ct = default)
    {
        await using var connection = Open();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO survey_responses (id, survey_id, definition_version, submitted_utc, answers, path) " +
            "VALUES (@id, @survey, @version, @submitted, @answers, @path)", connection);
        cmd.Parameters.AddWithValue("@id", r.Id);
        cmd.Parameters.AddWithValue("@survey", r.SurveyId);
        cmd.Parameters.AddWithValue("@version", r.DefinitionVersion);
        cmd.Parameters.AddWithValue("@submitted", r.SubmittedUtc.UtcDateTime);
        cmd.Parameters.Add(new NpgsqlParameter("@answers", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(r.Answers) });
        cmd.Parameters.Add(new NpgsqlParameter("@path", NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(r.Path) });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveTesterAsync(SurveyTesterRecord t, CancellationToken ct = default)
    {
        await using var connection = Open();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO survey_testers (id, email, survey_id, created_utc) VALUES (@id, @email, @survey, @created) " +
            "ON CONFLICT (email, survey_id) DO NOTHING", connection);
        cmd.Parameters.AddWithValue("@id", t.Id);
        cmd.Parameters.AddWithValue("@email", t.Email);
        cmd.Parameters.AddWithValue("@survey", t.SurveyId);
        cmd.Parameters.AddWithValue("@created", t.CreatedUtc.UtcDateTime);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<SurveyResponseRecord>> ListResponsesAsync(string surveyId, CancellationToken ct = default)
    {
        var list = new List<SurveyResponseRecord>();
        await using var connection = Open();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, survey_id, definition_version, submitted_utc, answers::text, path::text " +
            "FROM survey_responses WHERE survey_id = @survey ORDER BY submitted_utc", connection);
        cmd.Parameters.AddWithValue("@survey", surveyId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new SurveyResponseRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(3), DateTimeKind.Utc)),
                JsonSerializer.Deserialize<Dictionary<string, List<string>>>(reader.GetString(4)) ?? new(),
                JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? new()));
        }
        return list;
    }
}

/// <summary>Creates the survey tables if missing, once at startup, without delaying it. Failure is logged only.</summary>
public sealed class SurveySchemaBootstrap : BackgroundService
{
    private readonly ISurveyStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<SurveySchemaBootstrap> _logger;

    public SurveySchemaBootstrap(ISurveyStore store, IConfiguration config, ILogger<SurveySchemaBootstrap> logger)
    {
        _store = store;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        if (string.IsNullOrWhiteSpace(_config.GetConnectionString("PegboardDb")))
        {
            _logger.LogInformation("PegboardDb not configured; survey tables not checked.");
            return;
        }
        try
        {
            await _store.EnsureSchemaAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Survey schema check failed; submissions will show an error until the database is reachable.");
        }
    }
}
