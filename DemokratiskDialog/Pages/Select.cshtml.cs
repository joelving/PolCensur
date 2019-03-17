using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DemokratiskDialog.Data;
using DemokratiskDialog.Models;
using DemokratiskDialog.Options;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using NodaTime;

namespace DemokratiskDialog.Pages
{
    //[Authorize]
    public class SelectModel : AdminPageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ListsToCheck _listsToCheck;
        private readonly UsersToCheck _userToCheck;
        private readonly TwitterAdminService _twitterService;
        private readonly TwitterApiAdminOptions _options;
        private readonly IHostingEnvironment _environment;
        private readonly IClock _clock;
        private readonly string _listDir;

        public SelectModel(ApplicationDbContext dbContext, ListsToCheck listsToCheck, UsersToCheck userToCheck, TwitterAdminService twitterService, IOptionsSnapshot<TwitterApiAdminOptions> optionSnapshot, IHostingEnvironment environment, IClock clock)
        {
            _dbContext = dbContext;
            _listsToCheck = listsToCheck;
            _userToCheck = userToCheck;
            _twitterService = twitterService;
            _options = optionSnapshot.Value;
            _environment = environment;
            _clock = clock;

            _listDir = Path.Combine(environment.ContentRootPath, "lists");
        }

        public class ListData
        {
            public string Slug { get; set; }
            public UserCategory Category { get; set; }
            public Instant? LastSyncedInternal { get; set; }
            public Instant? LastSyncedExternal { get; set; }
            public int? InternalCount { get; set; }
            public int? ExternalCount { get; set; }
            public List<TwitterUser> MissingUsers { get; set; }
            public List<TwitterUser> AdditionalUsers { get; set; }
        }

        public class ListUser
        {
            public TwitterUser User { get; set; }
            public bool Selected { get; set; }
        }

        public List<ListData> Lists { get; set; } = new List<ListData>();


        [BindProperty]
        public List<string> SelectedIds { get; set; }

        public int InternalCount { get; set; }

        public string[] InternalSlugs = new[]
        {
            "politikere",
            "kommunikation",
            "myndigheder",
            "fagforeninger",
            "interesseorganisationer",
            "erhverv",
            "journalister"
        };

        public async Task<IActionResult> OnGet()
        {
            if (!IsAdmin())
                return NotFound();

            var selectedIds = new List<string>();

            // Get all internal.
            foreach (var slug in InternalSlugs)
            {
                var internalPath = Path.Combine(_listDir, $"internal-selected-{slug}.csv");
                var internalExists = System.IO.File.Exists(internalPath);
                if (internalExists)
                    selectedIds.AddRange((await System.IO.File.ReadAllLinesAsync(internalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).Select(u => u.IdStr));
            }

            var internalIds = selectedIds.ToHashSet();
            InternalCount = internalIds.Count;

            // Show count per group in current lists.
            foreach (var (owner, slug, category, internalSlug) in _listsToCheck)
            {
                var listData = new ListData { Slug = slug, Category = category };

                var externalPath = Path.Combine(_listDir, $"external-{slug}.csv");
                var externalExists = System.IO.File.Exists(externalPath);
                var externalUsers = externalExists ? (await System.IO.File.ReadAllLinesAsync(externalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList() : new List<TwitterUser>();
                listData.ExternalCount = externalExists ? externalUsers.Count : (int?)null;
                listData.LastSyncedExternal = externalExists ? Instant.FromDateTimeUtc(System.IO.File.GetLastWriteTimeUtc(externalPath)) : (Instant?)null;
                
                var externalIds = externalUsers.Select(u => u.IdStr).ToHashSet();
                listData.MissingUsers = externalUsers.Where(u => !internalIds.Contains(u.IdStr)).OrderByDescending(u => u.FollowersCount).ToList();

                Lists.Add(listData);
            }

            // Show when own lists where last synced.

            // Show when external lists where last synced.

            // Show difference in lists.

            // Show buttons to sync lists.

            // Show button to add profile from external to own lists.
            return Page();
        }

        public async Task<IActionResult> OnPostSelectUsersAsync(string list)
        {
            if (!IsAdmin())
                return NotFound();

            var selected = SelectedIds.ToHashSet();
            var internalPath = Path.Combine(_listDir, $"internal-selected-{list}.csv");
            var internalExists = System.IO.File.Exists(internalPath);
            var internalUsers = internalExists ? (await System.IO.File.ReadAllLinesAsync(internalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList() : new List<TwitterUser>();
            var internalIds = internalUsers.Select(u => u.IdStr).ToHashSet();

            foreach (var (owner, slug, category, internalSlug) in _listsToCheck)
            {
                var externalPath = Path.Combine(_listDir, $"external-{slug}.csv");
                var externalExists = System.IO.File.Exists(externalPath);
                var externalUsers = externalExists ? (await System.IO.File.ReadAllLinesAsync(externalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList() : new List<TwitterUser>();

                var newlySelected = externalUsers.Where(u => selected.Contains(u.IdStr) && !internalIds.Contains(u.IdStr));
                internalUsers.AddRange(newlySelected);
            }

            await System.IO.File.WriteAllLinesAsync(internalPath, internalUsers.Select(p => JsonConvert.SerializeObject(p)), HttpContext.RequestAborted);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostRemoveDuplicateUsersAsync()
        {
            if (!IsAdmin())
                return NotFound();

            foreach (var slug in InternalSlugs)
            {
                var internalPath = Path.Combine(_listDir, $"internal-selected-{slug}.csv");
                var internalExists = System.IO.File.Exists(internalPath);
                if (internalExists)
                {
                    var profiles = (await System.IO.File.ReadAllLinesAsync(internalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l));
                    var uniques = profiles.GroupBy(p => p.IdStr).Select(g => g.First());
                    await System.IO.File.WriteAllLinesAsync(internalPath, uniques.Select(p => JsonConvert.SerializeObject(p)), HttpContext.RequestAborted);
                }
            }

            return RedirectToPage();
        }

        const string internalListSlug = "demokratisk-dialog";
        public async Task<IActionResult> OnPostAddMissingUsersAsync()
        {
            if (!IsAdmin())
                return NotFound();

            var selectedIds = new List<string>();

            // Get all internal.
            foreach (var slug in InternalSlugs)
            {
                var internalPath = Path.Combine(_listDir, $"internal-selected-{slug}.csv");
                var internalExists = System.IO.File.Exists(internalPath);
                if (internalExists)
                    selectedIds.AddRange((await System.IO.File.ReadAllLinesAsync(internalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).Select(u => u.IdStr));
            }
            
            var currentProfiles = await _twitterService.ListMembers("DemokratiskD", internalListSlug, HttpContext.RequestAborted);
            var existing = currentProfiles.Select(p => p.IdStr).ToHashSet();

            var selected = selectedIds.ToHashSet();
            var newProfiles = new HashSet<string>(selected);
            newProfiles.ExceptWith(existing);

            int offset = 0, batchSize = 95;
            while (offset < newProfiles.Count)
            {
                await _twitterService.AddProfilesToListById(internalListSlug, newProfiles.Skip(offset).Take(batchSize), HttpContext.RequestAborted);
                offset += batchSize;
            }

            return RedirectToPage();
        }
    }
}