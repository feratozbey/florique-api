-- Migration: Add CreatedDate and DeviceType columns to users table
-- Database: SQL Server
-- Date: 2025-12-17

-- Add createdDate column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[users]') AND name = 'createdDate')
BEGIN
    ALTER TABLE [users]
    ADD [createdDate] DATETIME NULL;
END
GO

-- Add deviceType column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[users]') AND name = 'deviceType')
BEGIN
    ALTER TABLE [users]
    ADD [deviceType] NVARCHAR(100) NULL;
END
GO

-- Optional: Set default creation date for existing users
UPDATE [users]
SET [createdDate] = GETUTCDATE()
WHERE [createdDate] IS NULL;
GO
