using NodaTime;

namespace DemokratiskDialog.Models
{
    public class ExceptionLog
    {
        public long Id { get; set; }
        public string RequestIdentifier { get; set; }
        public string Route { get; set; }
        public string ErrorMessage { get; set; }
        public string StackTrace { get; set; }
        public string ExceptionType { get; set; }
        public Instant Timestamp { get; set; }
        public string Data { get; set; }
    }
}
