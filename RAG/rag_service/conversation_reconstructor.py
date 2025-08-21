"""
Conversation Reconstruction System for RAG Companions

This system can fully reconstruct previous conversations including:
- All text messages
- Generated multimedia content (images, audio, video)
- MCP tool executions and parameters
- File paths and metadata
- Conversation flow and context
"""

import asyncio
import json
from datetime import datetime
from typing import Dict, Any, List, Optional, Tuple
from pathlib import Path
import logging
from dataclasses import dataclass

from database import RAGDatabase
from multimedia_storage import MultimediaStorageService

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

@dataclass
class ReconstructedMessage:
    """A reconstructed message with all its content"""
    message_id: str
    user_id: str
    timestamp: datetime
    is_user_message: bool
    text_content: Optional[str] = None
    multimedia_content: List[Dict[str, Any]] = None
    mcp_tool_used: Optional[str] = None
    tool_parameters: Optional[Dict[str, Any]] = None
    tool_result: Optional[Dict[str, Any]] = None

@dataclass
class ReconstructedConversation:
    """A fully reconstructed conversation"""
    session_id: str
    user_id: str
    started_at: datetime
    ended_at: Optional[datetime] = None
    messages: List[ReconstructedMessage] = None
    total_messages: int = 0
    total_multimedia: int = 0
    mcp_tools_used: List[str] = None
    metadata: Dict[str, Any] = None

