using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DemokratiskDialog.Migrations
{
    public partial class PersistJobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    LastUpdate = table.Column<long>(nullable: false),
                    State = table.Column<int>(nullable: false),
                    CheckingForUserId = table.Column<string>(nullable: true),
                    CheckingForTwitterId = table.Column<string>(nullable: true),
                    CheckingForScreenName = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Jobs");
        }
    }
}
