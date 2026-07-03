using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataAccessLayer.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Users_AssignedSubjectId",
                table: "Users",
                column: "AssignedSubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedbackReplies_UserId",
                table: "FeedbackReplies",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_UploadedBy",
                table: "Documents",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_SubjectId",
                table: "ChatSessions",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Chapters_Subjects_SubjectId",
                table: "Chapters",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatSessions_SessionId",
                table: "ChatMessages",
                column: "SessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_Subjects_SubjectId",
                table: "ChatSessions",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_Users_UserId",
                table: "ChatSessions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentChunks_Documents_DocumentId",
                table: "DocumentChunks",
                column: "DocumentId",
                principalTable: "Documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DocumentChunks_Subjects_SubjectId",
                table: "DocumentChunks",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Chapters_ChapterId",
                table: "Documents",
                column: "ChapterId",
                principalTable: "Chapters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Subjects_SubjectId",
                table: "Documents",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Users_UploadedBy",
                table: "Documents",
                column: "UploadedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackReplies_Feedbacks_FeedbackId",
                table: "FeedbackReplies",
                column: "FeedbackId",
                principalTable: "Feedbacks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackReplies_Users_UserId",
                table: "FeedbackReplies",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Users_UserId",
                table: "Feedbacks",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Subjects_AssignedSubjectId",
                table: "Users",
                column: "AssignedSubjectId",
                principalTable: "Subjects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Chapters_Subjects_SubjectId",
                table: "Chapters");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatSessions_SessionId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_Subjects_SubjectId",
                table: "ChatSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_Users_UserId",
                table: "ChatSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentChunks_Documents_DocumentId",
                table: "DocumentChunks");

            migrationBuilder.DropForeignKey(
                name: "FK_DocumentChunks_Subjects_SubjectId",
                table: "DocumentChunks");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Chapters_ChapterId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Subjects_SubjectId",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Users_UploadedBy",
                table: "Documents");

            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackReplies_Feedbacks_FeedbackId",
                table: "FeedbackReplies");

            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackReplies_Users_UserId",
                table: "FeedbackReplies");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Users_UserId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Subjects_AssignedSubjectId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_AssignedSubjectId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_FeedbackReplies_UserId",
                table: "FeedbackReplies");

            migrationBuilder.DropIndex(
                name: "IX_Documents_UploadedBy",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_SubjectId",
                table: "ChatSessions");
        }
    }
}
