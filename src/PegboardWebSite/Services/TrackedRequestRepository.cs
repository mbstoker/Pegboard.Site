using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using MySql.Data.MySqlClient;

namespace PegboardWebSite.Services
{

    public class TrackedRequestRepository
    {
        private readonly IConfiguration _config;
        public TrackedRequestRepository(IConfiguration config)
        {
            _config = config;
        }

        public void Insert(TrackedRequestModel request)
        {
            string connectionString = _config.GetConnectionString("PegboardDb")!;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();

            using var cmd = new MySqlCommand(
                "INSERT INTO imagerequests (TrackerId, RequestTime, RequestedResource, SourceIP) " +
                "VALUES (@trackingId, @timestamp, @requestedResource, @sourceIp)",
                connection);
            cmd.Parameters.AddWithValue("@trackingId", (object?)request.TrackingId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@timestamp", request.Timestamp);
            cmd.Parameters.AddWithValue("@requestedResource", (object?)request.RequestedResource ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@sourceIp", (object?)request.SourceIP ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public List<TrackedRequestModel> GetAll(DateTime? minTime = null)
        {
            string connectionString = _config.GetConnectionString("PegboardDb")!;

            using var connection = new MySqlConnection(connectionString);
            connection.Open();


            using var cmd = new MySqlCommand("SELECT Id, TrackerId, RequestTime, RequestedResource, SourceIP FROM imagerequests", connection);
            List<TrackedRequestModel> requests = new List<TrackedRequestModel>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    TrackedRequestModel request = new TrackedRequestModel();
                    request.Id = reader.GetInt32(0);
                    request.TrackingId = reader.GetString(1);
                    request.Timestamp = reader.GetDateTime(2);
                    if (!reader.IsDBNull(3))
                    {
                        request.RequestedResource = reader.GetString(3);
                    }
                    if (!reader.IsDBNull(4))
                    {
                        request.SourceIP = reader.GetString(4);
                    }
                    requests.Add(request);
                }
            }
            return requests;
        }
    }
}
