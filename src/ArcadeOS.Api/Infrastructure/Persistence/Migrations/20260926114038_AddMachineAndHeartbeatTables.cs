using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArcadeOS.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMachineAndHeartbeatTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "machines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    credit_cost = table.Column<decimal>(type: "numeric(6,2)", nullable: false, defaultValue: 1.0m),
                    ticket_payout = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    qr_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "machine_heartbeats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    machine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    credit_balance = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    play_count = table.Column<int>(type: "integer", nullable: true),
                    error_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_machine_heartbeats", x => x.id);
                    table.ForeignKey(
                        name: "FK_machine_heartbeats_machines_machine_id",
                        column: x => x.machine_id,
                        principalTable: "machines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_heartbeats_machine_received",
                table: "machine_heartbeats",
                columns: new[] { "machine_id", "received_at" });

            migrationBuilder.CreateIndex(
                name: "IX_machines_qr_code",
                table: "machines",
                column: "qr_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_machines_serial_number",
                table: "machines",
                column: "serial_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "machine_heartbeats");

            migrationBuilder.DropTable(
                name: "machines");
        }
    }
}
