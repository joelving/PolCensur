using DemokratiskDialog.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NodaTime;

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
                .Property(e => e.Checked)
                .HasConversion(instantConversion);

            base.OnModelCreating(modelBuilder);
        }
        public DbSet<Block> Blocks { get; set; }
    }
}
