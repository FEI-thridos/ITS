using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KukaFm.Middleware.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveMissionContainerAndPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "container_code",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "container_code",
                table: "mission");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "mission");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "container_code",
                table: "mission_status_history",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<short>(
                name: "priority",
                table: "mission_status_history",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "container_code",
                table: "mission",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<short>(
                name: "priority",
                table: "mission",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }
    }
}
