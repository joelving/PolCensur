using NodaTime;
using System;

namespace DemokratiskDialog.Models
{
    public class Block
    {
        public Guid Id { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public string BlockedByTwitterId { get; set; }

        public Instant FirstSeen { get; set; }
        public Instant Checked { get; set; }
    }
}
