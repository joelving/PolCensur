using NodaTime;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemokratiskDialog.Models
{
    public class CheckBlockedJob
    {
        public Guid Id { get; set; }

        public Instant LastUpdate { get; set; }

        public CheckBlockedJobState State { get; set; }

        [NotMapped]
        public string Email { get; set; }

        public string CheckingForUserId { get; set; }

        public string CheckingForTwitterId { get; set; }

        public string CheckingForScreenName { get; set; }

        [NotMapped]
        public string AccessToken { get; set; }
        [NotMapped]
        public string AccessTokenSecret { get; set; }

        public enum CheckBlockedJobState
        {
            Pending,
            Running,
            Completed,
            Failed
        }
    }
}
