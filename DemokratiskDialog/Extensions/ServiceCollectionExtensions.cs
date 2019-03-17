using DemokratiskDialog.Models;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DemokratiskDialog.Extensions
{
    public static class ServiceCollectionExtensions
    {
        private static readonly string[] internalSlugs = new[]
        {
            "politikere",
            "kommunikation",
            "myndigheder",
            "fagforeninger",
            "interesseorganisationer",
            "erhverv",
            "journalister"
        };
        private static UserCategory SlugToCategory(string slug)
            => slug == internalSlugs[0] ? UserCategory.Politician
            : slug == internalSlugs[1] ? UserCategory.Spin
            : slug == internalSlugs[2] ? UserCategory.Public
            : slug == internalSlugs[3] ? UserCategory.Union
            : slug == internalSlugs[4] ? UserCategory.Lobby
            : slug == internalSlugs[5] ? UserCategory.Business
            : slug == internalSlugs[6] ? UserCategory.Journalist
            : UserCategory.Unspecified;

        private static UsersToCheck _twitterUsers = new UsersToCheck();

        public static IServiceCollection AddUsersToCheck(this IServiceCollection services, string directoryPath)
        {
            foreach (var slug in internalSlugs)
            {
                var internalPath = Path.Combine(directoryPath, $"internal-selected-{slug}.csv");
                if (File.Exists(internalPath))
                {
                    var updatedUsers = File.ReadAllLines(internalPath).Select(l => JsonConvert.DeserializeObject<TwitterUser>(l)).ToList();
                    _twitterUsers.Add(SlugToCategory(slug), updatedUsers);
                }
            }

            services.AddSingleton(_twitterUsers);

            return services;
        }

        public static IServiceCollection AddListsToCheck(this IServiceCollection services)
        {
            services.AddSingleton(
                new ListsToCheck(new[]
                {
                    ("ernstpoulsen", "valg-folketinget-2019", UserCategory.Politician, "valg-folketinget-2019"),
                    ("ernstpoulsen", "valg-europaparlament-2019", UserCategory.Politician, "valg-europaparlament-2019"),
                    ("ernstpoulsen", "region-nordjylland", UserCategory.Politician, "region-nordjylland"),
                    ("ernstpoulsen", "region-midtjylland", UserCategory.Politician, "region-midtjylland"),
                    ("ernstpoulsen", "region-syddanmark", UserCategory.Politician, "region-syddanmark"),
                    ("ernstpoulsen", "region-sjælland", UserCategory.Politician, "region-sj-lland"),
                    ("ernstpoulsen", "region-hovedstaden", UserCategory.Politician, "region-hovedstaden"),
                    ("ernstpoulsen", "byrådsmedlemmer-i-danmark", UserCategory.Politician, "byr-dsmedlemmer-i-danmark"),
                    ("ernstpoulsen", "politiske-partier", UserCategory.Politician, "politiske-partier"),
                    ("ernstpoulsen", "off-myndigheder-topfolk", UserCategory.Public, "off-myndigheder-topfolk"),
                    ("ernstpoulsen", "off-myndigheder", UserCategory.Public, "off-myndigheder"),
                    ("ernstpoulsen", "fagforeninger-og-topfolk", UserCategory.Union, "fagforeninger-og-topfolk"),
                    ("ernstpoulsen", "fagforeninger", UserCategory.Union, "fagforeninger"),
                    ("ernstpoulsen", "firmaer-større-topfolk", UserCategory.Business, "firmaer-st-rre-topfolk"),
                    ("ernstpoulsen", "interesseorg-topfolk", UserCategory.Lobby, "interesseorg-topfolk"),
                    ("ernstpoulsen", "interesseorganisationer", UserCategory.Lobby, "interesseorganisationer"),
                    ("ernstpoulsen", "kommunikationsfolk-dk", UserCategory.Spin, "kommunikationsfolk-dk"),
                    ("ernstpoulsen", "danske-journalister", UserCategory.Journalist, "danske-journalister")
                })
            );
            return services;
        }
    }
}
