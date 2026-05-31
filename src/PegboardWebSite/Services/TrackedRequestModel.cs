namespace PegboardWebSite.Services
{
    public class TrackedRequestModel
    {
        public int Id { get; set; }
        public string? RequestedResource { get; set; }
        public string? TrackingId { get; set; }
        public DateTime Timestamp { get; set; }
        public string? SourceIP { get; set; }
    }
}
