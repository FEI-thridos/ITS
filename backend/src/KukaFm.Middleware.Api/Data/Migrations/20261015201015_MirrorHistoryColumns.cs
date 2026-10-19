using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KukaFm.Middleware.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class MirrorHistoryColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "robot_code",
                table: "robot_status_history",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "robot_type",
                table: "robot_status_history",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                table: "mission_status_history",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "container_code",
                table: "mission_status_history",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "created_at",
                table: "mission_status_history",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "mission_code",
                table: "mission_status_history",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "mission_type",
                table: "mission_status_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<short>(
                name: "priority",
                table: "mission_status_history",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<string>(
                name: "request_id",
                table: "mission_status_history",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "start_node",
                table: "mission_status_history",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "target_node",
                table: "mission_status_history",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "robot_code",
                table: "robot_status_history");

            migrationBuilder.DropColumn(
                name: "robot_type",
                table: "robot_status_history");

            migrationBuilder.DropColumn(
                name: "completed_at",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "container_code",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "created_at",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "mission_code",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "mission_type",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "request_id",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "start_node",
                table: "mission_status_history");

            migrationBuilder.DropColumn(
                name: "target_node",
                table: "mission_status_history");
        }
    }
}
