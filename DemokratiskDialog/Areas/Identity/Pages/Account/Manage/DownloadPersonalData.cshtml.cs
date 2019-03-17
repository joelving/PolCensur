using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DemokratiskDialog.Data;
using DemokratiskDialog.Extensions;
using DemokratiskDialog.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace DemokratiskDialog.Areas.Identity.Pages.Account.Manage
{
    public class DownloadPersonalDataModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<DownloadPersonalDataModel> _logger;

        public DownloadPersonalDataModel(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager,
            ILogger<DownloadPersonalDataModel> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Kunne ikke finde brugeren med Id '{_userManager.GetUserId(User)}'.");
            }

            _logger.LogInformation("User with ID '{UserId}' asked for their personal data.", _userManager.GetUserId(User));

            // Only include personal data for download
            var personalData = new Dictionary<string, string>();
            var personalDataProps = typeof(ApplicationUser).GetProperties().Where(
                            prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
            foreach (var p in personalDataProps)
            {
                personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
            }

            var blocks = (await _dbContext.Blocks.Where(b => b.UserId == user.Id).ToListAsync()).Select(b => new
            {
                FirstSeen = b.FirstSeen.ToDanishTime().ToString(),
                Checked = b.Checked.ToDanishTime().ToString(),
                b.BlockedByTwitterId
            });

            var archivedBlocks = (await _dbContext.ArchivedBlocks.Where(b => b.UserId == user.Id).ToListAsync()).Select(b => new
            {
                Checked = b.Checked.ToString(),
                b.BlockedByTwitterId
            });

            var jobs = (await _dbContext.Jobs.Where(j => j.CheckingForUserId == user.Id).ToListAsync()).Select(j => new
            {
                j.LastUpdate,
                j.State,
                j.CheckingForUserId,
                j.CheckingForTwitterId,
                j.CheckingForScreenName
            });

            var continuousJobs = (await _dbContext.ContinuousJobs.Where(j => j.CheckingForUserId == user.Id).ToListAsync()).Select(j => new
            {
                j.Email,
                j.AccessToken,
                j.AccessTokenSecret,
                j.LastUpdate,
                j.State,
                j.CheckingForUserId,
                j.CheckingForTwitterId,
                j.CheckingForScreenName
            });

            Response.Headers.Add("Content-Disposition", "attachment; filename=PersonalData.json");
            return new FileContentResult(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new { personalData, blocks, archivedBlocks, jobs, continuousJobs })), "text/json");
        }
    }
}
