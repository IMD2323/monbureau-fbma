using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MonBureau.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExpensesAppointmentsDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ContactEmail = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ContactPhone = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Number = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<int>(type: "INTEGER", nullable: false),
                    CourtName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CourtRoom = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CourtAddress = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CourtContact = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    OpeningDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosingDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cases_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Appointments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ReminderEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReminderMinutesBefore = table.Column<int>(type: "INTEGER", nullable: false),
                    ReminderSentAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Attendees = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Appointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Appointments_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CaseItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseItems_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FileExtension = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    MimeType = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    UploadDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    UploadedByClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsConfidential = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_Clients_UploadedByClientId",
                        column: x => x.UploadedByClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    PaymentMethod = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Recipient = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    ReceiptPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsPaid = table.Column<bool>(type: "INTEGER", nullable: false),
                    CaseId = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedByClientId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Expenses_Clients_AddedByClientId",
                        column: x => x.AddedByClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CaseId",
                table: "Appointments",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CaseId_StartTime",
                table: "Appointments",
                columns: new[] { "CaseId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CreatedAt",
                table: "Appointments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_ReminderEnabled",
                table: "Appointments",
                column: "ReminderEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StartTime",
                table: "Appointments",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_StartTime_Status",
                table: "Appointments",
                columns: new[] { "StartTime", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Status",
                table: "Appointments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_Type",
                table: "Appointments",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_CaseId",
                table: "CaseItems",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_CaseId_Type_Date",
                table: "CaseItems",
                columns: new[] { "CaseId", "Type", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_CreatedAt",
                table: "CaseItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_Date",
                table: "CaseItems",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_Type",
                table: "CaseItems",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_CaseItems_Type_Date",
                table: "CaseItems",
                columns: new[] { "Type", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_ClientId",
                table: "Cases",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_ClientId_Status",
                table: "Cases",
                columns: new[] { "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_CreatedAt",
                table: "Cases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Number",
                table: "Cases",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cases_OpeningDate",
                table: "Cases",
                column: "OpeningDate");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Status",
                table: "Cases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Status_OpeningDate",
                table: "Cases",
                columns: new[] { "Status", "OpeningDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_ContactEmail",
                table: "Clients",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CreatedAt",
                table: "Clients",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_FirstName_LastName",
                table: "Clients",
                columns: new[] { "FirstName", "LastName" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CaseId",
                table: "Documents",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CaseId_UploadDate",
                table: "Documents",
                columns: new[] { "CaseId", "UploadDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Documents_Category",
                table: "Documents",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CreatedAt",
                table: "Documents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_IsConfidential",
                table: "Documents",
                column: "IsConfidential");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadDate",
                table: "Documents",
                column: "UploadDate");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedByClientId",
                table: "Documents",
                column: "UploadedByClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_AddedByClientId",
                table: "Expenses",
                column: "AddedByClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CaseId",
                table: "Expenses",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CaseId_Date",
                table: "Expenses",
                columns: new[] { "CaseId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Category",
                table: "Expenses",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_CreatedAt",
                table: "Expenses",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Date",
                table: "Expenses",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_IsPaid",
                table: "Expenses",
                column: "IsPaid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Appointments");

            migrationBuilder.DropTable(
                name: "CaseItems");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "Clients");
        }
    }
}
