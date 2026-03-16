using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudentTestingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddSuspiciousFlagsToAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Courses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Courses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Groups",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    MaxScore = table.Column<double>(type: "REAL", nullable: false),
                    MaxAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeLimitMinutes = table.Column<int>(type: "INTEGER", nullable: false),
                    ShuffleQuestions = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShuffleAnswers = table.Column<bool>(type: "INTEGER", nullable: false),
                    OneQuestionPerPage = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreventBackNavigation = table.Column<bool>(type: "INTEGER", nullable: false),
                    PassingScorePercent = table.Column<int>(type: "INTEGER", nullable: false),
                    ShowCorrectAnswers = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowResultImmediately = table.Column<bool>(type: "INTEGER", nullable: false),
                    AvailableFrom = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AvailableUntil = table.Column<DateTime>(type: "TEXT", nullable: true),
                    AccessPassword = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    DetectTabSwitch = table.Column<bool>(type: "INTEGER", nullable: false),
                    Instructions = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    DisableCopyPaste = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSuspicious = table.Column<bool>(type: "INTEGER", nullable: false),
                    SuspiciousReason = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CourseId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tests_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CourseGroups",
                columns: table => new
                {
                    CourseId = table.Column<int>(type: "INTEGER", nullable: false),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseGroups", x => new { x.CourseId, x.GroupId });
                    table.ForeignKey(
                        name: "FK_CourseGroups_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseGroups_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    GroupNumber = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    GroupId = table.Column<int>(type: "INTEGER", nullable: true),
                    PasswordHash = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    IsBlocked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "Groups",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TestElements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    QuestionText = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CorrectAnswer = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    OptionsJson = table.Column<string>(type: "TEXT", nullable: true),
                    Weight = table.Column<double>(type: "REAL", nullable: true),
                    Hint = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Explanation = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    TimeLimit = table.Column<int>(type: "INTEGER", nullable: false),
                    Tolerance = table.Column<double>(type: "REAL", nullable: true),
                    PenaltyPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    AllowPartialCredit = table.Column<bool>(type: "INTEGER", nullable: false),
                    ImageUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Order = table.Column<int>(type: "INTEGER", nullable: false),
                    TestId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestElements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestElements_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RequestedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsProcessed = table.Column<bool>(type: "INTEGER", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetRequests_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StudentResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BestScore = table.Column<double>(type: "REAL", nullable: false),
                    LastScore = table.Column<double>(type: "REAL", nullable: false),
                    AttemptsUsed = table.Column<int>(type: "INTEGER", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPassed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TotalTimeSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<int>(type: "INTEGER", nullable: false),
                    TestId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentResults_Tests_TestId",
                        column: x => x.TestId,
                        principalTable: "Tests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StudentResults_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    CorrectAnswers = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalQuestions = table.Column<int>(type: "INTEGER", nullable: false),
                    TimeSpentSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AnswersJson = table.Column<string>(type: "TEXT", nullable: true),
                    TabSwitchCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsVoided = table.Column<bool>(type: "INTEGER", nullable: false),
                    VoidReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    StudentResultId = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSuspicious = table.Column<bool>(type: "INTEGER", nullable: false),
                    SuspiciousReason = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestAttempts_StudentResults_StudentResultId",
                        column: x => x.StudentResultId,
                        principalTable: "StudentResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "FullName", "GroupId", "GroupNumber", "IsBlocked", "PasswordHash", "Role" },
                values: new object[] { 1, "vma@test.ru", "Администратор", null, null, false, "JAvlGPq9JyTdtvBO6x2llnRI1+gxwIyPqCKAn3THIKk=", 1 });

            migrationBuilder.CreateIndex(
                name: "IX_CourseGroups_GroupId",
                table: "CourseGroups",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetRequests_UserId",
                table: "PasswordResetRequests",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentResults_TestId",
                table: "StudentResults",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentResults_UserId_TestId",
                table: "StudentResults",
                columns: new[] { "UserId", "TestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestAttempts_StudentResultId_AttemptNumber",
                table: "TestAttempts",
                columns: new[] { "StudentResultId", "AttemptNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TestElements_TestId_Order",
                table: "TestElements",
                columns: new[] { "TestId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_Tests_CourseId",
                table: "Tests",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GroupId",
                table: "Users",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_GroupNumber",
                table: "Users",
                column: "GroupNumber");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseGroups");

            migrationBuilder.DropTable(
                name: "PasswordResetRequests");

            migrationBuilder.DropTable(
                name: "TestAttempts");

            migrationBuilder.DropTable(
                name: "TestElements");

            migrationBuilder.DropTable(
                name: "StudentResults");

            migrationBuilder.DropTable(
                name: "Tests");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Courses");

            migrationBuilder.DropTable(
                name: "Groups");
        }
    }
}
