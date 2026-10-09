using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LGMS.Core.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GitHubAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Host = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastVerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitHubAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GitOperationLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepositoryId = table.Column<int>(type: "int", nullable: true),
                    Operation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GitOperationLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LocalRepositories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LocalPath = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CurrentBranch = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastScannedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalRepositories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GitHubAccounts_Host_Username",
                table: "GitHubAccounts",
                columns: new[] { "Host", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GitOperationLogs_CreatedAt",
                table: "GitOperationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LocalRepositories_LocalPath",
                table: "LocalRepositories",
                column: "LocalPath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GitHubAccounts");

            migrationBuilder.DropTable(
                name: "GitOperationLogs");

            migrationBuilder.DropTable(
                name: "LocalRepositories");
        }
    }
}
