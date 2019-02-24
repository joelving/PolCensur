using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DemokratiskDialog.Models;
using NodaTime;
using System;
using System.Threading;
using System.Threading.Tasks;
using DemokratiskDialog.Options;
using DemokratiskDialog.Data;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using DemokratiskDialog.Exceptions;

namespace DemokratiskDialog.Services
{
    public class CheckBlockedJobProcessor : IBackgroundJobProcessor<CheckBlockedJob>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IClock _clock;
        private readonly TwitterApiOptions _twitterOptions;
        private readonly ILogger<CheckBlockedJobProcessor> _logger;
        private readonly TwitterService _twitterService;
        private readonly UsersToCheck _usersToCheck;
        private readonly UserTimelineRateLimiter _rateLimiter;
        private readonly EmailService _emailService;

        public CheckBlockedJobProcessor(
            ApplicationDbContext dbContext,
            IClock clock,
            IOptionsSnapshot<TwitterApiOptions> twitterOptions,
            ILogger<CheckBlockedJobProcessor> logger,
            TwitterService twitterService,
            UsersToCheck usersToCheck,
            UserTimelineRateLimiter rateLimiter,
            EmailService emailService
            )
        {
            _dbContext = dbContext;
            _clock = clock;
            _twitterOptions = twitterOptions.Value;
            _logger = logger;
            _twitterService = twitterService;
            _usersToCheck = usersToCheck;
            _rateLimiter = rateLimiter;
            _emailService = emailService;
        }

        public async Task ProcessJob((CheckBlockedJob job, Action callback) data, CancellationToken cancellationToken)
        {
            var (job, callback) = data;
            try
            {
                _logger.LogInformation($"Beginning to check blocks for user '{job.CheckingForUserId}'.");

                var blocks = new List<Block>();
                var aborted = false;
                foreach (var userToCheck in _usersToCheck)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    await _rateLimiter.WaitToProceed(job.CheckingForUserId);

                    var isBlocked = await _twitterService.IsBlocked(job.AccessTokenSecret, userToCheck);
                    _logger.LogInformation($"User {job.CheckingForUserId} {(isBlocked ? "IS" : "is NOT")} by user '{userToCheck}'.");

                    if (isBlocked)
                    {
                        var newBlock = new Block
                        {
                            UserId = job.CheckingForUserId,
                            BlockedByTwitterId = userToCheck,
                            Checked = _clock.GetCurrentInstant()
                        };
                        blocks.Add(newBlock);
                        _dbContext.Blocks.Add(newBlock);
                    }
                }
                
                if (!aborted)
                {
                    using (var transaction = _dbContext.Database.BeginTransaction())
                    {
                        try
                        {
                            var existing = await _dbContext.Blocks.Where(b => b.UserId == job.CheckingForUserId).ToListAsync();
                            if (existing.Any())
                            {
                                _dbContext.Blocks.RemoveRange(existing);
                                await _dbContext.SaveChangesAsync();
                            }

                            _dbContext.Blocks.AddRange(blocks);
                            await _dbContext.SaveChangesAsync();

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Exception occurred updating blocks of '{job.CheckingForUserId}'.");
                        }
                    }
                }
            }
            catch (TwitterUnauthorizedException tuex)
            {
                // We cannot possibly continue. Abort and notify user to reauthorize.
                _logger.LogError(tuex, $"Authentication error occurred trying to check blocks of '{job.CheckingForUserId}'.");
                
                // TODO: Get the url to the signin-page.
                var (subject, body) = EmailTemplates.UnauthorizedError(job.CheckingForScreenName, "");
                await _emailService.SendEmailAsync(job.Email, subject, body);
            }
            catch (OperationCanceledException cex)
            {
                _logger.LogWarning($"Check blocks of '{job.CheckingForUserId}' was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Exception occurred trying to check blocks of '{job.CheckingForUserId}'.");
            }
            finally
            {
                // Release our queue semaphore allowing an additional item to be processed.
                callback();
            }
        }
    }
}
