using DemokratiskDialog.Models;
using Microsoft.AspNetCore.Identity;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace DemokratiskDialog.Extensions
{
    public static class UserExtensions
    {
        public static async Task<string> GetTwitterIdByUserId(this UserManager<ApplicationUser> userManager, string userId)
        {
            var user = await userManager.FindByIdAsync(userId);
            return await GetTwitterIdFromUser(userManager, user);
        }
        public static async Task<string> GetTwitterId(this UserManager<ApplicationUser> userManager, ClaimsPrincipal principal)
        {
            var user = await userManager.GetUserAsync(principal);
            return await GetTwitterIdFromUser(userManager, user);
        }

        private static async Task<string> GetTwitterIdFromUser(UserManager<ApplicationUser> userManager, ApplicationUser user)
        {
            var logins = await userManager.GetLoginsAsync(user);

            return logins.FirstOrDefault(l => l.LoginProvider == "Twitter")?.ProviderKey;
        }
    }
}
