#!/usr/bin/env python3
"""
Script to fix Railway database schema issues with foreign key constraints
This should be run on the Railway server to fix the image storage issues.
"""

import asyncio
import psycopg
import os
from dotenv import load_dotenv

load_dotenv()

async def fix_railway_database_schema():
    """Fix the Railway database schema by recreating tables with proper constraints"""
    database_url = os.getenv("DATABASE_URL")
    
    if not database_url:
        print("❌ DATABASE_URL not found in environment variables")
        return False
    
    print(f"🔧 Fixing Railway database schema...")
    print(f"📊 Database URL: {database_url[:50]}...")
    
    try:
        async with await psycopg.AsyncConnection.connect(database_url) as conn:
            async with conn.cursor() as cur:
                print("🗑️ Dropping existing problematic tables...")
                
                # Drop tables in reverse dependency order
                await cur.execute("DROP TABLE IF EXISTS conversation_content CASCADE")
                await cur.execute("DROP TABLE IF EXISTS mcp_tool_executions CASCADE")
                await cur.execute("DROP TABLE IF EXISTS multimedia_content CASCADE")
                
                print("🏗️ Recreating multimedia_content table with proper constraints...")
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
                
                print("🏗️ Recreating conversation_content table with proper constraints...")
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
                
                print("🏗️ Recreating mcp_tool_executions table with proper constraints...")
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
                print("✅ Railway database schema fixed successfully!")
                
                # Test the fix by checking if we can insert a test record
                print("🧪 Testing the fix with a sample insert...")
                try:
                    await cur.execute("""
                        INSERT INTO multimedia_content (
                            content_id, user_id, content_type, file_path, file_name
                        ) VALUES (
                            'test-content-id', 'test-user', 'image', '/test/path', 'test.png'
                        )
                    """)
                    await cur.execute("DELETE FROM multimedia_content WHERE content_id = 'test-content-id'")
                    await conn.commit()
                    print("✅ Test insert/delete successful - foreign key constraints are working!")
                except Exception as test_error:
                    print(f"❌ Test insert failed: {test_error}")
                    return False
                
                return True
                
    except Exception as e:
        print(f"❌ Error fixing Railway database schema: {e}")
        return False

if __name__ == "__main__":
    print("🚀 Railway Database Schema Fix Tool")
    print("=" * 50)
    
    success = asyncio.run(fix_railway_database_schema())
    
    if success:
        print("\n🎉 Railway database schema fixed successfully!")
        print("💡 Next steps:")
        print("   1. Try generating an image in Unity")
        print("   2. Check Railway logs for successful storage")
        print("   3. Verify images appear in the gallery")
    else:
        print("\n❌ Failed to fix Railway database schema")
        print("💡 Check Railway logs for detailed error messages")
