using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    card_number_hash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    last4 = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    credit_limit_cents = table.Column<long>(type: "INTEGER", nullable: false),
                    created_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cards", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fx_rate_cache",
                columns: table => new
                {
                    currency_key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    record_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    exchange_rate = table.Column<decimal>(type: "NUMERIC", nullable: false),
                    cached_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fx_rate_cache", x => new { x.currency_key, x.record_date });
                });

            migrationBuilder.CreateTable(
                name: "purchases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    card_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    transaction_date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    amount_cents = table.Column<long>(type: "INTEGER", nullable: false),
                    created_utc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_purchases", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cards_card_number_hash",
                table: "cards",
                column: "card_number_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_fx_rate_cache_currency_key_record_date",
                table: "fx_rate_cache",
                columns: new[] { "currency_key", "record_date" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_purchases_card_id",
                table: "purchases",
                column: "card_id");

            migrationBuilder.CreateIndex(
                name: "IX_purchases_card_id_transaction_date",
                table: "purchases",
                columns: new[] { "card_id", "transaction_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "fx_rate_cache");

            migrationBuilder.DropTable(
                name: "purchases");
        }
    }
}
