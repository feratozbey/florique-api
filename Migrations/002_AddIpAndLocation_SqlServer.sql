-- Migration: Add IpAddress and Location columns to users table
-- Database: SQL Server
-- Date: 2025-12-17

-- Add ipAddress column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[users]') AND name = 'ipAddress')
BEGIN
    ALTER TABLE [users]
    ADD [ipAddress] NVARCHAR(45) NULL;
END
GO

-- Add location column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[users]') AND name = 'location')
BEGIN
    ALTER TABLE [users]
    ADD [location] NVARCHAR(255) NULL;
END
GO

PRINT 'Migration 002 completed: IpAddress and Location columns added to users table'
GO
