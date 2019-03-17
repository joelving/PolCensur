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
            var latestContinuousJob = await context.GetLatestContinuousJobByUserId(userId);
            if (latestJob is null && latestContinuousJob is null)
                return true;

            if (latestJob is null || (!(latestContinuousJob is null) && latestContinuousJob.LastUpdate > latestJob.LastUpdate))
            {
                if (latestContinuousJob.State == ContinuousCheckBlockedJob.JobState.Completed || latestContinuousJob.State == ContinuousCheckBlockedJob.JobState.Failed)
                    return true;

                // Prevent stalled jobs from blocking.
                if (latestContinuousJob.IsStale(clock))
                    return true;
            }
            else
            {
                if (latestJob.State == CheckBlockedJob.CheckBlockedJobState.Completed || latestJob.State == CheckBlockedJob.CheckBlockedJobState.Failed)
                    return true;

                // Prevent stalled jobs from blocking.
                if (latestJob.IsStale(clock))
                    return true;
            }

            return false;
        }

        public static async Task<CheckBlockedJob> GetLatestJobByUserId(this ApplicationDbContext context, string userId)
            => await context.Jobs.Where(j => j.CheckingForUserId == userId).OrderByDescending(j => j.LastUpdate).FirstOrDefaultAsync();

        public static async Task<ContinuousCheckBlockedJob> GetLatestContinuousJobByUserId(this ApplicationDbContext context, string userId)
            => await context.ContinuousJobs.Where(j => j.CheckingForUserId == userId).OrderByDescending(j => j.LastUpdate).FirstOrDefaultAsync();
    }
}