class ConversationReconstructor:
    """
    System for reconstructing complete conversations with multimedia content
    """
    
    def __init__(self, database: RAGDatabase, multimedia_storage: MultimediaStorageService):
        self.database = database
        self.multimedia_storage = multimedia_storage
    
    async def reconstruct_user_conversations(self, user_id: str, 
                                          limit: int = 10) -> List[ReconstructedConversation]:
        """
        Reconstruct all conversations for a user
        
        Args:
            user_id: User identifier
            limit: Maximum number of conversations to reconstruct
            
        Returns:
            List of fully reconstructed conversations
        """
        try:
            # Get user's conversation sessions
            sessions = await self.database.get_user_conversation_sessions(user_id, limit)
            
            reconstructed_conversations = []
            for session in sessions:
                conversation = await self.reconstruct_conversation(session["session_id"])
                if conversation:
                    reconstructed_conversations.append(conversation)
            
            return reconstructed_conversations
            
        except Exception as e:
            logger.error(f"Error reconstructing user conversations: {e}")
            return []
    
    async def reconstruct_conversation(self, session_id: str) -> Optional[ReconstructedConversation]:
        """
        Reconstruct a complete conversation session
        
        Args:
            session_id: Session identifier
            
        Returns:
            Fully reconstructed conversation or None if failed
        """
        try:
            # Get session information
            session_info = await self.database.get_conversation_session(session_id)
            if not session_info:
                return None
            
            # Get all conversation turns for this session
            turns = await self.database.get_conversation_turns(session_id)
            
            # Reconstruct each turn with full content
            reconstructed_messages = []
            total_multimedia = 0
            mcp_tools_used = set()
            
            for turn in turns:
                turn_messages = await self.reconstruct_conversation_turn(turn["turn_id"])
                reconstructed_messages.extend(turn_messages)
                
                # Count multimedia and track tools
                for msg in turn_messages:
                    if msg.multimedia_content:
                        total_multimedia += len(msg.multimedia_content)
                    if msg.mcp_tool_used:
                        mcp_tools_used.add(msg.mcp_tool_used)
            
            # Create reconstructed conversation
            conversation = ReconstructedConversation(
                session_id=session_id,
                user_id=session_info["user_id"],
                started_at=session_info["started_at"],
                ended_at=session_info.get("ended_at"),
                messages=reconstructed_messages,
                total_messages=len(reconstructed_messages),
                total_multimedia=total_multimedia,
                mcp_tools_used=list(mcp_tools_used),
                metadata=session_info.get("metadata", {})
            )
            
            return conversation
            
        except Exception as e:
            logger.error(f"Error reconstructing conversation {session_id}: {e}")
            return None
    
    async def reconstruct_conversation_turn(self, turn_id: str) -> List[ReconstructedMessage]:
        """
        Reconstruct a conversation turn with all its content
        
        Args:
            turn_id: Turn identifier
            
        Returns:
            List of reconstructed messages for this turn
        """
        try:
            # Get turn information
            turn_info = await self.database.get_conversation_turn(turn_id)
            if not turn_info:
                return []
            
            # Get all content for this turn
            content_items = await self.database.get_conversation_content(turn_id)
            
            # Group content by order and type
            messages = []
            
            # Process text content first
            text_content = [item for item in content_items if item["content_type"] == "text"]
            for text_item in text_content:
                message = ReconstructedMessage(
                    message_id=text_item["content_id"],
                    user_id=text_item["user_id"],
                    timestamp=text_item["created_at"],
                    is_user_message=text_item["is_user_content"],
                    text_content=text_item["content_data"],
                    multimedia_content=[],
                    mcp_tool_used=text_item.get("mcp_tool_used"),
                    tool_parameters=text_item.get("tool_parameters"),
                    tool_result=None
                )
                messages.append(message)
            
            # Process multimedia content
            multimedia_content = [item for item in content_items if item["content_type"] != "text"]
            for media_item in multimedia_content:
                # Find the corresponding text message or create a new one
                target_message = None
                for msg in messages:
                    if msg.mcp_tool_used == media_item.get("mcp_tool_used"):
                        target_message = msg
                        break
                
                if not target_message:
                    # Create a new message for standalone multimedia
                    target_message = ReconstructedMessage(
                        message_id=media_item["content_id"],
                        user_id=media_item["user_id"],
                        timestamp=media_item["created_at"],
                        is_user_message=media_item["is_user_content"],
                        text_content=None,
                        multimedia_content=[],
                        mcp_tool_used=media_item.get("mcp_tool_used"),
                        tool_parameters=media_item.get("tool_parameters"),
                        tool_result=None
                    )
                    messages.append(target_message)
                
                # Get multimedia file details
                multimedia_info = await self.get_multimedia_details(media_item["multimedia_id"])
                if multimedia_info:
                    target_message.multimedia_content.append(multimedia_info)
            
            # Sort messages by timestamp
            messages.sort(key=lambda x: x.timestamp)
            
            return messages
            
        except Exception as e:
            logger.error(f"Error reconstructing turn {turn_id}: {e}")
            return []
    
    async def get_multimedia_details(self, multimedia_id: str) -> Optional[Dict[str, Any]]:
        """
        Get complete details for multimedia content
        
        Args:
            multimedia_id: Multimedia content identifier
            
        Returns:
            Multimedia details including file path, metadata, and generation info
        """
        try:
            # Get multimedia content from storage
            content = self.multimedia_storage.get_content(multimedia_id)
            if not content:
                return None
            
            # Get database metadata
            db_content = await self.database.get_multimedia_content(multimedia_id)
            
            # Combine storage and database information
            multimedia_details = {
                "content_id": content.content_id,
                "content_type": content.content_type,
                "file_path": content.file_path,
                "file_name": content.file_name,
                "file_size": content.file_size,
                "mime_type": content.mime_type,
                "is_generated": content.is_generated,
                "generation_tool": content.generation_tool,
                "generation_prompt": content.generation_prompt,
                "tags": content.tags,
                "created_at": content.created_at,
                "metadata": content.metadata
            }
            
            # Add database-specific metadata
            if db_content:
                multimedia_details.update({
                    "session_id": db_content.get("session_id"),
                    "turn_id": db_content.get("turn_id"),
                    "content_hash": db_content.get("content_hash")
                })
            
            return multimedia_details
            
        except Exception as e:
            logger.error(f"Error getting multimedia details for {multimedia_id}: {e}")
            return None
    
    async def reconstruct_conversation_with_media_files(self, session_id: str, 
                                                      output_dir: str = None) -> Dict[str, Any]:
        """
        Reconstruct conversation and prepare media files for access
        
        Args:
            session_id: Session identifier
            output_dir: Directory to copy media files (optional)
            
        Returns:
            Conversation data with accessible media files
        """
        try:
            # Reconstruct the conversation
            conversation = await self.reconstruct_conversation(session_id)
            if not conversation:
                return {}
            
            # Prepare media files for access
            accessible_media = []
            
            for message in conversation.messages:
                if message.multimedia_content:
                    for media in message.multimedia_content:
                        media_info = {
                            "content_id": media["content_id"],
                            "content_type": media["content_type"],
                            "original_path": media["file_path"],
                            "accessible_path": media["file_path"],  # Default to original path
                            "file_name": media["file_name"],
                            "mime_type": media["mime_type"],
                            "is_generated": media["is_generated"],
                            "generation_tool": media["generation_tool"],
                            "generation_prompt": media["generation_prompt"]
                        }
                        
                        # Copy to output directory if specified
                        if output_dir:
                            accessible_path = await self.copy_media_to_directory(
                                media["file_path"], 
                                output_dir, 
                                media["content_id"]
                            )
                            if accessible_path:
                                media_info["accessible_path"] = accessible_path
                        
                        accessible_media.append(media_info)
            
            # Return reconstructed conversation with accessible media
            return {
                "conversation": conversation,
                "accessible_media": accessible_media,
                "media_count": len(accessible_media),
                "reconstruction_timestamp": datetime.now().isoformat()
            }
            
        except Exception as e:
            logger.error(f"Error reconstructing conversation with media files: {e}")
            return {}
    
    async def copy_media_to_directory(self, source_path: str, output_dir: str, 
                                    content_id: str) -> Optional[str]:
        """
        Copy media file to accessible directory
        
        Args:
            source_path: Source file path
            output_dir: Output directory
            content_id: Content identifier
            
        Returns:
            Path to copied file or None if failed
        """
        try:
            source_path = Path(source_path)
            if not source_path.exists():
                return None
            
            # Create output directory
            output_path = Path(output_dir)
            output_path.mkdir(parents=True, exist_ok=True)
            
            # Copy file with content ID prefix
            file_extension = source_path.suffix
            target_filename = f"{content_id}{file_extension}"
            target_path = output_path / target_filename
            
            import shutil
            shutil.copy2(source_path, target_path)
            
            return str(target_path)
            
        except Exception as e:
            logger.error(f"Error copying media file: {e}")
            return None
    
    async def export_conversation(self, session_id: str, export_format: str = "json",
                                include_media: bool = True) -> str:
        """
        Export conversation to various formats
        
        Args:
            session_id: Session identifier
            export_format: Export format (json, markdown, html)
            include_media: Whether to include media file information
            
        Returns:
            Exported conversation content
        """
        try:
            # Reconstruct conversation
            conversation = await self.reconstruct_conversation(session_id)
            if not conversation:
                return ""
            
            if export_format == "json":
                return self.export_to_json(conversation, include_media)
            elif export_format == "markdown":
                return self.export_to_markdown(conversation, include_media)
            elif export_format == "html":
                return self.export_to_html(conversation, include_media)
            else:
                return self.export_to_json(conversation, include_media)
                
        except Exception as e:
            logger.error(f"Error exporting conversation: {e}")
            return ""
    
    def export_to_json(self, conversation: ReconstructedConversation, 
                      include_media: bool = True) -> str:
        """Export conversation to JSON format"""
        export_data = {
            "session_id": conversation.session_id,
            "user_id": conversation.user_id,
            "started_at": conversation.started_at.isoformat(),
            "ended_at": conversation.ended_at.isoformat() if conversation.ended_at else None,
            "total_messages": conversation.total_messages,
            "total_multimedia": conversation.total_multimedia,
            "mcp_tools_used": conversation.mcp_tools_used,
            "metadata": conversation.metadata,
            "messages": []
        }
        
        for message in conversation.messages:
            msg_data = {
                "message_id": message.message_id,
                "timestamp": message.timestamp.isoformat(),
                "is_user_message": message.is_user_message,
                "text_content": message.text_content,
                "mcp_tool_used": message.mcp_tool_used,
                "tool_parameters": message.tool_parameters
            }
            
            if include_media and message.multimedia_content:
                msg_data["multimedia_content"] = message.multimedia_content
            
            export_data["messages"].append(msg_data)
        
        return json.dumps(export_data, indent=2, default=str)
    
    def export_to_markdown(self, conversation: ReconstructedConversation, 
                          include_media: bool = True) -> str:
        """Export conversation to Markdown format"""
        md_lines = []
        
        # Header
        md_lines.append(f"# Conversation Session: {conversation.session_id}")
        md_lines.append(f"**User:** {conversation.user_id}")
        md_lines.append(f"**Started:** {conversation.started_at.strftime('%Y-%m-%d %H:%M:%S')}")
        if conversation.ended_at:
            md_lines.append(f"**Ended:** {conversation.ended_at.strftime('%Y-%m-%d %H:%M:%S')}")
        md_lines.append(f"**Total Messages:** {conversation.total_messages}")
        md_lines.append(f"**Total Media:** {conversation.total_multimedia}")
        md_lines.append("")
        
        # Messages
        for i, message in enumerate(conversation.messages, 1):
            timestamp = message.timestamp.strftime('%H:%M:%S')
            sender = "👤 User" if message.is_user_message else "🤖 Assistant"
            
            md_lines.append(f"## Message {i} - {timestamp}")
            md_lines.append(f"**{sender}**")
            md_lines.append("")
            
            if message.text_content:
                md_lines.append(message.text_content)
                md_lines.append("")
            
            if message.mcp_tool_used:
                md_lines.append(f"*Generated using: {message.mcp_tool_used}*")
                if message.tool_parameters:
                    md_lines.append(f"*Parameters: {json.dumps(message.tool_parameters, indent=2)}*")
                md_lines.append("")
            
            if include_media and message.multimedia_content:
                for media in message.multimedia_content:
                    md_lines.append(f"**Media:** {media['content_type']} - {media['file_name']}")
                    if media['generation_prompt']:
                        md_lines.append(f"*Generated from: {media['generation_prompt']}*")
                    md_lines.append("")
        
        return "\n".join(md_lines)
    
    def export_to_html(self, conversation: ReconstructedConversation, 
                      include_media: bool = True) -> str:
        """Export conversation to HTML format"""
        html_lines = []
        
        # HTML header
        html_lines.append("""
        <!DOCTYPE html>
        <html>
        <head>
            <title>Conversation Export</title>
            <style>
                body { font-family: Arial, sans-serif; margin: 20px; }
                .message { margin: 20px 0; padding: 15px; border-left: 4px solid #007bff; }
                .user-message { border-left-color: #28a745; }
                .assistant-message { border-left-color: #007bff; }
                .timestamp { color: #666; font-size: 0.9em; }
                .media-item { background: #f8f9fa; padding: 10px; margin: 10px 0; border-radius: 5px; }
                .tool-info { background: #e9ecef; padding: 8px; margin: 8px 0; border-radius: 3px; }
            </style>
        </head>
        <body>
        """)
        
        # Header
        html_lines.append(f"<h1>Conversation Session: {conversation.session_id}</h1>")
        html_lines.append(f"<p><strong>User:</strong> {conversation.user_id}</p>")
        html_lines.append(f"<p><strong>Started:</strong> {conversation.started_at.strftime('%Y-%m-%d %H:%M:%S')}</p>")
        if conversation.ended_at:
            html_lines.append(f"<p><strong>Ended:</strong> {conversation.ended_at.strftime('%Y-%m-%d %H:%M:%S')}</p>")
        html_lines.append(f"<p><strong>Total Messages:</strong> {conversation.total_messages}</p>")
        html_lines.append(f"<p><strong>Total Media:</strong> {conversation.total_multimedia}</p>")
        
        # Messages
        for i, message in enumerate(conversation.messages, 1):
            timestamp = message.timestamp.strftime('%H:%M:%S')
            sender = "👤 User" if message.is_user_message else "🤖 Assistant"
            css_class = "user-message" if message.is_user_message else "assistant-message"
            
            html_lines.append(f'<div class="message {css_class}">')
            html_lines.append(f'<h3>Message {i} - {timestamp}</h3>')
            html_lines.append(f'<p><strong>{sender}</strong></p>')
            
            if message.text_content:
                html_lines.append(f'<p>{message.text_content}</p>')
            
            if message.mcp_tool_used:
                html_lines.append(f'<div class="tool-info">')
                html_lines.append(f'<strong>Generated using:</strong> {message.mcp_tool_used}')
                if message.tool_parameters:
                    html_lines.append(f'<br><strong>Parameters:</strong> <pre>{json.dumps(message.tool_parameters, indent=2)}</pre>')
                html_lines.append('</div>')
            
            if include_media and message.multimedia_content:
                for media in message.multimedia_content:
                    html_lines.append(f'<div class="media-item">')
                    html_lines.append(f'<strong>Media:</strong> {media["content_type"]} - {media["file_name"]}')
                    if media['generation_prompt']:
                        html_lines.append(f'<br><em>Generated from: {media["generation_prompt"]}</em>')
                    html_lines.append('</div>')
            
            html_lines.append('</div>')
        
        # HTML footer
        html_lines.append("</body></html>")
        
        return "\n".join(html_lines)

