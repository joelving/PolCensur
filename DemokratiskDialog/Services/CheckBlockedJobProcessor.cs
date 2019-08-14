using Microsoft.Extensions.Logging;
using DemokratiskDialog.Models;
using NodaTime;
using System;
using System.Threading;
using System.Threading.Tasks;
using DemokratiskDialog.Data;
using DemokratiskDialog.Exceptions;

namespace DemokratiskDialog.Services
{
    public class CheckBlockedJobProcessor : IBackgroundJobProcessor<CheckBlockedJob>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IClock _clock;
        private readonly ILogger<CheckBlockedJobProcessor> _logger;
        private readonly BlockService _blockService;
        private readonly EmailService _emailService;

        public CheckBlockedJobProcessor(
            ApplicationDbContext dbContext,
            IClock clock,
            ILogger<CheckBlockedJobProcessor> logger,
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

        public async Task ProcessJob((CheckBlockedJob job, Action callback) data, CancellationToken cancellationToken)
        {
            var (job, callback) = data;
            _dbContext.Jobs.Attach(job);
            await MarkRunning(job);
            try
            {
                _logger.LogInformation($"Beginning to check blocks for user with local Id {job.CheckingForUserId} and Twitter Id '{job.CheckingForTwitterId}'.");

                var shouldNotify = await _blockService.CheckBlocks(job.CheckingForUserId, job.CheckingForTwitterId, job.AccessToken, job.AccessTokenSecret, cancellationToken);

                var (subject, body) = EmailTemplates.Completed(job.CheckingForScreenName, "https://polcensur.dk/profile/blocks");
                await _emailService.SendEmailAsync(job.Email, subject, body);
                await MarkSuccessful(job);
            }
            catch (TwitterUnauthorizedException tuex)
            {
                // We cannot possibly continue. Abort and notify user to reauthorize.
                _logger.LogError(tuex, $"Authentication error occurred trying to check blocks of '{job.CheckingForUserId}'.");
                await _dbContext.LogException(job.Id.ToString(), "CheckBlockedJob", tuex, _clock);

                var (subject, body) = EmailTemplates.UnauthorizedError(job.CheckingForScreenName, "https://polcensur.dk/login");
                await _emailService.SendEmailAsync(job.Email, subject, body);
                await MarkFailed(job);
            }
            catch (OperationCanceledException cex)
            {
                await _dbContext.LogException(job.Id.ToString(), "CheckBlockedJob", cex, _clock);
                _logger.LogWarning($"Check blocks of '{job.CheckingForUserId}' was cancelled.");
                await MarkFailed(job);
            }
            catch (Exception ex)
            {
                await _dbContext.LogException(job.Id.ToString(), "CheckBlockedJob", ex, _clock);
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

        private async Task MarkRunning(CheckBlockedJob job)
        {
            job.State = CheckBlockedJob.CheckBlockedJobState.Running;
            job.LastUpdate = _clock.GetCurrentInstant();
            await _dbContext.SaveChangesAsync();
        }

        private async Task MarkSuccessful(CheckBlockedJob job)
        {
            job.State = CheckBlockedJob.CheckBlockedJobState.Completed;
            job.LastUpdate = _clock.GetCurrentInstant();
            await _dbContext.SaveChangesAsync();
        }

        private async Task MarkFailed(CheckBlockedJob job)
        {
            job.State = CheckBlockedJob.CheckBlockedJobState.Failed;
            job.LastUpdate = _clock.GetCurrentInstant();
            await _dbContext.SaveChangesAsync();
        }
    }
}
