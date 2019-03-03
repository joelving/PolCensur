using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using DemokratiskDialog.Data;
using DemokratiskDialog.Models;
using DemokratiskDialog.Options;
using DemokratiskDialog.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NodaTime;
using Polly;

namespace DemokratiskDialog
{
    public class Startup
    {
        public Startup(IConfiguration configuration, IHostingEnvironment environment)
        {
            Configuration = configuration;
            Environment = environment;
        }

        public readonly IConfiguration Configuration;
        private readonly IHostingEnvironment Environment;

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContextPool<ApplicationDbContext>(options =>
                options.UseSqlServer(
                    Configuration.GetConnectionString("DefaultConnection")));

            services.AddDefaultIdentity<ApplicationUser>()
                .AddEntityFrameworkStores<ApplicationDbContext>();

            services.AddAuthentication().AddTwitter(twitterOptions =>
            {
                twitterOptions.ConsumerKey = Configuration["Twitter:ConsumerKey"];
                twitterOptions.ConsumerSecret = Configuration["Twitter:ConsumerSecret"];
                twitterOptions.SaveTokens = true;
            });

            services.AddMvc()
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Login", "/login");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Logout", "/logout");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/ExternalLogin", "/external-login");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/AccessDenied", "/access-denied");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Lockout", "/lockout");

                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Manage/Index", "/profile");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Manage/ExternalLogins", "/profile/external-logins");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Manage/Blocks", "/profile/blocks");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Manage/PersonalData", "/profile/personal-data");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Manage/DownloadPersonalData", "/profile/personal-data/download");
                    options.Conventions.AddAreaPageRoute("Identity", "/Account/Manage/DeletePersonalData", "/profile/personal-data/delete");
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
            
            services.AddDataProtection();

            services.AddSingleton<IBackgroundQueue<CheckBlockedJob>>(new CheckBlockedJobQueue(50));
            services.AddTransient<IBackgroundJobProcessor<CheckBlockedJob>, CheckBlockedJobProcessor>();
            services.AddHostedService<BackgroundQueueService<CheckBlockedJob>>();
            services.AddSingleton<IClock>(SystemClock.Instance);
            services.AddHttpClient("RetryBacking")
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler())
                .AddTransientHttpErrorPolicy(builder => builder.WaitAndRetryAsync(new[]
                {
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(4),
                    TimeSpan.FromSeconds(8)
                }));
            services.AddScoped<TwitterService>();
            services.AddScoped<TwitterAdminService>();
            services.AddSingleton<TwitterRateLimits>();
            services.AddScoped<EmailService>();

            services.Configure<TwitterApiOptions>(Configuration.GetSection("Twitter"));
            services.Configure<TwitterApiAdminOptions>(Configuration.GetSection("TwitterAdmin"));
            services.Configure<EmailServiceOptions>(Configuration.GetSection("EmailService"));
            
            var twitterHandles = new UsersToCheck(File.ReadAllLines(Path.Combine(Environment.ContentRootPath, "influencers.csv"))
                .Select(JsonConvert.DeserializeObject<Influencer>));
            services.AddSingleton(twitterHandles);
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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ApplicationDbContext dbContext)
        {
            dbContext.Database.Migrate();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
