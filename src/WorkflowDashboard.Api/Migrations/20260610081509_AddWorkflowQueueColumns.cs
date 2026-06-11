using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowDashboard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowQueueColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "RepositoryId",
                table: "Workflows",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.AddColumn<string>(
                name: "BrokenReason",
                table: "Workflows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QueueReason",
                table: "Workflows",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAt",
                table: "Workflows",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BrokenReason",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "QueueReason",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "QueuedAt",
                table: "Workflows");

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryId",
                table: "Workflows",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }
    }
}
