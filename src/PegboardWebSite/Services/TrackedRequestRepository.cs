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

            var connection = new MySqlConnection(connectionString);
            connection.Open();

            string timestamp = request.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");

            MySqlCommand cmd = new MySqlCommand($"INSERT INTO imagerequests (TrackerId, RequestTime,RequestedResource, SourceIP) values ('{request.TrackingId}', '{timestamp}', '{request.RequestedResource}', '{request.SourceIP}')", connection);
            cmd.ExecuteNonQuery();
        }

        public List<TrackedRequestModel> GetAll(DateTime? minTime = null)
        {
            string connectionString = _config.GetConnectionString("PegboardDb")!;

            var connection = new MySqlConnection(connectionString);
            connection.Open();


            MySqlCommand cmd = new MySqlCommand($"SELECT * FROM imagerequests", connection);
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
