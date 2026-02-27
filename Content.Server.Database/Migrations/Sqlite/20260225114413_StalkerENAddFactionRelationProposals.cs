using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class StalkerENAddFactionRelationProposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stalker_faction_relation_proposals",
                columns: table => new
                {
                    initiating_faction = table.Column<string>(type: "TEXT", nullable: false),
                    target_faction = table.Column<string>(type: "TEXT", nullable: false),
                    proposed_relation_type = table.Column<int>(type: "INTEGER", nullable: false),
                    custom_message = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    broadcast = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stalker_faction_relation_proposals", x => new { x.initiating_faction, x.target_faction });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stalker_faction_relation_proposals");
        }
    }
}
