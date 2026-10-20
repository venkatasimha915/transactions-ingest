using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transactions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Trigger = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Inserted = table.Column<int>(type: "INTEGER", nullable: false),
                    Updated = table.Column<int>(type: "INTEGER", nullable: false),
                    Revoked = table.Column<int>(type: "INTEGER", nullable: false),
                    Reactivated = table.Column<int>(type: "INTEGER", nullable: false),
                    Finalized = table.Column<int>(type: "INTEGER", nullable: false),
                    Unchanged = table.Column<int>(type: "INTEGER", nullable: false),
                    Rejected = table.Column<int>(type: "INTEGER", nullable: false),
                    FailedSources = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    SourceSystem = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TransactionId = table.Column<int>(type: "INTEGER", nullable: false),
                    CardNumber = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    LocationCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ProductName = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Amount = table.Column<long>(type: "INTEGER", nullable: false),
                    TransactionTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Revision = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TransactionAudits",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TransactionRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    ImportRunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    FieldName = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    OldValue = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    NewValue = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ChangedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransactionAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransactionAudits_ImportRuns_ImportRunId",
                        column: x => x.ImportRunId,
                        principalTable: "ImportRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransactionAudits_Transactions_TransactionRecordId",
                        column: x => x.TransactionRecordId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportRuns_StartedAtUtc",
                table: "ImportRuns",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAudits_ImportRunId",
                table: "TransactionAudits",
                column: "ImportRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionAudits_TransactionRecordId",
                table: "TransactionAudits",
                column: "TransactionRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SourceSystem_Status",
                table: "Transactions",
                columns: new[] { "SourceSystem", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_SourceSystem_TransactionId",
                table: "Transactions",
                columns: new[] { "SourceSystem", "TransactionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionTime",
                table: "Transactions",
                column: "TransactionTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransactionAudits");

            migrationBuilder.DropTable(
                name: "ImportRuns");

            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
