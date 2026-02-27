using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class StalkerENAddFactionRelations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stalker_faction_relations",
                columns: table => new
                {
                    faction_a = table.Column<string>(type: "TEXT", nullable: false),
                    faction_b = table.Column<string>(type: "TEXT", nullable: false),
                    relation_type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stalker_faction_relations", x => new { x.faction_a, x.faction_b });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stalker_faction_relations");
        }
    }
}
