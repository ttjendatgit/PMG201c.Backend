using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PMG201c.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherReviewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "FinalConvertedScore",
                table: "GradingResults",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FinalRawScore",
                table: "GradingResults",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizedAt",
                table: "GradingResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStatus",
                table: "GradingResults",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "AI_GRADED");

            migrationBuilder.Sql(
                "UPDATE GradingResults SET ReviewStatus = 'AI_GRADED' WHERE ReviewStatus = ''");

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "GradingResults",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ReviewedConvertedScore",
                table: "GradingResults",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ReviewedRawScore",
                table: "GradingResults",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherOverallComment",
                table: "GradingResults",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsScoreOverridden",
                table: "GradingResultItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "MaxConvertedScore",
                table: "GradingResultItems",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "ReviewedConvertedScore",
                table: "GradingResultItems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ReviewedRawScore",
                table: "GradingResultItems",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TeacherComment",
                table: "GradingResultItems",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalConvertedScore",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "FinalRawScore",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "FinalizedAt",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "ReviewStatus",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "ReviewedConvertedScore",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "ReviewedRawScore",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "TeacherOverallComment",
                table: "GradingResults");

            migrationBuilder.DropColumn(
                name: "IsScoreOverridden",
                table: "GradingResultItems");

            migrationBuilder.DropColumn(
                name: "MaxConvertedScore",
                table: "GradingResultItems");

            migrationBuilder.DropColumn(
                name: "ReviewedConvertedScore",
                table: "GradingResultItems");

            migrationBuilder.DropColumn(
                name: "ReviewedRawScore",
                table: "GradingResultItems");

            migrationBuilder.DropColumn(
                name: "TeacherComment",
                table: "GradingResultItems");
        }
    }
}
