"""
User Mapping System for Unity Authentication

This module handles mapping Unity user IDs to consistent user identifiers
based on email addresses, ensuring the same email creates the same user account
across different devices.
"""

import os
import hashlib
import asyncio
import psycopg
from typing import Optional, Dict, Any
from datetime import datetime
import logging

logger = logging.getLogger(__name__)

class UserMappingService:
    """Service for mapping Unity user IDs to consistent user identifiers"""
    
    def __init__(self, db_url: str):
        self.db_url = db_url
        
    async def initialize(self):
        """Initialize the user mapping tables"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # Create user_mapping table
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS user_mapping (
                        id SERIAL PRIMARY KEY,
                        email_hash TEXT NOT NULL UNIQUE,
                        email TEXT NOT NULL,
                        primary_user_id TEXT NOT NULL,
                        unity_user_id TEXT NOT NULL,
                        device_id TEXT,
                        platform TEXT,
                        created_at TIMESTAMPTZ DEFAULT NOW(),
                        last_seen TIMESTAMPTZ DEFAULT NOW(),
                        is_active BOOLEAN DEFAULT TRUE
                    )
                """)
                
                # Create indexes
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS user_mapping_email_hash_idx 
                    ON user_mapping(email_hash)
                """)
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS user_mapping_unity_user_id_idx 
                    ON user_mapping(unity_user_id)
                """)
                await cur.execute("""
                    CREATE INDEX IF NOT EXISTS user_mapping_primary_user_id_idx 
                    ON user_mapping(primary_user_id)
                """)
                
                logger.info("User mapping tables initialized successfully")
    
    def _hash_email(self, email: str) -> str:
        """Create a consistent hash of the email address"""
        # Normalize email (lowercase, trim whitespace)
        normalized_email = email.lower().strip()
        # Create SHA-256 hash
        return hashlib.sha256(normalized_email.encode('utf-8')).hexdigest()
    
    async def get_or_create_primary_user_id(self, email: str, unity_user_id: str, 
                                          device_id: Optional[str] = None, 
                                          platform: Optional[str] = None) -> str:
        """
        Get or create a primary user ID for the given email.
        If the email already exists, return the existing primary user ID.
        If not, create a new primary user ID and mapping.
        """
        email_hash = self._hash_email(email)
        
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # Check if email already exists
                await cur.execute("""
                    SELECT primary_user_id FROM user_mapping 
                    WHERE email_hash = %s AND is_active = TRUE
                    ORDER BY created_at ASC
                    LIMIT 1
                """, (email_hash,))
                
                existing_mapping = await cur.fetchone()
                
                if existing_mapping:
                    # Email exists, return existing primary user ID
                    primary_user_id = existing_mapping[0]
                    
                    # Update or create mapping for this Unity user ID
                    await cur.execute("""
                        INSERT INTO user_mapping (email_hash, email, primary_user_id, unity_user_id, device_id, platform, last_seen)
                        VALUES (%s, %s, %s, %s, %s, %s, NOW())
                        ON CONFLICT (email_hash, unity_user_id) 
                        DO UPDATE SET 
                            last_seen = NOW(),
                            device_id = EXCLUDED.device_id,
                            platform = EXCLUDED.platform,
                            is_active = TRUE
                    """, (email_hash, email.lower().strip(), primary_user_id, unity_user_id, device_id, platform))
                    
                    await conn.commit()
                    
                    logger.info(f"Found existing user for email {email}: {primary_user_id}")
                    return primary_user_id
                else:
                    # Email doesn't exist, create new primary user ID
                    primary_user_id = f"user_{email_hash[:16]}"  # Use first 16 chars of hash
                    
                    # Create mapping
                    await cur.execute("""
                        INSERT INTO user_mapping (email_hash, email, primary_user_id, unity_user_id, device_id, platform, last_seen)
                        VALUES (%s, %s, %s, %s, %s, %s, NOW())
                    """, (email_hash, email.lower().strip(), primary_user_id, unity_user_id, device_id, platform))
                    
                    await conn.commit()
                    
                    logger.info(f"Created new user for email {email}: {primary_user_id}")
                    return primary_user_id
    
    async def get_primary_user_id(self, unity_user_id: str) -> Optional[str]:
        """Get the primary user ID for a given Unity user ID"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    SELECT primary_user_id FROM user_mapping 
                    WHERE unity_user_id = %s AND is_active = TRUE
                """, (unity_user_id,))
                
                result = await cur.fetchone()
                return result[0] if result else None
    
    async def get_user_mappings(self, primary_user_id: str) -> list:
        """Get all Unity user IDs mapped to a primary user ID"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    SELECT unity_user_id, device_id, platform, last_seen, email
                    FROM user_mapping 
                    WHERE primary_user_id = %s AND is_active = TRUE
                    ORDER BY last_seen DESC
                """, (primary_user_id,))
                
                mappings = []
                for row in await cur.fetchall():
                    mappings.append({
                        'unity_user_id': row[0],
                        'device_id': row[1],
                        'platform': row[2],
                        'last_seen': row[3],
                        'email': row[4]
                    })
                
                return mappings
    
    async def deactivate_user_mapping(self, unity_user_id: str):
        """Deactivate a user mapping (for logout)"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    UPDATE user_mapping 
                    SET is_active = FALSE 
                    WHERE unity_user_id = %s
                """, (unity_user_id,))
                
                await conn.commit()
                logger.info(f"Deactivated mapping for Unity user ID: {unity_user_id}")
    
    async def get_user_stats(self, primary_user_id: str) -> Dict[str, Any]:
        """Get statistics about a user's mappings"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                # Get total mappings
                await cur.execute("""
                    SELECT COUNT(*) FROM user_mapping 
                    WHERE primary_user_id = %s AND is_active = TRUE
                """, (primary_user_id,))
                total_mappings = (await cur.fetchone())[0]
                
                # Get unique devices
                await cur.execute("""
                    SELECT COUNT(DISTINCT device_id) FROM user_mapping 
                    WHERE primary_user_id = %s AND is_active = TRUE AND device_id IS NOT NULL
                """, (primary_user_id,))
                unique_devices = (await cur.fetchone())[0]
                
                # Get platforms
                await cur.execute("""
                    SELECT DISTINCT platform FROM user_mapping 
                    WHERE primary_user_id = %s AND is_active = TRUE AND platform IS NOT NULL
                """, (primary_user_id,))
                platforms = [row[0] for row in await cur.fetchall()]
                
                # Get email
                await cur.execute("""
                    SELECT email FROM user_mapping 
                    WHERE primary_user_id = %s AND is_active = TRUE
                    LIMIT 1
                """, (primary_user_id,))
                email_result = await cur.fetchone()
                email = email_result[0] if email_result else None
                
                return {
                    'primary_user_id': primary_user_id,
                    'email': email,
                    'total_mappings': total_mappings,
                    'unique_devices': unique_devices,
                    'platforms': platforms
                }
    
    async def cleanup_inactive_mappings(self, days_inactive: int = 30):
        """Clean up mappings that haven't been seen for specified days"""
        async with await psycopg.AsyncConnection.connect(self.db_url) as conn:
            async with conn.cursor() as cur:
                await cur.execute("""
                    UPDATE user_mapping 
                    SET is_active = FALSE 
                    WHERE last_seen < NOW() - INTERVAL '%s days' AND is_active = TRUE
                """, (days_inactive,))
                
                affected_rows = cur.rowcount
                await conn.commit()
                
                if affected_rows > 0:
                    logger.info(f"Deactivated {affected_rows} inactive user mappings")
                
                return affected_rows
