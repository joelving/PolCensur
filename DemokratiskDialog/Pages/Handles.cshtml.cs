using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DemokratiskDialog.Models;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace DemokratiskDialog.Pages
{
    public class HandlesModel : AdminPageModel
    {
        private readonly IHostingEnvironment _environment;
        private readonly TwitterService _twitterService;

        public HandlesModel (IHostingEnvironment environment, TwitterService twitterService)
        {
            _environment = environment;
            _twitterService = twitterService;
        }

        public class ScoredTwitterUser
        {
            public long Score { get; set; }
            public TwitterUser User { get; set; }
        }

        public ScoredTwitterUser[] Profiles { get; set; }

        public IActionResult OnGet()
        {
            if (!IsAdmin())
                return NotFound();

            var profiles = System.IO.File.ReadAllLines(Path.Combine(_environment.ContentRootPath, "twitter-handles-ids.csv")).Select(JsonConvert.DeserializeObject<TwitterUser>);
            Profiles = profiles.Select(p => new ScoredTwitterUser
            {
                User = p,
                Score = (p.FollowersCount + 1) * (int)Math.Sqrt(p.StatusesCount + 1)
            })
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.User.FollowersCount)
            .ToArray();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!IsAdmin())
                return NotFound();

            var handles = System.IO.File.ReadAllLines(Path.Combine(_environment.ContentRootPath, "twitter-handles.csv"));
            var count = handles.Count();
            int offset = 0, batchSize = 100;
            using (var writer = new StreamWriter(System.IO.File.Open(Path.Combine(_environment.ContentRootPath, "twitter-handles-ids.csv"), FileMode.Create, FileAccess.Write, FileShare.Read))) {
                while (offset < count)
                {
                    var batch = handles.Skip(offset).Take(batchSize);
                    
                    var profiles = await _twitterService.LookupByScreenNames(batch);

                    foreach (var profile in profiles)
                        await writer.WriteLineAsync(JsonConvert.SerializeObject(profile));
    
                    offset += batchSize;
                }
            }
            return Page();
        }
    }
}