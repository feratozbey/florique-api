-- Migration: Add CreatedDate and DeviceType columns to users table
-- Database: PostgreSQL
-- Date: 2025-12-17

-- Add createdDate column if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'createdDate'
    ) THEN
        ALTER TABLE users ADD COLUMN "createdDate" TIMESTAMP NULL;
    END IF;
END $$;

-- Add deviceType column if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'deviceType'
    ) THEN
        ALTER TABLE users ADD COLUMN "deviceType" VARCHAR(100) NULL;
    END IF;
END $$;

-- Optional: Set default creation date for existing users
UPDATE users
SET "createdDate" = NOW()
WHERE "createdDate" IS NULL;
