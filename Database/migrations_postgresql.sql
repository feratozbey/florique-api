-- PostgreSQL Migration Script for Firebase Push Notifications
-- Run this script to add the required tables for async job processing

-- Create device_tokens table
CREATE TABLE IF NOT EXISTS device_tokens (
    userid VARCHAR(50) PRIMARY KEY,
    firebasetoken VARCHAR(500) NOT NULL,
    platform VARCHAR(20) NOT NULL DEFAULT 'android',
    updatedat TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    CONSTRAINT fk_device_tokens_users FOREIGN KEY (userid) REFERENCES users(userid) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_device_tokens_updated ON device_tokens(updatedat);

-- Create enhancement_jobs table
CREATE TABLE IF NOT EXISTS enhancement_jobs (
    jobid VARCHAR(50) PRIMARY KEY,
    userid VARCHAR(50) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'processing', -- processing, completed, failed
    progress INTEGER NOT NULL DEFAULT 0,
    originalimageb64 TEXT NULL,
    enhancedimageb64 TEXT NULL,
    backgroundstyle VARCHAR(50) NOT NULL,
    devicetoken VARCHAR(500) NULL,
    createdat TIMESTAMP NOT NULL DEFAULT (NOW() AT TIME ZONE 'UTC'),
    completedat TIMESTAMP NULL,
    errormessage TEXT NULL,
    CONSTRAINT fk_enhancement_jobs_users FOREIGN KEY (userid) REFERENCES users(userid) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_jobs_user_status ON enhancement_jobs(userid, status);
CREATE INDEX IF NOT EXISTS idx_jobs_created ON enhancement_jobs(createdat);

-- Optional: Add a function to automatically delete old completed jobs (older than 7 days)
-- This helps keep the database size manageable
-- You can schedule this with pg_cron or run it manually

/*
CREATE OR REPLACE FUNCTION cleanup_old_jobs()
RETURNS void AS $$
BEGIN
    DELETE FROM enhancement_jobs
    WHERE status IN ('completed', 'failed')
      AND completedat < (NOW() AT TIME ZONE 'UTC') - INTERVAL '7 days';
END;
$$ LANGUAGE plpgsql;

-- To run cleanup:
-- SELECT cleanup_old_jobs();
*/

-- Display success message
DO $$
BEGIN
    RAISE NOTICE 'Migration completed successfully';
END $$;
