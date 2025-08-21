"""
Conversation Manager for RAG Companion

This module provides comprehensive conversation management including:
- Session creation and management
- Turn tracking with proper database storage
- Content organization (text, multimedia, MCP tools)
- Conversation history retrieval
- Context building for AI responses
"""

import asyncio
import uuid
import time
from datetime import datetime
from typing import Dict, List, Any, Optional, Tuple
import logging
from dataclasses import dataclass

from database import RAGDatabase

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

@dataclass
class ConversationSession:
    """Represents a conversation session"""
    session_id: str
    user_id: str
    started_at: datetime
    ended_at: Optional[datetime] = None
    metadata: Dict[str, Any] = None

@dataclass
class ConversationTurn:
    """Represents a conversation turn"""
    turn_id: str
    session_id: str
    user_id: str
    turn_index: int
    user_message: str
    assistant_response: str
    retrieved_chunks: List[str] = None
    metadata: Dict[str, Any] = None

@dataclass
class ConversationContent:
    """Represents content within a conversation turn"""
    content_id: str
    turn_id: str
    user_id: str
    content_type: str  # 'text', 'image', 'audio', 'video', 'file'
    content_data: Optional[str] = None
    multimedia_id: Optional[str] = None
    content_order: int = 0
    is_user_content: bool = True
    mcp_tool_used: Optional[str] = None
    tool_parameters: Optional[Dict[str, Any]] = None
    metadata: Optional[Dict[str, Any]] = None

