using System.Linq;
using System.Threading.Tasks;
using DemokratiskDialog.Data;
using DemokratiskDialog.Extensions;
using DemokratiskDialog.Models;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using NodaTime;

namespace DemokratiskDialog.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IBackgroundQueue<CheckBlockedJob> _checkQueue;
        private readonly IBackgroundQueue<ContinuousCheckBlockedJob> _continuousQueue;
        private readonly TwitterService _twitterService;
        private readonly IDataProtectionProvider _protectionProvider;
        private readonly IClock _clock;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            ApplicationDbContext dbContext,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IBackgroundQueue<CheckBlockedJob> checkQueue,
            IBackgroundQueue<ContinuousCheckBlockedJob> continuousQueue,
            TwitterService twitterService,
            IDataProtectionProvider protectionProvider,
            IClock clock,
            ILogger<ExternalLoginModel> logger)
        {
            _dbContext = dbContext;
            _signInManager = signInManager;
            _userManager = userManager;
            _checkQueue = checkQueue;
            _continuousQueue = continuousQueue;
            _twitterService = twitterService;
            _protectionProvider = protectionProvider;
            _clock = clock;
            _logger = logger;
        }

        public string LoginProvider { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public IActionResult OnGetAsync()
        {
            return RedirectToPage("./Login");
        }

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null, bool run = false, string email = null, bool? publicity = null, bool continuous = false)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Fejl fra eksternt login: {remoteError}";
                return RedirectToPage("./Login", new {ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Vi kunne ikke genfinde dine logininformationer. Prøv venligst igen.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor : true);
            if (result.Succeeded)
            {
                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                var user = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (publicity.HasValue)
                {
                    user.ShowProfileWithBlocks = publicity.Value;
                    await _userManager.UpdateAsync(user);
                }

                if (run)
                {
                    if (!(await _dbContext.CanStartNewJobs(_clock, user.Id)))
                        return RedirectToPage("/Check");

                    await StartJob(
                        email,
                        user.Id,
                        info.ProviderKey,
                        info.Principal.Identity.Name,
                        info.AuthenticationTokens.FirstOrDefault(t => t.Name == "access_token")?.Value,
                        info.AuthenticationTokens.FirstOrDefault(t => t.Name == "access_token_secret")?.Value,
                        continuous
                    );
                }
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // If the user does not have an account, create it.
                ReturnUrl = returnUrl;
                LoginProvider = info.LoginProvider;

                var profile = await _twitterService.ShowByScreenName(info.Principal.Identity.Name);
                var user = new ApplicationUser {
                    UserName = info.Principal.Identity.Name,
                    ProfilePictureUrl = profile?.ProfileImageUrlHttps.ToString(),
                    ShowProfileWithBlocks = publicity ?? false
                };
                var createResult = await _userManager.CreateAsync(user);
                if (createResult.Succeeded)
                {
                    createResult = await _userManager.AddLoginAsync(user, info);
                    if (createResult.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                        if (run)
                        {
                            if (!(await _dbContext.CanStartNewJobs(_clock, user.Id)))
                                return RedirectToPage("/Check");

                            await StartJob(
                                email,
                                user.Id,
                                info.ProviderKey,
                                info.Principal.Identity.Name,
                                info.AuthenticationTokens.FirstOrDefault(t => t.Name == "access_token")?.Value,
                                info.AuthenticationTokens.FirstOrDefault(t => t.Name == "access_token_secret")?.Value,
                                continuous
                            );
                        }
                        return LocalRedirect(returnUrl);
                    }
                }
                return Page();
            }
        }

        private async Task StartJob(string email, string checkingForUserId, string checkingForTwitterId, string checkingForScreenname, string accessToken, string accessTokenSecret, bool continuous)
        {
            string protectedToken = null, protectedTokenSecret = null;
            if (accessTokenSecret != null)
            {
                var protector = _protectionProvider.CreateProtector(checkingForUserId);
                protectedToken = protector.Protect(accessToken);
                protectedTokenSecret = protector.Protect(accessTokenSecret);
            }

            if (continuous)
            {
                var job = new ContinuousCheckBlockedJob
                {
                    Email = email,
                    State = ContinuousCheckBlockedJob.JobState.Pending,
                    LastUpdate = _clock.GetCurrentInstant(),
                    CheckingForUserId = checkingForUserId,
                    CheckingForTwitterId = checkingForTwitterId,
                    CheckingForScreenName = checkingForScreenname,
                    AccessToken = protectedToken,
                    AccessTokenSecret = protectedTokenSecret
                };

                _dbContext.ContinuousJobs.Add(job);
                await _dbContext.SaveChangesAsync();
                await _continuousQueue.EnqueueAsync(job, HttpContext.RequestAborted);
            }
            else
            {
                var job = new CheckBlockedJob
                {
                    Email = email,
                    State = CheckBlockedJob.CheckBlockedJobState.Pending,
                    LastUpdate = _clock.GetCurrentInstant(),
                    CheckingForUserId = checkingForUserId,
                    CheckingForTwitterId = checkingForTwitterId,
                    CheckingForScreenName = checkingForScreenname,
                    AccessToken = protectedToken,
                    AccessTokenSecret = protectedTokenSecret
                };

                _dbContext.Jobs.Add(job);
                await _dbContext.SaveChangesAsync();
                await _checkQueue.EnqueueAsync(job, HttpContext.RequestAborted);
            }
        }
    }
}
