"""
Audio handling module for RAG Companion System
Handles speech-to-text, text-to-speech, and real-time audio streaming
"""

import os
import json
import base64
import asyncio
import websockets
import logging
from typing import Dict, Any, Optional, AsyncGenerator
from openai import OpenAI
from dotenv import load_dotenv

# Optional PyAudio import (for local audio capture)
try:
    import pyaudio
    PYAUDIO_AVAILABLE = True
except ImportError:
    PYAUDIO_AVAILABLE = False
    logging.warning("PyAudio not available - local audio capture disabled")

# Load environment variables
load_dotenv()

# Initialize OpenAI client
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

class AudioHandler:
    """Handles audio processing and real-time communication with OpenAI Realtime API"""
    
    def __init__(self):
        self.openai_api_key = os.getenv("OPENAI_API_KEY")
        self.realtime_url = "wss://api.openai.com/v1/realtime?model=gpt-4o-mini-realtime-preview-2024-12-17"
        
    async def speech_to_text(self, audio_data: bytes, audio_format: str = "webm") -> str:
        """Convert speech to text using OpenAI Whisper API"""
        try:
            # Create a temporary file-like object for the audio data
            import io
            audio_file = io.BytesIO(audio_data)
            audio_file.name = f"audio.{audio_format}"
            
            # Use Whisper API for transcription
            transcript = client.audio.transcriptions.create(
                model="whisper-1",
                file=audio_file,
                response_format="text"
            )
            
            return transcript
            
        except Exception as e:
            logger.error(f"Speech-to-text error: {e}")
            raise
    
    async def text_to_speech(self, text: str, voice: str = "alloy") -> bytes:
        """Convert text to speech using OpenAI TTS API"""
        try:
            response = client.audio.speech.create(
                model="tts-1",
                voice=voice,
                input=text,
                response_format="mp3"
            )
            
            return response.content
            
        except Exception as e:
            logger.error(f"Text-to-speech error: {e}")
            raise
    
    async def create_realtime_session(self, session_config: Dict[str, Any]) -> Dict[str, Any]:
        """Create a real-time audio session configuration"""
        default_config = {
            "type": "session.update",
            "session": {
                "modalities": ["text", "audio"],
                "instructions": """You are a conversational AI companion that remembers personal details and maintains fluid conversation. 
                
Key behaviors:
- Naturally incorporate what you know about the user into responses
- Reference previous conversation topics when relevant  
- Ask follow-up questions to learn more about the user
- Be genuinely interested in their life, work, and interests
- Make connections between different pieces of information they've shared
- Respond in a warm, engaging, and personal way
- When you have tool results, incorporate them naturally into the conversation""",
                "voice": session_config.get("voice", "alloy"),
                "input_audio_format": "pcm16",
                "output_audio_format": "pcm16",
                "input_audio_transcription": {
                    "model": "whisper-1"
                },
                "turn_detection": {
                    "type": "server_vad",
                    "threshold": 0.5,
                    "prefix_padding_ms": 300,
                    "silence_duration_ms": 200
                },
                "tools": [],  # Will be populated with available tools
                "tool_choice": "auto",
                "temperature": 0.8,
                "max_response_output_tokens": "inf"
            }
        }
        
        # Merge with provided config
        if session_config:
            default_config["session"].update(session_config)
            
        return default_config
    
    async def handle_realtime_websocket(self, websocket_connection, user_id: str, user_profile: Dict[str, Any] = None):
        """Handle real-time WebSocket communication with OpenAI Realtime API"""
        try:
            # Connect to OpenAI Realtime API
            headers = {
                "Authorization": f"Bearer {self.openai_api_key}",
                "OpenAI-Beta": "realtime=v1"
            }
            
            async with websockets.connect(self.realtime_url, extra_headers=headers) as openai_ws:
                logger.info("Connected to OpenAI Realtime API")
                
                # Create session configuration
                session_config = await self.create_realtime_session({
                    "voice": "alloy",
                    "instructions": f"""You are a conversational AI companion for user {user_id}. 
                    What you know about this user: {json.dumps(user_profile or {})}
                    
                    Be warm, personal, and remember details they share with you."""
                })
                
                # Send session configuration
                await openai_ws.send(json.dumps(session_config))
                
                # Create bidirectional message routing
                async def forward_to_openai():
                    """Forward messages from client to OpenAI"""
                    try:
                        async for message in websocket_connection:
                            if isinstance(message, str):
                                data = json.loads(message)
                                logger.info(f"Client -> OpenAI: {data.get('type', 'unknown')}")
                                await openai_ws.send(message)
                            elif isinstance(message, bytes):
                                # Handle binary audio data
                                audio_event = {
                                    "type": "input_audio_buffer.append",
                                    "audio": base64.b64encode(message).decode()
                                }
                                await openai_ws.send(json.dumps(audio_event))
                    except websockets.exceptions.ConnectionClosed:
                        logger.info("Client connection closed")
                    except Exception as e:
                        logger.error(f"Error forwarding to OpenAI: {e}")
                
                async def forward_to_client():
                    """Forward messages from OpenAI to client"""
                    try:
                        async for message in openai_ws:
                            data = json.loads(message)
                            event_type = data.get("type", "unknown")
                            logger.info(f"OpenAI -> Client: {event_type}")
                            
                            # Handle different event types
                            if event_type == "response.audio.delta":
                                # Send audio data to client
                                audio_data = base64.b64decode(data.get("delta", ""))
                                await websocket_connection.send(audio_data)
                            else:
                                # Send JSON events to client
                                await websocket_connection.send(message)
                    except websockets.exceptions.ConnectionClosed:
                        logger.info("OpenAI connection closed")
                    except Exception as e:
                        logger.error(f"Error forwarding to client: {e}")
                
                # Run both forwarding tasks concurrently
                await asyncio.gather(
                    forward_to_openai(),
                    forward_to_client()
                )
                
        except Exception as e:
            logger.error(f"Realtime WebSocket error: {e}")
            error_msg = {
                "type": "error",
                "error": {
                    "message": str(e),
                    "type": "connection_error"
                }
            }
            await websocket_connection.send(json.dumps(error_msg))

    async def process_audio_with_tools(self, audio_data: bytes, user_id: str, user_profile: Dict[str, Any] = None) -> Dict[str, Any]:
        """Process audio input with tool integration"""
        try:
            # Convert speech to text
            text_query = await self.speech_to_text(audio_data)
            logger.info(f"Transcribed: {text_query}")
            
            # Import RAG functionality for tool integration
            from rag_api import check_and_use_tools, generate_conversational_response, extract_personal_info, store_personal_info_simple
            
            # Extract and store personal information
            personal_info = extract_personal_info(text_query)
            if personal_info:
                store_personal_info_simple(user_id, personal_info)
            
            # Check for tool usage
            tool_result = await check_and_use_tools(text_query, user_profile or {})
            
            # Generate conversational response
            text_response = await generate_conversational_response(user_id, text_query, personal_info)
            
            # Convert response to speech
            audio_response = await self.text_to_speech(text_response)
            
            return {
                "transcript": text_query,
                "response_text": text_response, 
                "audio_response": audio_response,
                "tool_result": tool_result,
                "personal_info": personal_info
            }
            
        except Exception as e:
            logger.error(f"Audio processing error: {e}")
            raise

# Global audio handler instance
audio_handler = AudioHandler()