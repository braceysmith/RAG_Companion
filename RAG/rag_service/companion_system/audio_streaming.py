"""
Enhanced Audio Streaming for RAG Companions

This module handles audio streaming with proper chunking, flow control,
and buffer management to prevent cutoffs during long conversations.
"""

import asyncio
import json
import base64
from typing import Dict, List, Optional, AsyncGenerator, Any
from dataclasses import dataclass
import logging

logger = logging.getLogger(__name__)

@dataclass
class AudioChunk:
    """Represents a chunk of audio data"""
    chunk_id: str
    sequence_number: int
    total_chunks: int
    audio_data: bytes
    is_final: bool
    timestamp: float

@dataclass
class MessageChunk:
    """Represents a chunk of a large message"""
    chunk_id: str
    sequence_number: int
    total_chunks: int
    data: str
    is_final: bool
    message_type: str

class AudioStreamManager:
    """Manages audio streaming with chunking and flow control"""
    
    def __init__(self, max_chunk_size: int = 16384, max_message_size: int = 50000):
        self.max_chunk_size = max_chunk_size  # 16KB chunks for audio
        self.max_message_size = max_message_size  # 50KB for text messages
        self.active_streams: Dict[str, Dict] = {}
        self.chunk_counters: Dict[str, int] = {}
        
    def chunk_audio_data(self, audio_data: bytes, stream_id: str) -> List[AudioChunk]:
        """Split audio data into manageable chunks"""
        chunks = []
        total_chunks = (len(audio_data) + self.max_chunk_size - 1) // self.max_chunk_size
        
        for i in range(total_chunks):
            start_idx = i * self.max_chunk_size
            end_idx = min(start_idx + self.max_chunk_size, len(audio_data))
            chunk_data = audio_data[start_idx:end_idx]
            
            chunk = AudioChunk(
                chunk_id=stream_id,
                sequence_number=i,
                total_chunks=total_chunks,
                audio_data=chunk_data,
                is_final=(i == total_chunks - 1),
                timestamp=asyncio.get_event_loop().time()
            )
            chunks.append(chunk)
        
        return chunks
    
    def chunk_text_message(self, message: str, message_type: str, stream_id: str) -> List[MessageChunk]:
        """Split large text messages into chunks"""
        if len(message) <= self.max_message_size:
            # Single chunk for small messages
            return [MessageChunk(
                chunk_id=stream_id,
                sequence_number=0,
                total_chunks=1,
                data=message,
                is_final=True,
                message_type=message_type
            )]
        
        chunks = []
        total_chunks = (len(message) + self.max_message_size - 1) // self.max_message_size
        
        for i in range(total_chunks):
            start_idx = i * self.max_message_size
            end_idx = min(start_idx + self.max_message_size, len(message))
            chunk_data = message[start_idx:end_idx]
            
            chunk = MessageChunk(
                chunk_id=stream_id,
                sequence_number=i,
                total_chunks=total_chunks,
                data=chunk_data,
                is_final=(i == total_chunks - 1),
                message_type=message_type
            )
            chunks.append(chunk)
        
        return chunks
    
    def create_chunked_audio_event(self, audio_data: bytes, stream_id: str) -> List[Dict]:
        """Create chunked audio events for WebRTC transmission"""
        chunks = self.chunk_audio_data(audio_data, stream_id)
        events = []
        
        for chunk in chunks:
            event = {
                "type": "input_audio_buffer.append",
                "audio": base64.b64encode(chunk.audio_data).decode(),
                "chunk_info": {
                    "chunk_id": chunk.chunk_id,
                    "sequence": chunk.sequence_number,
                    "total": chunk.total_chunks,
                    "is_final": chunk.is_final,
                    "timestamp": chunk.timestamp
                }
            }
            events.append(event)
        
        return events
    
    def create_chunked_message_event(self, message: str, message_type: str, stream_id: str) -> List[Dict]:
        """Create chunked message events for WebRTC transmission"""
        chunks = self.chunk_text_message(message, message_type, stream_id)
        events = []
        
        for chunk in chunks:
            event = {
                "type": "conversation.item.create",
                "item": {
                    "type": "message",
                    "role": "user",
                    "content": [
                        {
                            "type": "input_text",
                            "text": chunk.data
                        }
                    ]
                },
                "chunk_info": {
                    "chunk_id": chunk.chunk_id,
                    "sequence": chunk.sequence_number,
                    "total": chunk.total_chunks,
                    "is_final": chunk.is_final,
                    "message_type": chunk.message_type
                }
            }
            events.append(event)
        
        return events

class FlowController:
    """Manages flow control to prevent buffer overflow"""
    
    def __init__(self, max_buffer_size: int = 1024 * 1024):  # 1MB buffer
        self.max_buffer_size = max_buffer_size
        self.current_buffer_size = 0
        self.pending_messages: List[Dict] = []
        self.is_flow_controlled = False
        
    async def can_send_message(self, message_size: int) -> bool:
        """Check if we can send a message without overflowing the buffer"""
        if self.current_buffer_size + message_size > self.max_buffer_size:
            self.is_flow_controlled = True
            return False
        return True
    
    async def wait_for_buffer_space(self, required_size: int):
        """Wait until there's enough buffer space"""
        while not await self.can_send_message(required_size):
            await asyncio.sleep(0.1)  # Wait 100ms before checking again
    
    def message_sent(self, message_size: int):
        """Update buffer state after message is sent"""
        self.current_buffer_size += message_size
        
    def message_acknowledged(self, message_size: int):
        """Update buffer state after message is acknowledged"""
        self.current_buffer_size = max(0, self.current_buffer_size - message_size)
        if self.current_buffer_size < self.max_buffer_size * 0.8:  # 80% threshold
            self.is_flow_controlled = False

