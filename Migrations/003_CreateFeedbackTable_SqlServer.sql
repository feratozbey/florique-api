-- Migration: Create feedback table
-- Database: SQL Server
-- Date: 2026-01-08

-- Create feedback table if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'feedback' AND type = 'U')
BEGIN
    CREATE TABLE [feedback] (
        [id] INT IDENTITY(1,1) PRIMARY KEY,
        [userId] NVARCHAR(255) NOT NULL,
        [email] NVARCHAR(255) NOT NULL,
        [feedbackText] NVARCHAR(MAX) NOT NULL,
        [createdAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
    );
END
GO

PRINT 'Migration 003 completed: Feedback table created'
GO
