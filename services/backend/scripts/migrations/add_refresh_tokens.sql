-- Migration: AddRefreshTokens
-- Date: 2024-12-18
-- Description: Creates RefreshTokens table for JWT refresh token management

-- Create the RefreshTokens table
CREATE TABLE IF NOT EXISTS "RefreshTokens" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "Token" character varying(88) NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedByIp" character varying(45),
    "RevokedAt" timestamp with time zone,
    "RevokedByIp" character varying(45),
    "ReplacedByToken" character varying(88),
    "RevokeReason" character varying(200),
    CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId")
        REFERENCES "Users" ("Id") ON DELETE CASCADE
);

-- Create unique index on Token for fast lookups
CREATE UNIQUE INDEX IF NOT EXISTS "IX_RefreshTokens_Token"
    ON "RefreshTokens" ("Token");

-- Create composite index for user queries
CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_UserId_ExpiresAt"
    ON "RefreshTokens" ("UserId", "ExpiresAt");

-- Add PasswordChangedAt column to Users if it doesn't exist
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Users' AND column_name = 'PasswordChangedAt'
    ) THEN
        ALTER TABLE "Users" ADD COLUMN "PasswordChangedAt" timestamp with time zone;
    END IF;
END $$;

-- Record migration in EF migrations history (if using EF migrations)
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251218000000_AddRefreshTokens', '9.0.0')
ON CONFLICT ("MigrationId") DO NOTHING;
