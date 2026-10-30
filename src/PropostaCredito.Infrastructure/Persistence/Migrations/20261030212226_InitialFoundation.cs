using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PropostaCredito.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "PC_OWNER");

            migrationBuilder.CreateTable(
                name: "PLATFORM_METADATA",
                schema: "PC_OWNER",
                columns: table => new
                {
                    KEY = table.Column<string>(type: "NVARCHAR2(100)", maxLength: 100, nullable: false),
                    VALUE = table.Column<string>(type: "NVARCHAR2(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PLATFORM_METADATA", x => x.KEY);
                });

            migrationBuilder.InsertData(
                schema: "PC_OWNER",
                table: "PLATFORM_METADATA",
                columns: new[] { "KEY", "VALUE" },
                values: new object[] { "schema-version", "1" });

            migrationBuilder.Sql("GRANT SELECT ON PC_OWNER.PLATFORM_METADATA TO PC_APP");
            migrationBuilder.Sql("GRANT SELECT ON PC_OWNER.\"__EFMigrationsHistory\" TO PC_APP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PLATFORM_METADATA",
                schema: "PC_OWNER");
        }
    }
}
