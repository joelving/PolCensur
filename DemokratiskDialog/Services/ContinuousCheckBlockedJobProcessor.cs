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
using Microsoft.AspNetCore.Mvc;

namespace DemokratiskDialog.Services
{
    public class ContinuousCheckBlockedJobProcessor : IBackgroundJobProcessor<ContinuousCheckBlockedJob>
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IClock _clock;
        private readonly ILogger<ContinuousCheckBlockedJobProcessor> _logger;
        private readonly TwitterService _twitterService;
        private readonly UsersToCheck _usersToCheck;
        private readonly EmailService _emailService;

        public ContinuousCheckBlockedJobProcessor(
            ApplicationDbContext dbContext,
            IClock clock,
            ILogger<ContinuousCheckBlockedJobProcessor> logger,
            TwitterService twitterService,
            UsersToCheck usersToCheck,
            EmailService emailService
            )
        {
            _dbContext = dbContext;
            _clock = clock;
            _logger = logger;
            _twitterService = twitterService;
            _usersToCheck = usersToCheck;
            _emailService = emailService;
        }

        public async Task ProcessJob((ContinuousCheckBlockedJob job, Action callback) data, CancellationToken cancellationToken)
        {
            var (job, callback) = data;
            _dbContext.ContinuousJobs.Attach(job);
            await MarkRunning(job);

            try
            {
                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(job.CancellationToken, cancellationToken))
                using (var ratelimiter = new RateGate(1, TimeSpan.FromMinutes(10)))
                {
                    while (!linkedCts.IsCancellationRequested)
                    {
                        await ratelimiter.WaitToProceed(linkedCts.Token);
                        _logger.LogInformation($"Beginning to check blocks for user with local Id {job.CheckingForUserId} and Twitter Id '{job.CheckingForTwitterId}'.");

                        var existing = await _dbContext.Blocks.Where(b => b.UserId == job.CheckingForUserId).ToDictionaryAsync(b => b.BlockedByTwitterId, b => b);
                        List<TwitterUser> blocks = await GetBlocksByList(job, existing, linkedCts.Token);

                        var removed = existing.Where(e => !blocks.Any(b => b.IdStr == e.Key)).Select(e => e.Value);
                        if (removed.Any())
                        {
                            _dbContext.Blocks.RemoveRange(removed);
                            var archived = removed.Select(b => ArchivedBlock.CreateFromBlock(b, _clock));
                            _dbContext.ArchivedBlocks.AddRange(archived);
                            await _dbContext.SaveChangesAsync(linkedCts.Token);
                        }

                        var added = blocks.Where(b => !existing.Any(e => e.Key == b.IdStr));

                        if (added.Any() || removed.Any())
                        {
                            var (subject, body) = EmailTemplates.BlocksUpdated(job.CheckingForScreenName, "https://polcensur.dk/profile/blocks");
                            await _emailService.SendEmailAsync(job.Email, subject, body);
                        }

                        job.LastUpdate = _clock.GetCurrentInstant();
                        await _dbContext.SaveChangesAsync();
                    }
                }
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
                if (job.CancellationToken.IsCancellationRequested)
                {
                    await MarkSuccessful(job);
                }
                else
                {
                    await _dbContext.LogException(job.Id.ToString(), "CheckBlockedJob", cex, _clock);
                    _logger.LogWarning($"Check blocks of '{job.CheckingForUserId}' was cancelled.");
                    await MarkFailed(job);
                }
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

        const string listOwner = "DemokratiskD";
        const string listSlug = "demokratisk-dialog";
        private async Task<List<TwitterUser>> GetBlocksByList(ContinuousCheckBlockedJob job, Dictionary<string, Block> existing, CancellationToken cancellationToken)
        {
            var profiles = await _twitterService.ListMembersAsUser(job.CheckingForUserId, job.AccessToken, job.AccessTokenSecret, listOwner, listSlug, cancellationToken);
            var candidates = profiles.Where(p => p.Status is null);
            var blockedByIds = await VerifyBlocks(job, existing, candidates, cancellationToken);

            return candidates.Where(c => blockedByIds.Contains(c.IdStr)).ToList();
        }

        private async Task<List<string>> VerifyBlocks(ContinuousCheckBlockedJob job, Dictionary<string, Block> existing, IEnumerable<TwitterUser> candidates, CancellationToken cancellationToken)
        {
            var blocks = new List<string>();
            foreach (var userToCheck in candidates)
            {
                var twitterId = userToCheck.IdStr;
                var screenName = userToCheck.ScreenName;
                cancellationToken.ThrowIfCancellationRequested();
                if (twitterId == job.CheckingForTwitterId) continue;

                var isBlocked = await _twitterService.IsBlocked(job.CheckingForUserId, job.AccessToken, job.AccessTokenSecret, twitterId, cancellationToken);
                _logger.LogInformation($"User {job.CheckingForTwitterId} {(isBlocked ? "IS" : "is NOT")} by user '{screenName}' (Id {twitterId}).");

                if (isBlocked)
                {
                    blocks.Add(twitterId);
                    if (existing.ContainsKey(twitterId))
                    {
                        existing[twitterId].Checked = _clock.GetCurrentInstant();
                    }
                    else
                    {
                        var newBlock = new Block
                        {
                            UserId = job.CheckingForUserId,
                            BlockedByTwitterId = twitterId,
                            Checked = _clock.GetCurrentInstant()
                        };
                        _dbContext.Blocks.Add(newBlock);
                    }
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            return blocks;
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
