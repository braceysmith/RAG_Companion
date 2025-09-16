"""
Communication System for RAG Companions

This module handles real-time communication including TTS/STT, text chat,
image processing, and companion action communication.
"""

from typing import Dict, List, Optional, Any, Union, Callable
from dataclasses import dataclass, field
from datetime import datetime, timedelta
import json
import uuid
import asyncio
import logging
from enum import Enum
import base64
import io
from PIL import Image
import requests
import websockets
from websockets.server import WebSocketServerProtocol
import threading
import time

logger = logging.getLogger(__name__)

class MessageType(Enum):
    """Types of messages in the communication system"""
    TEXT = "text"
    VOICE = "voice"
    IMAGE = "image"
    AUDIO = "audio"
    SYSTEM = "system"
    ACTION = "action"
    TYPING = "typing"
    READ_RECEIPT = "read_receipt"

class MessageStatus(Enum):
    """Message processing status"""
    PENDING = "pending"
    PROCESSING = "processing"
    COMPLETED = "completed"
    FAILED = "failed"
    DELIVERED = "delivered"
    READ = "read"

@dataclass
class ChatMessage:
    """A message in the chat system"""
    message_id: str
    user_id: str
    companion_id: str
    message_type: MessageType
    content: str
    timestamp: datetime
    status: MessageStatus = MessageStatus.PENDING
    metadata: Dict[str, Any] = field(default_factory=dict)
    response: Optional[str] = None
    processing_time: Optional[float] = None
    read_at: Optional[datetime] = None
    delivered_at: Optional[datetime] = None

@dataclass
class CompanionAction:
    """Represents an action the companion is performing"""
    action_id: str
    action_type: str
    description: str
    estimated_duration: float  # seconds
    progress: float = 0.0  # 0.0 to 1.0
    status: str = "starting"  # starting, in_progress, completed, failed
    start_time: datetime = field(default_factory=datetime.now)
    metadata: Dict[str, Any] = field(default_factory=dict)

@dataclass
class WebSocketSession:
    """WebSocket session for real-time communication"""
    session_id: str
    websocket: WebSocketServerProtocol
    user_id: str
    companion_id: str
    connected_at: datetime
    last_activity: datetime
    is_active: bool = True