class ConversationManager:
    """
    Manages conversation sessions, turns, and content storage
    """
    
    def __init__(self, database: RAGDatabase):
        self.database = database
        self.active_sessions: Dict[str, ConversationSession] = {}
        self.session_turn_counts: Dict[str, int] = {}
    
    async def start_conversation_session(self, user_id: str, metadata: Dict[str, Any] = None) -> str:
        """
        Start a new conversation session
        
        Args:
            user_id: User identifier
            metadata: Optional session metadata
            
        Returns:
            Session ID for the new conversation
        """
        try:
            session_id = str(uuid.uuid4())
            session = ConversationSession(
                session_id=session_id,
                user_id=user_id,
                started_at=datetime.now(),
                metadata=metadata or {}
            )
            
            # Store session in database
            await self.database.store_conversation_session(session_id, user_id, metadata)
            
            # Track locally
            self.active_sessions[session_id] = session
            self.session_turn_counts[session_id] = 0
            
            logger.info(f"Started conversation session {session_id} for user {user_id}")
            return session_id
            
        except Exception as e:
            logger.error(f"Error starting conversation session: {e}")
            raise
    
    async def end_conversation_session(self, session_id: str) -> bool:
        """
        End a conversation session
        
        Args:
            session_id: Session identifier
            
        Returns:
            True if successful, False otherwise
        """
        try:
            if session_id in self.active_sessions:
                session = self.active_sessions[session_id]
                session.ended_at = datetime.now()
                
                # Update database
                await self.database.update_conversation_session(session_id, ended_at=session.ended_at)
                
                # Remove from local tracking
                del self.active_sessions[session_id]
                if session_id in self.session_turn_counts:
                    del self.session_turn_counts[session_id]
                
                logger.info(f"Ended conversation session {session_id}")
                return True
            
            return False
            
        except Exception as e:
            logger.error(f"Error ending conversation session {session_id}: {e}")
            return False
    
    async def add_conversation_turn(self, session_id: str, user_id: str, 
                                  user_message: str, assistant_response: str,
                                  retrieved_chunks: List[str] = None,
                                  metadata: Dict[str, Any] = None) -> str:
        """
        Add a new conversation turn to a session
        
        Args:
            session_id: Session identifier
            user_id: User identifier
            user_message: User's message
            assistant_response: AI's response
            retrieved_chunks: List of retrieved document chunks
            metadata: Additional turn metadata
            
        Returns:
            Turn ID for the new turn
        """
        try:
            # Validate session exists
            if session_id not in self.active_sessions:
                raise ValueError(f"Session {session_id} not found or not active")
            
            # Generate turn ID and index
            turn_id = str(uuid.uuid4())
            turn_index = self.session_turn_counts.get(session_id, 0) + 1
            
            # Create turn object
            turn = ConversationTurn(
                turn_id=turn_id,
                session_id=session_id,
                user_id=user_id,
                turn_index=turn_index,
                user_message=user_message,
                assistant_response=assistant_response,
                retrieved_chunks=retrieved_chunks or [],
                metadata=metadata or {}
            )
            
            # Store turn in database
            await self.database.log_conversation_turn(
                turn_id=turn_id,
                session_id=session_id,
                user_id=user_id,
                turn_index=turn_index,
                user_message=user_message,
                assistant_response=assistant_response,
                retrieved_chunks=retrieved_chunks or [],
                metadata=metadata or {}
            )
            
            # Update turn count
            self.session_turn_counts[session_id] = turn_index
            
            logger.info(f"Added turn {turn_index} to session {session_id}")
            return turn_id
            
        except Exception as e:
            logger.error(f"Error adding conversation turn: {e}")
            raise
    
    async def add_conversation_content(self, turn_id: str, user_id: str,
                                     content_type: str, content_data: str = None,
                                     multimedia_id: str = None, content_order: int = 0,
                                     is_user_content: bool = True, mcp_tool_used: str = None,
                                     tool_parameters: Dict[str, Any] = None,
                                     metadata: Dict[str, Any] = None) -> str:
        """
        Add content to a conversation turn
        
        Args:
            turn_id: Turn identifier
            user_id: User identifier
            content_type: Type of content ('text', 'image', 'audio', 'video', 'file')
            content_data: Text content (for text type)
            multimedia_id: Reference to multimedia content
            content_order: Order within the turn
            is_user_content: Whether content is from user
            mcp_tool_used: MCP tool that generated this content
            tool_parameters: Parameters used by the tool
            metadata: Additional content metadata
            
        Returns:
            Content ID for the new content
        """
        try:
            content_id = str(uuid.uuid4())
            
            # Store content in database
            success = await self.database.store_conversation_content(
                content_id=content_id,
                turn_id=turn_id,
                user_id=user_id,
                content_type=content_type,
                content_data=content_data,
                multimedia_id=multimedia_id,
                content_order=content_order,
                is_user_content=is_user_content,
                mcp_tool_used=mcp_tool_used,
                tool_parameters=tool_parameters,
                metadata=metadata
            )
            
            if success:
                logger.info(f"Added {content_type} content to turn {turn_id}")
                return content_id
            else:
                raise Exception("Failed to store conversation content")
                
        except Exception as e:
            logger.error(f"Error adding conversation content: {e}")
            raise
    
    async def get_conversation_history(self, user_id: str, limit: int = 10) -> List[Dict[str, Any]]:
        """
        Get conversation history for a user
        
        Args:
            user_id: User identifier
            limit: Maximum number of sessions to retrieve
            
        Returns:
            List of conversation sessions with turns
        """
        try:
            # Get user's conversation sessions
            sessions = await self.database.get_user_conversation_sessions(user_id, limit)
            
            conversation_history = []
            for session in sessions:
                # Get turns for this session
                turns = await self.database.get_conversation_turns(session["session_id"])
                
                # Get content for each turn
                session_with_turns = {
                    "session": session,
                    "turns": []
                }
                
                for turn in turns:
                    # Get content for this turn
                    content = await self.database.get_conversation_content(turn["turn_id"])
                    
                    turn_with_content = {
                        "turn": turn,
                        "content": content
                    }
                    session_with_turns["turns"].append(turn_with_content)
                
                conversation_history.append(session_with_turns)
            
            return conversation_history
            
        except Exception as e:
            logger.error(f"Error getting conversation history: {e}")
            return []
    
    async def get_conversation_context(self, user_id: str, session_id: str = None, 
                                     context_length: int = 5) -> str:
        """
        Get conversation context for AI response generation
        
        Args:
            user_id: User identifier
            session_id: Optional specific session ID
            context_length: Number of recent turns to include
            
        Returns:
            Formatted conversation context string
        """
        try:
            if session_id:
                # Get context from specific session
                turns = await self.database.get_conversation_turns(session_id)
                turns = turns[-context_length:] if len(turns) > context_length else turns
            else:
                # Get context from most recent session
                sessions = await self.database.get_user_conversation_sessions(user_id, 1)
                if not sessions:
                    return ""
                
                turns = await self.database.get_conversation_turns(sessions[0]["session_id"])
                turns = turns[-context_length:] if len(turns) > context_length else turns
            
            if not turns:
                return ""
            
            context_parts = []
            for turn in turns:
                context_parts.append(f"User: {turn['user_message']}")
                context_parts.append(f"Assistant: {turn['assistant_response']}")
            
            return "\n".join(context_parts)
            
        except Exception as e:
            logger.error(f"Error getting conversation context: {e}")
            return ""
    
    async def search_conversation_history(self, user_id: str, query: str, 
                                        limit: int = 5) -> List[Dict[str, Any]]:
        """
        Search conversation history using vector similarity
        
        Args:
            user_id: User identifier
            query: Search query
            limit: Maximum number of results
            
        Returns:
            List of relevant conversation turns
        """
        try:
            # This would require implementing vector search on conversation content
            # For now, return basic text search results
            sessions = await self.database.get_user_conversation_sessions(user_id, 20)
            
            relevant_turns = []
            query_lower = query.lower()
            
            for session in sessions:
                turns = await self.database.get_conversation_turns(session["session_id"])
                
                for turn in turns:
                    # Simple text matching for now
                    if (query_lower in turn["user_message"].lower() or 
                        query_lower in turn["assistant_response"].lower()):
                        
                        relevant_turns.append({
                            "session_id": session["session_id"],
                            "turn": turn,
                            "relevance_score": 1.0  # Placeholder
                        })
                        
                        if len(relevant_turns) >= limit:
                            break
                
                if len(relevant_turns) >= limit:
                    break
            
            return relevant_turns
            
        except Exception as e:
            logger.error(f"Error searching conversation history: {e}")
            return []
    
    async def get_active_session(self, user_id: str) -> Optional[str]:
        """
        Get the active session ID for a user
        
        Args:
            user_id: User identifier
            
        Returns:
            Active session ID or None
        """
        for session_id, session in self.active_sessions.items():
            if session.user_id == user_id:
                return session_id
        return None
    
    async def get_session_summary(self, session_id: str) -> Dict[str, Any]:
        """
        Get a summary of a conversation session
        
        Args:
            session_id: Session identifier
            
        Returns:
            Session summary with statistics
        """
        try:
            session = await self.database.get_conversation_session(session_id)
            if not session:
                return {}
            
            turns = await self.database.get_conversation_turns(session_id)
            
            # Count different types of content
            content_stats = {"text": 0, "image": 0, "audio": 0, "video": 0, "file": 0}
            mcp_tools_used = set()
            
            for turn in turns:
                content = await self.database.get_conversation_content(turn["turn_id"])
                for item in content:
                    content_type = item["content_type"]
                    if content_type in content_stats:
                        content_stats[content_type] += 1
                    
                    if item.get("mcp_tool_used"):
                        mcp_tools_used.add(item["mcp_tool_used"])
            
            summary = {
                "session_id": session_id,
                "user_id": session["user_id"],
                "started_at": session["started_at"],
                "ended_at": session.get("ended_at"),
                "total_turns": len(turns),
                "content_stats": content_stats,
                "mcp_tools_used": list(mcp_tools_used),
                "metadata": session.get("metadata", {})
            }
            
            return summary
            
        except Exception as e:
            logger.error(f"Error getting session summary: {e}")
            return {}
    
    async def cleanup_old_sessions(self, days_old: int = 30) -> int:
        """
        Clean up old conversation sessions
        
        Args:
            days_old: Remove sessions older than this many days
            
        Returns:
            Number of sessions removed
        """
        try:
            # This would require implementing cleanup in the database
            # For now, just log the request
            logger.info(f"Cleanup requested for sessions older than {days_old} days")
            return 0
            
        except Exception as e:
            logger.error(f"Error during session cleanup: {e}")
            return 0
