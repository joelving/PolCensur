using DemokratiskDialog.Data;
using DemokratiskDialog.Models;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DemokratiskDialog.Services
{
    public class GDPRService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<ApplicationUser> _userManager;

        public GDPRService(
            ApplicationDbContext dbContext,
            UserManager<ApplicationUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public async Task RemoveBlocks(ApplicationUser user)
        {
            var blocks = await _dbContext.Blocks.Where(b => b.UserId == user.Id).ToListAsync();
            _dbContext.Blocks.RemoveRange(blocks);
            await _dbContext.SaveChangesAsync();
        }

        const string TwitterProviderKey = "Twitter";
        public async Task AnonymizeBlocks(ApplicationUser user)
        {
            var dummyUser = new ApplicationUser { IsDummyUser = true };

            var result = await _userManager.CreateAsync(dummyUser);
            if (!result.Succeeded)
                throw new Exception();

            var logins = await _userManager.GetLoginsAsync(user);
            var twitterId = logins.FirstOrDefault(l => l.LoginProvider == TwitterProviderKey)?.ProviderKey;
            var hashedTwitterId = HashString(twitterId);

            result = await _userManager.AddLoginAsync(dummyUser, new UserLoginInfo(TwitterProviderKey, hashedTwitterId, "Dummy user"));
            if (!result.Succeeded)
                throw new Exception();

            var blocks = await _dbContext.Blocks.Where(b => b.UserId == user.Id).ToListAsync();
            blocks.ForEach(b => b.User = dummyUser);
            await _dbContext.SaveChangesAsync();
        }

        private string HashString(string value)
        {
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // derive a 256-bit subkey (use HMACSHA1 with 10,000 iterations)
            return Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: value,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA1,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));
        }
    }
}