class CommunicationSystem:
    """
    Handles real-time communication between users and companions
    """
    
    def __init__(self, openai_api_key: Optional[str] = None, websocket_port: int = 8765):
        self.openai_api_key = openai_api_key
        self.websocket_port = websocket_port
        self.active_sessions: Dict[str, Dict[str, Any]] = {}
        self.websocket_sessions: Dict[str, WebSocketSession] = {}
        self.action_callbacks: Dict[str, Callable] = {}
        self.tts_engine = None
        self.stt_engine = None
        self.websocket_server = None
        self.websocket_thread = None
        
        # Initialize communication engines
        self._init_tts_engine()
        self._init_stt_engine()
        
        # Start WebSocket server
        self._start_websocket_server()
        
        logger.info("Communication system initialized")
    
    def _init_tts_engine(self):
        """Initialize text-to-speech engine"""
        try:
            if self.openai_api_key:
                # Initialize OpenAI TTS
                self.tts_engine = "openai"
                logger.info("OpenAI TTS engine initialized")
            else:
                # Fallback to system TTS
                self.tts_engine = "system"
                logger.info("System TTS engine initialized")
        except Exception as e:
            logger.warning(f"TTS engine initialization failed: {e}")
            self.tts_engine = "system"
    
    def _init_stt_engine(self):
        """Initialize speech-to-text engine"""
        try:
            if self.openai_api_key:
                # Initialize OpenAI Whisper
                self.stt_engine = "openai"
                logger.info("OpenAI STT engine initialized")
            else:
                # Fallback to system STT
                self.stt_engine = "system"
                logger.info("System STT engine initialized")
        except Exception as e:
            logger.warning(f"STT engine initialization failed: {e}")
            self.stt_engine = "system"
    
    def _start_websocket_server(self):
        """Start WebSocket server for real-time communication"""
        try:
            async def websocket_handler(websocket):
                await self._handle_websocket_connection(websocket, "")
            
            async def start_server():
                self.websocket_server = await websockets.serve(
                    websocket_handler, "localhost", self.websocket_port
                )
                logger.info(f"WebSocket server started on port {self.websocket_port}")
                await self.websocket_server.wait_closed()
            
            # Start WebSocket server in a separate thread
            self.websocket_thread = threading.Thread(
                target=lambda: asyncio.run(start_server()),
                daemon=True
            )
            self.websocket_thread.start()
            
        except Exception as e:
            logger.error(f"Failed to start WebSocket server: {e}")
    
    async def _handle_websocket_connection(self, websocket: WebSocketServerProtocol, path: str):
        """Handle WebSocket connection"""
        session_id = None
        try:
            # Wait for authentication message
            auth_message = await websocket.recv()
            auth_data = json.loads(auth_message)
            
            user_id = auth_data.get("user_id")
            companion_id = auth_data.get("companion_id")
            session_id = auth_data.get("session_id")
            
            if not all([user_id, companion_id, session_id]):
                await websocket.close(1008, "Missing authentication data")
                return
            
            # Create WebSocket session
            ws_session = WebSocketSession(
                session_id=session_id,
                websocket=websocket,
                user_id=user_id,
                companion_id=companion_id,
                connected_at=datetime.now(),
                last_activity=datetime.now()
            )
            
            self.websocket_sessions[session_id] = ws_session
            logger.info(f"WebSocket session established: {session_id}")
            
            # Send connection confirmation
            await websocket.send(json.dumps({
                "type": "connection_confirmed",
                "session_id": session_id,
                "timestamp": datetime.now().isoformat()
            }))
            
            # Handle incoming messages
            async for message in websocket:
                await self._handle_websocket_message(ws_session, message)
                
        except websockets.exceptions.ConnectionClosed:
            logger.info(f"WebSocket connection closed: {path}")
        except Exception as e:
            logger.error(f"WebSocket error: {e}")
        finally:
            # Clean up session
            if session_id and session_id in self.websocket_sessions:
                del self.websocket_sessions[session_id]
    
    async def _handle_websocket_message(self, ws_session: WebSocketSession, message: str):
        """Handle incoming WebSocket message"""
        try:
            data = json.loads(message)
            message_type = data.get("type")
            
            # Update last activity
            ws_session.last_activity = datetime.now()
            
            if message_type == "chat_message":
                await self._handle_chat_message(ws_session, data)
            elif message_type == "typing_indicator":
                await self._handle_typing_indicator(ws_session, data)
            elif message_type == "read_receipt":
                await self._handle_read_receipt(ws_session, data)
            elif message_type == "ping":
                await ws_session.websocket.send(json.dumps({"type": "pong"}))
            else:
                logger.warning(f"Unknown message type: {message_type}")
                
        except json.JSONDecodeError:
            logger.error("Invalid JSON message received")
        except Exception as e:
            logger.error(f"Error handling WebSocket message: {e}")
    
    async def _handle_chat_message(self, ws_session: WebSocketSession, data: Dict[str, Any]):
        """Handle chat message from WebSocket"""
        try:
            # Create message directly for WebSocket (no need for regular session)
            message = ChatMessage(
                message_id=str(uuid.uuid4()),
                user_id=ws_session.user_id,
                companion_id=ws_session.companion_id,
                message_type=MessageType(data.get("message_type", "text")),
                content=data.get("content", ""),
                timestamp=datetime.now()
            )
            
            # Send acknowledgment
            await ws_session.websocket.send(json.dumps({
                "type": "message_received",
                "message_id": message.message_id,
                "timestamp": datetime.now().isoformat()
            }))
            
            # Process message directly (no need for regular session)
            response = self._process_text_message(message)
            if response:
                await self._send_chat_response(ws_session, response, message.message_id)
                
        except Exception as e:
            logger.error(f"Error handling chat message: {e}")
            await ws_session.websocket.send(json.dumps({
                "type": "error",
                "message": "Failed to process message",
                "timestamp": datetime.now().isoformat()
            }))
    
    async def _handle_typing_indicator(self, ws_session: WebSocketSession, data: Dict[str, Any]):
        """Handle typing indicator"""
        try:
            # Broadcast typing indicator to other sessions with same companion
            for session_id, other_session in self.websocket_sessions.items():
                if (session_id != ws_session.session_id and 
                    other_session.companion_id == ws_session.companion_id):
                    await other_session.websocket.send(json.dumps({
                        "type": "typing_indicator",
                        "user_id": ws_session.user_id,
                        "is_typing": data.get("is_typing", False),
                        "timestamp": datetime.now().isoformat()
                    }))
        except Exception as e:
            logger.error(f"Error handling typing indicator: {e}")
    
    async def _handle_read_receipt(self, ws_session: WebSocketSession, data: Dict[str, Any]):
        """Handle read receipt"""
        try:
            message_id = data.get("message_id")
            if message_id:
                # Update message status
                self._mark_message_read(message_id, ws_session.user_id)
                
                # Send read receipt to message sender
                for session_id, other_session in self.websocket_sessions.items():
                    if other_session.user_id == data.get("sender_id"):
                        await other_session.websocket.send(json.dumps({
                            "type": "read_receipt",
                            "message_id": message_id,
                            "read_by": ws_session.user_id,
                            "timestamp": datetime.now().isoformat()
                        }))
                        break
        except Exception as e:
            logger.error(f"Error handling read receipt: {e}")
    
    async def _send_chat_response(self, ws_session: WebSocketSession, response: str, original_message_id: str):
        """Send chat response via WebSocket"""
        try:
            await ws_session.websocket.send(json.dumps({
                "type": "chat_response",
                "content": response,
                "original_message_id": original_message_id,
                "timestamp": datetime.now().isoformat(),
                "companion_id": ws_session.companion_id
            }))
        except Exception as e:
            logger.error(f"Error sending chat response: {e}")
    
    def _mark_message_read(self, message_id: str, user_id: str):
        """Mark a message as read"""
        if message_id in self.active_sessions:
            session = self.active_sessions[message_id]
            for msg in session.get("messages", []):
                if msg.get("message_id") == message_id:
                    msg["status"] = MessageStatus.READ.value
                    msg["read_at"] = datetime.now().isoformat()
                    break
    
    def create_chat_session(self, user_id: str, companion_id: str) -> str:
        """Create a new chat session"""
        session_id = str(uuid.uuid4())
        
        self.active_sessions[session_id] = {
            'user_id': user_id,
            'companion_id': companion_id,
            'created_at': datetime.now(),
            'last_activity': datetime.now(),
            'messages': [],
            'is_active': True
        }
        
        logger.info(f"Chat session created: {session_id}")
        return session_id
    
    def send_message(self, session_id: str, user_id: str, content: str, 
                    message_type: MessageType = MessageType.TEXT) -> str:
        """Send a message in a chat session"""
        
        if session_id not in self.active_sessions:
            raise ValueError("Invalid session ID")
        
        session = self.active_sessions[session_id]
        # Allow system messages or messages from the session owner
        if user_id != "system" and session['user_id'] != user_id:
            raise ValueError("User not authorized for this session")
        
        # Create message
        message = ChatMessage(
            message_id=str(uuid.uuid4()),
            user_id=user_id,
            companion_id=session['companion_id'],
            message_type=message_type,
            content=content,
            timestamp=datetime.now()
        )
        
        # Add to session
        session['messages'].append(message)
        session['last_activity'] = datetime.now()
        
        logger.info(f"Message sent in session {session_id}: {message.message_id}")
        return message.message_id
    
    def process_message(self, session_id: str, message_id: str) -> Optional[str]:
        """Process a message and generate companion response"""
        
        if session_id not in self.active_sessions:
            return None
        
        session = self.active_sessions[session_id]
        message = next((m for m in session['messages'] if m.message_id == message_id), None)
        
        if not message:
            return None
        
        # Update status
        message.status = MessageStatus.PROCESSING
        start_time = datetime.now()
        
        try:
            # Process based on message type
            if message.message_type == MessageType.TEXT:
                response = self._process_text_message(message)
            elif message.message_type == MessageType.VOICE:
                response = self._process_voice_message(message)
            elif message.message_type == MessageType.IMAGE:
                response = self._process_image_message(message)
            else:
                response = "I'm not sure how to process this type of message."
            
            # Update message
            message.response = response
            message.status = MessageStatus.COMPLETED
            message.processing_time = (datetime.now() - start_time).total_seconds()
            
            logger.info(f"Message processed successfully: {message_id}")
            return response
            
        except Exception as e:
            message.status = MessageStatus.FAILED
            message.response = f"Sorry, I encountered an error: {str(e)}"
            logger.error(f"Message processing failed: {message_id}, error: {e}")
            return message.response
    
    def _process_text_message(self, message: ChatMessage) -> str:
        """Process text message and generate response"""
        try:
            # Check if we have a companion core callback
            if hasattr(self, 'companion_core_callback') and self.companion_core_callback:
                # Use companion core for response generation
                response = self.companion_core_callback(
                    user_id=message.user_id,
                    message_content=message.content,
                    message_type="text"
                )
                return response
            
            # Fallback response
            return f"I received your message: '{message.content}'. I'm processing this through my personality and wellbeing framework."
            
        except Exception as e:
            logger.error(f"Text message processing failed: {e}")
            return "I'm having trouble processing your message right now. Could you try again?"
    
    def _process_voice_message(self, message: ChatMessage) -> str:
        """Process voice message (convert to text first)"""
        try:
            # Convert audio to text
            transcript = self.speech_to_text(message.content)
            
            if transcript:
                # Update message content with transcript
                message.content = transcript
                message.metadata['transcript'] = transcript
                
                # Process as text message
                return self._process_text_message(message)
            else:
                return "I couldn't understand what you said. Could you please repeat that?"
                
        except Exception as e:
            logger.error(f"Voice message processing failed: {e}")
            return "I'm having trouble processing your voice message. Could you try typing instead?"
    
    def _process_image_message(self, message: ChatMessage) -> str:
        """Process image message using vision capabilities"""
        try:
            # Analyze the image
            image_analysis = self.process_image(message.content, task="analyze")
            
            if image_analysis.get('success'):
                analysis_text = image_analysis.get('analysis', '')
                
                # Check if we have a companion core callback for image processing
                if hasattr(self, 'companion_core_callback') and self.companion_core_callback:
                    # Create a context-aware prompt for the companion
                    image_prompt = f"I can see an image. Here's what I observe: {analysis_text}. Please respond as if you can see this image and engage with me about it."
                    
                    response = self.companion_core_callback(
                        user_id=message.user_id,
                        message_content=image_prompt,
                        message_type="image_analysis",
                        context={'image_analysis': image_analysis}
                    )
                    return response
                else:
                    return f"I can see you've shared an image. Here's what I observe: {analysis_text}"
            else:
                error_msg = image_analysis.get('error', 'Unknown error')
                return f"I'm having trouble analyzing this image: {error_msg}. Could you describe what you'd like me to look at?"
                
        except Exception as e:
            logger.error(f"Image processing failed: {e}")
            return "I'm having trouble analyzing this image. Could you describe what you'd like me to look at?"
    
    def text_to_speech(self, text: str, voice: str = "alloy") -> Optional[bytes]:
        """Convert text to speech"""
        if not self.tts_engine:
            logger.warning("TTS engine not available")
            return None
        
        try:
            if self.tts_engine == "openai" and self.openai_api_key:
                # Use OpenAI TTS API
                import openai
                client = openai.OpenAI(api_key=self.openai_api_key)
                
                response = client.audio.speech.create(
                    model="tts-1",
                    voice=voice,
                    input=text
                )
                
                audio_data = response.content
                logger.info(f"OpenAI TTS conversion successful: {text[:50]}...")
                return audio_data
                
            elif self.tts_engine == "system":
                # Use system TTS (macOS, Windows, Linux)
                import subprocess
                import tempfile
                import os
                
                # Create temporary file for output
                with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as temp_file:
                    temp_path = temp_file.name
                
                try:
                    # Use system TTS command
                    if os.name == 'posix':  # macOS/Linux
                        # macOS say command works better with .aiff format
                        if temp_path.endswith('.wav'):
                            temp_path = temp_path.replace('.wav', '.aiff')
                        
                        # Try different voice options for macOS
                        try:
                            # First try with default voice
                            subprocess.run([
                                'say', '-o', temp_path, text
                            ], check=True, capture_output=True)
                        except subprocess.CalledProcessError:
                            # Fallback to specific voice if available
                            try:
                                subprocess.run([
                                    'say', '-o', temp_path, '-v', 'Alex', text
                                ], check=True, capture_output=True)
                            except subprocess.CalledProcessError:
                                # Last resort: try without voice specification
                                subprocess.run([
                                    'say', '-o', temp_path, text
                                ], check=True, capture_output=True)
                    elif os.name == 'nt':  # Windows
                        # Windows SAPI TTS
                        import pyttsx3
                        engine = pyttsx3.init()
                        engine.save_to_file(text, temp_path)
                        engine.runAndWait()
                    else:
                        logger.warning("System TTS not supported on this platform")
                        return None
                    
                    # Read the generated audio file
                    with open(temp_path, 'rb') as f:
                        audio_data = f.read()
                    
                    # Clean up temporary file
                    os.unlink(temp_path)
                    
                    logger.info(f"System TTS conversion successful: {text[:50]}...")
                    return audio_data
                    
                except Exception as e:
                    logger.error(f"System TTS failed: {e}")
                    if os.path.exists(temp_path):
                        os.unlink(temp_path)
                    return None
            else:
                logger.warning(f"Unsupported TTS engine: {self.tts_engine}")
                return None
                
        except Exception as e:
            logger.error(f"TTS conversion failed: {e}")
            return None
    
    def speech_to_text(self, audio_data: bytes) -> Optional[str]:
        """Convert speech to text"""
        if not self.stt_engine:
            logger.warning("STT engine not available")
            return None
        
        try:
            if self.stt_engine == "openai" and self.openai_api_key:
                # Use OpenAI Whisper API
                import openai
                import tempfile
                import os
                
                # Create temporary file for audio
                with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as temp_file:
                    temp_path = temp_file.name
                    temp_file.write(audio_data)
                
                try:
                    client = openai.OpenAI(api_key=self.openai_api_key)
                    
                    with open(temp_path, 'rb') as audio_file:
                        response = client.audio.transcriptions.create(
                            model="whisper-1",
                            file=audio_file,
                            response_format="text"
                        )
                    
                    transcript = response
                    logger.info(f"OpenAI STT conversion successful: {len(audio_data)} bytes")
                    return transcript
                    
                finally:
                    # Clean up temporary file
                    if os.path.exists(temp_path):
                        os.unlink(temp_path)
                        
            elif self.stt_engine == "system":
                # Use system STT (limited support)
                logger.warning("System STT not fully implemented - requires additional setup")
                return None
            else:
                logger.warning(f"Unsupported STT engine: {self.stt_engine}")
                return None
                
        except Exception as e:
            logger.error(f"STT conversion failed: {e}")
            return None
    
    def process_image(self, image_data: bytes, task: str = "analyze") -> Dict[str, Any]:
        """Process image using vision capabilities"""
        try:
            if self.openai_api_key:
                # Use OpenAI GPT-4 Vision for image analysis
                import openai
                import base64
                
                # Convert image to base64
                image_base64 = base64.b64encode(image_data).decode('utf-8')
                
                client = openai.OpenAI(api_key=self.openai_api_key)
                
                # Create analysis prompt based on task
                if task == "analyze":
                    prompt = "Please analyze this image in detail. Describe what you see, including objects, people, actions, setting, and any notable details."
                elif task == "describe":
                    prompt = "Please provide a clear, detailed description of this image."
                elif task == "extract_text":
                    prompt = "Please extract and transcribe any text visible in this image."
                elif task == "identify_objects":
                    prompt = "Please identify and list the main objects, people, and elements visible in this image."
                else:
                    prompt = f"Please analyze this image for the task: {task}"
                
                response = client.chat.completions.create(
                    model="gpt-5",
                    messages=[
                        {
                            "role": "user",
                            "content": [
                                {"type": "text", "text": prompt},
                                {
                                    "type": "image_url",
                                    "image_url": {
                                        "url": f"data:image/jpeg;base64,{image_base64}"
                                    }
                                }
                            ]
                        }
                    ],
                    max_tokens=500
                )
                
                analysis = response.choices[0].message.content
                
                result = {
                    'task': task,
                    'analysis': analysis,
                    'model': 'gpt-5',
                    'timestamp': datetime.now().isoformat(),
                    'success': True
                }
                
                logger.info(f"OpenAI Vision analysis successful for task: {task}")
                return result
                
            else:
                # Fallback to basic image analysis
                image = Image.open(io.BytesIO(image_data))
                
                # Basic image properties
                result = {
                    'task': task,
                    'image_size': image.size,
                    'image_mode': image.mode,
                    'image_format': image.format,
                    'analysis': f"Basic image analysis - Size: {image.size}, Mode: {image.mode}, Format: {image.format}",
                    'timestamp': datetime.now().isoformat(),
                    'success': True,
                    'note': 'OpenAI API key not available - using basic analysis'
                }
                
                # Try to extract basic information
                try:
                    if hasattr(image, 'info'):
                        result['metadata'] = image.info
                    
                    # Convert to RGB if needed for further processing
                    if image.mode != 'RGB':
                        image_rgb = image.convert('RGB')
                        result['converted_to_rgb'] = True
                    
                    # Basic color analysis
                    if image.mode in ['RGB', 'RGBA']:
                        # Get dominant colors
                        colors = image.getcolors(maxcolors=256)
                        if colors:
                            result['color_count'] = len(colors)
                            result['dominant_colors'] = sorted(colors, key=lambda x: x[0], reverse=True)[:5]
                    
                except Exception as e:
                    result['processing_errors'] = str(e)
                
                logger.info(f"Basic image analysis completed: {image.size} {image.mode}")
                return result
            
        except Exception as e:
            logger.error(f"Image processing failed: {e}")
            return {
                'error': str(e),
                'task': task,
                'timestamp': datetime.now().isoformat(),
                'success': False
            }
    
    def start_companion_action(self, session_id: str, action_type: str, 
                             description: str, estimated_duration: float) -> str:
        """Start a companion action and communicate it to the user"""
        
        if session_id not in self.active_sessions:
            raise ValueError("Invalid session ID")
        
        action_id = str(uuid.uuid4())
        
        action = CompanionAction(
            action_id=action_id,
            action_type=action_type,
            description=description,
            estimated_duration=estimated_duration
        )
        
        # Store action
        if 'actions' not in self.active_sessions[session_id]:
            self.active_sessions[session_id]['actions'] = {}
        
        self.active_sessions[session_id]['actions'][action_id] = action
        
        # Send system message about the action
        action_message = f"I'm {description}. This might take a moment..."
        self.send_message(session_id, "system", action_message, MessageType.SYSTEM)
        
        logger.info(f"Companion action started: {action_id} - {description}")
        return action_id
    
    def update_action_progress(self, session_id: str, action_id: str, 
                             progress: float, status: str = None):
        """Update the progress of a companion action"""
        
        if session_id not in self.active_sessions:
            return
        
        session = self.active_sessions[session_id]
        if 'actions' not in session or action_id not in session['actions']:
            return
        
        action = session['actions'][action_id]
        action.progress = max(0.0, min(1.0, progress))
        
        if status:
            action.status = status
        
        # Send progress update if significant change
        if progress >= 1.0:
            completion_message = f"I've completed: {action.description}"
            self.send_message(session_id, "system", completion_message, MessageType.SYSTEM)
            logger.info(f"Action completed: {action_id}")
        elif progress > 0.5 and action.status == "starting":
            action.status = "in_progress"
            progress_message = f"I'm still working on: {action.description}"
            self.send_message(session_id, "system", progress_message, MessageType.SYSTEM)
    
    def get_session_messages(self, session_id: str, limit: int = 50) -> List[Dict[str, Any]]:
        """Get recent messages from a session"""
        
        if session_id not in self.active_sessions:
            return []
        
        session = self.active_sessions[session_id]
        messages = session['messages'][-limit:]
        
        return [
            {
                'message_id': msg.message_id,
                'user_id': msg.user_id,
                'message_type': msg.message_type.value,
                'content': msg.content,
                'timestamp': msg.timestamp.isoformat(),
                'status': msg.status.value,
                'response': msg.response,
                'processing_time': msg.processing_time
            }
            for msg in messages
        ]
    
    def get_active_actions(self, session_id: str) -> List[Dict[str, Any]]:
        """Get active companion actions for a session"""
        
        if session_id not in self.active_sessions:
            return []
        
        session = self.active_sessions[session_id]
        if 'actions' not in session:
            return []
        
        return [
            {
                'action_id': action.action_id,
                'action_type': action.action_type,
                'description': action.description,
                'estimated_duration': action.estimated_duration,
                'progress': action.progress,
                'status': action.status,
                'start_time': action.start_time.isoformat(),
                'elapsed_time': (datetime.now() - action.start_time).total_seconds()
            }
            for action in session['actions'].values()
            if action.status in ['starting', 'in_progress']
        ]
    
    def end_chat_session(self, session_id: str):
        """End a chat session"""
        
        if session_id in self.active_sessions:
            self.active_sessions[session_id]['is_active'] = False
            logger.info(f"Chat session ended: {session_id}")
    
    def cleanup_inactive_sessions(self, max_idle_hours: int = 24):
        """Clean up inactive chat sessions"""
        
        cutoff_time = datetime.now() - timedelta(hours=max_idle_hours)
        sessions_to_remove = []
        
        for session_id, session in self.active_sessions.items():
            if (not session['is_active'] or 
                session['last_activity'] < cutoff_time):
                sessions_to_remove.append(session_id)
        
        for session_id in sessions_to_remove:
            del self.active_sessions[session_id]
            logger.info(f"Cleaned up inactive session: {session_id}")
    
    def get_session_stats(self, session_id: str) -> Dict[str, Any]:
        """Get statistics for a chat session"""
        
        if session_id not in self.active_sessions:
            return {}
        
        session = self.active_sessions[session_id]
        messages = session['messages']
        
        stats = {
            'total_messages': len(messages),
            'user_messages': len([m for m in messages if m.user_id != "system"]),
            'system_messages': len([m for m in messages if m.user_id == "system"]),
            'message_types': {},
            'average_response_time': 0.0,
            'session_duration': (datetime.now() - session['created_at']).total_seconds()
        }
        
        # Count message types
        for msg in messages:
            msg_type = msg.message_type.value
            stats['message_types'][msg_type] = stats['message_types'].get(msg_type, 0) + 1
        
        # Calculate average response time
        response_times = [m.processing_time for m in messages if m.processing_time]
        if response_times:
            stats['average_response_time'] = sum(response_times) / len(response_times)
        
        return stats

    def communicate_action(self, user_id: str, action: str, details: str = "", 
                         estimated_duration: float = 0.0) -> str:
        """
        Communicate what the companion is doing when actions take longer
        than would be appropriate for a fluid conversation
        """
        try:
            # Create action message
            action_message = f"I'm {action}"
            if details:
                action_message += f": {details}"
            
            if estimated_duration > 0:
                if estimated_duration < 1.0:
                    action_message += " - this should only take a moment"
                elif estimated_duration < 5.0:
                    action_message += " - this will take a few seconds"
                else:
                    action_message += f" - this may take about {estimated_duration:.0f} seconds"
            
            # Store action in session - find session by user_id
            user_session = None
            for session_id, session in self.active_sessions.items():
                if session.get('user_id') == user_id and session.get('is_active', False):
                    user_session = session
                    break
            
            if user_session:
                action_msg = ChatMessage(
                    message_id=str(uuid.uuid4()),
                    user_id=user_id,
                    companion_id=user_session.get('companion_id', 'unknown'),
                    content=action_message,
                    message_type=MessageType.ACTION,
                    timestamp=datetime.now(),
                    status=MessageStatus.COMPLETED
                )
                user_session['messages'].append(action_msg)
                
                # Send to WebSocket if available
                if user_id in self.websocket_sessions:
                    self._send_websocket_message(user_id, action_msg)
                
                logger.info(f"Action communicated to user {user_id}: {action_message}")
            
            return action_message
            
        except Exception as e:
            logger.error(f"Failed to communicate action: {e}")
            return f"I'm {action} right now."
    
    def set_companion_core_callback(self, callback: Callable):
        """Set callback for companion core integration"""
        self.companion_core_callback = callback
        logger.info("Companion core callback set")

    def _send_websocket_message(self, user_id: str, message: ChatMessage):
        """Send message to user via WebSocket if available"""
        try:
            # Find active WebSocket session for this user
            for session_id, ws_session in self.websocket_sessions.items():
                if ws_session.user_id == user_id and ws_session.is_active:
                    asyncio.create_task(self._send_message_to_websocket(ws_session, message))
                    break
        except Exception as e:
            logger.error(f"Failed to send WebSocket message: {e}")

    async def _send_message_to_websocket(self, ws_session: WebSocketSession, message: ChatMessage):
        """Send a message to a specific WebSocket session"""
        try:
            if ws_session.websocket.open:
                await ws_session.websocket.send(json.dumps({
                    "type": "chat_message",
                    "message_id": message.message_id,
                    "content": message.content,
                    "message_type": message.message_type.value,
                    "timestamp": message.timestamp.isoformat(),
                    "user_id": message.user_id,
                    "companion_id": message.companion_id
                }))
                ws_session.last_activity = datetime.now()
            else:
                logger.warning(f"WebSocket closed for session {ws_session.session_id}")
        except Exception as e:
            logger.error(f"Failed to send message to WebSocket: {e}")

    def broadcast_message(self, companion_id: str, message: str, message_type: MessageType = MessageType.SYSTEM):
        """Broadcast a message to all active sessions with a specific companion"""
        try:
            broadcast_message = ChatMessage(
                message_id=str(uuid.uuid4()),
                user_id="system",
                companion_id=companion_id,
                message_type=message_type,
                content=message,
                timestamp=datetime.now(),
                status=MessageStatus.COMPLETED
            )

            # Send to all active sessions with this companion
            for session_id, session in self.active_sessions.items():
                if (session['is_active'] and 
                    session['companion_id'] == companion_id):
                    session['messages'].append(broadcast_message)
                    
                    # Send via WebSocket if available
                    for ws_session_id, ws_session in self.websocket_sessions.items():
                        if ws_session.companion_id == companion_id:
                            asyncio.create_task(self._send_message_to_websocket(ws_session, broadcast_message))

            logger.info(f"Broadcast message sent to companion {companion_id}: {message}")
            return broadcast_message.message_id

        except Exception as e:
            logger.error(f"Failed to broadcast message: {e}")
            return None

    def get_user_sessions(self, user_id: str) -> List[Dict[str, Any]]:
        """Get all active sessions for a specific user"""
        user_sessions = []
        
        for session_id, session in self.active_sessions.items():
            if session['user_id'] == user_id and session['is_active']:
                user_sessions.append({
                    'session_id': session_id,
                    'companion_id': session['companion_id'],
                    'created_at': session['created_at'].isoformat(),
                    'last_activity': session['last_activity'].isoformat(),
                    'message_count': len(session['messages']),
                    'active_actions': len(session.get('actions', {}))
                })
        
        return user_sessions

    def get_companion_sessions(self, companion_id: str) -> List[Dict[str, Any]]:
        """Get all active sessions for a specific companion"""
        companion_sessions = []
        
        for session_id, session in self.active_sessions.items():
            if session['companion_id'] == companion_id and session['is_active']:
                companion_sessions.append({
                    'session_id': session_id,
                    'user_id': session['user_id'],
                    'created_at': session['created_at'].isoformat(),
                    'last_activity': session['last_activity'].isoformat(),
                    'message_count': len(session['messages']),
                    'active_actions': len(session.get('actions', {}))
                })
        
        return companion_sessions

    def pause_session(self, session_id: str):
        """Pause a chat session (temporarily disable message processing)"""
        if session_id in self.active_sessions:
            self.active_sessions[session_id]['is_active'] = False
            self.active_sessions[session_id]['paused_at'] = datetime.now()
            logger.info(f"Session paused: {session_id}")

    def resume_session(self, session_id: str):
        """Resume a paused chat session"""
        if session_id in self.active_sessions:
            self.active_sessions[session_id]['is_active'] = True
            if 'paused_at' in self.active_sessions[session_id]:
                del self.active_sessions[session_id]['paused_at']
            logger.info(f"Session resumed: {session_id}")

    def add_session_metadata(self, session_id: str, key: str, value: Any):
        """Add metadata to a session"""
        if session_id in self.active_sessions:
            if 'metadata' not in self.active_sessions[session_id]:
                self.active_sessions[session_id]['metadata'] = {}
            self.active_sessions[session_id]['metadata'][key] = value
            logger.info(f"Metadata added to session {session_id}: {key}")

    def get_session_metadata(self, session_id: str, key: str = None) -> Any:
        """Get metadata from a session"""
        if session_id not in self.active_sessions:
            return None
        
        session = self.active_sessions[session_id]
        if 'metadata' not in session:
            return None
        
        if key is None:
            return session['metadata']
        return session['metadata'].get(key)

    def set_message_priority(self, message_id: str, priority: str = "normal"):
        """Set priority for a message (low, normal, high, urgent)"""
        for session in self.active_sessions.values():
            for msg in session.get('messages', []):
                if msg.message_id == message_id:
                    msg.metadata['priority'] = priority
                    logger.info(f"Message priority set: {message_id} -> {priority}")
                    return
        logger.warning(f"Message not found for priority setting: {message_id}")

    def get_high_priority_messages(self, session_id: str) -> List[ChatMessage]:
        """Get high priority messages from a session"""
        if session_id not in self.active_sessions:
            return []
        
        session = self.active_sessions[session_id]
        return [
            msg for msg in session.get('messages', [])
            if msg.metadata.get('priority') in ['high', 'urgent']
        ]

    def add_message_reaction(self, message_id: str, user_id: str, reaction: str):
        """Add a reaction to a message"""
        for session in self.active_sessions.values():
            for msg in session.get('messages', []):
                if msg.message_id == message_id:
                    if 'reactions' not in msg.metadata:
                        msg.metadata['reactions'] = {}
                    if user_id not in msg.metadata['reactions']:
                        msg.metadata['reactions'][user_id] = []
                    msg.metadata['reactions'][user_id].append(reaction)
                    logger.info(f"Reaction added to message {message_id}: {user_id} -> {reaction}")
                    return
        logger.warning(f"Message not found for reaction: {message_id}")

    def get_message_reactions(self, message_id: str) -> Dict[str, List[str]]:
        """Get all reactions for a message"""
        for session in self.active_sessions.values():
            for msg in session.get('messages', []):
                if msg.message_id == message_id:
                    return msg.metadata.get('reactions', {})
        return {}

    def search_messages(self, session_id: str, query: str, 
                       message_types: List[MessageType] = None) -> List[ChatMessage]:
        """Search messages in a session"""
        if session_id not in self.active_sessions:
            return []
        
        session = self.active_sessions[session_id]
        messages = session.get('messages', [])
        
        # Filter by message type if specified
        if message_types:
            messages = [msg for msg in messages if msg.message_type in message_types]
        
        # Simple text search
        query_lower = query.lower()
        matching_messages = []
        
        for msg in messages:
            if (query_lower in msg.content.lower() or 
                (msg.response and query_lower in msg.response.lower())):
                matching_messages.append(msg)
        
        return matching_messages

    def export_session_history(self, session_id: str, format: str = "json") -> str:
        """Export session history in specified format"""
        if session_id not in self.active_sessions:
            return ""
        
        session = self.active_sessions[session_id]
        
        if format.lower() == "json":
            export_data = {
                'session_id': session_id,
                'user_id': session['user_id'],
                'companion_id': session['companion_id'],
                'created_at': session['created_at'].isoformat(),
                'last_activity': session['last_activity'].isoformat(),
                'messages': [
                    {
                        'message_id': msg.message_id,
                        'user_id': msg.user_id,
                        'message_type': msg.message_type.value,
                        'content': msg.content,
                        'timestamp': msg.timestamp.isoformat(),
                        'status': msg.status.value,
                        'response': msg.response,
                        'processing_time': msg.processing_time,
                        'metadata': msg.metadata
                    }
                    for msg in session.get('messages', [])
                ]
            }
            return json.dumps(export_data, indent=2, default=str)
        
        elif format.lower() == "txt":
            lines = [
                f"Session: {session_id}",
                f"User: {session['user_id']}",
                f"Companion: {session['companion_id']}",
                f"Created: {session['created_at']}",
                f"Last Activity: {session['last_activity']}",
                "",
                "=== MESSAGE HISTORY ===",
                ""
            ]
            
            for msg in session.get('messages', []):
                lines.append(f"[{msg.timestamp}] {msg.user_id}: {msg.content}")
                if msg.response:
                    lines.append(f"  Companion: {msg.response}")
                lines.append("")
            
            return "\n".join(lines)
        
        else:
            logger.warning(f"Unsupported export format: {format}")
            return ""

    def get_system_health(self) -> Dict[str, Any]:
        """Get system health information"""
        try:
            health_info = {
                'status': 'healthy',
                'timestamp': datetime.now().isoformat(),
                'active_sessions': len(self.active_sessions),
                'websocket_sessions': len(self.websocket_sessions),
                'tts_engine': self.tts_engine,
                'stt_engine': self.stt_engine,
                'websocket_port': self.websocket_port,
                'websocket_server_running': self.websocket_server is not None,
                'websocket_thread_alive': self.websocket_thread and self.websocket_thread.is_alive()
            }
            
            # Check for potential issues
            issues = []
            if not self.websocket_server:
                issues.append("WebSocket server not running")
            if not self.websocket_thread or not self.websocket_thread.is_alive():
                issues.append("WebSocket thread not alive")
            if not self.tts_engine:
                issues.append("TTS engine not available")
            if not self.stt_engine:
                issues.append("STT engine not available")
            
            if issues:
                health_info['status'] = 'degraded'
                health_info['issues'] = issues
            
            return health_info
            
        except Exception as e:
            logger.error(f"Failed to get system health: {e}")
            return {
                'status': 'error',
                'error': str(e),
                'timestamp': datetime.now().isoformat()
            }

    def restart_websocket_server(self):
        """Restart the WebSocket server"""
        try:
            logger.info("Restarting WebSocket server...")
            
            # Stop existing server
            if self.websocket_server:
                asyncio.create_task(self.websocket_server.close())
                self.websocket_server = None
            
            # Stop existing thread
            if self.websocket_thread and self.websocket_thread.is_alive():
                # Note: This is a simplified approach - in production you'd want proper thread management
                self.websocket_thread = None
            
            # Start new server
            self._start_websocket_server()
            logger.info("WebSocket server restarted successfully")
            
        except Exception as e:
            logger.error(f"Failed to restart WebSocket server: {e}")

    def cleanup_old_messages(self, session_id: str, max_messages: int = 1000):
        """Clean up old messages to prevent memory issues"""
        if session_id not in self.active_sessions:
            return
        
        session = self.active_sessions[session_id]
        messages = session.get('messages', [])
        
        if len(messages) > max_messages:
            # Keep the most recent messages
            messages_to_keep = messages[-max_messages:]
            session['messages'] = messages_to_keep
            
            # Archive old messages if needed
            old_messages = messages[:-max_messages]
            if 'archived_messages' not in session:
                session['archived_messages'] = []
            session['archived_messages'].extend(old_messages)
            
            logger.info(f"Cleaned up {len(old_messages)} old messages from session {session_id}")

    def get_companion_activity_summary(self, companion_id: str, hours: int = 24) -> Dict[str, Any]:
        """Get activity summary for a companion over a time period"""
        try:
            cutoff_time = datetime.now() - timedelta(hours=hours)
            companion_sessions = self.get_companion_sessions(companion_id)
            
            total_messages = 0
            total_users = set()
            active_sessions = 0
            
            for session in companion_sessions:
                session_data = self.active_sessions.get(session['session_id'])
                if session_data:
                    total_users.add(session['user_id'])
                    total_messages += len(session_data.get('messages', []))
                    if session_data['is_active']:
                        active_sessions += 1
            
            return {
                'companion_id': companion_id,
                'time_period_hours': hours,
                'total_sessions': len(companion_sessions),
                'active_sessions': active_sessions,
                'total_messages': total_messages,
                'unique_users': len(total_users),
                'average_messages_per_session': total_messages / len(companion_sessions) if companion_sessions else 0
            }
            
        except Exception as e:
            logger.error(f"Failed to get companion activity summary: {e}")
            return {}

    def __del__(self):
        """Cleanup when the communication system is destroyed"""
        try:
            if self.websocket_server:
                asyncio.create_task(self.websocket_server.close())
            logger.info("Communication system destroyed")
        except Exception as e:
            logger.error(f"Error during cleanup: {e}")
