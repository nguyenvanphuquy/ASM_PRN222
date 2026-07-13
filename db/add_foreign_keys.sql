/* =====================================================================
   ChatBotPRN222 — Add / re-connect ALL foreign keys
   Target : Microsoft SQL Server   (database: ChatBotPRN222)
   Author : generated to match DataAccessLayer/Context/AppDbContext.cs

   - Idempotent: every FK is guarded by IF NOT EXISTS on the EF
     constraint name, so you can paste & run this as many times as you
     want. FKs already created by EF (EnsureCreated) are skipped.
   - The 5 FKs at the bottom are the ones EF never declared
     (TokenUsageLogs, PackagePurchases.PackageId, ExperimentRuns) —
     those are what actually get added on an existing DB.
   - Orphan rows on NULLABLE columns are set to NULL first so the
     trusted (WITH CHECK) constraints won't fail. NOT-NULL columns that
     may contain legacy/empty values use WITH NOCHECK (enforced from now
     on, existing rows left untouched).

   ⚠ If your database name is different, change it on the USE line below.
   ===================================================================== */
USE ChatBotPRN222;
GO

SET NOCOUNT ON;

/* ---------------------------------------------------------------------
   0) Clean orphans on NULLABLE reference columns
      (rows pointing at a parent that no longer exists → set to NULL)
   --------------------------------------------------------------------- */
UPDATE dbo.TokenUsageLogs SET SessionId = NULL
 WHERE SessionId IS NOT NULL
   AND SessionId NOT IN (SELECT Id FROM dbo.ChatSessions);

UPDATE dbo.ExperimentRuns SET SubjectId = NULL
 WHERE SubjectId IS NOT NULL
   AND SubjectId NOT IN (SELECT Id FROM dbo.Subjects);
GO

/* =====================================================================
   1) FOREIGN KEYS ALREADY DEFINED IN THE EF MODEL
      (present if the DB was made by EnsureCreated; guarded = skipped)
   ===================================================================== */

