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

        public BlocksModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            UsersToCheck usersToCheck
            )
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _usersToCheck = usersToCheck;
        }

        public BlockViewModel[] Blocks { get; set; }

        public class BlockViewModel
        {
            public string TwitterId { get; set; }
            public string Handle { get; set; }
            public string ProfilePictureUrl { get; set; }
            public Instant Checked { get; set; }
        }

        public CheckBlockedJob LatestJob { get; set; }

        public async Task<IActionResult> OnGet()
        {
            var userId = _userManager.GetUserId(User);
            if (userId is null) return Unauthorized();

            Blocks = (await _dbContext.Blocks.Where(b => b.UserId == userId).ToArrayAsync()).Select(b =>
            {
                var blocker = _usersToCheck.FirstOrDefault(u => u.Profile.IdStr == b.BlockedByTwitterId);
                return new BlockViewModel
                {
                    TwitterId = b.BlockedByTwitterId,
                    Handle = blocker?.Profile.ScreenName,
                    ProfilePictureUrl = blocker?.Profile.ProfileImageUrlHttps.ToString(),
                    Checked = b.Checked
                };
            }).ToArray();

            LatestJob = await _dbContext.GetLatestJobByUserId(userId);

            return Page();
        }
    }
}