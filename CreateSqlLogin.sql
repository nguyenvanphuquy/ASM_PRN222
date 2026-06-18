-- ============================================================
--  Chay script nay bang SSMS voi Windows Authentication (sa)
--  de tao SQL login cho app
-- ============================================================

USE master;
GO

-- 1. Bat SQL Server Authentication (Mixed Mode)
EXEC xp_instance_regwrite
    N'HKEY_LOCAL_MACHINE',
    N'Software\Microsoft\MSSQLServer\MSSQLServer',
    N'LoginMode',
    REG_DWORD,
    2;  -- 1 = Windows only, 2 = Mixed Mode
GO

-- 2. Tao login 'admin' neu chua co
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'admin' AND type = 'S')
BEGIN
    CREATE LOGIN [admin] WITH
        PASSWORD    = N'123',
        CHECK_POLICY = OFF,        -- tat policy password complexity
        CHECK_EXPIRATION = OFF;
    PRINT 'Login [admin] da duoc tao.';
END
ELSE
BEGIN
    -- Neu da co thi reset password
    ALTER LOGIN [admin] WITH
        PASSWORD    = N'123',
        CHECK_POLICY = OFF,
        CHECK_EXPIRATION = OFF;
    PRINT 'Login [admin] da duoc cap nhat password.';
END
GO

-- 3. Cap quyen dbowner tren database ChatBotPRN222
USE ChatBotPRN222;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'admin')
BEGIN
    CREATE USER [admin] FOR LOGIN [admin];
END

ALTER ROLE db_owner ADD MEMBER [admin];
PRINT 'User [admin] da co quyen db_owner tren ChatBotPRN222.';
GO

-- 4. Kiem tra
SELECT
    sp.name         AS [Login],
    sp.type_desc    AS [Type],
    sp.is_disabled  AS [Disabled],
    dp.name         AS [DbUser],
    r.name          AS [DbRole]
FROM sys.server_principals sp
LEFT JOIN sys.database_principals dp ON dp.name = sp.name
LEFT JOIN sys.database_role_members drm ON drm.member_principal_id = dp.principal_id
LEFT JOIN sys.database_principals r ON r.principal_id = drm.role_principal_id
WHERE sp.name = 'admin';
GO

PRINT '=== Xong! Restart SQL Server Service de Mixed Mode co hieu luc. ===';
GO
