-- Initialize pgvector extension for vector similarity search
CREATE EXTENSION IF NOT EXISTS vector;

-- Verify installation
DO $$
BEGIN
    RAISE NOTICE 'pgvector extension initialized successfully';
END $$;
