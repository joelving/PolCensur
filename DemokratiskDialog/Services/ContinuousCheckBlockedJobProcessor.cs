using Microsoft.Extensions.Logging;
using DemokratiskDialog.Models;
using NodaTime;
using System;
using System.Threading;
using System.Threading.Tasks;
using DemokratiskDialog.Data;
using DemokratiskDialog.Exceptions;
using System.Data.SqlClient;

namespace DemokratiskDialog.Services
{
    public class ContinuousCheckBlockedJobProcessor : IBackgroundJobProcessor<ContinuousCheckBlockedJob>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IClock _clock;
        private readonly ILogger<ContinuousCheckBlockedJobProcessor> _logger;
        private readonly BlockService _blockService;
        private readonly EmailService _emailService;

        public ContinuousCheckBlockedJobProcessor(
            ApplicationDbContext dbContext,
            IClock clock,
            ILogger<ContinuousCheckBlockedJobProcessor> logger,
            BlockService blockService,
            EmailService emailService
            )
        {
            _dbContext = dbContext;
            _clock = clock;
            _logger = logger;
            _blockService = blockService;
            _emailService = emailService;
        }

        public async Task ProcessJob((ContinuousCheckBlockedJob job, Action callback) data, CancellationToken cancellationToken)
        {
            var (job, callback) = data;
            _dbContext.ContinuousJobs.Attach(job);
            await MarkRunning(job);

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(job.CancellationToken, cancellationToken))
            {
                try
                {
                    using (var ratelimiter = new RateGate(1, TimeSpan.FromMinutes(10)))
                    {
                        _logger.LogInformation($"Beginning to check blocks for user with local Id {job.CheckingForUserId} and Twitter Id '{job.CheckingForTwitterId}'.");
                        while (true)
                        {
                            linkedCts.Token.ThrowIfCancellationRequested();
                            await ratelimiter.WaitToProceed(linkedCts.Token);

                            var shouldNotify = await _blockService.CheckBlocks(job.CheckingForUserId, job.CheckingForTwitterId, job.AccessToken, job.AccessTokenSecret, linkedCts.Token);

                            job.LastUpdate = _clock.GetCurrentInstant();
                            await _dbContext.SaveChangesAsync(cancellationToken);

                            if (shouldNotify)
                            {
                                var (subject, body) = EmailTemplates.BlocksUpdated(job.CheckingForScreenName, "https://polcensur.dk/profile/blocks");
                                await _emailService.SendEmailAsync(job.Email, subject, body);
                            }
                        }
                    }
                }
                catch (TwitterUnauthorizedException tuex)
                {
                    // We cannot possibly continue. Abort and notify user to reauthorize.
                    _logger.LogError(tuex, $"Authentication error occurred trying to check blocks of '{job.CheckingForUserId}'.");
                    await _dbContext.LogException(job.Id.ToString(), "ContinuousCheckBlockedJob", tuex, _clock, linkedCts.Token);

                    var (subject, body) = EmailTemplates.UnauthorizedError(job.CheckingForScreenName, "https://polcensur.dk/login");
                    await _emailService.SendEmailAsync(job.Email, subject, body);
                    await MarkFailed(job);
                }
                catch (OperationCanceledException cex)
                {
                    if (job.CancellationToken.IsCancellationRequested)
                    {
                        // User requested cancellation.
                        await MarkSuccessful(job);
                    }
                    else
                    {
                        // Server requested cancellation.
                        _logger.LogWarning($"Check blocks of '{job.CheckingForUserId}' was cancelled, most likely due to shutdown.");
                        await MarkPending(job);
                    }
                }
                catch (SqlException)
                {
                    // Do nothing. This may pass.
                }
                catch (Exception ex)
                {
                    await _dbContext.LogException(job.Id.ToString(), "ContinuousCheckBlockedJob", ex, _clock, linkedCts.Token);
                    _logger.LogError(ex, $"Exception occurred trying to check blocks of '{job.CheckingForUserId}'.");
                    var (subject, body) = EmailTemplates.Failed(job.CheckingForScreenName, "https://polcensur.dk");
                    await _emailService.SendEmailAsync(job.Email, subject, body);
                    await MarkFailed(job);
                }
                finally
                {
                    // Release our queue semaphore allowing an additional item to be processed.
                    callback();
                }
            }
        }

        private async Task MarkPending(ContinuousCheckBlockedJob job)
        {
            job.State = ContinuousCheckBlockedJob.JobState.Pending;
            job.LastUpdate = _clock.GetCurrentInstant();
            await _dbContext.SaveChangesAsync();
        }

        private async Task MarkRunning(ContinuousCheckBlockedJob job)
        {
            job.State = ContinuousCheckBlockedJob.JobState.Running;
            job.LastUpdate = _clock.GetCurrentInstant();
            await _dbContext.SaveChangesAsync();
        }

        private async Task MarkSuccessful(ContinuousCheckBlockedJob job)
        {
            job.State = ContinuousCheckBlockedJob.JobState.Completed;
            job.LastUpdate = _clock.GetCurrentInstant();
            job.AccessToken = null;
            job.AccessTokenSecret = null;
            job.Email = null;
            await _dbContext.SaveChangesAsync();
        }

        private async Task MarkFailed(ContinuousCheckBlockedJob job)
        {
            job.State = ContinuousCheckBlockedJob.JobState.Failed;
            job.LastUpdate = _clock.GetCurrentInstant();
            job.AccessToken = null;
            job.AccessTokenSecret = null;
            job.Email = null;
            await _dbContext.SaveChangesAsync();
        }
    }
}
