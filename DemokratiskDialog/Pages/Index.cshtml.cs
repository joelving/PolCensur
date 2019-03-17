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

        public int UsersChecked { get; set; }

        public bool DefaultPublicity { get; set; }

        public bool CanSubmit { get; set; }

        public CheckBlockedJob LatestJob { get; set; }
        public ContinuousCheckBlockedJob LatestContinuousJob { get; set; }

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
        }

        private async Task PrepareBlocks()
        {
            UsersChecked = await _dbContext.Jobs.Select(j => j.CheckingForUserId).Distinct().CountAsync();
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
                    ORDER BY MAX(b.Checked) DESC;", blockCount.BlockedByTwitterId).ToListAsync();
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