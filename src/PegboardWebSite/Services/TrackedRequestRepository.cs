using Npgsql;

namespace PegboardWebSite.Services
{
    /// <summary>
    /// Visit / email-open / download tracking, persisted to PostgreSQL (VPS).
    /// Table: tracked_requests (see deploy/sql/tracked_requests.sql).
    /// Inserts are best-effort: a tracking-DB failure must never surface as a 5xx on
    /// the page the visitor is loading (an unguarded insert against an unreachable DB
    /// took the marketing site down on 2026-05-31).
    /// </summary>
    public class TrackedRequestRepository
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TrackedRequestRepository> _logger;

        public TrackedRequestRepository(IConfiguration config, ILogger<TrackedRequestRepository> logger)
        {
            _config = config;
            _logger = logger;
        }

        private string ConnectionString => _config.GetConnectionString("PegboardDb")!;

        public void Insert(TrackedRequestModel request)
        {
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(
                    "INSERT INTO tracked_requests (tracker_id, request_time, requested_resource, source_ip) " +
                    "VALUES (@trackingId, @timestamp, @requestedResource, @sourceIp)",
                    connection);
                cmd.Parameters.AddWithValue("@trackingId", (object?)request.TrackingId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@timestamp", request.Timestamp);
                cmd.Parameters.AddWithValue("@requestedResource", (object?)request.RequestedResource ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@sourceIp", (object?)request.SourceIP ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // Swallow: tracking is non-critical. Log and move on so the request still serves.
                _logger.LogWarning(ex, "TrackedRequest insert failed (resource={Resource}, trackingId={TrackingId})",
                    request.RequestedResource, request.TrackingId);
            }
        }

        public List<TrackedRequestModel> GetAll(DateTime? minTime = null)
        {
            var results = new List<TrackedRequestModel>();
            try
            {
                using var connection = new NpgsqlConnection(ConnectionString);
                connection.Open();

                using var cmd = new NpgsqlCommand(
                    "SELECT id, tracker_id, request_time, requested_resource, source_ip FROM tracked_requests " +
                    "WHERE (@minTime IS NULL OR request_time >= @minTime) ORDER BY request_time DESC",
                    connection);
                cmd.Parameters.AddWithValue("@minTime", (object?)minTime ?? DBNull.Value);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    results.Add(new TrackedRequestModel
                    {
                        Id = reader.GetInt32(0),
                        TrackingId = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Timestamp = reader.GetDateTime(2),
                        RequestedResource = reader.IsDBNull(3) ? null : reader.GetString(3),
                        SourceIP = reader.IsDBNull(4) ? null : reader.GetString(4),
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "TrackedRequest GetAll failed");
            }
            return results;
        }
    }
}
