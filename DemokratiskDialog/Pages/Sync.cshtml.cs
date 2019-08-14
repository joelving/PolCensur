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
    public class SyncModel : AdminPageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ListsToCheck _listsToCheck;
        private readonly UsersToCheck _userToCheck;
        private readonly TwitterAdminService _twitterService;
        private readonly TwitterApiAdminOptions _options;
        private readonly IHostingEnvironment _environment;
        private readonly IClock _clock;
        private readonly string _listDir;

        public SyncModel(ApplicationDbContext dbContext, ListsToCheck listsToCheck, UsersToCheck userToCheck, TwitterAdminService twitterService, IOptionsSnapshot<TwitterApiAdminOptions> optionSnapshot, IHostingEnvironment environment, IClock clock)
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

        public List<ListData> Lists { get; set; } = new List<ListData>();

        public async Task<IActionResult> OnGet()
        {
            if (!IsAdmin())
                return NotFound();

            // Show count per group in current lists.
            foreach (var (owner, slug, category, internalSlug) in _listsToCheck)
            {
                var listData = new ListData { Slug = slug, Category = category };

                var internalPath = Path.Combine(_listDir, $"internal-{slug}.csv");
                var internalExists = System.IO.File.Exists(internalPath);
                var internalUsers = internalExists ? (await System.IO.File.ReadAllLinesAsync(internalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList() : new List<TwitterUser>();
                listData.InternalCount = internalExists ? internalUsers.Count : (int?)null;
                listData.LastSyncedInternal = internalExists ? Instant.FromDateTimeUtc(System.IO.File.GetLastWriteTimeUtc(internalPath)) : (Instant?)null;

                var externalPath = Path.Combine(_listDir, $"external-{slug}.csv");
                var externalExists = System.IO.File.Exists(externalPath);
                var externalUsers = externalExists ? (await System.IO.File.ReadAllLinesAsync(externalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList() : new List<TwitterUser>();
                listData.ExternalCount = externalExists ? externalUsers.Count : (int?)null;
                listData.LastSyncedExternal = externalExists ? Instant.FromDateTimeUtc(System.IO.File.GetLastWriteTimeUtc(externalPath)) : (Instant?)null;

                var internalIds = internalUsers.Select(u => u.IdStr).ToHashSet();
                var externalIds = externalUsers.Select(u => u.IdStr).ToHashSet();
                listData.MissingUsers = externalUsers.Where(u => !internalIds.Contains(u.IdStr)).ToList();
                listData.AdditionalUsers = internalUsers.Where(u => !externalIds.Contains(u.IdStr)).ToList();

                Lists.Add(listData);
            }

            // Show when own lists where last synced.

            // Show when external lists where last synced.

            // Show difference in lists.

            // Show buttons to sync lists.

            // Show button to add profile from external to own lists.
            return Page();
        }

        public async Task<IActionResult> OnPostAddMissingUsersAsync(string slug)
        {
            if (!IsAdmin())
                return NotFound();

            var (Owner, Slug, Category, InternalSlug) = _listsToCheck.FirstOrDefault(l => l.Slug == slug);

            var internalPath = Path.Combine(_listDir, $"internal-{InternalSlug}.csv");
            var internalExists = System.IO.File.Exists(internalPath);
            var internalUsers = internalExists ? (await System.IO.File.ReadAllLinesAsync(internalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList() : new List<TwitterUser>();

            var externalPath = Path.Combine(_listDir, $"external-{Slug}.csv");
            var externalExists = System.IO.File.Exists(externalPath);
            var externalUsers = externalExists ? (await System.IO.File.ReadAllLinesAsync(externalPath, HttpContext.RequestAborted)).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList() : new List<TwitterUser>();

            var internalIds = internalUsers.Select(u => u.IdStr).ToHashSet();
            var diff = externalUsers.Where(u => !internalIds.Contains(u.IdStr)).ToList();

            int offset = 0, batchSize = 100;
            while (offset < diff.Count)
            {
                await _twitterService.AddProfilesToList(InternalSlug, diff.Skip(offset).Take(batchSize).Select(u => u.ScreenName), HttpContext.RequestAborted);
                offset += batchSize;
            }
            
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSyncInternalAsync()
        {
            if (!IsAdmin())
                return NotFound();

            foreach (var (owner, slug, category, internalSlug) in _listsToCheck)
            {
                try
                {
                    var profiles = await _twitterService.ListMembers("DemokratiskD", slug, HttpContext.RequestAborted);
                    await System.IO.File.WriteAllLinesAsync(Path.Combine(_listDir, $"internal-{slug}.csv"), profiles.Select(p => JsonConvert.SerializeObject(p)), HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    await _dbContext.LogException("SyncInternal", "/sync", ex, _clock, HttpContext.RequestAborted);
                }
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostSyncExternalAsync()
        {
            if (!IsAdmin())
                return NotFound();

            foreach (var (owner, slug, category, internalSlug) in _listsToCheck) {
                try
                {
                    var profiles = await _twitterService.ListMembers(owner, slug, HttpContext.RequestAborted);
                    await System.IO.File.WriteAllLinesAsync(Path.Combine(_listDir, $"external-{slug}.csv"), profiles.Select(p => JsonConvert.SerializeObject(p)), HttpContext.RequestAborted);
                }
                catch (Exception ex)
                {
                    await _dbContext.LogException("SyncExternal", "/sync", ex, _clock, HttpContext.RequestAborted);
                }
            }
            return RedirectToPage();
        }
    }
}