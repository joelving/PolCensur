using NodaTime;
using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Threading;

namespace DemokratiskDialog.Models
{
    public class ContinuousCheckBlockedJob
    {
        public Guid Id { get; set; }

        public Instant LastUpdate { get; set; }

        public JobState State { get; set; }
        
        public string Email { get; set; }

        public string CheckingForUserId { get; set; }

        public string CheckingForTwitterId { get; set; }

        public string CheckingForScreenName { get; set; }
        
        public string AccessToken { get; set; }

        public string AccessTokenSecret { get; set; }

        public enum JobState
        {
            Pending,
            Running,
            Completed,
            Failed
        }

        [NotMapped]
        public bool Terminate { get; set; }

        [NotMapped]
        public CancellationToken CancellationToken { get; set; }

        public bool IsStale(IClock clock)
            => clock.GetCurrentInstant().Minus(LastUpdate) > Duration.FromMinutes(30);
    }
}
