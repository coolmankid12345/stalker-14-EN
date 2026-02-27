using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
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
                    initiating_faction = table.Column<string>(type: "text", nullable: false),
                    target_faction = table.Column<string>(type: "text", nullable: false),
                    proposed_relation_type = table.Column<int>(type: "integer", nullable: false),
                    custom_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    broadcast = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
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
