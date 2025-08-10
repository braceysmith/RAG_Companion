"""
Memory System for RAG Companions

This module implements a hybrid RAG memory system with encrypted local storage
for sensitive data and secure transfer mechanisms between devices.
"""

from typing import Dict, List, Optional, Any, Union, Tuple
from dataclasses import dataclass, field
from datetime import datetime, timedelta
import json
import uuid
import hashlib
import hmac
import os
import base64
from pathlib import Path
import logging
from cryptography.fernet import Fernet
from cryptography.hazmat.primitives import hashes
from cryptography.hazmat.primitives.kdf.pbkdf2 import PBKDF2HMAC
import sqlite3
from enum import Enum

logger = logging.getLogger(__name__)

class DataSensitivity(Enum):
    """Data sensitivity levels for routing decisions"""
    PUBLIC = "public"           # Can be stored in cloud
    INTERNAL = "internal"       # Company/internal data
    PERSONAL = "personal"       # Personal but not highly sensitive
    SENSITIVE = "sensitive"     # Highly sensitive personal data
    CRITICAL = "critical"       # Critical security/health data

@dataclass
class MemoryChunk:
    """A chunk of memory with metadata and content"""
    chunk_id: str
    user_id: str
    companion_id: str
    content: str
    content_type: str  # text, image, audio, etc.
    sensitivity: DataSensitivity
    tags: List[str] = field(default_factory=list)
    created_at: datetime = field(default_factory=datetime.now)
    last_accessed: datetime = field(default_factory=datetime.now)
    access_count: int = 0
    embedding: Optional[List[float]] = None
    metadata: Dict[str, Any] = field(default_factory=dict)

@dataclass
class TransferRequest:
    """Request for secure data transfer between devices"""
    transfer_id: str
    source_device_id: str
    target_device_id: str
    user_id: str
    chunk_ids: List[str]
    requested_at: datetime
    expires_at: datetime
    verification_code: str
    status: str = "pending"  # pending, verified, completed, expired

