using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorkflowDashboard.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRunnerSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentRunnerSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CliTool = table.Column<string>(type: "TEXT", nullable: false),
                    Executable = table.Column<string>(type: "TEXT", nullable: false),
                    ExtraArgsJson = table.Column<string>(type: "TEXT", nullable: false),
                    InstructionsRelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    InputFileRelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    InteractiveTerminal = table.Column<bool>(type: "INTEGER", nullable: false),
                    InteractiveStartPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    Enabled = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRunnerSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRunnerSettings");
        }
    }
}
