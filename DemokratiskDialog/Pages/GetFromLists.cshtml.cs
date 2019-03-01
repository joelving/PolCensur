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

        public GetFromListsModel(IHostingEnvironment environment, TwitterService twitterService, UsersToCheck usersToCheck)
        {
            _environment = environment;
            _twitterService = twitterService;
            _usersToCheck = usersToCheck;
        }

        public (string owner, string slug, UserCategory category)[] Lists { get; set; } = new[]
        {
            ("ernstpoulsen", "valg-folketinget-2019", UserCategory.Politician),
            ("ernstpoulsen", "valg-europaparlament-2019", UserCategory.Politician),
            ("ernstpoulsen", "region-nordjylland", UserCategory.Politician),
            ("ernstpoulsen", "region-midtjylland", UserCategory.Politician),
            ("ernstpoulsen", "region-syddanmark", UserCategory.Politician),
            ("ernstpoulsen", "region-sjælland", UserCategory.Politician),
            ("ernstpoulsen", "region-hovedstaden", UserCategory.Politician),
            ("ernstpoulsen", "byrådsmedlemmer-i-danmark", UserCategory.Politician),
            ("ernstpoulsen", "off-myndigheder-topfolk", UserCategory.Public),
            ("ernstpoulsen", "off-myndigheder", UserCategory.Public),
            ("ernstpoulsen", "fagforeninger-og-topfolk", UserCategory.Union),
            ("ernstpoulsen", "fagforeninger", UserCategory.Union),
            ("ernstpoulsen", "firmaer-større-topfolk", UserCategory.Business),
            ("ernstpoulsen", "interesseorg-topfolk", UserCategory.Lobby),
            ("ernstpoulsen", "interesseorganisationer", UserCategory.Lobby),
            ("ernstpoulsen", "kommunikationsfolk-dk", UserCategory.Spin)
        };

        public List<Influencer> Profiles { get; set; }

        public async Task<IActionResult> OnGet()
        {
            if (!_environment.IsDevelopment())
                return Forbid();

            var knownIds = _usersToCheck.Select(u => u.Profile.IdStr).ToHashSet();

            var filePath = Path.Combine(_environment.ContentRootPath, "ernstpoulsen-profiles.csv");
            if (System.IO.File.Exists(filePath))
            {
                //Profiles = (await System.IO.File.ReadAllLinesAsync(filePath)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList();
            }
            else
            {
                Profiles = new List<Influencer>();
                foreach (var (owner, slug, category) in Lists)
                {
                    Profiles.AddRange((await _twitterService.ListMembers(owner, slug, HttpContext.RequestAborted)).Select(u => new Influencer { Category = category, Profile = u }));
                }
                Profiles = Profiles.GroupBy(p => p.Profile.IdStr).Select(g => g.FirstOrDefault()).ToList();
                await System.IO.File.WriteAllLinesAsync(filePath, Profiles.Select(p => JsonConvert.SerializeObject(p)));
            }

            return Page();
        }
    }
}