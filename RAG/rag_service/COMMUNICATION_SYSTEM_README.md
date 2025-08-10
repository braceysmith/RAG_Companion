# Communication System for RAG Companions

A comprehensive real-time communication system designed for AI companions, supporting text chat, voice communication, image processing, and WebSocket-based real-time interactions.

## 🚀 Features

### Core Communication
- **Real-time Chat**: WebSocket-based messaging with typing indicators and read receipts
- **Multi-modal Support**: Text, voice, image, and audio message types
- **Session Management**: Create, pause, resume, and manage chat sessions
- **Message History**: Full conversation logging with export capabilities

### Voice & Audio
- **Text-to-Speech (TTS)**: OpenAI TTS API integration with system fallback
- **Speech-to-Text (STT)**: OpenAI Whisper integration for voice input
- **Audio Processing**: Support for various audio formats and real-time streaming

### Image Processing
- **Vision Analysis**: OpenAI GPT-4 Vision integration for image understanding
- **Basic Image Analysis**: Fallback processing when API is unavailable
- **Multi-task Support**: Analyze, describe, extract text, identify objects

### Advanced Features
- **Companion Actions**: Track and communicate long-running tasks
- **Message Priority**: Set and manage message importance levels
- **Reactions & Metadata**: Rich message interactions and session data
- **Broadcasting**: Send messages to multiple sessions simultaneously
- **Search & Export**: Find messages and export conversation history

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   User Client   │    │  Communication   │    │  Companion      │
│                 │◄──►│     System       │◄──►│     Core        │
│  (WebSocket)    │    │                  │    │                 │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌──────────────────┐
                       │   TTS/STT        │
                       │   Engines        │
                       └──────────────────┘
```

## 📦 Installation

1. **Clone the repository**:
```bash
git clone <repository-url>
cd RAG/rag_service
```

2. **Install dependencies**:
```bash
pip install -r requirements.txt
```

3. **Set up environment variables** (optional):
```bash
export OPENAI_API_KEY="your-api-key-here"
```

## 🚀 Quick Start

### Basic Usage

```python
from companion_system.communication_system import CommunicationSystem, MessageType

# Initialize the system
comm_system = CommunicationSystem(
    openai_api_key="your-key",  # Optional
    websocket_port=8765
)

# Create a chat session
session_id = comm_system.create_chat_session(
    user_id="alice",
    companion_id="ai_companion"
)

# Send a message
message_id = comm_system.send_message(
    session_id=session_id,
    user_id="alice",
    content="Hello! How are you?",
    message_type=MessageType.TEXT
)

# Process the message
response = comm_system.process_message(session_id, message_id)
print(f"Companion: {response}")
```

### WebSocket Real-time Communication

```python
import asyncio
import websockets
import json

async def connect_to_companion():
    uri = "ws://localhost:8765"
    async with websockets.connect(uri) as websocket:
        # Authenticate
        auth_message = {
            "user_id": "user123",
            "companion_id": "companion456",
            "session_id": "session789"
        }
        await websocket.send(json.dumps(auth_message))
        
        # Send chat message
        chat_message = {
            "type": "chat_message",
            "content": "Hello from WebSocket!",
            "message_type": "text"
        }
        await websocket.send(json.dumps(chat_message))
        
        # Listen for responses
        async for message in websocket:
            data = json.loads(message)
            print(f"Received: {data}")

# Run the WebSocket client
asyncio.run(connect_to_companion())
```

## 🔧 API Reference

### Core Methods

#### Session Management
- `create_chat_session(user_id, companion_id)` → `str`
- `end_chat_session(session_id)` → `None`
- `pause_session(session_id)` → `None`
- `resume_session(session_id)` → `None`

#### Messaging
- `send_message(session_id, user_id, content, message_type)` → `str`
- `process_message(session_id, message_id)` → `Optional[str]`
- `broadcast_message(companion_id, message, message_type)` → `str`

#### Voice & Audio
- `text_to_speech(text, voice)` → `Optional[bytes]`
- `speech_to_text(audio_data)` → `Optional[str]`

#### Image Processing
- `process_image(image_data, task)` → `Dict[str, Any]`

#### Companion Actions
- `start_companion_action(session_id, action_type, description, duration)` → `str`
- `update_action_progress(session_id, action_id, progress, status)` → `None`

### Data Structures

#### ChatMessage
```python
@dataclass
class ChatMessage:
    message_id: str
    user_id: str
    companion_id: str
    message_type: MessageType
    content: str
    timestamp: datetime
    status: MessageStatus
    metadata: Dict[str, Any]
    response: Optional[str]
    processing_time: Optional[float]
