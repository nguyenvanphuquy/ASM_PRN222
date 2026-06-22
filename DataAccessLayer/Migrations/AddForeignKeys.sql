-- ==========================================
-- SQL Script: Add Foreign Keys to ChatBotPRN222 Database
-- Every table will have at least 1 Foreign Key.
-- ==========================================

USE ChatBotPRN222;
GO

-- 1. Thêm các cột khoá ngoại mới nếu chưa tồn tại
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Subjects') AND name = 'CreatedByUserId')
BEGIN
    ALTER TABLE Subjects ADD CreatedByUserId NVARCHAR(36) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('SystemSettings') AND name = 'LastModifiedByUserId')
BEGIN
    ALTER TABLE SystemSettings ADD LastModifiedByUserId NVARCHAR(36) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('AllowedEmails') AND name = 'AddedByUserId')
BEGIN
    ALTER TABLE AllowedEmails ADD AddedByUserId NVARCHAR(36) NULL;
END
GO

-- Thủ tục drop FK nếu đã tồn tại để chạy script lặp lại không bị lỗi trùng lặp
CREATE OR ALTER PROCEDURE #DropForeignKeyIfExists 
    @TableName NVARCHAR(128), 
    @ConstraintName NVARCHAR(128)
AS
BEGIN
    IF EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID(@ConstraintName) AND parent_object_id = OBJECT_ID(@TableName))
    BEGIN
        DECLARE @Sql NVARCHAR(MAX) = 'ALTER TABLE ' + QUOTENAME(@TableName) + ' DROP CONSTRAINT ' + QUOTENAME(@ConstraintName);
        EXEC sp_executesql @Sql;
    END
END
GO

-- Thực thi drop các ràng buộc cũ trước khi định nghĩa lại
EXEC #DropForeignKeyIfExists 'Users', 'FK_Users_Subjects_AssignedSubjectId';
EXEC #DropForeignKeyIfExists 'Subjects', 'FK_Subjects_Users_CreatedByUserId';
EXEC #DropForeignKeyIfExists 'Chapters', 'FK_Chapters_Subjects_SubjectId';
EXEC #DropForeignKeyIfExists 'Documents', 'FK_Documents_Subjects_SubjectId';
EXEC #DropForeignKeyIfExists 'Documents', 'FK_Documents_Chapters_ChapterId';
EXEC #DropForeignKeyIfExists 'Documents', 'FK_Documents_Users_UploadedBy';
EXEC #DropForeignKeyIfExists 'DocumentChunks', 'FK_DocumentChunks_Documents_DocumentId';
EXEC #DropForeignKeyIfExists 'DocumentChunks', 'FK_DocumentChunks_Subjects_SubjectId';
EXEC #DropForeignKeyIfExists 'SystemSettings', 'FK_SystemSettings_Users_LastModifiedByUserId';
EXEC #DropForeignKeyIfExists 'AllowedEmails', 'FK_AllowedEmails_Users_AddedByUserId';
EXEC #DropForeignKeyIfExists 'ChatSessions', 'FK_ChatSessions_Users_UserId';
EXEC #DropForeignKeyIfExists 'ChatSessions', 'FK_ChatSessions_Subjects_SubjectId';
EXEC #DropForeignKeyIfExists 'ChatMessages', 'FK_ChatMessages_ChatSessions_SessionId';
EXEC #DropForeignKeyIfExists 'Feedbacks', 'FK_Feedbacks_Users_UserId';
EXEC #DropForeignKeyIfExists 'FeedbackReplies', 'FK_FeedbackReplies_Feedbacks_FeedbackId';
EXEC #DropForeignKeyIfExists 'FeedbackReplies', 'FK_FeedbackReplies_Users_UserId';
EXEC #DropForeignKeyIfExists 'Notifications', 'FK_Notifications_Users_UserId';
GO

-- ==========================================
-- 1.5. Dọn dẹp dữ liệu rác/mồ côi để tránh xung đột khoá ngoại (Constraint Conflicts)
-- ==========================================
DECLARE @AdminId NVARCHAR(36);
SELECT TOP 1 @AdminId = Id FROM Users WHERE Role = 'Admin' ORDER BY CreatedAt ASC;
IF @AdminId IS NULL SET @AdminId = '5A3DFB03-DBFC-4718-9358-2998F41C1FD6'; -- Fallback ID

-- Sửa/gán UserId mồ côi trong FeedbackReplies
UPDATE FeedbackReplies 
SET UserId = @AdminId 
WHERE UserId = '' OR UserId IS NULL OR UserId NOT IN (SELECT Id FROM Users);

-- Sửa AssignedSubjectId trong Users
UPDATE Users 
SET AssignedSubjectId = NULL 
WHERE AssignedSubjectId = '' OR AssignedSubjectId NOT IN (SELECT Id FROM Subjects);

-- Sửa ChapterId trong Documents
UPDATE Documents 
SET ChapterId = NULL 
WHERE ChapterId = '' OR ChapterId NOT IN (SELECT Id FROM Chapters);

-- Sửa UploadedBy trong Documents
UPDATE Documents 
SET UploadedBy = @AdminId 
WHERE UploadedBy = '' OR UploadedBy IS NULL OR UploadedBy NOT IN (SELECT Id FROM Users);

