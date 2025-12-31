-- SQL Server Migration Script for Firebase Push Notifications
-- Run this script to add the required tables for async job processing

-- Create device_tokens table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='device_tokens' AND xtype='U')
BEGIN
    CREATE TABLE [device_tokens] (
        [userId] VARCHAR(50) PRIMARY KEY,
        [firebaseToken] VARCHAR(500) NOT NULL,
        [platform] VARCHAR(20) NOT NULL DEFAULT 'android',
        [updatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_device_tokens_users FOREIGN KEY ([userId]) REFERENCES [users]([userId]) ON DELETE CASCADE
    );

    CREATE INDEX idx_device_tokens_updated ON [device_tokens]([updatedAt]);

    PRINT 'Created device_tokens table';
END
ELSE
BEGIN
    PRINT 'device_tokens table already exists';
END
GO

-- Create enhancement_jobs table
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='enhancement_jobs' AND xtype='U')
BEGIN
    CREATE TABLE [enhancement_jobs] (
        [jobId] VARCHAR(50) PRIMARY KEY,
        [userId] VARCHAR(50) NOT NULL,
        [status] VARCHAR(20) NOT NULL DEFAULT 'processing', -- processing, completed, failed
        [progress] INT NOT NULL DEFAULT 0,
        [originalImageBase64] NVARCHAR(MAX) NULL,
        [enhancedImageBase64] NVARCHAR(MAX) NULL,
        [backgroundStyle] VARCHAR(50) NOT NULL,
        [deviceToken] VARCHAR(500) NULL,
        [createdAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [completedAt] DATETIME2 NULL,
        [errorMessage] NVARCHAR(MAX) NULL,
        CONSTRAINT FK_enhancement_jobs_users FOREIGN KEY ([userId]) REFERENCES [users]([userId]) ON DELETE CASCADE
    );

    CREATE INDEX idx_jobs_user_status ON [enhancement_jobs]([userId], [status]);
    CREATE INDEX idx_jobs_created ON [enhancement_jobs]([createdAt]);

    PRINT 'Created enhancement_jobs table';
END
ELSE
BEGIN
    PRINT 'enhancement_jobs table already exists';
END
GO

-- Optional: Add a cleanup job to delete old completed jobs (older than 7 days)
-- This helps keep the database size manageable
-- You can schedule this as a SQL Server Agent job or run it manually

/*
DELETE FROM [enhancement_jobs]
WHERE [status] IN ('completed', 'failed')
  AND [completedAt] < DATEADD(day, -7, GETUTCDATE());
*/

PRINT 'Migration completed successfully';
GO