```

#### CompanionAction
```python
@dataclass
class CompanionAction:
    action_id: str
    action_type: str
    description: str
    estimated_duration: float
    progress: float
    status: str
    start_time: datetime
    metadata: Dict[str, Any]
```

## 🧪 Testing

Run the comprehensive test suite:

```bash
python test_communication_system.py
```

Or run specific examples:

```bash
python example_usage.py
```

## 🔌 WebSocket Protocol

### Message Types

#### Authentication
```json
{
    "user_id": "string",
    "companion_id": "string",
    "session_id": "string"
}
```

#### Chat Message
```json
{
    "type": "chat_message",
    "content": "string",
    "message_type": "text|voice|image|audio"
}
```

#### Typing Indicator
```json
{
    "type": "typing_indicator",
    "is_typing": boolean
}
```

#### Read Receipt
```json
{
    "type": "read_receipt",
    "message_id": "string"
}
```

### Response Types

#### Connection Confirmed
```json
{
    "type": "connection_confirmed",
    "session_id": "string",
    "timestamp": "ISO8601"
}
```

#### Chat Response
```json
{
    "type": "chat_response",
    "content": "string",
    "original_message_id": "string",
    "timestamp": "ISO8601",
    "companion_id": "string"
}
```

## 🎯 Use Cases

### 1. AI Companion Chat
- Real-time conversation with AI companions
- Multi-modal input (text, voice, images)
- Context-aware responses

### 2. Task Management
- Track companion actions and progress
- Communicate long-running operations
- Provide status updates to users

### 3. Multi-user Sessions
- Support multiple users per companion
- Session isolation and management
- Broadcast messages to all users

### 4. Voice Interfaces
- Voice-controlled companions
- Audio response generation
- Speech recognition for commands

## 🔒 Security Features

- **Session Authentication**: Required for WebSocket connections
- **User Authorization**: Users can only access their own sessions
- **Input Validation**: All messages are validated before processing
- **Rate Limiting**: Built-in protection against abuse

## 🚨 Error Handling

The system includes comprehensive error handling:

- **Connection Failures**: Automatic reconnection attempts
- **Message Processing**: Graceful degradation on errors
- **API Failures**: Fallback to system capabilities
- **Session Recovery**: Automatic cleanup of stale sessions

## 📊 Monitoring & Health

```python
# Get system health
health = comm_system.get_system_health()
print(f"Status: {health['status']}")
print(f"Active sessions: {health['active_sessions']}")
print(f"WebSocket sessions: {health['websocket_sessions']}")

# Get session statistics
stats = comm_system.get_session_stats(session_id)
print(f"Total messages: {stats['total_messages']}")
print(f"Average response time: {stats['average_response_time']:.2f}s")
```

## 🔧 Configuration

### Environment Variables
- `OPENAI_API_KEY`: OpenAI API key for enhanced features
- `WEBSOCKET_PORT`: WebSocket server port (default: 8765)
- `LOG_LEVEL`: Logging level (default: INFO)

### Customization
```python
# Custom TTS/STT engines
comm_system.tts_engine = "custom_engine"
comm_system.stt_engine = "custom_engine"

# Set companion core callback
def my_companion_callback(user_id, message_content, message_type, context=None):
    # Custom response generation logic
    return "Custom response"

comm_system.set_companion_core_callback(my_companion_callback)
```

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For issues and questions:
1. Check the documentation
2. Review existing issues
3. Create a new issue with detailed information
4. Include logs and error messages

## 🔮 Roadmap

- [ ] Enhanced audio streaming
- [ ] Video message support
- [ ] Advanced encryption
- [ ] Load balancing
- [ ] Metrics and analytics
- [ ] Plugin system
- [ ] Multi-language support

---

**Built with ❤️ for AI companions everywhere**
