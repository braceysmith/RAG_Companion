"""
Sprig Management System for RAG Service

This module provides comprehensive Sprig (AI companion character) management
including creation, storage, retrieval, and content management.
"""

import os
import uuid
import json
import asyncio
from datetime import datetime, timedelta
from typing import List, Dict, Any, Optional, Union
from dataclasses import dataclass, asdict
from pathlib import Path

import psycopg
from psycopg import sql
import numpy as np
from openai import OpenAI

# Configure logging
import logging
logger = logging.getLogger(__name__)

@dataclass
class SprigPersonality:
    """Sprig personality traits"""
    honesty_humility: float = 5.0
    emotionality: float = 5.0
    extraversion: float = 5.0
    agreeableness: float = 5.0
    conscientiousness: float = 5.0
    openness: float = 5.0

@dataclass
class SprigAppearance:
    """Sprig visual appearance"""
    wood_type: str = "oak"
    color_scheme: str = "natural"
    size: str = "medium"
    texture: str = "smooth"
    special_features: List[str] = None
    
    def __post_init__(self):
        if self.special_features is None:
            self.special_features = []

@dataclass
class SprigContent:
    """Sprig content and conversation data"""
    content_id: str
    sprig_id: str
    content_type: str  # 'conversation', 'memory', 'preference', 'skill'
    content: str
    metadata: Dict[str, Any] = None
    created_at: datetime = None
    updated_at: datetime = None
    
    def __post_init__(self):
        if self.metadata is None:
            self.metadata = {}
        if self.created_at is None:
            self.created_at = datetime.now()
        if self.updated_at is None:
            self.updated_at = datetime.now()

@dataclass
class Sprig:
    """Complete Sprig character definition"""
    sprig_id: str
    user_id: str
    name: str
    description: str
    personality: SprigPersonality
    appearance: SprigAppearance
    backstory: str = ""
    skills: List[str] = None
    preferences: Dict[str, Any] = None
    mood: str = "neutral"
    energy_level: int = 5
    created_at: datetime = None
    updated_at: datetime = None
    is_active: bool = True
    is_default: bool = False
    
    def __post_init__(self):
        if self.skills is None:
            self.skills = []
        if self.preferences is None:
            self.preferences = {}
        if self.created_at is None:
            self.created_at = datetime.now()
        if self.updated_at is None:
            self.updated_at = datetime.now()

