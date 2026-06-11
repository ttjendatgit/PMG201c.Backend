using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMG201c.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAiGradingJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GradingJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TotalSubmissions = table.Column<int>(type: "int", nullable: false),
                    ProcessedSubmissions = table.Column<int>(type: "int", nullable: false),
                    FailedSubmissions = table.Column<int>(type: "int", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingJobs_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GradingResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssessmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradingJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalRawScore = table.Column<double>(type: "float", nullable: false),
                    TotalConvertedScore = table.Column<double>(type: "float", nullable: false),
                    AiOverallComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AiModel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingResults_Assessments_AssessmentId",
                        column: x => x.AssessmentId,
                        principalTable: "Assessments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GradingResults_GradingJobs_GradingJobId",
                        column: x => x.GradingJobId,
                        principalTable: "GradingJobs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GradingResults_Submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "Submissions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GradingResultItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradingResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RubricItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuestionNo = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    MaxRawScore = table.Column<double>(type: "float", nullable: false),
                    AwardedRawScore = table.Column<double>(type: "float", nullable: false),
                    AwardedConvertedScore = table.Column<double>(type: "float", nullable: false),
                    AiComment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GradingResultItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GradingResultItems_GradingResults_GradingResultId",
                        column: x => x.GradingResultId,
                        principalTable: "GradingResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GradingJobs_AssessmentId",
                table: "GradingJobs",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingResultItems_GradingResultId",
                table: "GradingResultItems",
                column: "GradingResultId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingResults_AssessmentId",
                table: "GradingResults",
                column: "AssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingResults_GradingJobId",
                table: "GradingResults",
                column: "GradingJobId");

            migrationBuilder.CreateIndex(
                name: "IX_GradingResults_SubmissionId",
                table: "GradingResults",
                column: "SubmissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GradingResultItems");

            migrationBuilder.DropTable(
                name: "GradingResults");

            migrationBuilder.DropTable(
                name: "GradingJobs");
        }
    }
}
