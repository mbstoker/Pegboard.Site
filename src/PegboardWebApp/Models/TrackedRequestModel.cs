using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PegboardWebApp.Models
{
    public class TrackedRequestModel
    {
        public int Id { get; set; }
        public string RequestedResource { get; set; }
        public string TrackingId { get; set; }
        public DateTime Timestamp { get; set; }
        public string SourceIP { get; set; }
    }
}