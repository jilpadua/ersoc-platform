using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ersms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3HardeningAccountingPrep : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "SaleLines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PurchaseReceives",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedByUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseReceives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseReceives_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceives_OrganizationId_PurchaseOrderId",
                table: "PurchaseReceives",
                columns: new[] { "OrganizationId", "PurchaseOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseReceives_PurchaseOrderId",
                table: "PurchaseReceives",
                column: "PurchaseOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PurchaseReceives");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "SaleLines");
        }
    }
}
