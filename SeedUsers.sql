-- ============================================================
--  ChatBotPRN222 - Seed Test Users
--  Password duoc hash bang BCrypt work-factor 11
-- ============================================================
--  | Username  | Password     | Role      |
--  |-----------|--------------|-----------|
--  | admin     | admin123     | Admin     |
--  | lecturer  | lecturer123  | Lecturer  |
--  | student   | student123   | Student   |
--  | lecturer2 | Test@123     | Lecturer  |
--  | student2  | Test@123     | Student   |
-- ============================================================

USE ChatBotPRN222;
GO

-- Admin
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'admin')
INSERT INTO Users (Id, Username, Email, PasswordHash, FullName, Role, CreatedAt)
VALUES (
    NEWID(),
    'admin',
    'admin@chatbot.local',
    '$2a$11$J/idQLg54GO9cOOEUsClpuf.OgCTMF57Hw65HVRIwAGoYzWcb2NNS',
    'Administrator',
    'Admin',
    GETUTCDATE()
);

-- Lecturer chinh
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'lecturer')
INSERT INTO Users (Id, Username, Email, PasswordHash, FullName, Role, CreatedAt)
VALUES (
    NEWID(),
    'lecturer',
    'lecturer@chatbot.local',
    '$2a$11$FVRoyjXb.5QyA19JgWOfSeaj1KLgcHSJ0e/FdtGfAckPnJV1Eq22W',
    N'Giảng viên Demo',
    'Lecturer',
    GETUTCDATE()
);

-- Student chinh
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'student')
INSERT INTO Users (Id, Username, Email, PasswordHash, FullName, Role, CreatedAt)
VALUES (
    NEWID(),
    'student',
    'student@chatbot.local',
    '$2a$11$5j3USefBYfX3THGtKMFn2OfwdAvKpYG4hP5DOb6fG11qgDqDZZxJS',
    N'Sinh viên Demo',
    'Student',
    GETUTCDATE()
);

-- Lecturer phu (password: Test@123)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'lecturer2')
INSERT INTO Users (Id, Username, Email, PasswordHash, FullName, Role, CreatedAt)
VALUES (
    NEWID(),
    'lecturer2',
    'lecturer2@chatbot.local',
    '$2a$11$KpsQHe2zcrlkkGpRk/FbUeP0iPjYq7WESHjA2AdGLharmRzPGz.JC',
    N'Giảng viên 2',
    'Lecturer',
    GETUTCDATE()
);

-- Student phu (password: Test@123)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Username = 'student2')
INSERT INTO Users (Id, Username, Email, PasswordHash, FullName, Role, CreatedAt)
VALUES (
    NEWID(),
    'student2',
    'student2@chatbot.local',
    '$2a$11$KpsQHe2zcrlkkGpRk/FbUeP0iPjYq7WESHjA2AdGLharmRzPGz.JC',
    N'Sinh viên 2',
    'Student',
    GETUTCDATE()
);

-- Kiem tra ket qua
SELECT Id, Username, Email, FullName, Role, CreatedAt
FROM Users
ORDER BY Role, Username;
GO
