import os
import asyncio
import psycopg
from psycopg import sql
from typing import List, Dict, Any
import numpy as np
from pgvector.psycopg import register_vector

class RAGDatabase:
    def __init__(self, db_url: str):
        self.db_url = db_url
        
    async def initialize(self):
        """Initialize database with required tables and extensions"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            await register_vector(conn)
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
                
                await conn.commit()
    
    async def upsert_chunks(self, chunks: List[Dict[str, Any]]):
        """Insert or update chunks in the database"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            await register_vector(conn)
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
            await register_vector(conn)
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
            await register_vector(conn)
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
            await register_vector(conn)
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