-- Sửa SubjectId trong ChatSessions
UPDATE ChatSessions 
SET SubjectId = NULL 
WHERE SubjectId = '' OR SubjectId NOT IN (SELECT Id FROM Subjects);

-- Xoá ChatSessions mồ côi
DELETE FROM ChatSessions WHERE UserId = '' OR UserId IS NULL OR UserId NOT IN (SELECT Id FROM Users);

-- Xoá ChatMessages mồ côi
DELETE FROM ChatMessages WHERE SessionId = '' OR SessionId IS NULL OR SessionId NOT IN (SELECT Id FROM ChatSessions);

-- Xoá DocumentChunks mồ côi
DELETE FROM DocumentChunks WHERE DocumentId = '' OR DocumentId IS NULL OR DocumentId NOT IN (SELECT Id FROM Documents);

-- Xoá Feedbacks mồ côi
DELETE FROM Feedbacks WHERE UserId = '' OR UserId IS NULL OR UserId NOT IN (SELECT Id FROM Users);

-- Xoá FeedbackReplies mồ côi
DELETE FROM FeedbackReplies WHERE FeedbackId = '' OR FeedbackId IS NULL OR FeedbackId NOT IN (SELECT Id FROM Feedbacks);

-- Xoá Notifications mồ côi
DELETE FROM Notifications WHERE UserId = '' OR UserId IS NULL OR UserId NOT IN (SELECT Id FROM Users);
GO

-- 2. Thêm các khoá ngoại (Foreign Keys) cho các bảng

-- Users -> Subjects
ALTER TABLE Users ADD CONSTRAINT FK_Users_Subjects_AssignedSubjectId 
FOREIGN KEY (AssignedSubjectId) REFERENCES Subjects(Id) ON DELETE SET NULL;

-- Subjects -> Users
ALTER TABLE Subjects ADD CONSTRAINT FK_Subjects_Users_CreatedByUserId 
FOREIGN KEY (CreatedByUserId) REFERENCES Users(Id) ON DELETE SET NULL;

-- Chapters -> Subjects
ALTER TABLE Chapters ADD CONSTRAINT FK_Chapters_Subjects_SubjectId 
FOREIGN KEY (SubjectId) REFERENCES Subjects(Id) ON DELETE CASCADE;

-- Documents -> Subjects, Chapters, Users
ALTER TABLE Documents ADD CONSTRAINT FK_Documents_Subjects_SubjectId 
FOREIGN KEY (SubjectId) REFERENCES Subjects(Id) ON DELETE CASCADE;

ALTER TABLE Documents ADD CONSTRAINT FK_Documents_Chapters_ChapterId 
FOREIGN KEY (ChapterId) REFERENCES Chapters(Id) ON DELETE NO ACTION;

ALTER TABLE Documents ADD CONSTRAINT FK_Documents_Users_UploadedBy 
FOREIGN KEY (UploadedBy) REFERENCES Users(Id) ON DELETE CASCADE;

-- DocumentChunks -> Documents, Subjects
ALTER TABLE DocumentChunks ADD CONSTRAINT FK_DocumentChunks_Documents_DocumentId 
FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE;

ALTER TABLE DocumentChunks ADD CONSTRAINT FK_DocumentChunks_Subjects_SubjectId 
FOREIGN KEY (SubjectId) REFERENCES Subjects(Id) ON DELETE NO ACTION;

-- SystemSettings -> Users
ALTER TABLE SystemSettings ADD CONSTRAINT FK_SystemSettings_Users_LastModifiedByUserId 
FOREIGN KEY (LastModifiedByUserId) REFERENCES Users(Id) ON DELETE SET NULL;

-- AllowedEmails -> Users
ALTER TABLE AllowedEmails ADD CONSTRAINT FK_AllowedEmails_Users_AddedByUserId 
FOREIGN KEY (AddedByUserId) REFERENCES Users(Id) ON DELETE SET NULL;

-- ChatSessions -> Users, Subjects
ALTER TABLE ChatSessions ADD CONSTRAINT FK_ChatSessions_Users_UserId 
FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE;

ALTER TABLE ChatSessions ADD CONSTRAINT FK_ChatSessions_Subjects_SubjectId 
FOREIGN KEY (SubjectId) REFERENCES Subjects(Id) ON DELETE SET NULL;

-- ChatMessages -> ChatSessions
ALTER TABLE ChatMessages ADD CONSTRAINT FK_ChatMessages_ChatSessions_SessionId 
FOREIGN KEY (SessionId) REFERENCES ChatSessions(Id) ON DELETE CASCADE;

-- Feedbacks -> Users
ALTER TABLE Feedbacks ADD CONSTRAINT FK_Feedbacks_Users_UserId 
FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE;

-- FeedbackReplies -> Feedbacks, Users
ALTER TABLE FeedbackReplies ADD CONSTRAINT FK_FeedbackReplies_Feedbacks_FeedbackId 
FOREIGN KEY (FeedbackId) REFERENCES Feedbacks(Id) ON DELETE CASCADE;

ALTER TABLE FeedbackReplies ADD CONSTRAINT FK_FeedbackReplies_Users_UserId 
FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION;

-- Notifications -> Users
ALTER TABLE Notifications ADD CONSTRAINT FK_Notifications_Users_UserId 
FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE;
GO
