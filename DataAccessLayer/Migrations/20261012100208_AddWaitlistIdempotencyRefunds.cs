using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistIdempotencyRefunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "refunded_visits",
                table: "enrollments",
                type: "integer",
                nullable: true);

            // решение: базы, засеянные до этой миграции, получают возврат по тому же правилу, что и сиды;
            // порог в 2 часа зафиксирован на момент выпуска миграции, дальше он берётся из Booking:RefundCutoffSeconds.
            migrationBuilder.Sql(
                """
                UPDATE enrollments e
                SET refunded_visits = CASE
                    WHEN c.is_cancelled OR c.starts_at - e.cancelled_at >= interval '2 hours' THEN e.seats
                    ELSE 0
                END
                FROM classes c
                WHERE c.id = e.class_id AND e.status = 'cancelled';
                """);

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    operation = table.Column<string>(type: "text", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: false),
                    response_body = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idempotency_keys", x => x.id);
                    table.ForeignKey(
                        name: "FK_idempotency_keys_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "waitlist_entries",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<long>(type: "bigint", nullable: false),
                    class_id = table.Column<long>(type: "bigint", nullable: false),
                    seats = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    enrollment_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waitlist_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_waitlist_entries_classes_class_id",
                        column: x => x.class_id,
                        principalTable: "classes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_waitlist_entries_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalTable: "enrollments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_waitlist_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_enrollments_class_id_active",
                table: "enrollments",
                column: "class_id",
                filter: "status = 'active'")
                .Annotation("Npgsql:IndexInclude", new[] { "seats" });

            migrationBuilder.CreateIndex(
                name: "IX_classes_starts_at_id_not_cancelled",
                table: "classes",
                columns: new[] { "starts_at", "id" },
                filter: "NOT is_cancelled");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_created_at",
                table: "idempotency_keys",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_idempotency_keys_user_id_operation_key",
                table: "idempotency_keys",
                columns: new[] { "user_id", "operation", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_class_id_queue_waiting",
                table: "waitlist_entries",
                columns: new[] { "class_id", "created_at", "id" },
                filter: "status = 'waiting'");

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_enrollment_id",
                table: "waitlist_entries",
                column: "enrollment_id");

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_user_id_class_id_waiting",
                table: "waitlist_entries",
                columns: new[] { "user_id", "class_id" },
                unique: true,
                filter: "status = 'waiting'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "idempotency_keys");

            migrationBuilder.DropTable(
                name: "waitlist_entries");

            migrationBuilder.DropIndex(
                name: "IX_enrollments_class_id_active",
                table: "enrollments");

            migrationBuilder.DropIndex(
                name: "IX_classes_starts_at_id_not_cancelled",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "refunded_visits",
                table: "enrollments");
        }
    }
}
