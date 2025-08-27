#!/usr/bin/env python3
"""
Script to fix database schema issues with foreign key constraints
"""

import asyncio
import psycopg
import os
from dotenv import load_dotenv

load_dotenv()

async def fix_database_schema():
    """Fix the database schema by recreating tables with proper constraints"""
    database_url = os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db")
    
    try:
        async with await psycopg.AsyncConnection.connect(database_url) as conn:
            async with conn.cursor() as cur:
                print("🔧 Fixing database schema...")
                
                # Drop problematic tables (in reverse dependency order)
                print("🗑️ Dropping existing tables...")
                await cur.execute("DROP TABLE IF EXISTS conversation_content CASCADE")
                await cur.execute("DROP TABLE IF EXISTS mcp_tool_executions CASCADE")
                await cur.execute("DROP TABLE IF EXISTS multimedia_content CASCADE")
                
                # Recreate multimedia_content table with proper constraints
                print("🏗️ Recreating multimedia_content table...")
                await cur.execute("""
                    CREATE TABLE multimedia_content (
                        content_id TEXT PRIMARY KEY,
                        user_id TEXT NOT NULL,
                        session_id TEXT,
                        turn_id TEXT,
                        content_type TEXT NOT NULL,
                        file_path TEXT NOT NULL,
                        file_name TEXT NOT NULL,
                        file_size BIGINT,
                        mime_type TEXT,
                        content_hash TEXT,
                        metadata JSONB,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        created_by TEXT DEFAULT 'mcp_tool',
                        is_generated BOOLEAN DEFAULT FALSE,
                        generation_tool TEXT,
                        generation_prompt TEXT,
                        tags TEXT[],
                        FOREIGN KEY (session_id) REFERENCES conversation_sessions(session_id) ON DELETE SET NULL,
                        FOREIGN KEY (turn_id) REFERENCES conversation_turns(turn_id) ON DELETE SET NULL
                    )
                """)
                
                # Recreate conversation_content table with proper constraints
                print("🏗️ Recreating conversation_content table...")
                await cur.execute("""
                    CREATE TABLE conversation_content (
                        content_id TEXT PRIMARY KEY,
                        turn_id TEXT NOT NULL,
                        user_id TEXT NOT NULL,
                        content_type TEXT NOT NULL,
                        content_data TEXT,
                        multimedia_id TEXT,
                        content_order INTEGER,
                        is_user_content BOOLEAN,
                        mcp_tool_used TEXT,
                        tool_parameters JSONB,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        metadata JSONB,
                        FOREIGN KEY (turn_id) REFERENCES conversation_turns(turn_id) ON DELETE CASCADE,
                        FOREIGN KEY (multimedia_id) REFERENCES multimedia_content(content_id) ON DELETE CASCADE
                    )
                """)
                
                # Recreate mcp_tool_executions table with proper constraints
                print("🏗️ Recreating mcp_tool_executions table...")
                await cur.execute("""
                    CREATE TABLE mcp_tool_executions (
                        execution_id TEXT PRIMARY KEY,
                        user_id TEXT NOT NULL,
                        session_id TEXT,
                        turn_id TEXT,
                        tool_name TEXT NOT NULL,
                        tool_parameters JSONB,
                        execution_result JSONB,
                        execution_time_ms INTEGER,
                        success BOOLEAN,
                        error_message TEXT,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        metadata JSONB,
                        FOREIGN KEY (session_id) REFERENCES conversation_sessions(session_id) ON DELETE SET NULL,
                        FOREIGN KEY (turn_id) REFERENCES conversation_turns(turn_id) ON DELETE SET NULL
                    )
                """)
                
                # Recreate indexes
                print("🔍 Recreating indexes...")
                await cur.execute("""
                    CREATE INDEX multimedia_content_user_idx ON multimedia_content(user_id)
                """)
                await cur.execute("""
                    CREATE INDEX multimedia_content_session_idx ON multimedia_content(session_id)
                """)
                await cur.execute("""
                    CREATE INDEX multimedia_content_type_idx ON multimedia_content(content_type)
                """)
                await cur.execute("""
                    CREATE INDEX conversation_content_turn_idx ON conversation_content(turn_id)
                """)
                await cur.execute("""
                    CREATE INDEX mcp_tool_executions_user_idx ON mcp_tool_executions(user_id)
                """)
                
                await conn.commit()
                print("✅ Database schema fixed successfully!")
                
    except Exception as e:
        print(f"❌ Error fixing database schema: {e}")
        return False
    
    return True

if __name__ == "__main__":
    asyncio.run(fix_database_schema())
