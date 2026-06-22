using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToSubject : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastModifiedByUserId",
                table: "SystemSettings",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Subjects",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Subjects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AddedByUserId",
                table: "AllowedEmails",
                type: "nvarchar(36)",
                maxLength: 36,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SystemSettings_LastModifiedByUserId",
                table: "SystemSettings",
                column: "LastModifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_CreatedByUserId",
                table: "Subjects",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AllowedEmails_AddedByUserId",
                table: "AllowedEmails",
                column: "AddedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AllowedEmails_Users_AddedByUserId",
                table: "AllowedEmails",
                column: "AddedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Subjects_Users_CreatedByUserId",
                table: "Subjects",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Users_LastModifiedByUserId",
                table: "SystemSettings",
                column: "LastModifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AllowedEmails_Users_AddedByUserId",
                table: "AllowedEmails");

            migrationBuilder.DropForeignKey(
                name: "FK_Subjects_Users_CreatedByUserId",
                table: "Subjects");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Users_LastModifiedByUserId",
                table: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_SystemSettings_LastModifiedByUserId",
                table: "SystemSettings");

            migrationBuilder.DropIndex(
                name: "IX_Subjects_CreatedByUserId",
                table: "Subjects");

            migrationBuilder.DropIndex(
                name: "IX_AllowedEmails_AddedByUserId",
                table: "AllowedEmails");

            migrationBuilder.DropColumn(
                name: "LastModifiedByUserId",
                table: "SystemSettings");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Subjects");

            migrationBuilder.DropColumn(
                name: "AddedByUserId",
                table: "AllowedEmails");
        }
    }
}
