using DemokratiskDialog.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using NodaTime;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DemokratiskDialog.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var instantConversion = new ValueConverter<Instant, long>(
                v => v.ToUnixTimeMilliseconds(),
                v => Instant.FromUnixTimeMilliseconds(v)
            );

            modelBuilder
                .Entity<Block>()
                .Property(e => e.FirstSeen)
                .HasConversion(instantConversion);
            modelBuilder
                .Entity<Block>()
                .Property(e => e.Checked)
                .HasConversion(instantConversion);

            modelBuilder
                .Entity<ArchivedBlock>()
                .Property(e => e.FirstSeen)
                .HasConversion(instantConversion);
            modelBuilder
                .Entity<ArchivedBlock>()
                .Property(e => e.Checked)
                .HasConversion(instantConversion);
            modelBuilder
                .Entity<ArchivedBlock>()
                .Property(e => e.VerifiedGone)
                .HasConversion(instantConversion);

            modelBuilder
                .Entity<ExceptionLog>()
                .Property(e => e.Timestamp)
                .HasConversion(instantConversion);

            modelBuilder
                .Entity<CheckBlockedJob>()
                .Property(e => e.LastUpdate)
                .HasConversion(instantConversion);

            modelBuilder
                .Entity<ContinuousCheckBlockedJob>()
                .Property(e => e.LastUpdate)
                .HasConversion(instantConversion);

            //modelBuilder
            //    .Query<BlockCounts>().ToView("Blocks");
            //modelBuilder
            //    .Query<UserBlock>().ToView("Blocks");

            base.OnModelCreating(modelBuilder);
        }
        public DbSet<Block> Blocks { get; set; }
        public DbSet<ArchivedBlock> ArchivedBlocks { get; set; }
        public DbSet<CheckBlockedJob> Jobs { get; set; }
        public DbSet<ContinuousCheckBlockedJob> ContinuousJobs { get; set; }
        
        public DbSet<ExceptionLog> ExceptionLogs { get; set; }

        public DbQuery<BlockCounts> BlockCounts { get; set; }
        public DbQuery<UserBlock> UserBlocks { get; set; }


        private ExceptionLog GetBaseExceptionLog(string identifier, string path, Exception exception, IClock clock)
            => new ExceptionLog
            {
                RequestIdentifier = identifier,
                Route = path,
                ErrorMessage = exception?.Message,
                StackTrace = exception?.StackTrace,
                ExceptionType = exception?.GetType().Name,
                Timestamp = clock.GetCurrentInstant()
            };
        public Task LogException(string identifier, string path, Exception exception, IClock clock, CancellationToken cancellationToken)
        {
            ExceptionLogs.Add(GetBaseExceptionLog(identifier, path, exception, clock));
            return SaveChangesAsync(cancellationToken);
        }
        public Task LogException<T>(string identifier, string path, Exception exception, T data, IClock clock, CancellationToken cancellationToken)
        {
            var log = GetBaseExceptionLog(identifier, path, exception, clock);
            log.Data = JsonConvert.SerializeObject(data);
            ExceptionLogs.Add(log);
            return SaveChangesAsync(cancellationToken);
        }
    }
}
