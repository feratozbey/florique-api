-- Migration: Create feedback table
-- Database: PostgreSQL
-- Date: 2026-01-08

-- Create feedback table if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_name = 'feedback'
    ) THEN
        CREATE TABLE feedback (
            id SERIAL PRIMARY KEY,
            userid VARCHAR(255) NOT NULL,
            email VARCHAR(255) NOT NULL,
            feedbacktext TEXT NOT NULL,
            createdat TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC')
        );
    END IF;
END $$;
