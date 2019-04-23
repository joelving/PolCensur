using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DemokratiskDialog.Areas.Identity.Pages.Account;
using DemokratiskDialog.Data;
using DemokratiskDialog.Extensions;
using DemokratiskDialog.Models;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace DemokratiskDialog.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UsersToCheck _userToCheck;
        private readonly IClock _clock;
        private readonly ImageService _imageService;
        private readonly ILogger<ExternalLoginModel> _logger;

        public IndexModel(
            ApplicationDbContext dbContext,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            UsersToCheck userToCheck,
            IClock clock,
            ImageService imageService,
            ILogger<ExternalLoginModel> logger)
        {
            _dbContext = dbContext;
            _signInManager = signInManager;
            _userManager = userManager;
            _userToCheck = userToCheck;
            _clock = clock;
            _imageService = imageService;
            _logger = logger;
        }

        public class Blocker
        {
            public string Handle { get; set; }
            public string ImageUrl { get; set; }
            public int BlockCount { get; set; }
            public List<UserBlock> BlockedProfiles { get; set; }
        }

        public List<Blocker> Blockers { get; set; }

        public bool DefaultPublicity { get; set; }

        public bool CanSubmit { get; set; }

        public CheckBlockedJob LatestJob { get; set; }
        public ContinuousCheckBlockedJob LatestContinuousJob { get; set; }

        public class StatsModel
        {
            public int UsersChecked { get; set; }
            public int UsersContinuouslyChecked { get; set; }
            public List<(int, int)> BlockedHistogram { get; set; }
            public List<(int, int)> BlockerHistogram { get; set; }
            public int TotalNumberOfBlocks { get; set; }

        }

        public StatsModel Stats { get; set; }
        public async Task PrepareStats()
        {
            Stats = new StatsModel();

            var continuousJobs = (await _dbContext.ContinuousJobs.Select(j => j.CheckingForTwitterId).Distinct().ToListAsync()).ToHashSet();
            var jobs = (await _dbContext.Jobs.Select(j => j.CheckingForTwitterId).Distinct().ToListAsync()).ToHashSet();
            jobs.UnionWith(continuousJobs);
            Stats.UsersChecked = jobs.Count;
            Stats.UsersContinuouslyChecked = await _dbContext.ContinuousJobs.Where(j => j.State == ContinuousCheckBlockedJob.JobState.Pending || j.State == ContinuousCheckBlockedJob.JobState.Running)
                .Select(j => j.CheckingForTwitterId).Distinct().CountAsync();

            var blocked = (await _dbContext.Blocks.GroupBy(b => b.UserId).Select(g => new { User = g.Key, Count = g.Count() }).ToListAsync())
                .GroupBy(b => b.Count).ToDictionary(g => g.Key, g => g.Count());
            var maxBlocked = blocked.Keys.Any() ? blocked.Keys.Max() : 0;
            Stats.BlockedHistogram = Enumerable.Range(1, maxBlocked).Select(i => (i, blocked.ContainsKey(i) ? blocked[i] : 0)).ToList();

            var blockers = await _dbContext.Blocks.GroupBy(b => b.BlockedByTwitterId).Select(g => new { User = g.Key, Count = g.Count() }).ToListAsync();
            var blockersHistogram = blockers.GroupBy(b => b.Count).ToDictionary(g => g.Key, g => g.Count());
            var maxBlocker = blockersHistogram.Keys.Any() ? blockersHistogram.Keys.Max() : 0;
            Stats.BlockerHistogram = Enumerable.Range(1, maxBlocker).Select(i => (i, blockersHistogram.ContainsKey(i) ? blockersHistogram[i] : 0)).ToList();

            Stats.TotalNumberOfBlocks = blocked.Sum(kvp => kvp.Key * kvp.Value);
        }

        public async Task OnGetAsync()
        {
            CanSubmit = true;
            if (_signInManager.IsSignedIn(User))
            {
                var user = await _userManager.GetUserAsync(User);
                if (!(user is null))
                {
                    DefaultPublicity = user.ShowProfileWithBlocks;
                    LatestJob = await _dbContext.GetLatestJobByUserId(user.Id);
                    LatestContinuousJob = await _dbContext.GetLatestContinuousJobByUserId(user.Id);

                    if (LatestContinuousJob?.LastUpdate > LatestJob?.LastUpdate)
                        LatestJob = null;
                    else
                        LatestContinuousJob = null;
                    CanSubmit = await _dbContext.CanStartNewJobs(_clock, user.Id);
                }
            }

            await PrepareBlocks();
            await PrepareStats();
        }

        private async Task PrepareBlocks()
        {
            Blockers = new List<Blocker>();

            var blockCounts = await _dbContext.BlockCounts.FromSql(
                @"SELECT TOP 50
                BlockedByTwitterId, COUNT(*) AS Count
                FROM Blocks
                GROUP BY BlockedByTwitterId
                ORDER BY COUNT(*) DESC;").ToListAsync();

            foreach (var blockCount in blockCounts)
            {
                var userBlocks = await _dbContext.UserBlocks.FromSql(
                    @"SELECT TOP 9
                    l.ProviderKey AS TwitterId, u.UserName AS Handle, u.ProfilePictureUrl, b.BlockedByTwitterId
                    FROM Blocks b
                    INNER JOIN AspNetUsers u ON u.Id = b.UserId
                    INNER JOIN AspNetUserLogins l ON l.UserId = u.Id
                    WHERE u.ShowProfileWithBlocks = 1
                    AND l.LoginProvider = 'Twitter'
                    AND b.BlockedByTwitterId = {0}
                    GROUP BY l.ProviderKey, u.UserName, u.ProfilePictureUrl, b.BlockedByTwitterId
                    ORDER BY NEWID();", blockCount.BlockedByTwitterId).ToListAsync();
                foreach (var userBlock in userBlocks)
                    userBlock.ProfilePictureUrl = await _imageService.GetProfileImage(userBlock.Handle, "bigger");

                var blocker = _userToCheck.FindById(blockCount.BlockedByTwitterId);
                if (blocker is null) continue;

                Blockers.Add(new Blocker
                {
                    Handle = blocker?.ScreenName,
                    ImageUrl = await _imageService.GetProfileImage(blocker?.ScreenName, "original"),
                    BlockCount = blockCount.Count,
                    BlockedProfiles = userBlocks
                });

                if (Blockers.Count >= 30)
                    break;
            }
        }
    }
}