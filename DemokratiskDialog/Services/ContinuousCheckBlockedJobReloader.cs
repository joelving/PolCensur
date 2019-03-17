using DemokratiskDialog.Data;
using DemokratiskDialog.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class ContinuousCheckBlockedJobReloader : BackgroundService
    {
        public IBackgroundQueue<ContinuousCheckBlockedJob> TaskQueue { get; }
        private readonly ILogger _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ContinuousCheckBlockedJobReloader(IBackgroundQueue<ContinuousCheckBlockedJob> taskQueue, ILoggerFactory loggerFactory, IServiceScopeFactory serviceScopeFactory)
        {
            TaskQueue = taskQueue;
            _logger = loggerFactory.CreateLogger<ContinuousCheckBlockedJobReloader>();
            _serviceScopeFactory = serviceScopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var jobs = await _dbContext.ContinuousJobs.Where(j => j.State == ContinuousCheckBlockedJob.JobState.Pending || j.State == ContinuousCheckBlockedJob.JobState.Running).ToListAsync();
                if (!jobs.Any())
                    return;

                var intervalSeconds = 10 * 60 / jobs.Count;
                using (var ratelimiter = new RateGate(1, TimeSpan.FromSeconds(intervalSeconds)))
                {
                    foreach (var job in jobs)
                    {
                        await ratelimiter.WaitToProceed(stoppingToken);
                        await TaskQueue.EnqueueAsync(job);
                    }
                }
            }
        }
    }
}
