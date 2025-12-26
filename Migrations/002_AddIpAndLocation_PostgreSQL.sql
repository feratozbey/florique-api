-- Migration: Add IpAddress and Location columns to users table
-- Database: PostgreSQL
-- Date: 2025-12-17

-- Add ipAddress column if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'ipAddress'
    ) THEN
        ALTER TABLE users ADD COLUMN "ipAddress" VARCHAR(45) NULL;
    END IF;
END $$;

-- Add location column if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'users' AND column_name = 'location'
    ) THEN
        ALTER TABLE users ADD COLUMN "location" VARCHAR(255) NULL;
    END IF;
END $$;
