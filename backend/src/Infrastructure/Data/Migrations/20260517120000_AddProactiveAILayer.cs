using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Backend.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProactiveAILayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AIMemories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    MemoryType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RelevanceScore = table.Column<int>(type: "integer", nullable: false),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    LastObservedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIMemories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIMemories_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FamilyItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    ChildId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyItems_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FamilyItems_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaveHomeChecklists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FamilyId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ItemsJson = table.Column<string>(type: "jsonb", nullable: false),
                    TriggerEventTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    EventStartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DispatchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveHomeChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveHomeChecklists_Families_FamilyId",
                        column: x => x.FamilyId,
                        principalTable: "Families",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Indexes for AIMemories
            migrationBuilder.CreateIndex(
                name: "IX_AIMemories_FamilyId",
                table: "AIMemories",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_AIMemories_FamilyId_Key",
                table: "AIMemories",
                columns: new[] { "FamilyId", "Key" },
                unique: true);

            // Indexes for FamilyItems
            migrationBuilder.CreateIndex(
                name: "IX_FamilyItems_FamilyId",
                table: "FamilyItems",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyItems_FamilyId_IsActive",
                table: "FamilyItems",
                columns: new[] { "FamilyId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_FamilyItems_ChildId",
                table: "FamilyItems",
                column: "ChildId");

            // Indexes for LeaveHomeChecklists
            migrationBuilder.CreateIndex(
                name: "IX_LeaveHomeChecklists_FamilyId",
                table: "LeaveHomeChecklists",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveHomeChecklists_FamilyId_Date",
                table: "LeaveHomeChecklists",
                columns: new[] { "FamilyId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AIMemories");
            migrationBuilder.DropTable(name: "FamilyItems");
            migrationBuilder.DropTable(name: "LeaveHomeChecklists");
        }
    }
}
