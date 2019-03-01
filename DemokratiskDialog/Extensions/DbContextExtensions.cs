using DemokratiskDialog.Data;
using DemokratiskDialog.Models;
using Microsoft.EntityFrameworkCore;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemokratiskDialog.Extensions
{
    public static class DbContextExtensions
    {
        public static async Task<bool> CanStartNewJobs(this ApplicationDbContext context, IClock clock, string userId)
        {
            var latestJob = await context.GetLatestJobByUserId(userId);
            if (latestJob is null)
                return true;

            if (latestJob.State == CheckBlockedJob.CheckBlockedJobState.Completed || latestJob.State == CheckBlockedJob.CheckBlockedJobState.Failed)
                return true;
            
            // Prevent stalled jobs from blocking.
            if (clock.GetCurrentInstant().Minus(latestJob.LastUpdate) > Duration.FromHours(2))
                return true;

            return false;
        }

        public static async Task<CheckBlockedJob> GetLatestJobByUserId(this ApplicationDbContext context, string userId)
            => await context.Jobs.Where(j => j.CheckingForUserId == userId).OrderByDescending(j => j.LastUpdate).FirstOrDefaultAsync();
    }
}
