using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DemokratiskDialog.Migrations
{
    public partial class ContinuousJobs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FirstSeen",
                table: "Blocks",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ArchivedBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    UserId = table.Column<string>(nullable: true),
                    BlockedByTwitterId = table.Column<string>(nullable: true),
                    FirstSeen = table.Column<long>(nullable: false),
                    Checked = table.Column<long>(nullable: false),
                    VerifiedGone = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedBlocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArchivedBlocks_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContinuousJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    LastUpdate = table.Column<long>(nullable: false),
                    State = table.Column<int>(nullable: false),
                    Email = table.Column<string>(nullable: true),
                    CheckingForUserId = table.Column<string>(nullable: true),
                    CheckingForTwitterId = table.Column<string>(nullable: true),
                    CheckingForScreenName = table.Column<string>(nullable: true),
                    AccessToken = table.Column<string>(nullable: true),
                    AccessTokenSecret = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContinuousJobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedBlocks_UserId",
                table: "ArchivedBlocks",
                column: "UserId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedBlocks");

            migrationBuilder.DropTable(
                name: "ContinuousJobs");

            migrationBuilder.DropColumn(
                name: "FirstSeen",
                table: "Blocks");
        }
    }
}
