using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DemokratiskDialog.Data;
using DemokratiskDialog.Extensions;
using DemokratiskDialog.Models;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace DemokratiskDialog.Areas.Identity.Pages.Account.Manage
{
    public class BlocksModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly UsersToCheck _usersToCheck;
        private readonly ImageService _imageService;

        public BlocksModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            UsersToCheck usersToCheck,
            ImageService imageService
            )
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _usersToCheck = usersToCheck;
            _imageService = imageService;
        }

        public BlockViewModel[] Blocks { get; set; }
        public ArchivedBlockViewModel[] ArchivedBlocks { get; set; }

        public class BlockViewModel
        {
            public string TwitterId { get; set; }
            public string Handle { get; set; }
            public string ProfilePictureUrl { get; set; }
            public Instant FirstSeen { get; set; }
            public Instant Checked { get; set; }
        }
        public class ArchivedBlockViewModel : BlockViewModel
        {
            public Instant VerifiedGone { get; set; }
        }

        public CheckBlockedJob LatestJob { get; set; }
        public ContinuousCheckBlockedJob LatestContinuousJob { get; set; }

        public async Task<IActionResult> OnGet()
        {
            var userId = _userManager.GetUserId(User);
            if (userId is null) return Unauthorized();

            Blocks = (await _dbContext.Blocks.Where(b => b.UserId == userId).ToArrayAsync()).Select(b =>
            {
                var blocker = _usersToCheck.FindById(b.BlockedByTwitterId);
                return new BlockViewModel
                {
                    TwitterId = b.BlockedByTwitterId,
                    Handle = blocker?.ScreenName,
                    FirstSeen = b.FirstSeen,
                    Checked = b.Checked
                };
            }).ToArray();
            foreach (var block in Blocks)
                block.ProfilePictureUrl = await _imageService.GetProfileImage(block.Handle, "original");

            ArchivedBlocks = (await _dbContext.ArchivedBlocks.Where(b => b.UserId == userId).ToArrayAsync()).Select(b =>
            {
                var blocker = _usersToCheck.FindById(b.BlockedByTwitterId);
                return new ArchivedBlockViewModel
                {
                    TwitterId = b.BlockedByTwitterId,
                    Handle = blocker?.ScreenName,
                    FirstSeen = b.FirstSeen,
                    Checked = b.Checked,
                    VerifiedGone = b.VerifiedGone
                };
            }).ToArray();
            foreach (var block in ArchivedBlocks)
                block.ProfilePictureUrl = await _imageService.GetProfileImage(block.Handle, "original");

            LatestJob = await _dbContext.GetLatestJobByUserId(userId);
            LatestContinuousJob = await _dbContext.GetLatestContinuousJobByUserId(userId);

            if (LatestContinuousJob?.LastUpdate > LatestJob?.LastUpdate)
                LatestJob = null;
            else
                LatestContinuousJob = null;

            return Page();
        }
    }
}