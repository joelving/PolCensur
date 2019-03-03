using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DemokratiskDialog.Models;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;

namespace DemokratiskDialog.Pages
{
    public class GetFromListsModel : PageModel
    {
        private readonly IHostingEnvironment _environment;
        private readonly TwitterService _twitterService;
        private readonly UsersToCheck _usersToCheck;
        private readonly ListsToCheck _listsToCheck;

        public GetFromListsModel(IHostingEnvironment environment, TwitterService twitterService, UsersToCheck usersToCheck, ListsToCheck listsToCheck)
        {
            _environment = environment;
            _twitterService = twitterService;
            _usersToCheck = usersToCheck;
            _listsToCheck = listsToCheck;
        }

        public List<Influencer> Profiles { get; set; }

        public async Task<IActionResult> OnGet()
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            var knownIds = _usersToCheck.Select(u => u.Profile.IdStr).ToHashSet();

            var filePath = Path.Combine(_environment.ContentRootPath, "rwar.csv");
            if (System.IO.File.Exists(filePath))
            {
                //Profiles = (await System.IO.File.ReadAllLinesAsync(filePath)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList();
            }
            else
            {
                Profiles = new List<Influencer>();
                foreach (var (owner, slug, category, internalSlug) in _listsToCheck)
                {
                    Profiles.AddRange((await _twitterService.ListMembers(owner, slug, HttpContext.RequestAborted)).Select(u => new Influencer { Category = category, Profile = u }));
                }
                Profiles = Profiles.GroupBy(p => p.Profile.IdStr).Select(g => g.FirstOrDefault()).OrderByDescending(u => u.Profile.FollowersCount).Take(500).ToList();
                await System.IO.File.WriteAllLinesAsync(filePath, Profiles.Select(p => JsonConvert.SerializeObject(p)));
            }

            return Page();
        }
    }
}