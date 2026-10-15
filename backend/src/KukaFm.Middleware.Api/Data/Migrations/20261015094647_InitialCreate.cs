using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace KukaFm.Middleware.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "robot",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    robot_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    robot_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    status_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    battery_level = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    map_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    node_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mission_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_robot", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mission",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    request_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    mission_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    start_node = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_node = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    container_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    priority = table.Column<short>(type: "smallint", nullable: false),
                    robot_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    status_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mission", x => x.id);
                    table.ForeignKey(
                        name: "fk_mission_robots_robot_id",
                        column: x => x.robot_id,
                        principalTable: "robot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "robot_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    robot_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    status_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    battery_level = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    map_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    node_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    mission_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_robot_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_robot_status_history_robot_robot_id",
                        column: x => x.robot_id,
                        principalTable: "robot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mission_status_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    mission_id = table.Column<long>(type: "bigint", nullable: false),
                    robot_id = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    status_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mission_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_mission_status_history_mission_mission_id",
                        column: x => x.mission_id,
                        principalTable: "mission",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_mission_status_history_robots_robot_id",
                        column: x => x.robot_id,
                        principalTable: "robot",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mission_mission_code",
                table: "mission",
                column: "mission_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mission_request_id",
                table: "mission",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mission_robot_id",
                table: "mission",
                column: "robot_id");

            migrationBuilder.CreateIndex(
                name: "ix_mission_status_history_mission_id",
                table: "mission_status_history",
                column: "mission_id");

            migrationBuilder.CreateIndex(
                name: "ix_mission_status_history_robot_id",
                table: "mission_status_history",
                column: "robot_id");

            migrationBuilder.CreateIndex(
                name: "ix_robot_robot_code",
                table: "robot",
                column: "robot_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_robot_status_history_robot_id",
                table: "robot_status_history",
                column: "robot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mission_status_history");

            migrationBuilder.DropTable(
                name: "robot_status_history");

            migrationBuilder.DropTable(
                name: "mission");

            migrationBuilder.DropTable(
                name: "robot");
        }
    }
}
