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

        private static Duration EmailThreshold = Duration.FromMilliseconds(90 * 60 * 1000); // Minimum milliseconds between unblock and new block before sending an email.

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
                    var existing = (await _dbContext.Blocks.Where(b => b.UserId == job.CheckingForUserId).ToListAsync())
                        .GroupBy(b => b.BlockedByTwitterId).ToDictionary(g => g.Key, g => g.First());
                    var lastSeen = await _dbContext.ArchivedBlocks.Where(b => b.UserId == job.CheckingForUserId).Select(b => new { b.BlockedByTwitterId, b.VerifiedGone }).GroupBy(b => b.BlockedByTwitterId).ToDictionaryAsync(g => g.Key, g => g.Max(b => b.VerifiedGone));

                    while (true)
                    {
                        linkedCts.Token.ThrowIfCancellationRequested();
                        await ratelimiter.WaitToProceed(linkedCts.Token);
                        linkedCts.Token.ThrowIfCancellationRequested();
                        _logger.LogInformation($"Beginning to check blocks for user with local Id {job.CheckingForUserId} and Twitter Id '{job.CheckingForTwitterId}'.");

                        try
                        {
                            var (blocks, hasNewBlocks) = await GetBlocksByList(job, existing, lastSeen, linkedCts.Token);

                            var unblockCandidates = existing.Where(e => !blocks.Any(b => b.BlockedByTwitterId == e.Key)).Select(e => e.Value).ToList();
                            var hasUnblocks = await VerifyUnblocks(job, unblockCandidates, existing, cancellationToken);

                            if (_dbContext.ChangeTracker.HasChanges())
                                await _dbContext.SaveChangesAsync(linkedCts.Token);

                            if (hasNewBlocks || hasUnblocks)
                            {
                                var (subject, body) = EmailTemplates.BlocksUpdated(job.CheckingForScreenName, "https://polcensur.dk/profile/blocks");
                                await _emailService.SendEmailAsync(job.Email, subject, body);
                            }

                            job.LastUpdate = _clock.GetCurrentInstant();
                            await _dbContext.SaveChangesAsync();
                        }
                        catch (TwitterTransientException)
                        {
                            // Do nothing. This may pass.
                        }
                    }
                }
            }
            catch (TwitterUnauthorizedException tuex)
            {
                // We cannot possibly continue. Abort and notify user to reauthorize.
                _logger.LogError(tuex, $"Authentication error occurred trying to check blocks of '{job.CheckingForUserId}'.");
                await _dbContext.LogException(job.Id.ToString(), "ContinuousCheckBlockedJob", tuex, _clock);

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
            catch (Exception ex)
            {
                await _dbContext.LogException(job.Id.ToString(), "ContinuousCheckBlockedJob", ex, _clock);
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

        private async Task<bool> VerifyUnblocks(ContinuousCheckBlockedJob job, List<Block> unblockCandidates, Dictionary<string, Block> existing, CancellationToken cancellationToken)
        {
            var unblocks = false;
            foreach (var oldBlock in unblockCandidates)
            {
                var twitterId = oldBlock.BlockedByTwitterId;
                cancellationToken.ThrowIfCancellationRequested();
                if (twitterId == job.CheckingForTwitterId) continue;

                var isBlocked = await _twitterService.IsBlocked(job.CheckingForUserId, job.AccessToken, job.AccessTokenSecret, twitterId, cancellationToken);
                if (!isBlocked)
                {
                    _logger.LogInformation($"User {job.CheckingForTwitterId} has been unblocked by user with Id {twitterId}.");

                    existing.Remove(twitterId);
                    _dbContext.Blocks.Remove(oldBlock);

                    var archived = ArchivedBlock.CreateFromBlock(oldBlock, _clock);
                    _dbContext.ArchivedBlocks.Add(archived);

                    unblocks = true;
                }
            }
            return unblocks;
        }

        const string listOwner = "DemokratiskD";
        const string listSlug = "demokratisk-dialog";
        private async Task<(List<Block>, bool)> GetBlocksByList(ContinuousCheckBlockedJob job, Dictionary<string, Block> existing, Dictionary<string, Instant> lastSeen, CancellationToken cancellationToken)
        {
            var profiles = await _twitterService.ListMembersAsUser(job.CheckingForUserId, job.AccessToken, job.AccessTokenSecret, listOwner, listSlug, cancellationToken);
            var candidates = profiles.Where(p => p.Status is null);

            var blocks = new List<Block>();
            var newBlocks = false;
            foreach (var userToCheck in candidates)
            {
                if (userToCheck.StatusesCount == 0 || userToCheck.Protected)
                    continue;

                var twitterId = userToCheck.IdStr;
                var screenName = userToCheck.ScreenName;

                if (existing.ContainsKey(twitterId))
                {
                    existing[twitterId].Checked = _clock.GetCurrentInstant();
                    blocks.Add(existing[twitterId]);
                }
                else
                {
                    // This is a brand new block. Let's verify it to be sure.
                    var isBlocked = await VerifyBlock(job, existing, userToCheck, cancellationToken);

                    if (isBlocked)
                    {
                        _logger.LogInformation($"User {job.CheckingForTwitterId} has been blocked by user '{screenName}' (Id {twitterId}).");
                        var now = _clock.GetCurrentInstant();
                        var newBlock = new Block
                        {
                            UserId = job.CheckingForUserId,
                            BlockedByTwitterId = twitterId,
                            FirstSeen = now,
                            Checked = now
                        };
                        blocks.Add(newBlock);
                        _dbContext.Blocks.Add(newBlock);

                        // Only mark new blocks if we have no archived blocks within the threshold period.
                        if (!(lastSeen.ContainsKey(twitterId) && newBlock.FirstSeen - lastSeen[twitterId] < ContinuousCheckBlockedJobProcessor.EmailThreshold))
                            newBlocks = true;
                    }
                }
            }
            return (blocks, newBlocks);
        }

        private async Task<bool> VerifyBlock(ContinuousCheckBlockedJob job, Dictionary<string, Block> existing, TwitterUser userToCheck, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (userToCheck.IdStr == job.CheckingForTwitterId)
                return false;

            return await _twitterService.IsBlocked(job.CheckingForUserId, job.AccessToken, job.AccessTokenSecret, userToCheck.IdStr, cancellationToken);

        }

        //private async Task<(List<Block>, bool)> VerifyBlocks(ContinuousCheckBlockedJob job, Dictionary<string, Block> existing, IEnumerable<TwitterUser> candidates, CancellationToken cancellationToken)
        //{
        //    var blocks = new List<Block>();
        //    var newBlocks = false;
        //    foreach (var userToCheck in candidates)
        //    {
        //        var twitterId = userToCheck.IdStr;
        //        var screenName = userToCheck.ScreenName;
        //        cancellationToken.ThrowIfCancellationRequested();
        //        if (twitterId == job.CheckingForTwitterId) continue;

        //        var isBlocked = await _twitterService.IsBlocked(job.CheckingForUserId, job.AccessToken, job.AccessTokenSecret, twitterId, cancellationToken);

        //        if (isBlocked)
        //        {
        //            if (existing.ContainsKey(twitterId))
        //            {
        //                existing[twitterId].Checked = _clock.GetCurrentInstant();
        //                blocks.Add(existing[twitterId]);
        //            }
        //            else
        //            {
        //                _logger.LogInformation($"User {job.CheckingForTwitterId} has been blocked by user '{screenName}' (Id {twitterId}).");
        //                var now = _clock.GetCurrentInstant();
        //                var newBlock = new Block
        //                {
        //                    UserId = job.CheckingForUserId,
        //                    BlockedByTwitterId = twitterId,
        //                    FirstSeen = now,
        //                    Checked = now
        //                };
        //                blocks.Add(newBlock);
        //                _dbContext.Blocks.Add(newBlock);
        //                newBlocks = true;
        //            }
        //        }
        //    }
        //    return (blocks, newBlocks);
        //}

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