class AudioBufferManager:
    """Manages audio buffers with proper sizing and flow control"""
    
    def __init__(self, buffer_size_ms: int = 1000, sample_rate: int = 24000):
        self.buffer_size_ms = buffer_size_ms
        self.sample_rate = sample_rate
        self.buffer_size_samples = (sample_rate * buffer_size_ms) // 1000
        self.audio_buffer: List[bytes] = []
        self.is_processing = False
        
    def add_audio_chunk(self, audio_data: bytes) -> bool:
        """Add audio chunk to buffer, return True if buffer is ready to process"""
        self.audio_buffer.append(audio_data)
        
        # Check if buffer is ready to process
        total_samples = sum(len(chunk) for chunk in self.audio_buffer)
        return total_samples >= self.buffer_size_samples
    
    def get_and_clear_buffer(self) -> bytes:
        """Get all audio data and clear the buffer"""
        if not self.audio_buffer:
            return b""
        
        # Combine all chunks
        combined_audio = b"".join(self.audio_buffer)
        self.audio_buffer.clear()
        return combined_audio
    
    def get_buffer_status(self) -> Dict[str, Any]:
        """Get current buffer status"""
        total_samples = sum(len(chunk) for chunk in self.audio_buffer)
        buffer_percentage = (total_samples / self.buffer_size_samples) * 100
        
        return {
            "total_samples": total_samples,
            "buffer_size_samples": self.buffer_size_samples,
            "buffer_percentage": buffer_percentage,
            "chunks_in_buffer": len(self.audio_buffer),
            "is_ready": total_samples >= self.buffer_size_samples
        }

class EnhancedAudioHandler:
    """Enhanced audio handler with streaming and flow control"""
    
    def __init__(self):
        self.stream_manager = AudioStreamManager()
        self.flow_controller = FlowController()
        self.audio_buffer_manager = AudioBufferManager()
        
    async def send_audio_with_flow_control(self, audio_data: bytes, websocket_connection) -> bool:
        """Send audio data with proper flow control and chunking"""
        try:
            # Check if we can send
            if not await self.flow_controller.can_send_message(len(audio_data)):
                await self.flow_controller.wait_for_buffer_space(len(audio_data))
            
            # Create chunked audio events
            stream_id = f"audio_{asyncio.get_event_loop().time()}"
            audio_events = self.stream_manager.create_chunked_audio_event(audio_data, stream_id)
            
            # Send chunks with flow control
            for event in audio_events:
                event_json = json.dumps(event)
                await websocket_connection.send(event_json)
                self.flow_controller.message_sent(len(event_json))
                
                # Small delay between chunks to prevent overwhelming
                await asyncio.sleep(0.01)
            
            return True
            
        except Exception as e:
            logger.error(f"Error sending audio with flow control: {e}")
            return False
    
    async def send_message_with_flow_control(self, message: str, message_type: str, websocket_connection) -> bool:
        """Send text message with proper flow control and chunking"""
        try:
            # Check if we can send
            if not await self.flow_controller.can_send_message(len(message)):
                await self.flow_controller.wait_for_buffer_space(len(message))
            
            # Create chunked message events
            stream_id = f"message_{asyncio.get_event_loop().time()}"
            message_events = self.stream_manager.create_chunked_message_event(message, message_type, stream_id)
            
            # Send chunks with flow control
            for event in message_events:
                event_json = json.dumps(event)
                await websocket_connection.send(event_json)
                self.flow_controller.message_sent(len(event_json))
                
                # Small delay between chunks
                await asyncio.sleep(0.01)
            
            return True
            
        except Exception as e:
            logger.error(f"Error sending message with flow control: {e}")
            return False
    
    def process_audio_buffer(self, audio_data: bytes) -> Optional[bytes]:
        """Process audio buffer and return combined audio when ready"""
        if self.audio_buffer_manager.add_audio_chunk(audio_data):
            return self.audio_buffer_manager.get_and_clear_buffer()
        return None
    
    def get_system_status(self) -> Dict[str, Any]:
        """Get current system status for monitoring"""
        return {
            "flow_control": {
                "is_flow_controlled": self.flow_controller.is_flow_controlled,
                "current_buffer_size": self.flow_controller.current_buffer_size,
                "max_buffer_size": self.flow_controller.max_buffer_size,
                "pending_messages": len(self.flow_controller.pending_messages)
            },
            "audio_buffer": self.audio_buffer_manager.get_buffer_status(),
            "stream_manager": {
                "active_streams": len(self.stream_manager.active_streams),
                "chunk_counters": self.stream_manager.chunk_counters
            }
        }

# Global enhanced audio handler
enhanced_audio_handler = EnhancedAudioHandler()
