import os
import asyncio
import psycopg
from psycopg import sql
from typing import List, Dict, Any, Optional
import numpy as np
# Note: register_vector is not needed for async connections in newer versions

class RAGDatabase:
    def __init__(self, db_url: str):
        self.db_url = db_url
        
    async def initialize(self):
        """Initialize database with required tables and extensions"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            await conn.execute("SELECT 1")  # Ensure connection is ready
            # Note: register_vector is not needed for async connections in newer versions
            async with conn.cursor() as cur:
                # Create extension
                await cur.execute("CREATE EXTENSION IF NOT EXISTS vector")
                
                # Create main chunks table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS rag_chunks (
                        chunk_id TEXT PRIMARY KEY,
                        embedding VECTOR(1536),
                        text TEXT NOT NULL,
                        doc_title TEXT,
                        section TEXT,
                        tags TEXT[],
                        lang TEXT DEFAULT 'en',
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        user_scope TEXT DEFAULT 'global',
                        safety_level TEXT DEFAULT 'public',
                        token_estimate INTEGER,
                        source_path TEXT
                    )
                """)
                
                # Create vector index
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS rag_chunks_embedding_idx 
                    ON rag_chunks USING ivfflat (embedding vector_l2_ops) 
                    WITH (lists = 100)
                """)
                
                # Create user memory table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS user_memory (
                        memory_id TEXT PRIMARY KEY,
                        user_id TEXT NOT NULL,
                        memory_type TEXT NOT NULL, -- 'episodic', 'semantic', 'profile'
                        embedding VECTOR(1536),
                        content TEXT NOT NULL,
                        metadata JSONB,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        updated_at TIMESTAMPTZ DEFAULT NOW()
                    )
                """)
                
                # Create memory index
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS user_memory_embedding_idx 
                    ON user_memory USING ivfflat (embedding vector_l2_ops) 
                    WITH (lists = 100)
                """)
                
                # Create conversation sessions table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS conversation_sessions (
                        session_id TEXT PRIMARY KEY,
                        user_id TEXT NOT NULL,
                        started_at TIMESTAMPTZ DEFAULT NOW(),
                        ended_at TIMESTAMPTZ,
                        metadata JSONB
                    )
                """)
                
                # Create conversation turns table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS conversation_turns (
                        turn_id TEXT PRIMARY KEY,
                        session_id TEXT NOT NULL,
                        user_id TEXT NOT NULL,
                        turn_index INTEGER NOT NULL,
                        user_message TEXT,
                        assistant_response TEXT,
                        retrieved_chunks TEXT[],
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        metadata JSONB,
                        FOREIGN KEY (session_id) REFERENCES conversation_sessions(session_id)
                    )
                """)
                
                # NEW: Create multimedia content table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS multimedia_content (
                        content_id TEXT PRIMARY KEY,
                        user_id TEXT NOT NULL,
                        session_id TEXT,
                        turn_id TEXT,
                        content_type TEXT NOT NULL, -- 'image', 'audio', 'video', 'document'
                        file_path TEXT NOT NULL,
                        file_name TEXT NOT NULL,
                        file_size BIGINT,
                        mime_type TEXT,
                        content_hash TEXT, -- For deduplication
                        metadata JSONB, -- Store MCP tool info, generation params, etc.
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        created_by TEXT DEFAULT 'mcp_tool', -- 'user', 'mcp_tool', 'system'
                        is_generated BOOLEAN DEFAULT FALSE, -- True for MCP-generated content
                        generation_tool TEXT, -- Which MCP tool created this
                        generation_prompt TEXT, -- The prompt that generated this content
                        tags TEXT[],
                        FOREIGN KEY (session_id) REFERENCES conversation_sessions(session_id),
                        FOREIGN KEY (turn_id) REFERENCES conversation_turns(turn_id)
                    )
                """)
                
                # NEW: Create enhanced conversation content table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS conversation_content (
                        content_id TEXT PRIMARY KEY,
                        turn_id TEXT NOT NULL,
                        user_id TEXT NOT NULL,
                        content_type TEXT NOT NULL, -- 'text', 'image', 'audio', 'video', 'file'
                        content_data TEXT, -- For text content
                        multimedia_id TEXT, -- Reference to multimedia_content table
                        content_order INTEGER, -- Order within the turn
                        is_user_content BOOLEAN, -- True if from user, False if from assistant
                        mcp_tool_used TEXT, -- If content was generated by MCP tool
                        tool_parameters JSONB, -- Parameters used by the MCP tool
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        metadata JSONB,
                        FOREIGN KEY (turn_id) REFERENCES conversation_turns(turn_id),
                        FOREIGN KEY (multimedia_id) REFERENCES multimedia_content(content_id)
                    )
                """)
                
                # NEW: Create MCP tool execution log
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS mcp_tool_executions (
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
                        FOREIGN KEY (session_id) REFERENCES conversation_sessions(session_id),
                        FOREIGN KEY (turn_id) REFERENCES conversation_turns(turn_id)
                    )
                """)
                
                # Create indexes for new tables
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS multimedia_content_user_idx 
                    ON multimedia_content(user_id)
                """)
                
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS multimedia_content_session_idx 
                    ON multimedia_content(session_id)
                """)
                
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS multimedia_content_type_idx 
                    ON multimedia_content(content_type)
                """)
                
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS conversation_content_turn_idx 
                    ON conversation_content(turn_id)
                """)
                
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS mcp_tool_executions_user_idx 
                    ON mcp_tool_executions(user_id)
                """)
                
                await conn.commit()
        
        # Initialize multimedia embeddings table
        await self.initialize_multimedia_embeddings()
    
    async def upsert_chunks(self, chunks: List[Dict[str, Any]]):
        """Insert or update chunks in the database"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            await conn.execute("SELECT 1")  # Ensure connection is ready
            # Note: register_vector is not needed for async connections
            async with conn.cursor() as cur:
                for chunk in chunks:
                    await cur.execute("""
                        INSERT INTO rag_chunks (
                            chunk_id, embedding, text, doc_title, section, 
                            tags, lang, user_scope, safety_level, token_estimate, source_path
                        ) VALUES (
                            %(chunk_id)s, %(embedding)s, %(text)s, %(doc_title)s, %(section)s,
                            %(tags)s, %(lang)s, %(user_scope)s, %(safety_level)s, %(token_estimate)s, %(source_path)s
                        ) ON CONFLICT (chunk_id) DO UPDATE SET
                            embedding = EXCLUDED.embedding,
                            text = EXCLUDED.text,
                            doc_title = EXCLUDED.doc_title,
                            section = EXCLUDED.section,
                            tags = EXCLUDED.tags,
                            lang = EXCLUDED.lang,
                            user_scope = EXCLUDED.user_scope,
                            safety_level = EXCLUDED.safety_level,
                            token_estimate = EXCLUDED.token_estimate,
                            source_path = EXCLUDED.source_path
                    """, chunk)
                await conn.commit()
    
    async def search_chunks(self, query_embedding: List[float], top_k: int = 5, 
                          user_scopes: List[str] = None, safety_levels: List[str] = None):
        """Search for similar chunks using vector similarity"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            await conn.execute("SELECT 1")  # Ensure connection is ready
            # Note: register_vector is not needed for async connections
            async with conn.cursor() as cur:
                # Build WHERE clause
                where_conditions = []
                params = {"embedding": query_embedding, "top_k": top_k}
                
                if user_scopes:
                    where_conditions.append("user_scope = ANY(%(user_scopes)s)")
                    params["user_scopes"] = user_scopes
                
                if safety_levels:
                    where_conditions.append("safety_level = ANY(%(safety_levels)s)")
                    params["safety_levels"] = safety_levels
                
                where_clause = "WHERE " + " AND ".join(where_conditions) if where_conditions else ""
                
                query = f"""
                    SELECT chunk_id, text, doc_title, section, tags, source_path,
                           1 - (embedding <=> %(embedding)s::vector) AS score
                    FROM rag_chunks
                    {where_clause}
                    ORDER BY embedding <=> %(embedding)s::vector
                    LIMIT %(top_k)s
                """
                
                await cur.execute(query, params)
                rows = await cur.fetchall()
                
                return [
                    {
                        "chunk_id": row[0],
                        "text": row[1],
                        "doc_title": row[2],
                        "section": row[3],
                        "tags": row[4],
                        "source_path": row[5],
                        "score": float(row[6])
                    }
                    for row in rows
                ]
    
    async def upsert_user_memory(self, user_id: str, memory_type: str, 
                               content: str, embedding: List[float], metadata: Dict = None):
        """Insert or update user memory"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            await conn.execute("SELECT 1")  # Ensure connection is ready
            # Note: register_vector is not needed for async connections
            async with conn.cursor() as cur:
                memory_id = f"{user_id}_{memory_type}_{hash(content)}"
                await cur.execute("""
                    INSERT INTO user_memory (
                        memory_id, user_id, memory_type, embedding, content, metadata
                    ) VALUES (
                        %(memory_id)s, %(user_id)s, %(memory_type)s, %(embedding)s, %(content)s, %(metadata)s
                    ) ON CONFLICT (memory_id) DO UPDATE SET
                        embedding = EXCLUDED.embedding,
                        content = EXCLUDED.content,
                        metadata = EXCLUDED.metadata,
                        updated_at = NOW()
                """, {
                    "memory_id": memory_id,
                    "user_id": user_id,
                    "memory_type": memory_type,
                    "embedding": embedding,
                    "content": content,
                    "metadata": metadata or {}
                })
                await conn.commit()
    
    async def search_user_memory(self, user_id: str, query_embedding: List[float], 
                               memory_types: List[str] = None, top_k: int = 3):
        """Search user memory using vector similarity"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            await conn.execute("SELECT 1")  # Ensure connection is ready
            # Note: register_vector is not needed for async connections
            async with conn.cursor() as cur:
                where_conditions = ["user_id = %(user_id)s"]
                params = {"user_id": user_id, "embedding": query_embedding, "top_k": top_k}
                
                if memory_types:
                    where_conditions.append("memory_type = ANY(%(memory_types)s)")
                    params["memory_types"] = memory_types
                
                where_clause = "WHERE " + " AND ".join(where_conditions)
                
                query = f"""
                    SELECT memory_id, memory_type, content, metadata,
                           1 - (embedding <=> %(embedding)s::vector) AS score
                    FROM user_memory
                    {where_clause}
                    ORDER BY embedding <=> %(embedding)s::vector
                    LIMIT %(top_k)s
                """
                
                await cur.execute(query, params)
                rows = await cur.fetchall()
                
                return [
                    {
                        "memory_id": row[0],
                        "memory_type": row[1],
                        "content": row[2],
                        "metadata": row[3],
                        "score": float(row[4])
                    }
                    for row in rows
                ]
    
    async def log_conversation_turn(self, turn_id: str, session_id: str, user_id: str,
                                  turn_index: int, user_message: str, assistant_response: str,
                                  retrieved_chunks: List[str], metadata: Dict = None):
        """Log a conversation turn"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    INSERT INTO conversation_turns (
                        turn_id, session_id, user_id, turn_index, user_message, 
                        assistant_response, retrieved_chunks, metadata
                    ) VALUES (
                        %(turn_id)s, %(session_id)s, %(user_id)s, %(turn_index)s, 
                        %(user_message)s, %(assistant_response)s, %(retrieved_chunks)s, %(metadata)s
                    )
                """, {
                    "turn_id": turn_id,
                    "session_id": session_id,
                    "user_id": user_id,
                    "turn_index": turn_index,
                    "user_message": user_message,
                    "assistant_response": assistant_response,
                    "retrieved_chunks": retrieved_chunks,
                    "metadata": metadata or {}
                })
                await conn.commit()
    
    async def store_user_profile(self, user_id: str, profile_data: dict):
        """Store user profile information in database"""
        import json
        import time
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                # Note: register_vector is not needed for async connections
                async with conn.cursor() as cur:
                    memory_id = f"profile_{user_id}_{int(time.time())}"
                    
                    # Store as JSON in content and metadata
                    content = f"User profile for {user_id}: {json.dumps(profile_data)}"
                    
                    await cur.execute("""
                        INSERT INTO user_memory (
                            memory_id, user_id, memory_type, content, metadata
                        ) VALUES (
                            %(memory_id)s, %(user_id)s, 'profile', %(content)s, %(metadata)s
                        )
                        ON CONFLICT (memory_id) 
                        DO UPDATE SET content = %(content)s, metadata = %(metadata)s, updated_at = NOW()
                    """, {
                        "memory_id": memory_id,
                        "user_id": user_id,
                        "content": content,
                        "metadata": profile_data
                    })
                    await conn.commit()
        except Exception as e:
            print(f"❌ Error storing user profile: {e}")
    
    async def get_user_profile(self, user_id: str):
        """Retrieve user profile information from database"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                # Note: register_vector is not needed for async connections
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT metadata FROM user_memory 
                        WHERE user_id = %(user_id)s AND memory_type = 'profile'
                        ORDER BY updated_at DESC LIMIT 1
                    """, {"user_id": user_id})
                    
                    row = await cur.fetchone()
                    if row and row[0]:
                        return row[0]  # metadata contains the profile data
                    return {}
        except Exception as e:
            print(f"❌ Error retrieving user profile: {e}")
            return {}
    
    # NEW: Multimedia content management methods
    
    async def store_multimedia_content(self, content_id: str, user_id: str, content_type: str,
                                     file_path: str, file_name: str, file_size: int = None,
                                     mime_type: str = None, content_hash: str = None,
                                     session_id: str = None, turn_id: str = None,
                                     metadata: Dict = None, is_generated: bool = False,
                                     generation_tool: str = None, generation_prompt: str = None,
                                     tags: List[str] = None):
        """Store multimedia content metadata in database"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        INSERT INTO multimedia_content (
                            content_id, user_id, session_id, turn_id, content_type,
                            file_path, file_name, file_size, mime_type, content_hash,
                            metadata, is_generated, generation_tool, generation_prompt, tags
                        ) VALUES (
                            %(content_id)s, %(user_id)s, %(session_id)s, %(turn_id)s, %(content_type)s,
                            %(file_path)s, %(file_name)s, %(file_size)s, %(mime_type)s, %(content_hash)s,
                            %(metadata)s, %(is_generated)s, %(generation_tool)s, %(generation_prompt)s, %(tags)s
                        )
                    """, {
                        "content_id": content_id,
                        "user_id": user_id,
                        "session_id": session_id,
                        "turn_id": turn_id,
                        "content_type": content_type,
                        "file_path": file_path,
                        "file_name": file_name,
                        "file_size": file_size,
                        "mime_type": mime_type,
                        "content_hash": content_hash,
                        "metadata": metadata or {},
                        "is_generated": is_generated,
                        "generation_tool": generation_tool,
                        "generation_prompt": generation_prompt,
                        "tags": tags or []
                    })
                    await conn.commit()
                    print(f"✅ Stored multimedia content: {content_id}")
                    return True
        except Exception as e:
            print(f"❌ Error storing multimedia content: {e}")
            return False
    
    async def get_multimedia_content(self, content_id: str):
        """Retrieve multimedia content metadata"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT * FROM multimedia_content WHERE content_id = %(content_id)s
                    """, {"content_id": content_id})
                    
                    row = await cur.fetchone()
                    if row:
                        return {
                            "content_id": row[0],
                            "user_id": row[1],
                            "session_id": row[2],
                            "turn_id": row[3],
                            "content_type": row[4],
                            "file_path": row[5],
                            "file_name": row[6],
                            "file_size": row[7],
                            "mime_type": row[8],
                            "content_hash": row[9],
                            "metadata": row[10],
                            "created_at": row[11],
                            "created_by": row[12],
                            "is_generated": row[13],
                            "generation_tool": row[14],
                            "generation_prompt": row[15],
                            "tags": row[16]
                        }
                    return None
        except Exception as e:
            print(f"❌ Error retrieving multimedia content: {e}")
            return None
    
    async def get_user_multimedia_content(self, user_id: str, content_type: str = None,
                                        limit: int = 50, offset: int = 0):
        """Get multimedia content for a user"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    where_clause = "WHERE user_id = %(user_id)s"
                    params = {"user_id": user_id, "limit": limit, "offset": offset}
                    
                    if content_type:
                        where_clause += " AND content_type = %(content_type)s"
                        params["content_type"] = content_type
                    
                    await cur.execute(f"""
                        SELECT * FROM multimedia_content 
                        {where_clause}
                        ORDER BY created_at DESC
                        LIMIT %(limit)s OFFSET %(offset)s
                    """, params)
                    
                    rows = await cur.fetchall()
                    return [
                        {
                            "content_id": row[0],
                            "user_id": row[1],
                            "session_id": row[2],
                            "turn_id": row[3],
                            "content_type": row[4],
                            "file_path": row[5],
                            "file_name": row[6],
                            "file_size": row[7],
                            "mime_type": row[8],
                            "content_hash": row[9],
                            "metadata": row[10],
                            "created_at": row[11],
                            "created_by": row[12],
                            "is_generated": row[13],
                            "generation_tool": row[14],
                            "generation_prompt": row[15],
                            "tags": row[16]
                        }
                        for row in rows
                    ]
        except Exception as e:
            print(f"❌ Error retrieving user multimedia content: {e}")
            return []
    
    # NEW: Enhanced conversation content methods
    
    async def store_conversation_content(self, content_id: str, turn_id: str, user_id: str,
                                       content_type: str, content_data: str = None,
                                       multimedia_id: str = None, content_order: int = 0,
                                       is_user_content: bool = True, mcp_tool_used: str = None,
                                       tool_parameters: Dict = None, metadata: Dict = None):
        """Store conversation content (text or multimedia reference)"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        INSERT INTO conversation_content (
                            content_id, turn_id, user_id, content_type, content_data,
                            multimedia_id, content_order, is_user_content, mcp_tool_used,
                            tool_parameters, metadata
                        ) VALUES (
                            %(content_id)s, %(turn_id)s, %(user_id)s, %(content_type)s, %(content_data)s,
                            %(multimedia_id)s, %(content_order)s, %(is_user_content)s, %(mcp_tool_used)s,
                            %(tool_parameters)s, %(metadata)s
                        )
                    """, {
                        "content_id": content_id,
                        "turn_id": turn_id,
                        "user_id": user_id,
                        "content_type": content_type,
                        "content_data": content_data,
                        "multimedia_id": multimedia_id,
                        "content_order": content_order,
                        "is_user_content": is_user_content,
                        "mcp_tool_used": mcp_tool_used,
                        "tool_parameters": tool_parameters or {},
                        "metadata": metadata or {}
                    })
                    await conn.commit()
                    return True
        except Exception as e:
            print(f"❌ Error storing conversation content: {e}")
            return False
    
    async def get_conversation_content(self, turn_id: str):
        """Get all content for a conversation turn"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT * FROM conversation_content 
                        WHERE turn_id = %(turn_id)s
                        ORDER BY content_order, created_at
                    """, {"turn_id": turn_id})
                    
                    rows = await cur.fetchall()
                    return [
                        {
                            "content_id": row[0],
                            "turn_id": row[1],
                            "user_id": row[2],
                            "content_type": row[3],
                            "content_data": row[4],
                            "multimedia_id": row[5],
                            "content_order": row[6],
                            "is_user_content": row[7],
                            "mcp_tool_used": row[8],
                            "tool_parameters": row[9],
                            "created_at": row[10],
                            "metadata": row[11]
                        }
                        for row in rows
                    ]
        except Exception as e:
            print(f"❌ Error retrieving conversation content: {e}")
            return []
    
    # NEW: MCP tool execution logging
    
    async def log_mcp_tool_execution(self, execution_id: str, user_id: str, tool_name: str,
                                   tool_parameters: Dict, execution_result: Dict,
                                   execution_time_ms: int = None, success: bool = True,
                                   error_message: str = None, session_id: str = None,
                                   turn_id: str = None, metadata: Dict = None):
        """Log MCP tool execution for analytics and debugging"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        INSERT INTO mcp_tool_executions (
                            execution_id, user_id, session_id, turn_id, tool_name,
                            tool_parameters, execution_result, execution_time_ms,
                            success, error_message, metadata
                        ) VALUES (
                            %(execution_id)s, %(user_id)s, %(session_id)s, %(turn_id)s, %(tool_name)s,
                            %(tool_parameters)s, %(execution_result)s, %(execution_time_ms)s,
                            %(success)s, %(error_message)s, %(metadata)s
                        )
                    """, {
                        "execution_id": execution_id,
                        "user_id": user_id,
                        "session_id": session_id,
                        "turn_id": turn_id,
                        "tool_name": tool_name,
                        "tool_parameters": tool_parameters or {},
                        "execution_result": execution_result or {},
                        "execution_time_ms": execution_time_ms,
                        "success": success,
                        "error_message": error_message,
                        "metadata": metadata or {}
                    })
                    await conn.commit()
                    return True
        except Exception as e:
            print(f"❌ Error logging MCP tool execution: {e}")
            return False
    
    async def get_mcp_tool_executions(self, user_id: str = None, tool_name: str = None,
                                    limit: int = 100, offset: int = 0):
        """Get MCP tool execution history"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    where_conditions = []
                    params = {"limit": limit, "offset": offset}
                    
                    if user_id:
                        where_conditions.append("user_id = %(user_id)s")
                        params["user_id"] = user_id
                    
                    if tool_name:
                        where_conditions.append("tool_name = %(tool_name)s")
                        params["tool_name"] = tool_name
                    
                    where_clause = "WHERE " + " AND ".join(where_conditions) if where_conditions else ""
                    
                    await cur.execute(f"""
                        SELECT * FROM mcp_tool_executions 
                        {where_clause}
                        ORDER BY created_at DESC
                        LIMIT %(limit)s OFFSET %(offset)s
                    """, params)
                    
                    rows = await cur.fetchall()
                    return [
                        {
                            "execution_id": row[0],
                            "user_id": row[1],
                            "session_id": row[2],
                            "turn_id": row[3],
                            "tool_name": row[4],
                            "tool_parameters": row[5],
                            "execution_result": row[6],
                            "execution_time_ms": row[7],
                            "success": row[8],
                            "error_message": row[9],
                            "created_at": row[10],
                            "metadata": row[11]
                        }
                        for row in rows
                    ]
        except Exception as e:
            print(f"❌ Error retrieving MCP tool executions: {e}")
            return []
    
    # NEW: pgvector integration for multimedia content
    
    async def initialize_multimedia_embeddings(self):
        """Initialize multimedia embeddings table with pgvector support"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    # Create multimedia embeddings table with vector support
                    await cur.execute("""
                        CREATE TABLE IF NOT EXISTS multimedia_embeddings (
                            content_id TEXT PRIMARY KEY,
                            user_id TEXT NOT NULL,
                            description TEXT NOT NULL,
                            embedding VECTOR(1536),
                            content_type TEXT NOT NULL,
                            metadata JSONB,
                            created_at TIMESTAMPTZ DEFAULT NOW(),
                            FOREIGN KEY (content_id) REFERENCES multimedia_content(content_id) ON DELETE CASCADE
                        )
                    """)
                    
                    # Create vector index for similarity search
                    await cur.execute("""
                        CREATE INDEX IF NOT EXISTS multimedia_embeddings_vector_idx 
                        ON multimedia_embeddings USING ivfflat (embedding vector_l2_ops) 
                        WITH (lists = 100)
                    """)
                    
                    # Create indexes for filtering
                    await cur.execute("""
                        CREATE INDEX IF NOT EXISTS multimedia_embeddings_user_idx 
                        ON multimedia_embeddings(user_id)
                    """)
                    
                    await cur.execute("""
                        CREATE INDEX IF NOT EXISTS multimedia_embeddings_type_idx 
                        ON multimedia_embeddings(content_type)
                    """)
                    
                    await conn.commit()
                    print("✅ Multimedia embeddings table initialized with pgvector support")
                    
        except Exception as e:
            print(f"❌ Error initializing multimedia embeddings: {e}")
    
    async def store_multimedia_embedding(self, content_id: str, user_id: str,
                                       description: str, embedding: List[float],
                                       content_type: str, metadata: Dict = None):
        """Store multimedia content embedding with vector support"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        INSERT INTO multimedia_embeddings (
                            content_id, user_id, description, embedding, content_type, metadata
                        ) VALUES (
                            %(content_id)s, %(user_id)s, %(description)s, %(embedding)s, %(content_type)s, %(metadata)s
                        ) ON CONFLICT (content_id) DO UPDATE SET
                            description = EXCLUDED.description,
                            embedding = EXCLUDED.embedding,
                            content_type = EXCLUDED.content_type,
                            metadata = EXCLUDED.metadata,
                            created_at = NOW()
                    """, {
                        "content_id": content_id,
                        "user_id": user_id,
                        "description": description,
                        "embedding": embedding,
                        "content_type": content_type,
                        "metadata": metadata or {}
                    })
                    await conn.commit()
                    print(f"✅ Stored multimedia embedding: {content_id}")
                    
        except Exception as e:
            print(f"❌ Error storing multimedia embedding: {e}")
    
    async def search_multimedia_embeddings(self, query_embedding: List[float],
                                         user_id: str = None, content_type: str = None,
                                         top_k: int = 5) -> List[Dict[str, Any]]:
        """Search multimedia content using vector similarity"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    # Build WHERE clause for filtering
                    where_conditions = []
                    params = {"embedding": query_embedding, "top_k": top_k}
                    
                    if user_id:
                        where_conditions.append("user_id = %(user_id)s")
                        params["user_id"] = user_id
                    
                    if content_type:
                        where_conditions.append("content_type = %(content_type)s")
                        params["content_type"] = content_type
                    
                    where_clause = "WHERE " + " AND ".join(where_conditions) if where_conditions else ""
                    
                    # Perform vector similarity search
                    query = f"""
                        SELECT content_id, user_id, description, content_type, metadata,
                               1 - (embedding <=> %(embedding)s::vector) AS similarity_score
                        FROM multimedia_embeddings
                        {where_clause}
                        ORDER BY embedding <=> %(embedding)s::vector
                        LIMIT %(top_k)s
                    """
                    
                    await cur.execute(query, params)
                    rows = await cur.fetchall()
                    
                    return [
                        {
                            "content_id": row[0],
                            "user_id": row[1],
                            "description": row[2],
                            "content_type": row[3],
                            "metadata": row[4],
                            "similarity_score": float(row[5])
                        }
                        for row in rows
                    ]
                    
        except Exception as e:
            print(f"❌ Error searching multimedia embeddings: {e}")
            return []
    
    async def get_multimedia_embedding(self, content_id: str) -> Optional[List[float]]:
        """Get vector embedding for multimedia content"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT embedding FROM multimedia_embeddings 
                        WHERE content_id = %(content_id)s
                    """, {"content_id": content_id})
                    
                    row = await cur.fetchone()
                    if row and row[0]:
                        return row[0]  # Return the vector
                    return None
                    
        except Exception as e:
            print(f"❌ Error retrieving multimedia embedding: {e}")
            return None
    
    async def get_similar_multimedia_content(self, content_id: str, top_k: int = 5,
                                           user_id: str = None) -> List[Dict[str, Any]]:
        """Get similar multimedia content based on vector similarity"""
        try:
            # Get the reference embedding
            reference_embedding = await self.get_multimedia_embedding(content_id)
            if not reference_embedding:
                return []
            
            # Search for similar content
            return await self.search_multimedia_embeddings(
                query_embedding=reference_embedding,
                user_id=user_id,
                top_k=top_k + 1  # +1 to exclude the reference content
            )
            
        except Exception as e:
            print(f"❌ Error getting similar multimedia content: {e}")
            return []
    
    async def get_multimedia_content_analytics(self, user_id: str = None) -> Dict[str, Any]:
        """Get analytics about multimedia content and embeddings"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    analytics = {}
                    
                    # Total multimedia content
                    if user_id:
                        await cur.execute("""
                            SELECT COUNT(*) FROM multimedia_content WHERE user_id = %(user_id)s
                        """, {"user_id": user_id})
                    else:
                        await cur.execute("SELECT COUNT(*) FROM multimedia_content")
                    
                    analytics["total_content"] = (await cur.fetchone())[0]
                    
                    # Content by type
                    if user_id:
                        await cur.execute("""
                            SELECT content_type, COUNT(*) 
                            FROM multimedia_content 
                            WHERE user_id = %(user_id)s 
                            GROUP BY content_type
                        """, {"user_id": user_id})
                    else:
                        await cur.execute("""
                            SELECT content_type, COUNT(*) 
                            FROM multimedia_content 
                            GROUP BY content_type
                        """)
                    
                    analytics["by_type"] = dict(await cur.fetchall())
                    
                    # Generated content count
                    if user_id:
                        await cur.execute("""
                            SELECT COUNT(*) FROM multimedia_content 
                            WHERE user_id = %(user_id)s AND is_generated = true
                        """, {"user_id": user_id})
                    else:
                        await cur.execute("""
                            SELECT COUNT(*) FROM multimedia_content WHERE is_generated = true
                        """)
                    
                    analytics["generated_content"] = (await cur.fetchone())[0]
                    
                    # Embeddings coverage
                    if user_id:
                        await cur.execute("""
                            SELECT COUNT(*) FROM multimedia_embeddings WHERE user_id = %(user_id)s
                        """, {"user_id": user_id})
                    else:
                        await cur.execute("SELECT COUNT(*) FROM multimedia_embeddings")
                    
                    analytics["with_embeddings"] = (await cur.fetchone())[0]
                    
                    return analytics
                    
        except Exception as e:
            print(f"❌ Error getting multimedia analytics: {e}")
            return {}
    
    # NEW: Conversation reconstruction methods
    
    async def get_user_conversation_sessions(self, user_id: str, limit: int = 10) -> List[Dict[str, Any]]:
        """Get conversation sessions for a user"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT session_id, user_id, started_at, ended_at, metadata
                        FROM conversation_sessions 
                        WHERE user_id = %(user_id)s
                        ORDER BY started_at DESC
                        LIMIT %(limit)s
                    """, {"user_id": user_id, "limit": limit})
                    
                    rows = await cur.fetchall()
                    return [
                        {
                            "session_id": row[0],
                            "user_id": row[1],
                            "started_at": row[2],
                            "ended_at": row[3],
                            "metadata": row[4]
                        }
                        for row in rows
                    ]
        except Exception as e:
            print(f"❌ Error getting user conversation sessions: {e}")
            return []
    
    async def get_conversation_session(self, session_id: str) -> Optional[Dict[str, Any]]:
        """Get a specific conversation session"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT session_id, user_id, started_at, ended_at, metadata
                        FROM conversation_sessions 
                        WHERE session_id = %(session_id)s
                    """, {"session_id": session_id})
                    
                    row = await cur.fetchone()
                    if row:
                        return {
                            "session_id": row[0],
                            "user_id": row[1],
                            "started_at": row[2],
                            "ended_at": row[3],
                            "metadata": row[4]
                        }
                    return None
        except Exception as e:
            print(f"❌ Error getting conversation session: {e}")
            return None
    
    async def get_conversation_turns(self, session_id: str) -> List[Dict[str, Any]]:
        """Get all conversation turns for a session"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT turn_id, session_id, user_id, turn_index, user_message, 
                               assistant_response, retrieved_chunks, created_at, metadata
                        FROM conversation_turns 
                        WHERE session_id = %(session_id)s
                        ORDER BY turn_index, created_at
                    """, {"session_id": session_id})
                    
                    rows = await cur.fetchall()
                    return [
                        {
                            "turn_id": row[0],
                            "session_id": row[1],
                            "user_id": row[2],
                            "turn_index": row[3],
                            "user_message": row[4],
                            "assistant_response": row[5],
                            "retrieved_chunks": row[6],
                            "created_at": row[7],
                            "metadata": row[8]
                        }
                        for row in rows
                    ]
        except Exception as e:
            print(f"❌ Error getting conversation turns: {e}")
            return []
    
    async def get_conversation_turn(self, turn_id: str) -> Optional[Dict[str, Any]]:
        """Get a specific conversation turn"""
        try:
            async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT turn_id, session_id, user_id, turn_index, user_message, 
                               assistant_response, retrieved_chunks, created_at, metadata
                        FROM conversation_turns 
                        WHERE turn_id = %(turn_id)s
                    """, {"turn_id": turn_id})
                    
                    row = await cur.fetchone()
                    if row:
                        return {
                            "turn_id": row[0],
                            "session_id": row[1],
                            "user_id": row[2],
                            "turn_index": row[3],
                            "user_message": row[4],
                            "assistant_response": row[5],
                            "retrieved_chunks": row[6],
                            "created_at": row[7],
                            "metadata": row[8]
                        }
                    return None
        except Exception as e:
            print(f"❌ Error getting conversation turn: {e}")
            return None