class HybridMemorySystem:
    """
    Hybrid RAG memory system with local/cloud routing based on sensitivity
    """
    
    def __init__(self, data_dir: str = "data", encryption_key: Optional[str] = None):
        self.data_dir = Path(data_dir)
        self.data_dir.mkdir(exist_ok=True)
        
        # Initialize encryption
        self.encryption_key = encryption_key or self._generate_encryption_key()
        self.cipher_suite = Fernet(self.encryption_key)
        
        # Initialize databases
        self._init_local_db()
        self._init_transfer_db()
        
        # Device identification
        self.device_id = self._get_device_id()
        
        logger.info(f"Memory system initialized for device: {self.device_id}")
    
    def _generate_encryption_key(self) -> str:
        """Generate a new encryption key"""
        key = Fernet.generate_key()
        key_path = self.data_dir / "encryption.key"
        with open(key_path, "wb") as f:
            f.write(key)
        return key.decode()
    
    def _get_device_id(self) -> str:
        """Get or generate unique device identifier"""
        device_id_path = self.data_dir / "device_id"
        if device_id_path.exists():
            with open(device_id_path, "r") as f:
                return f.read().strip()
        else:
            device_id = str(uuid.uuid4())
            with open(device_id_path, "w") as f:
                f.write(device_id)
            return device_id
    
    def _init_local_db(self):
        """Initialize local SQLite database for sensitive data"""
        db_path = self.data_dir / "local_memory.db"
        self.local_conn = sqlite3.connect(str(db_path))
        self.local_conn.row_factory = sqlite3.Row
        
        # Create tables
        self.local_conn.executescript("""
            CREATE TABLE IF NOT EXISTS memory_chunks (
                chunk_id TEXT PRIMARY KEY,
                user_id TEXT NOT NULL,
                companion_id TEXT NOT NULL,
                content TEXT NOT NULL,
                content_type TEXT NOT NULL,
                sensitivity TEXT NOT NULL,
                tags TEXT,
                created_at TEXT NOT NULL,
                last_accessed TEXT NOT NULL,
                access_count INTEGER DEFAULT 0,
                embedding TEXT,
                metadata TEXT
            );
            
            CREATE INDEX IF NOT EXISTS idx_user_companion ON memory_chunks(user_id, companion_id);
            CREATE INDEX IF NOT EXISTS idx_sensitivity ON memory_chunks(sensitivity);
            CREATE INDEX IF NOT EXISTS idx_tags ON memory_chunks(tags);
        """)
        self.local_conn.commit()
    
    def _init_transfer_db(self):
        """Initialize transfer request database"""
        db_path = self.data_dir / "transfers.db"
        self.transfer_conn = sqlite3.connect(str(db_path))
        self.transfer_conn.row_factory = sqlite3.Row
        
        self.transfer_conn.executescript("""
            CREATE TABLE IF NOT EXISTS transfer_requests (
                transfer_id TEXT PRIMARY KEY,
                source_device_id TEXT NOT NULL,
                target_device_id TEXT NOT NULL,
                user_id TEXT NOT NULL,
                chunk_ids TEXT NOT NULL,
                requested_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                verification_code TEXT NOT NULL,
                status TEXT DEFAULT 'pending'
            );
            
            CREATE INDEX IF NOT EXISTS idx_user_status ON transfer_requests(user_id, status);
            CREATE INDEX IF NOT EXISTS idx_expires ON transfer_requests(expires_at);
        """)
        self.transfer_conn.commit()
    
    def store_memory(self, user_id: str, companion_id: str, content: str, 
                    content_type: str, sensitivity: DataSensitivity, 
                    tags: List[str] = None, metadata: Dict[str, Any] = None) -> str:
        """Store a memory chunk with appropriate routing"""
        
        chunk_id = str(uuid.uuid4())
        tags = tags or []
        metadata = metadata or {}
        
        # Create memory chunk
        chunk = MemoryChunk(
            chunk_id=chunk_id,
            user_id=user_id,
            companion_id=companion_id,
            content=content,
            content_type=content_type,
            sensitivity=sensitivity,
            tags=tags,
            metadata=metadata
        )
        
        # Route based on sensitivity
        if sensitivity in [DataSensitivity.SENSITIVE, DataSensitivity.CRITICAL]:
            # Store locally with encryption
            self._store_local(chunk)
            logger.info(f"Stored sensitive memory locally: {chunk_id}")
        else:
            # Store locally for now (until cloud storage is implemented)
            # This ensures all data is accessible during testing
            self._store_local(chunk)
            logger.info(f"Stored {sensitivity.value} memory locally (cloud integration pending): {chunk_id}")
        
        return chunk_id
    
    def _store_local(self, chunk: MemoryChunk):
        """Store chunk in local encrypted database"""
        encrypted_content = self.cipher_suite.encrypt(chunk.content.encode())
        
        self.local_conn.execute("""
            INSERT INTO memory_chunks 
            (chunk_id, user_id, companion_id, content, content_type, sensitivity,
             tags, created_at, last_accessed, access_count, embedding, metadata)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, (
            chunk.chunk_id,
            chunk.user_id,
            chunk.companion_id,
            base64.b64encode(encrypted_content).decode(),
            chunk.content_type,
            chunk.sensitivity.value,
            json.dumps(chunk.tags),
            chunk.created_at.isoformat(),
            chunk.last_accessed.isoformat(),
            chunk.access_count,
            json.dumps(chunk.embedding) if chunk.embedding else None,
            json.dumps(chunk.metadata)
        ))
        self.local_conn.commit()
    
    def _store_cloud(self, chunk: MemoryChunk):
        """Store chunk in cloud storage (placeholder)"""
        # TODO: Implement cloud storage integration
        # For now, just log the intention
        logger.info(f"Would store in cloud: {chunk.chunk_id}")
    
    def retrieve_memory(self, chunk_id: str, user_id: str) -> Optional[MemoryChunk]:
        """Retrieve a memory chunk by ID"""
        
        # Try local storage first
        chunk = self._retrieve_local(chunk_id, user_id)
        if chunk:
            return chunk
        
        # Try cloud storage
        chunk = self._retrieve_cloud(chunk_id, user_id)
        if chunk:
            return chunk
        
        return None
    
    def _retrieve_local(self, chunk_id: str, user_id: str) -> Optional[MemoryChunk]:
        """Retrieve chunk from local storage"""
        cursor = self.local_conn.execute("""
            SELECT * FROM memory_chunks 
            WHERE chunk_id = ? AND user_id = ?
        """, (chunk_id, user_id))
        
        row = cursor.fetchone()
        if not row:
            return None
        
        # Decrypt content
        encrypted_content = base64.b64decode(row['content'])
        decrypted_content = self.cipher_suite.decrypt(encrypted_content).decode()
        
        # Update access tracking
        self.local_conn.execute("""
            UPDATE memory_chunks 
            SET last_accessed = ?, access_count = access_count + 1
            WHERE chunk_id = ?
        """, (datetime.now().isoformat(), chunk_id))
        self.local_conn.commit()
        
        return MemoryChunk(
            chunk_id=row['chunk_id'],
            user_id=row['user_id'],
            companion_id=row['companion_id'],
            content=decrypted_content,
            content_type=row['content_type'],
            sensitivity=DataSensitivity(row['sensitivity']),
            tags=json.loads(row['tags']) if row['tags'] else [],
            created_at=datetime.fromisoformat(row['created_at']),
            last_accessed=datetime.fromisoformat(row['last_accessed']),
            access_count=row['access_count'] + 1,
            embedding=json.loads(row['embedding']) if row['embedding'] else None,
            metadata=json.loads(row['metadata']) if row['metadata'] else {}
        )
    
    def _retrieve_cloud(self, chunk_id: str, user_id: str) -> Optional[MemoryChunk]:
        """Retrieve chunk from cloud storage (placeholder)"""
        # TODO: Implement cloud retrieval
        return None
    
    def search_memories(self, user_id: str, companion_id: str, 
                       query: str, limit: int = 10) -> List[MemoryChunk]:
        """Search memories by content and tags"""
        
        # Search local storage
        local_results = self._search_local(user_id, companion_id, query, limit)
        
        # Search cloud storage
        cloud_results = self._search_cloud(user_id, companion_id, query, limit)
        
        # Combine and rank results
        all_results = local_results + cloud_results
        all_results.sort(key=lambda x: x.access_count, reverse=True)
        
        return all_results[:limit]
    
    def _search_local(self, user_id: str, companion_id: str, 
                     query: str, limit: int) -> List[MemoryChunk]:
        """Search local storage"""
        # First get all memories for this user/companion, then filter by content
        cursor = self.local_conn.execute("""
            SELECT * FROM memory_chunks 
            WHERE user_id = ? AND companion_id = ?
            ORDER BY access_count DESC, last_accessed DESC
        """, (user_id, companion_id))
        
        results = []
        for row in cursor.fetchall():
            # Decrypt content for search
            encrypted_content = base64.b64decode(row['content'])
            decrypted_content = self.cipher_suite.decrypt(encrypted_content).decode()
            
            # Check if query matches content or tags
            tags = json.loads(row['tags']) if row['tags'] else []
            if (query.lower() in decrypted_content.lower() or 
                any(query.lower() in tag.lower() for tag in tags)):
                
                results.append(MemoryChunk(
                    chunk_id=row['chunk_id'],
                    user_id=row['user_id'],
                    companion_id=row['companion_id'],
                    content=decrypted_content,
                    content_type=row['content_type'],
                    sensitivity=DataSensitivity(row['sensitivity']),
                    tags=tags,
                    created_at=datetime.fromisoformat(row['created_at']),
                    last_accessed=datetime.fromisoformat(row['last_accessed']),
                    access_count=row['access_count'],
                    embedding=json.loads(row['embedding']) if row['embedding'] else None,
                    metadata=json.loads(row['metadata']) if row['metadata'] else {}
                ))
                
                # Stop if we have enough results
                if len(results) >= limit:
                    break
        
        return results
    
    def _search_cloud(self, user_id: str, companion_id: str, 
                     query: str, limit: int) -> List[MemoryChunk]:
        """Search cloud storage (placeholder)"""
        # TODO: Implement cloud search
        return []
    
    def initiate_transfer(self, target_device_id: str, user_id: str, 
                         chunk_ids: List[str]) -> TransferRequest:
        """Initiate secure transfer of sensitive data to another device"""
        
        # Verify chunks exist and are sensitive
        sensitive_chunks = []
        for chunk_id in chunk_ids:
            chunk = self.retrieve_memory(chunk_id, user_id)
            if chunk and chunk.sensitivity in [DataSensitivity.SENSITIVE, DataSensitivity.CRITICAL]:
                sensitive_chunks.append(chunk_id)
        
        if not sensitive_chunks:
            raise ValueError("No sensitive chunks found for transfer")
        
        # Create transfer request
        transfer_id = str(uuid.uuid4())
        verification_code = self._generate_verification_code()
        
        transfer = TransferRequest(
            transfer_id=transfer_id,
            source_device_id=self.device_id,
            target_device_id=target_device_id,
            user_id=user_id,
            chunk_ids=sensitive_chunks,
            requested_at=datetime.now(),
            expires_at=datetime.now() + timedelta(minutes=30),  # 30 minute safety window
            verification_code=verification_code
        )
        
        # Store transfer request
        self.transfer_conn.execute("""
            INSERT INTO transfer_requests 
            (transfer_id, source_device_id, target_device_id, user_id, chunk_ids,
             requested_at, expires_at, verification_code, status)
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
        """, (
            transfer.transfer_id,
            transfer.source_device_id,
            transfer.target_device_id,
            transfer.user_id,
            json.dumps(transfer.chunk_ids),
            transfer.requested_at.isoformat(),
            transfer.expires_at.isoformat(),
            transfer.verification_code,
            transfer.status
        ))
        self.transfer_conn.commit()
        
        logger.info(f"Transfer initiated: {transfer_id} to {target_device_id}")
        return transfer
    
    def verify_transfer(self, transfer_id: str, verification_code: str) -> bool:
        """Verify transfer request with code"""
        
        cursor = self.transfer_conn.execute("""
            SELECT * FROM transfer_requests 
            WHERE transfer_id = ? AND status = 'pending'
        """, (transfer_id,))
        
        row = cursor.fetchone()
        if not row:
            return False
        
        # Check if expired
        expires_at = datetime.fromisoformat(row['expires_at'])
        if datetime.now() > expires_at:
            self._update_transfer_status(transfer_id, 'expired')
            return False
        
        # Verify code
        if row['verification_code'] != verification_code:
            return False
        
        # Update status to verified
        self._update_transfer_status(transfer_id, 'verified')
        return True
    
    def complete_transfer(self, transfer_id: str) -> bool:
        """Complete verified transfer"""
        
        cursor = self.transfer_conn.execute("""
            SELECT * FROM transfer_requests 
            WHERE transfer_id = ? AND status = 'verified'
        """, (transfer_id,))
        
        row = cursor.fetchone()
        if not row:
            return False
        
        # Mark as completed
        self._update_transfer_status(transfer_id, 'completed')
        
        logger.info(f"Transfer completed: {transfer_id}")
        return True
    
    def _update_transfer_status(self, transfer_id: str, status: str):
        """Update transfer request status"""
        self.transfer_conn.execute("""
            UPDATE transfer_requests 
            SET status = ? 
            WHERE transfer_id = ?
        """, (status, transfer_id))
        self.transfer_conn.commit()
    
    def _generate_verification_code(self) -> str:
        """Generate 6-digit verification code"""
        return str(uuid.uuid4().int % 1000000).zfill(6)
    
    def cleanup_expired_transfers(self):
        """Clean up expired transfer requests"""
        expired = datetime.now() - timedelta(hours=24)
        
        self.transfer_conn.execute("""
            UPDATE transfer_requests 
            SET status = 'expired' 
            WHERE expires_at < ? AND status = 'pending'
        """, (expired.isoformat(),))
        self.transfer_conn.commit()
        
        # Delete old completed/expired transfers
        self.transfer_conn.execute("""
            DELETE FROM transfer_requests 
            WHERE expires_at < ? AND status IN ('completed', 'expired')
        """, (expired.isoformat(),))
        self.transfer_conn.commit()
    
    def get_transfer_status(self, transfer_id: str) -> Optional[Dict[str, Any]]:
        """Get current status of a transfer request"""
        cursor = self.transfer_conn.execute("""
            SELECT * FROM transfer_requests 
            WHERE transfer_id = ?
        """, (transfer_id,))
        
        row = cursor.fetchone()
        if not row:
            return None
        
        return {
            'transfer_id': row['transfer_id'],
            'source_device_id': row['source_device_id'],
            'target_device_id': row['target_device_id'],
            'user_id': row['user_id'],
            'chunk_ids': json.loads(row['chunk_ids']),
            'requested_at': row['requested_at'],
            'expires_at': row['expires_at'],
            'status': row['status']
        }
    
    def close(self):
        """Close database connections"""
        self.local_conn.close()
        self.transfer_conn.close()