/* Users */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Roles_RoleId')
    ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT FK_Users_Roles_RoleId
        FOREIGN KEY (RoleId) REFERENCES dbo.Roles (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Users_Subjects_AssignedSubjectId')
    ALTER TABLE dbo.Users WITH CHECK ADD CONSTRAINT FK_Users_Subjects_AssignedSubjectId
        FOREIGN KEY (AssignedSubjectId) REFERENCES dbo.Subjects (Id) ON DELETE NO ACTION;

/* Subjects */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Subjects_Users_CreatedByUserId')
    ALTER TABLE dbo.Subjects WITH CHECK ADD CONSTRAINT FK_Subjects_Users_CreatedByUserId
        FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users (Id) ON DELETE SET NULL;

/* Chapters */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Chapters_Subjects_SubjectId')
    ALTER TABLE dbo.Chapters WITH CHECK ADD CONSTRAINT FK_Chapters_Subjects_SubjectId
        FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (Id) ON DELETE NO ACTION;

/* Documents */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Documents_Subjects_SubjectId')
    ALTER TABLE dbo.Documents WITH CHECK ADD CONSTRAINT FK_Documents_Subjects_SubjectId
        FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Documents_Chapters_ChapterId')
    ALTER TABLE dbo.Documents WITH CHECK ADD CONSTRAINT FK_Documents_Chapters_ChapterId
        FOREIGN KEY (ChapterId) REFERENCES dbo.Chapters (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Documents_Users_UploadedBy')
    ALTER TABLE dbo.Documents WITH CHECK ADD CONSTRAINT FK_Documents_Users_UploadedBy
        FOREIGN KEY (UploadedBy) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

/* DocumentChunks */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DocumentChunks_Documents_DocumentId')
    ALTER TABLE dbo.DocumentChunks WITH CHECK ADD CONSTRAINT FK_DocumentChunks_Documents_DocumentId
        FOREIGN KEY (DocumentId) REFERENCES dbo.Documents (Id) ON DELETE CASCADE;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DocumentChunks_Subjects_SubjectId')
    ALTER TABLE dbo.DocumentChunks WITH CHECK ADD CONSTRAINT FK_DocumentChunks_Subjects_SubjectId
        FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (Id) ON DELETE NO ACTION;

/* SystemSettings */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SystemSettings_Users_LastModifiedByUserId')
    ALTER TABLE dbo.SystemSettings WITH CHECK ADD CONSTRAINT FK_SystemSettings_Users_LastModifiedByUserId
        FOREIGN KEY (LastModifiedByUserId) REFERENCES dbo.Users (Id) ON DELETE SET NULL;

/* ChatSessions */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatSessions_Users_UserId')
    ALTER TABLE dbo.ChatSessions WITH CHECK ADD CONSTRAINT FK_ChatSessions_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatSessions_Subjects_SubjectId')
    ALTER TABLE dbo.ChatSessions WITH CHECK ADD CONSTRAINT FK_ChatSessions_Subjects_SubjectId
        FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (Id) ON DELETE NO ACTION;

/* ChatMessages */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatMessages_ChatSessions_SessionId')
    ALTER TABLE dbo.ChatMessages WITH CHECK ADD CONSTRAINT FK_ChatMessages_ChatSessions_SessionId
        FOREIGN KEY (SessionId) REFERENCES dbo.ChatSessions (Id) ON DELETE CASCADE;

/* Feedbacks */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Feedbacks_Users_UserId')
    ALTER TABLE dbo.Feedbacks WITH CHECK ADD CONSTRAINT FK_Feedbacks_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

/* FeedbackReplies */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FeedbackReplies_Feedbacks_FeedbackId')
    ALTER TABLE dbo.FeedbackReplies WITH CHECK ADD CONSTRAINT FK_FeedbackReplies_Feedbacks_FeedbackId
        FOREIGN KEY (FeedbackId) REFERENCES dbo.Feedbacks (Id) ON DELETE CASCADE;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_FeedbackReplies_Users_UserId')
    ALTER TABLE dbo.FeedbackReplies WITH CHECK ADD CONSTRAINT FK_FeedbackReplies_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

/* AllowedEmails */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_AllowedEmails_Users_AddedByUserId')
    ALTER TABLE dbo.AllowedEmails WITH CHECK ADD CONSTRAINT FK_AllowedEmails_Users_AddedByUserId
        FOREIGN KEY (AddedByUserId) REFERENCES dbo.Users (Id) ON DELETE SET NULL;

/* Notifications */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Notifications_Users_UserId')
    ALTER TABLE dbo.Notifications WITH CHECK ADD CONSTRAINT FK_Notifications_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE;

/* LecturerSubjects */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LecturerSubjects_Users_UserId')
    ALTER TABLE dbo.LecturerSubjects WITH CHECK ADD CONSTRAINT FK_LecturerSubjects_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_LecturerSubjects_Subjects_SubjectId')
    ALTER TABLE dbo.LecturerSubjects WITH CHECK ADD CONSTRAINT FK_LecturerSubjects_Subjects_SubjectId
        FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (Id) ON DELETE CASCADE;

/* PackagePurchases → Users */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PackagePurchases_Users_UserId')
    ALTER TABLE dbo.PackagePurchases WITH CHECK ADD CONSTRAINT FK_PackagePurchases_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE;

/* ExperimentVariants → ExperimentRuns */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExperimentVariants_ExperimentRuns_ExperimentRunId')
    ALTER TABLE dbo.ExperimentVariants WITH CHECK ADD CONSTRAINT FK_ExperimentVariants_ExperimentRuns_ExperimentRunId
        FOREIGN KEY (ExperimentRunId) REFERENCES dbo.ExperimentRuns (Id) ON DELETE CASCADE;
GO

/* =====================================================================
   2) MISSING FOREIGN KEYS  (these are the ones EF never declared)
   ===================================================================== */

/* --- nullable columns → trusted constraints (orphans already NULLed) --- */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TokenUsageLogs_ChatSessions_SessionId')
    ALTER TABLE dbo.TokenUsageLogs WITH CHECK ADD CONSTRAINT FK_TokenUsageLogs_ChatSessions_SessionId
        FOREIGN KEY (SessionId) REFERENCES dbo.ChatSessions (Id) ON DELETE SET NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExperimentRuns_Subjects_SubjectId')
    ALTER TABLE dbo.ExperimentRuns WITH CHECK ADD CONSTRAINT FK_ExperimentRuns_Subjects_SubjectId
        FOREIGN KEY (SubjectId) REFERENCES dbo.Subjects (Id) ON DELETE SET NULL;

/* --- NOT-NULL columns → WITH NOCHECK (won't fail on legacy/empty rows;
       enforced for every INSERT/UPDATE from now on) --- */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TokenUsageLogs_Users_UserId')
    ALTER TABLE dbo.TokenUsageLogs WITH NOCHECK ADD CONSTRAINT FK_TokenUsageLogs_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ExperimentRuns_Users_UserId')
    ALTER TABLE dbo.ExperimentRuns WITH NOCHECK ADD CONSTRAINT FK_ExperimentRuns_Users_UserId
        FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

/* NOTE: DeletePackageAsync currently HARD-deletes a package row. With this
   FK, deleting a package that already has purchases will be blocked
   (NO ACTION) — the correct behaviour. Prefer soft-delete (IsActive = 0). */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_PackagePurchases_Packages_PackageId')
    ALTER TABLE dbo.PackagePurchases WITH NOCHECK ADD CONSTRAINT FK_PackagePurchases_Packages_PackageId
        FOREIGN KEY (PackageId) REFERENCES dbo.Packages (Id) ON DELETE NO ACTION;
GO

/* =====================================================================
   3) Supporting indexes on the new FK columns (optional, for speed)
   ===================================================================== */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TokenUsageLogs_SessionId' AND object_id = OBJECT_ID('dbo.TokenUsageLogs'))
    CREATE INDEX IX_TokenUsageLogs_SessionId ON dbo.TokenUsageLogs (SessionId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExperimentRuns_SubjectId' AND object_id = OBJECT_ID('dbo.ExperimentRuns'))
    CREATE INDEX IX_ExperimentRuns_SubjectId ON dbo.ExperimentRuns (SubjectId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ExperimentRuns_UserId' AND object_id = OBJECT_ID('dbo.ExperimentRuns'))
    CREATE INDEX IX_ExperimentRuns_UserId ON dbo.ExperimentRuns (UserId);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PackagePurchases_PackageId' AND object_id = OBJECT_ID('dbo.PackagePurchases'))
    CREATE INDEX IX_PackagePurchases_PackageId ON dbo.PackagePurchases (PackageId);
GO

/* =====================================================================
   4) Verify — list every foreign key in the database
   ===================================================================== */
SELECT  fk.name                               AS ForeignKey,
        OBJECT_NAME(fk.parent_object_id)      AS FromTable,
        cp.name                               AS FromColumn,
        OBJECT_NAME(fk.referenced_object_id)  AS ToTable,
        cr.name                               AS ToColumn,
        fk.delete_referential_action_desc     AS OnDelete,
        fk.is_not_trusted                     AS NotTrusted
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN sys.columns cp ON cp.object_id = fk.parent_object_id     AND cp.column_id = fkc.parent_column_id
JOIN sys.columns cr ON cr.object_id = fk.referenced_object_id AND cr.column_id = fkc.referenced_column_id
ORDER BY FromTable, ForeignKey;
GO

/* =====================================================================
   5) OPTIONAL — after cleaning orphans yourself, upgrade the 3 NOCHECK
      constraints to "trusted". Run these ONLY if the SELECTs below
      return 0 rows, otherwise they will fail.

   -- orphan reports:
   -- SELECT * FROM dbo.TokenUsageLogs  WHERE UserId    NOT IN (SELECT Id FROM dbo.Users);
   -- SELECT * FROM dbo.ExperimentRuns  WHERE UserId    NOT IN (SELECT Id FROM dbo.Users);
   -- SELECT * FROM dbo.PackagePurchases WHERE PackageId NOT IN (SELECT Id FROM dbo.Packages);

   ALTER TABLE dbo.TokenUsageLogs   WITH CHECK CHECK CONSTRAINT FK_TokenUsageLogs_Users_UserId;
   ALTER TABLE dbo.ExperimentRuns   WITH CHECK CHECK CONSTRAINT FK_ExperimentRuns_Users_UserId;
   ALTER TABLE dbo.PackagePurchases WITH CHECK CHECK CONSTRAINT FK_PackagePurchases_Packages_PackageId;
   ===================================================================== */