class SprigManager:
    """Manages Sprig creation, storage, and retrieval"""
    
    def __init__(self, db_url: str, openai_api_key: str):
        self.db_url = db_url
        self.openai_client = OpenAI(api_key=openai_api_key)
        
    async def initialize(self):
        """Initialize Sprig management tables"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # Create sprigs table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS sprigs (
                        sprig_id TEXT PRIMARY KEY,
                        user_id TEXT NOT NULL,
                        name TEXT NOT NULL,
                        description TEXT,
                        backstory TEXT,
                        personality JSONB NOT NULL,
                        appearance JSONB NOT NULL,
                        skills TEXT[],
                        preferences JSONB,
                        mood TEXT DEFAULT 'neutral',
                        energy_level INTEGER DEFAULT 5,
                        is_active BOOLEAN DEFAULT TRUE,
                        is_default BOOLEAN DEFAULT FALSE,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        updated_at TIMESTAMPTZ DEFAULT NOW()
                    )
                """)
                
                # Create sprig_content table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS sprig_content (
                        content_id TEXT PRIMARY KEY,
                        sprig_id TEXT NOT NULL,
                        content_type TEXT NOT NULL,
                        content TEXT NOT NULL,
                        metadata JSONB,
                        embedding VECTOR(1536),
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        updated_at TIMESTAMPTZ DEFAULT NOW(),
                        FOREIGN KEY (sprig_id) REFERENCES sprigs(sprig_id) ON DELETE CASCADE
                    )
                """)
                
                # Create sprig_conversations table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS sprig_conversations (
                        conversation_id TEXT PRIMARY KEY,
                        sprig_id TEXT NOT NULL,
                        user_id TEXT NOT NULL,
                        session_id TEXT,
                        message TEXT NOT NULL,
                        response TEXT,
                        message_type TEXT DEFAULT 'text',
                        metadata JSONB,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        FOREIGN KEY (sprig_id) REFERENCES sprigs(sprig_id) ON DELETE CASCADE
                    )
                """)
                
                # Create indexes
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS sprigs_user_id_idx ON sprigs(user_id)
                """)
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS sprigs_active_idx ON sprigs(is_active)
                """)
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS sprig_content_sprig_id_idx ON sprig_content(sprig_id)
                """)
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS sprig_content_type_idx ON sprig_content(content_type)
                """)
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS sprig_conversations_sprig_id_idx ON sprig_conversations(sprig_id)
                """)
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS sprig_conversations_user_id_idx ON sprig_conversations(user_id)
                """)
                
                # Create vector index for content embeddings
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS sprig_content_embedding_idx 
                    ON sprig_content USING ivfflat (embedding vector_l2_ops) 
                    WITH (lists = 100)
                """)
                
                logger.info("Sprig management tables initialized successfully")
    
    async def create_sprig(self, user_id: str, name: str, description: str = "", 
                          backstory: str = "", personality: Optional[SprigPersonality] = None,
                          appearance: Optional[SprigAppearance] = None) -> Sprig:
        """Create a new Sprig for a user"""
        sprig_id = str(uuid.uuid4())
        
        # Set defaults if not provided
        if personality is None:
            personality = SprigPersonality()
        if appearance is None:
            appearance = SprigAppearance()
        
        sprig = Sprig(
            sprig_id=sprig_id,
            user_id=user_id,
            name=name,
            description=description,
            backstory=backstory,
            personality=personality,
            appearance=appearance
        )
        
        # Store in database
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    INSERT INTO sprigs (sprig_id, user_id, name, description, backstory,
                                      personality, appearance, skills, preferences, mood, energy_level)
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                """, (
                    sprig.sprig_id,
                    sprig.user_id,
                    sprig.name,
                    sprig.description,
                    sprig.backstory,
                    json.dumps(asdict(sprig.personality)),
                    json.dumps(asdict(sprig.appearance)),
                    sprig.skills,
                    json.dumps(sprig.preferences),
                    sprig.mood,
                    sprig.energy_level
                ))
        
        logger.info(f"Created Sprig {name} ({sprig_id}) for user {user_id}")
        return sprig
    
    async def get_sprig(self, sprig_id: str) -> Optional[Sprig]:
        """Get a Sprig by ID"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    SELECT sprig_id, user_id, name, description, backstory, personality,
                           appearance, skills, preferences, mood, energy_level, is_active,
                           is_default, created_at, updated_at
                    FROM sprigs WHERE sprig_id = %s
                """, (sprig_id,))
                
                row = await cur.fetchone()
                if not row:
                    return None
                
                return self._row_to_sprig(row)
    
    async def get_user_sprigs(self, user_id: str, active_only: bool = True) -> List[Sprig]:
        """Get all Sprigs for a user"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                query = """
                    SELECT sprig_id, user_id, name, description, backstory, personality,
                           appearance, skills, preferences, mood, energy_level, is_active,
                           is_default, created_at, updated_at
                    FROM sprigs WHERE user_id = %s
                """
                params = [user_id]
                
                if active_only:
                    query += " AND is_active = TRUE"
                
                query += " ORDER BY created_at DESC"
                
                await cur.execute(query, params)
                rows = await cur.fetchall()
                
                return [self._row_to_sprig(row) for row in rows]
    
    async def get_default_sprig(self, user_id: str) -> Optional[Sprig]:
        """Get the default Sprig for a user"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    SELECT sprig_id, user_id, name, description, backstory, personality,
                           appearance, skills, preferences, mood, energy_level, is_active,
                           is_default, created_at, updated_at
                    FROM sprigs WHERE user_id = %s AND is_default = TRUE AND is_active = TRUE
                    LIMIT 1
                """, (user_id,))
                
                row = await cur.fetchone()
                if not row:
                    return None
                
                return self._row_to_sprig(row)
    
    async def set_default_sprig(self, user_id: str, sprig_id: str) -> bool:
        """Set a Sprig as the default for a user"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # First, unset any existing default
                await cur.execute("""
                    UPDATE sprigs SET is_default = FALSE 
                    WHERE user_id = %s AND is_default = TRUE
                """, (user_id,))
                
                # Set the new default
                await cur.execute("""
                    UPDATE sprigs SET is_default = TRUE, updated_at = NOW()
                    WHERE sprig_id = %s AND user_id = %s AND is_active = TRUE
                """, (sprig_id, user_id))
                
                return cur.rowcount > 0
    
    async def update_sprig(self, sprig_id: str, updates: Dict[str, Any]) -> bool:
        """Update a Sprig with new data"""
        if not updates:
            return False
        
        # Build dynamic update query
        set_clauses = []
        params = []
        
        for key, value in updates.items():
            if key in ['personality', 'appearance', 'preferences']:
                set_clauses.append(f"{key} = %s")
                params.append(json.dumps(value) if isinstance(value, dict) else value)
            elif key in ['skills']:
                set_clauses.append(f"{key} = %s")
                params.append(value)
            else:
                set_clauses.append(f"{key} = %s")
                params.append(value)
        
        set_clauses.append("updated_at = NOW()")
        params.append(sprig_id)
        
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                query = f"UPDATE sprigs SET {', '.join(set_clauses)} WHERE sprig_id = %s"
                await cur.execute(query, params)
                
                return cur.rowcount > 0
    
    async def delete_sprig(self, sprig_id: str, user_id: str) -> bool:
        """Delete a Sprig (soft delete by setting is_active = FALSE)"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    UPDATE sprigs SET is_active = FALSE, updated_at = NOW()
                    WHERE sprig_id = %s AND user_id = %s
                """, (sprig_id, user_id))
                
                return cur.rowcount > 0
    
    async def add_sprig_content(self, sprig_id: str, content_type: str, content: str,
                               metadata: Optional[Dict[str, Any]] = None) -> str:
        """Add content to a Sprig"""
        content_id = str(uuid.uuid4())
        
        # Generate embedding for the content
        embedding = await self._generate_embedding(content)
        
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    INSERT INTO sprig_content (content_id, sprig_id, content_type, content, 
                                             metadata, embedding)
                    VALUES (%s, %s, %s, %s, %s, %s)
                """, (
                    content_id,
                    sprig_id,
                    content_type,
                    content,
                    json.dumps(metadata or {}),
                    embedding
                ))
        
        logger.info(f"Added {content_type} content to Sprig {sprig_id}")
        return content_id
    
    async def get_sprig_content(self, sprig_id: str, content_type: Optional[str] = None,
                               limit: int = 50) -> List[SprigContent]:
        """Get content for a Sprig"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                query = """
                    SELECT content_id, sprig_id, content_type, content, metadata, 
                           created_at, updated_at
                    FROM sprig_content WHERE sprig_id = %s
                """
                params = [sprig_id]
                
                if content_type:
                    query += " AND content_type = %s"
                    params.append(content_type)
                
                query += " ORDER BY created_at DESC LIMIT %s"
                params.append(limit)
                
                await cur.execute(query, params)
                rows = await cur.fetchall()
                
                return [self._row_to_sprig_content(row) for row in rows]
    
    async def search_sprig_content(self, sprig_id: str, query: str, 
                                  content_type: Optional[str] = None,
                                  top_k: int = 5) -> List[Dict[str, Any]]:
        """Search Sprig content using semantic similarity"""
        # Generate embedding for the query
        query_embedding = await self._generate_embedding(query)
        
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                sql_query = """
                    SELECT content_id, sprig_id, content_type, content, metadata,
                           created_at, updated_at,
                           1 - (embedding <=> %s) as similarity
                    FROM sprig_content 
                    WHERE sprig_id = %s
                """
                params = [query_embedding, sprig_id]
                
                if content_type:
                    sql_query += " AND content_type = %s"
                    params.append(content_type)
                
                sql_query += " ORDER BY similarity DESC LIMIT %s"
                params.append(top_k)
                
                await cur.execute(sql_query, params)
                rows = await cur.fetchall()
                
                results = []
                for row in rows:
                    results.append({
                        'content_id': row[0],
                        'sprig_id': row[1],
                        'content_type': row[2],
                        'content': row[3],
                        'metadata': json.loads(row[4]) if row[4] else {},
                        'created_at': row[5],
                        'updated_at': row[6],
                        'similarity': float(row[7])
                    })
                
                return results
    
    async def log_conversation(self, sprig_id: str, user_id: str, message: str,
                              response: str, session_id: Optional[str] = None,
                              message_type: str = "text", metadata: Optional[Dict[str, Any]] = None):
        """Log a conversation with a Sprig"""
        conversation_id = str(uuid.uuid4())
        
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    INSERT INTO sprig_conversations (conversation_id, sprig_id, user_id, 
                                                   session_id, message, response, message_type, metadata)
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                """, (
                    conversation_id,
                    sprig_id,
                    user_id,
                    session_id,
                    message,
                    response,
                    message_type,
                    json.dumps(metadata or {})
                ))
        
        logger.info(f"Logged conversation for Sprig {sprig_id}")
        return conversation_id
    
    async def get_conversation_history(self, sprig_id: str, user_id: str,
                                      limit: int = 50) -> List[Dict[str, Any]]:
        """Get conversation history for a Sprig"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    SELECT conversation_id, session_id, message, response, message_type,
                           metadata, created_at
                    FROM sprig_conversations 
                    WHERE sprig_id = %s AND user_id = %s
                    ORDER BY created_at DESC
                    LIMIT %s
                """, (sprig_id, user_id, limit))
                
                rows = await cur.fetchall()
                
                conversations = []
                for row in rows:
                    conversations.append({
                        'conversation_id': row[0],
                        'session_id': row[1],
                        'message': row[2],
                        'response': row[3],
                        'message_type': row[4],
                        'metadata': json.loads(row[5]) if row[5] else {},
                        'created_at': row[6]
                    })
                
                return conversations
    
    async def _generate_embedding(self, text: str) -> List[float]:
        """Generate embedding for text using OpenAI"""
        try:
            response = self.openai_client.embeddings.create(
                model="text-embedding-3-small",
                input=text
            )
            return response.data[0].embedding
        except Exception as e:
            logger.error(f"Error generating embedding: {e}")
            # Return zero vector as fallback
            return [0.0] * 1536
    
    def _row_to_sprig(self, row) -> Sprig:
        """Convert database row to Sprig object"""
        return Sprig(
            sprig_id=row[0],
            user_id=row[1],
            name=row[2],
            description=row[3] or "",
            backstory=row[4] or "",
            personality=SprigPersonality(**json.loads(row[5])),
            appearance=SprigAppearance(**json.loads(row[6])),
            skills=row[7] or [],
            preferences=json.loads(row[8]) if row[8] else {},
            mood=row[9] or "neutral",
            energy_level=row[10] or 5,
            is_active=row[11],
            is_default=row[12],
            created_at=row[13],
            updated_at=row[14]
        )
    
    def _row_to_sprig_content(self, row) -> SprigContent:
        """Convert database row to SprigContent object"""
        return SprigContent(
            content_id=row[0],
            sprig_id=row[1],
            content_type=row[2],
            content=row[3],
            metadata=json.loads(row[4]) if row[4] else {},
            created_at=row[5],
            updated_at=row[6]
        )
