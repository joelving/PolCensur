using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using DemokratiskDialog.Data;
using DemokratiskDialog.Extensions;
using DemokratiskDialog.Models;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace DemokratiskDialog.Pages
{
    public class CheckModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IClock _clock;
        private readonly ILogger<CheckModel> _logger;

        public CheckModel(
            ApplicationDbContext dbContext,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IClock clock,
            ILogger<CheckModel> logger)
        {
            _dbContext = dbContext;
            _signInManager = signInManager;
            _userManager = userManager;
            _clock = clock;
            _logger = logger;
        }

        [BindProperty]
        public CheckSubmissionModel Input { get; set; }

        public bool CanSubmit { get; set; }

        public CheckBlockedJob LatestJob { get; set; }

        public async Task OnGet()
        {
            CanSubmit = true;
            Input = new CheckSubmissionModel();
            if (_signInManager.IsSignedIn(User))
            {
                var user = await _userManager.GetUserAsync(User);
                if (!(user is null))
                {
                    Input.Publicity = user.ShowProfileWithBlocks;
                    LatestJob = await _dbContext.GetLatestJobByUserId(user.Id);
                    CanSubmit = await _dbContext.CanStartNewJobs(_clock, user.Id);
                }
            }
        }
        public IActionResult OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("/Account/ExternalLogin", pageHandler: "Callback", values: new { area = "Identity", Run = true, Input.Email, Input.Publicity, returnUrl = Url.Page("/check-pending") });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Twitter", redirectUrl);
            return new ChallengeResult("Twitter", properties);
        }
    }
}