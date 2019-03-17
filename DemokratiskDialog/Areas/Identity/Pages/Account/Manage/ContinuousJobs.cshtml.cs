using System.Threading.Tasks;
using DemokratiskDialog.Data;
using DemokratiskDialog.Extensions;
using DemokratiskDialog.Models;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DemokratiskDialog.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class ContinuousJobs : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBackgroundQueue<ContinuousCheckBlockedJob> _continuousQueue;

        public ContinuousJobs(
            UserManager<ApplicationUser> userManager,
            IBackgroundQueue<ContinuousCheckBlockedJob> continuousQueue
            )
        {
            _userManager = userManager;
            _continuousQueue = continuousQueue;
        }

        public async Task<IActionResult> OnGet()
        {
            var userId = _userManager.GetUserId(User);
            if (userId is null) return Forbid();

            var terminator = new ContinuousCheckBlockedJob { CheckingForUserId = userId, Terminate = true };
            await _continuousQueue.EnqueueAsync(terminator, HttpContext.RequestAborted);

            return Redirect("/");
        }
    }
}