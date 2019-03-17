using NodaTime;
using System;

namespace DemokratiskDialog.Models
{
    public class ArchivedBlock
    {
        public Guid Id { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public string BlockedByTwitterId { get; set; }

        public Instant FirstSeen { get; set; }
        public Instant Checked { get; set; }
        public Instant VerifiedGone { get; set; }

        public static ArchivedBlock CreateFromBlock(Block block, IClock clock)
            => new ArchivedBlock
            {
                UserId = block.UserId,
                User = block.User,
                BlockedByTwitterId = block.BlockedByTwitterId,

                FirstSeen = block.FirstSeen,
                Checked = block.Checked,
                VerifiedGone = clock.GetCurrentInstant()
            };
    }
}
