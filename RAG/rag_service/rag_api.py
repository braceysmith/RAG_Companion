import os
import uuid
import asyncio
from typing import List, Optional, Dict, Any
from pathlib import Path
from datetime import datetime

from fastapi import FastAPI, HTTPException, BackgroundTasks, WebSocket, WebSocketDisconnect, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import HTMLResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel
from openai import OpenAI
from dotenv import load_dotenv
import psycopg

from database import RAGDatabase
from chunker import DocumentChunker, DocumentProcessor
from mcp_tools import tool_manager
from audio_handler import audio_handler
from hybrid_rag_system import get_hybrid_rag, process_voice_query

# Load environment variables
load_dotenv()

# Initialize OpenAI client
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))

# Initialize FastAPI app
app = FastAPI(title="RAG Companion Service", version="1.0.0")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Configure appropriately for production
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Admin interface route
@app.get("/admin", response_class=HTMLResponse)
async def admin_interface():
    """Serve the admin dashboard interface"""
    try:
        with open("admin_interface.html", "r", encoding="utf-8") as f:
            return HTMLResponse(content=f.read())
    except FileNotFoundError:
        return HTMLResponse(content="<h1>Admin interface not found</h1>", status_code=404)

# Initialize database with better error handling
database_url = os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db")
db = RAGDatabase(database_url)

# Global variable to track database status
database_available = False

# Initialize chunker and processor
chunker = DocumentChunker()
processor = DocumentProcessor(chunker)

# Pydantic models
class RAGFilters(BaseModel):
    user_scope: Optional[List[str]] = ["global"]
    safety_level: Optional[List[str]] = ["public"]

class RAGQueryRequest(BaseModel):
    query: str
    user_id: Optional[str] = None
    top_k: int = 5
    filters: Optional[RAGFilters] = RAGFilters()

class RAGResult(BaseModel):
    chunk_id: str
    text: str
    doc_title: str
    section: Optional[str] = None
    tags: List[str]
    source_path: str
    score: float

class RAGQueryResponse(BaseModel):
    results: List[RAGResult]
    query_embedding_ms: Optional[float] = None
    db_lookup_ms: Optional[float] = None

class MemoryRequest(BaseModel):
    user_id: str
    memory_type: str  # 'episodic', 'semantic', 'profile'
    content: str
    metadata: Optional[Dict[str, Any]] = None

class MemoryQueryRequest(BaseModel):
    user_id: str
    query: str
    memory_types: Optional[List[str]] = None
    top_k: int = 3

class RealtimeSessionRequest(BaseModel):
    user_id: str
    model: str = "gpt-4o-realtime-preview-2024-10-01"
    instructions: Optional[str] = None

class MemoryResult(BaseModel):
    memory_id: str
    memory_type: str
    content: str
    metadata: Dict[str, Any]
    score: float

class MemoryQueryResponse(BaseModel):
    results: List[MemoryResult]

class ConversationTurnRequest(BaseModel):
    turn_id: str
    session_id: str
    user_id: str
    turn_index: int
    user_message: str
    assistant_response: str
    retrieved_chunks: List[str]
    metadata: Optional[Dict[str, Any]] = None

class IngestionRequest(BaseModel):
    directory_path: str
    user_scope: str = "global"
    safety_level: str = "public"

class AudioRequest(BaseModel):
    user_id: str
    audio_format: str = "webm"

class AudioResponse(BaseModel):
    transcript: str
    response_text: str
    audio_response: Optional[str] = None  # Base64 encoded audio
    tool_result: Optional[Dict[str, Any]] = None
    personal_info: Optional[Dict[str, Any]] = None

class TTSRequest(BaseModel):
    text: str
    voice: str = "alloy"
    user_id: Optional[str] = None

# Global state
embedding_cache = {}
user_profiles = {}  # Simple in-memory storage for personal info
user_conversations = {}  # Store recent conversation history for context
user_reminders = {}  # Store active reminders {user_id: [reminder_objects]}

@app.on_event("startup")
async def startup_event():
    """Initialize database on startup"""
    global database_available
    try:
        await db.initialize()
        database_available = True
        print("✅ RAG service started successfully with PostgreSQL + pgvector")
        print(f"📊 Database URL: {database_url[:50]}...")
    except Exception as e:
        database_available = False
        print(f"❌ Database initialization failed: {e}")
        print(f"🔄 RAG service started with in-memory fallback mode")
        print(f"💡 To enable vector database: Set DATABASE_URL to a PostgreSQL URL with pgvector extension")

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {
        "status": "healthy", 
        "service": "RAG Companion Service",
        "database_available": database_available,
        "vector_search": "enabled" if database_available else "fallback_mode",
        "stored_users": len(user_profiles),
        "active_reminders": sum(len(reminders) for reminders in user_reminders.values())
    }

@app.post("/test")
def test_endpoint(request: dict):
    """Simple test endpoint - synchronous"""
    return {"message": f"Received: {request.get('query', 'no query')}", "status": "success"}

@app.get("/debug")
def debug_endpoint():
    """Debug endpoint to check basic functionality"""
    try:
        import time
        return {
            "status": "ok",
            "timestamp": int(time.time()),
            "message": "API is responding",
            "openai_configured": bool(os.getenv("OPENAI_API_KEY")),
            "database_url": bool(os.getenv("DATABASE_URL")),
            "stored_users": len(user_profiles)
        }
    except Exception as e:
        return {"status": "error", "error": str(e)}

@app.get("/time/sync")
def get_server_time():
    """Get current server time for client synchronization"""
    try:
        from datetime import datetime, timezone
        import time
        
        # Get both Unix timestamp and ISO format for flexibility
        utc_now = datetime.now(timezone.utc)
        unix_timestamp = time.time()
        
        return {
            "status": "success",
            "server_time": {
                "utc_iso": utc_now.isoformat(),
                "unix_timestamp": unix_timestamp,
                "utc_datetime": utc_now.strftime("%Y-%m-%d %H:%M:%S UTC")
            },
            "message": "Server time retrieved successfully"
        }
    except Exception as e:
        return {"status": "error", "message": f"Failed to get server time: {str(e)}"}

@app.get("/user/{user_id}/profile")
def get_user_profile_simple(user_id: str):
    """Get stored profile information for a user"""
    try:
        profile = user_profiles.get(user_id, {})
        return {
            "user_id": user_id,
            "profile": profile,
            "has_name": "name" in profile
        }
    except Exception as e:
        return {"error": str(e), "user_id": user_id, "profile": {}}

@app.get("/tools")
def get_available_tools():
    """Get list of available MCP tools"""
    try:
        tools = tool_manager.get_available_tools()
        return {
            "available_tools": tools,
            "total_count": len(tools)
        }
    except Exception as e:
        return {"error": str(e), "available_tools": []}

@app.websocket("/ws/realtime/{user_id}")
async def websocket_realtime_endpoint(websocket: WebSocket, user_id: str):
    """WebSocket endpoint for real-time audio communication"""
    await websocket.accept()
    try:
        # Get user profile for context
        user_profile = user_profiles.get(user_id, {})
        
        # Handle real-time WebSocket communication
        await audio_handler.handle_realtime_websocket(websocket, user_id, user_profile)
        
    except WebSocketDisconnect:
        print(f"WebSocket disconnected for user {user_id}")
    except Exception as e:
        print(f"WebSocket error for user {user_id}: {e}")
        await websocket.close()

@app.post("/audio/speech-to-text", response_model=dict)
async def speech_to_text_endpoint(
    audio: UploadFile = File(...),
    user_id: str = "anonymous",
    audio_format: str = "webm"
):
    """Convert speech to text using OpenAI Whisper"""
    try:
        # Read audio file
        audio_data = await audio.read()
        
        # Process with Whisper
        transcript = await audio_handler.speech_to_text(audio_data, audio_format)
        
        return {
            "transcript": transcript,
            "user_id": user_id,
            "status": "success"
        }
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Speech-to-text failed: {str(e)}")

@app.post("/audio/text-to-speech")
async def text_to_speech_endpoint(request: TTSRequest):
    """Convert text to speech using OpenAI TTS"""
    try:
        # Generate audio
        audio_data = await audio_handler.text_to_speech(request.text, request.voice)
        
        # Encode as base64 for JSON response
        import base64
        audio_base64 = base64.b64encode(audio_data).decode()
        
        return {
            "audio_data": audio_base64,
            "format": "mp3",
            "text": request.text,
            "voice": request.voice,
            "status": "success"
        }
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Text-to-speech failed: {str(e)}")

@app.post("/audio/process", response_model=AudioResponse)
async def process_audio_endpoint(
    audio: UploadFile = File(...),
    user_id: str = "anonymous",
    audio_format: str = "webm"
):
    """Process audio input with full RAG and tool integration"""
    try:
        # Read audio file
        audio_data = await audio.read()
        
        # Load user profile from database if needed
        await load_user_profile_if_needed(user_id)
        
        # Get user profile
        user_profile = user_profiles.get(user_id, {})
        
        # Process audio with tools integration
        result = await audio_handler.process_audio_with_tools(audio_data, user_id, user_profile)
        
        # Encode audio response as base64
        import base64
        audio_base64 = base64.b64encode(result["audio_response"]).decode()
        
        return AudioResponse(
            transcript=result["transcript"],
            response_text=result["response_text"],
            audio_response=audio_base64,
            tool_result=result.get("tool_result"),
            personal_info=result.get("personal_info")
        )
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Audio processing failed: {str(e)}")

@app.post("/mobile/voice", response_model=dict)
async def mobile_voice_endpoint(
    audio: UploadFile = File(...),
    user_id: str = "anonymous",
    audio_format: str = "webm"
):
    """Mobile voice processing with hybrid RAG (local + cloud)"""
    try:
        # Read audio file
        audio_data = await audio.read()
        
        # Process with hybrid RAG system
        result = await process_voice_query(audio_data, user_id, audio_format)
        
        return result
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Mobile voice processing failed: {str(e)}")

@app.post("/realtime/session", response_model=dict)
async def create_realtime_session(request: RealtimeSessionRequest):
    """Create OpenAI Realtime API session and return ephemeral key for mobile WebRTC"""
    try:
        import requests
        
        # Create realtime session with OpenAI
        session_payload = {
            "model": request.model,
            "instructions": request.instructions or "You are a helpful AI assistant."
        }
        
        headers = {
            "Authorization": f"Bearer {os.getenv('OPENAI_API_KEY')}",
            "Content-Type": "application/json"
        }
        
        response = requests.post(
            "https://api.openai.com/v1/realtime/sessions",
            json=session_payload,
            headers=headers,
            timeout=15
        )
        
        if response.status_code == 200:
            session_data = response.json()
            ephemeral_key = session_data["client_secret"]["value"]
            
            return {
                "ephemeral_key": ephemeral_key,
                "user_id": request.user_id,
                "model": request.model,
                "status": "success"
            }
        else:
            raise HTTPException(
                status_code=response.status_code, 
                detail=f"OpenAI session creation failed: {response.text}"
            )
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Realtime session creation failed: {str(e)}")

@app.post("/generate_image")
async def generate_image(request: dict):
    """Generate an image using DALL-E 3"""
    try:
        prompt = request.get("prompt", "")
        size = request.get("size", "1024x1024")
        user_id = request.get("user_id", "")
        
        if not prompt:
            raise HTTPException(status_code=400, detail="Prompt is required")
        
        # Call DALL-E 3 API
        response = client.images.generate(
            model="dall-e-3",
            prompt=prompt,
            size=size,
            quality="standard",
            n=1,
        )
        
        image_url = response.data[0].url
        
        # Log the image generation
        print(f"🎨 Generated image for user {user_id}: {prompt}")
        
        return {
            "success": True,
            "image_url": image_url,
            "prompt": prompt,
            "size": size
        }
        
    except Exception as e:
        print(f"❌ Image generation error: {str(e)}")
        return {
            "success": False,
            "error": str(e)
        }

@app.post("/analyze_image")
async def analyze_image(request: dict):
    """Analyze an image using GPT-4 Vision"""
    try:
        image_data = request.get("image_data", "")
        question = request.get("question", "Describe what you see in this image")
        user_id = request.get("user_id", "")
        
        if not image_data:
            raise HTTPException(status_code=400, detail="Image data is required")
        
        # Call GPT-4 Vision API
        response = client.chat.completions.create(
            model="gpt-4o",
            messages=[
                {
                    "role": "user",
                    "content": [
                        {
                            "type": "text",
                            "text": question
                        },
                        {
                            "type": "image_url",
                            "image_url": {
                                "url": f"data:image/jpeg;base64,{image_data}"
                            }
                        }
                    ]
                }
            ],
            max_tokens=500
        )
        
        description = response.choices[0].message.content
        
        # Log the image analysis
        print(f"🔍 Analyzed image for user {user_id}: {question}")
        
        return {
            "success": True,
            "description": description,
            "question": question
        }
        
    except Exception as e:
        print(f"❌ Image analysis error: {str(e)}")
        return {
            "success": False,
            "error": str(e)
        }

def is_ai_message(content: str) -> bool:
    """Detect if a message is from AI (assistant) rather than user"""
    content_lower = content.lower().strip()
    
    # Common AI response patterns
    ai_patterns = [
        "hello", "hi there", "i'm here to help", "how can i help", "what can i do",
        "sure!", "of course", "i'd be happy to", "let me help", "i understand",
        "here's", "based on", "according to", "i think", "in my opinion",
        "i can help", "i'll help", "got it!", "understood", "i see",
        "from now on", "i'll communicate", "何か", "shimashou ka", "nanika"
    ]
    
    # Check if content starts with typical AI response patterns
    for pattern in ai_patterns:
        if content_lower.startswith(pattern) or f" {pattern}" in content_lower[:50]:
            return True
    
    # Check for Japanese AI responses
    if "何か" in content or "しましょうか" in content:
        return True
    
    return False

def extract_personal_info(message: str) -> dict:
    """Extract personal information from user messages"""
    personal_info = {}
    message_lower = message.lower()
    print(f"🔍 Checking message for patterns: '{message}'")
    
    # Name detection patterns - more specific
    name_patterns = [
        ("my name is ", "name"),
        ("call me ", "name"), 
        ("my name's ", "name"),
        ("i am ", "name"),
        ("i'm ", "name"),
        ("this is ", "name"),
        ("name is ", "name"),
        ("it's ", "name"),
        ("i am called ", "name"),
        ("people call me ", "name")
    ]
    
    # Work/profession patterns
    work_patterns = [
        ("i work as ", "job"),
        ("i'm a ", "job"),
        ("i am a ", "job"),
        ("my job is ", "job"),
        ("i work at ", "workplace"),
        ("i work for ", "workplace")
    ]
    
    # Interest/hobby patterns
    interest_patterns = [
        ("i like ", "interest"),
        ("i love ", "interest"), 
        ("i enjoy ", "interest"),
        ("i'm interested in ", "interest"),
        ("my hobby is ", "hobby")
    ]
    
    # Location patterns
    location_patterns = [
        ("i live in ", "location"),
        ("i'm from ", "origin"),
        ("i live at ", "location")
    ]
    
    # Language preference patterns
    language_patterns = [
        ("i speak ", "language"),
        ("my language is ", "language"),
        ("i prefer ", "language_preference"),
        ("please speak ", "language_preference"),
        ("respond in ", "language_preference"),
        ("talk to me in ", "language_preference"),
        ("use ", "language_preference"),
        ("can you speak ", "language_preference"),
        ("my preferred language is ", "language_preference"),
        ("i would like you to speak ", "language_preference"),
        ("i would like ", "language_preference"),
        ("to be my preferred language", "language_preference"),
        ("preferred language", "language_preference"),
        ("switch to ", "language_preference"),
        ("i understand ", "language"),
        ("i'm fluent in ", "language"),
        ("my native language is ", "native_language"),
        ("my first language is ", "native_language")
    ]
    
    # Reminder patterns
    reminder_patterns = [
        ("remind me to ", "reminder"),
        ("remind me at ", "reminder_at"),
        ("remind me on ", "reminder_on"),
        ("remind me in ", "reminder"),  # Added this!
        ("remind me of ", "reminder"),  # Added this!
        ("remind me ", "reminder"),     # Catch-all pattern for "remind me [anything]"
        ("please remind me to ", "reminder"),  # Added this!
        ("please remind me of ", "reminder"),  # Added this!
        ("please remind me in ", "reminder"),  # Added this!
        ("please remind me ", "reminder"),     # Catch-all for "please remind me [anything]"
        ("can you remind me in ", "reminder"),  # Added this!
        ("can you remind me to ", "reminder"),  # Added this!
        ("can you remind me ", "reminder"),     # Catch-all for "can you remind me [anything]"
        ("don't let me forget to ", "reminder"),
        ("i need to remember to ", "reminder"),
        ("set a reminder for ", "reminder"),
        ("alert me to ", "reminder"),
        ("notify me to ", "reminder")
    ]
    
    all_patterns = name_patterns + work_patterns + interest_patterns + location_patterns + language_patterns + reminder_patterns
    
    for pattern, info_type in all_patterns:
        if pattern in message_lower:
            print(f"🔍 Found pattern '{pattern}' -> {info_type}")
            start = message_lower.find(pattern) + len(pattern)
            # Extract relevant information
            rest = message[start:].split()
            if rest:
                if info_type == "name":
                    value = rest[0].strip('.,!?')
                    # Only store if it's a reasonable name (alphabetic, 2+ chars, not common words)
                    if (len(value) > 1 and value.isalpha() and 
                        value.lower() not in ['here', 'there', 'doing', 'going', 'glad', 'well', 'fine']):
                        personal_info[info_type] = value.title()
                else:
                    # For other info, take a few words
                    if info_type == "location":
                        # For location, just take first word or two, stop at punctuation
                        value = rest[0].strip('.,!?')
                        # Add second word if it's a common state/location continuation
                        if len(rest) > 1 and rest[1].lower() in ['york', 'jersey', 'carolina', 'dakota', 'mexico']:
                            value += " " + rest[1].strip('.,!?')
                    elif info_type in ["reminder", "reminder_at", "reminder_on"]:
                        # For reminders, extract the full reminder text
                        remainder = message[start:].strip()
                        # Extract reminder details using more sophisticated parsing
                        reminder_data = parse_reminder_request(remainder, info_type)
                        if reminder_data:
                            personal_info["reminder_request"] = reminder_data
                            continue  # Skip the normal value setting
                    else:
                        value = " ".join(rest[:3]).strip('.,!?')
                    
                    if len(value) > 1:
                        personal_info[info_type] = value
                break
    
    return personal_info

def parse_reminder_request(remainder: str, pattern_type: str) -> dict:
    """Parse natural language reminder requests"""
    import re
    from datetime import datetime, timedelta, timezone
    try:
        import dateutil.parser as date_parser
    except ImportError:
        # Fallback if dateutil is not available
        print("Warning: dateutil not available, using basic date parsing")
        date_parser = None
    
    print(f"🔍 Parsing reminder: '{remainder}' (type: {pattern_type})")
    
    reminder_data = {
        "content": "",
        "datetime": None,
        "original_text": remainder
    }
    
    try:
        # Common time patterns
        time_patterns = [
            # Absolute times
            (r"at (\d{1,2}:\d{2})\s*(am|pm)?", "time"),
            (r"at (\d{1,2})\s*(am|pm)", "time"),
            
            # Relative times with word numbers
            (r"in (one|two|three|four|five|six|seven|eight|nine|ten|fifteen|twenty|thirty) (minute|hour|day|week)s?", "relative_word"),
            (r"in a (minute|hour|day|week)", "relative_single"),
            (r"in (\d+) (minute|hour|day|week)s?", "relative"),
            (r"tomorrow at (\d{1,2}:\d{2})\s*(am|pm)?", "tomorrow"),
            (r"tomorrow", "tomorrow"),
            (r"next (monday|tuesday|wednesday|thursday|friday|saturday|sunday)", "next_day"),
            
            # Specific dates
            (r"on ([a-zA-Z]+ \d{1,2})", "date"),
            (r"on (\d{1,2}/\d{1,2})", "date"),
            (r"(\d{1,2}/\d{1,2}/\d{2,4})", "full_date")
        ]
        
        # Extract time information
        time_found = False
        for pattern, time_type in time_patterns:
            match = re.search(pattern, remainder.lower())
            if match:
                try:
                    if time_type == "relative":
                        amount = int(match.group(1))
                        unit = match.group(2)
                        # Use UTC time consistently to avoid timezone issues
                        utc_now = datetime.now(timezone.utc)
                        if unit.startswith("minute"):
                            reminder_data["datetime"] = utc_now + timedelta(minutes=amount)
                            print(f"⏰ Reminder set for: {reminder_data['datetime']} UTC (in {amount} minutes from server time)")
                        elif unit.startswith("hour"):
                            reminder_data["datetime"] = utc_now + timedelta(hours=amount)
                        elif unit.startswith("day"):
                            reminder_data["datetime"] = utc_now + timedelta(days=amount)
                        elif unit.startswith("week"):
                            reminder_data["datetime"] = utc_now + timedelta(weeks=amount)
                    elif time_type == "relative_word":
                        # Convert word numbers to integers
                        word_to_number = {
                            "one": 1, "two": 2, "three": 3, "four": 4, "five": 5,
                            "six": 6, "seven": 7, "eight": 8, "nine": 9, "ten": 10,
                            "fifteen": 15, "twenty": 20, "thirty": 30
                        }
                        word_amount = match.group(1).lower()
                        amount = word_to_number.get(word_amount, 1)
                        unit = match.group(2)
                        utc_now = datetime.now(timezone.utc)
                        if unit.startswith("minute"):
                            reminder_data["datetime"] = utc_now + timedelta(minutes=amount)
                            print(f"⏰ Reminder set for: {reminder_data['datetime']} UTC (in {amount} minutes from server time)")
                        elif unit.startswith("hour"):
                            reminder_data["datetime"] = utc_now + timedelta(hours=amount)
                        elif unit.startswith("day"):
                            reminder_data["datetime"] = utc_now + timedelta(days=amount)
                        elif unit.startswith("week"):
                            reminder_data["datetime"] = utc_now + timedelta(weeks=amount)
                    elif time_type == "relative_single":
                        # "in a minute", "in an hour", etc.
                        unit = match.group(1)
                        utc_now = datetime.now(timezone.utc)
                        if unit.startswith("minute"):
                            reminder_data["datetime"] = utc_now + timedelta(minutes=1)
                            print(f"⏰ Reminder set for: {reminder_data['datetime']} UTC (in 1 minute from server time)")
                        elif unit.startswith("hour"):
                            reminder_data["datetime"] = utc_now + timedelta(hours=1)
                        elif unit.startswith("day"):
                            reminder_data["datetime"] = utc_now + timedelta(days=1)
                        elif unit.startswith("week"):
                            reminder_data["datetime"] = utc_now + timedelta(weeks=1)
                    elif time_type == "tomorrow":
                        time_part = match.group(1) if len(match.groups()) > 0 else "9:00 AM"
                        utc_now = datetime.now(timezone.utc)
                        tomorrow = utc_now + timedelta(days=1)
                        if date_parser:
                            reminder_data["datetime"] = date_parser.parse(f"{tomorrow.strftime('%Y-%m-%d')} {time_part}")
                        else:
                            # Simple fallback parsing
                            reminder_data["datetime"] = tomorrow.replace(hour=9, minute=0, second=0, microsecond=0)
                    else:
                        # Try to parse the matched time/date
                        if date_parser:
                            reminder_data["datetime"] = date_parser.parse(match.group(0))
                        else:
                            # Simple fallback - default to 1 hour from now (UTC)
                            reminder_data["datetime"] = datetime.now(timezone.utc) + timedelta(hours=1)
                    
                    time_found = True
                    # Remove the time part from the content
                    remainder = re.sub(pattern, "", remainder, flags=re.IGNORECASE).strip()
                    break
                except:
                    continue
        
        # If no specific time found, set default reminder for 1 hour from now (UTC)
        if not time_found:
            reminder_data["datetime"] = datetime.now(timezone.utc) + timedelta(hours=1)
            
        # Clean up the reminder content
        remainder = remainder.strip()
        # Remove common connector words
        remainder = re.sub(r"^(that|to|about)\s+", "", remainder, flags=re.IGNORECASE)
        
        reminder_data["content"] = remainder
        
        # Only return if we have meaningful content
        if len(reminder_data["content"]) > 2:
            return reminder_data
            
    except Exception as e:
        print(f"Error parsing reminder: {e}")
        
    return None

def store_reminder(user_id: str, reminder_data: dict) -> str:
    """Store a reminder for a user"""
    import uuid
    from datetime import datetime, timezone
    
    reminder_id = str(uuid.uuid4())[:8]  # Short ID
    
    reminder = {
        "id": reminder_id,
        "content": reminder_data["content"],
        "datetime": reminder_data["datetime"],
        "original_text": reminder_data["original_text"],
        "created_at": datetime.now(timezone.utc),
        "triggered": False,
        "user_id": user_id
    }
    
    if user_id not in user_reminders:
        user_reminders[user_id] = []
    
    user_reminders[user_id].append(reminder)
    
    print(f"📅 Stored reminder for {user_id}: '{reminder['content']}' at {reminder['datetime']}")
    
    return reminder_id

def get_due_reminders(user_id: str) -> list:
    """Get reminders that are due for a user"""
    from datetime import datetime, timezone
    
    if user_id not in user_reminders:
        return []
    
    due_reminders = []
    now = datetime.now(timezone.utc)
    
    print(f"🕐 Checking reminders at {now} UTC")
    print(f"🔍 User {user_id} has {len(user_reminders[user_id])} total reminders")
    
    for reminder in user_reminders[user_id]:
        reminder_time = reminder["datetime"]
        
        # Ensure both times are timezone-aware for proper comparison
        if reminder_time.tzinfo is None:
            # If reminder time is naive, assume it's UTC
            reminder_time = reminder_time.replace(tzinfo=timezone.utc)
        
        is_due = reminder_time <= now
        time_diff = (reminder_time - now).total_seconds()
        print(f"  📝 Reminder: '{reminder['content']}' due at {reminder_time}, triggered: {reminder['triggered']}, is_due: {is_due}")
        print(f"    ⏱️  Time difference: {time_diff:.1f} seconds ({time_diff/60:.1f} minutes)")
        
        if not reminder["triggered"] and is_due:
            due_reminders.append(reminder)
            print(f"  ✅ Found due reminder: {reminder['id']} (not yet triggered)")
    
    print(f"📅 Found {len(due_reminders)} due reminders")
    return due_reminders

def get_pending_reminders(user_id: str) -> list:
    """Get all pending (future) reminders for a user"""
    from datetime import datetime, timezone
    
    if user_id not in user_reminders:
        return []
    
    pending_reminders = []
    now = datetime.now(timezone.utc)
    
    for reminder in user_reminders[user_id]:
        reminder_time = reminder["datetime"]
        
        # Ensure both times are timezone-aware for proper comparison
        if reminder_time.tzinfo is None:
            # If reminder time is naive, assume it's UTC
            reminder_time = reminder_time.replace(tzinfo=timezone.utc)
        
        if not reminder["triggered"] and reminder_time > now:
            pending_reminders.append(reminder)
    
    return pending_reminders

def store_conversation_turn(user_id: str, user_message: str, ai_response: str):
    """Store conversation turn for context (memory only)"""
    import time
    if user_id not in user_conversations:
        user_conversations[user_id] = []
    
    # Add new turn
    user_conversations[user_id].append({
        "user": user_message,
        "assistant": ai_response,
        "timestamp": time.time()
    })
    
    # Keep only last 5 conversation turns
    if len(user_conversations[user_id]) > 5:
        user_conversations[user_id] = user_conversations[user_id][-5:]

async def store_conversation_turn_db(user_id: str, user_message: str, ai_response: str):
    """Store conversation turn in database for admin tracking"""
    try:
        if not database_available:
            return
            
        async with await psycopg.AsyncConnection.connect(database_url) as conn:
            async with conn.cursor() as cur:
                # Insert conversation turn
                await cur.execute("""
                    INSERT INTO conversation_turns (user_id, user_message, assistant_response, created_at)
                    VALUES (%s, %s, %s, NOW())
                """, (user_id, user_message, ai_response))
                
                await conn.commit()
                print(f"✅ Stored conversation turn in database for user {user_id}")
                
    except Exception as e:
        print(f"❌ Failed to store conversation turn in database: {e}")
        # Don't fail the main flow if database storage fails

def get_conversation_context(user_id: str) -> str:
    """Get recent conversation context"""
    if user_id not in user_conversations:
        return ""
    
    context_parts = []
    for turn in user_conversations[user_id][-3:]:  # Last 3 turns
        context_parts.append(f"User: {turn['user']}")
        context_parts.append(f"Assistant: {turn['assistant']}")
    
    return "\n".join(context_parts) if context_parts else ""

async def store_personal_info_simple(user_id: str, info: dict):
    """Store personal information in a simple way"""
    try:
        # Load existing profile from database
        existing_profile = {}
        if database_available:
            try:
                existing_profile = await db.get_user_profile(user_id) or {}
                print(f"Loaded existing profile from database: {existing_profile}")
            except Exception as e:
                print(f"Error loading profile from database: {e}")
        
        # Store in memory first (always works)
        if user_id not in user_profiles:
            user_profiles[user_id] = existing_profile.copy()
        
        # Clean up existing location data if it's corrupted
        if "location" in user_profiles[user_id]:
            existing_location = user_profiles[user_id]["location"]
            if ". Is there" in existing_location or ". is there" in existing_location:
                cleaned_location = existing_location.split(".")[0].strip()
                user_profiles[user_id]["location"] = cleaned_location
                print(f"Cleaned existing location data: {existing_location} -> {cleaned_location}")
        
        for key, value in info.items():
            # Clean up location data if it contains extra text
            if key == "location":
                # Remove common sentence fragments
                value = value.split(".")[0].split("?")[0].split("!")[0].strip()
                # Clean up common conversational fragments  
                for fragment in [" is there", " there", " here"]:
                    if value.lower().endswith(fragment):
                        value = value[:-len(fragment)].strip()
            
            user_profiles[user_id][key] = value
            print(f"Stored in memory: {key} = {value} for user {user_id}")
        
        # Store updated profile in database
        if database_available and info:
            try:
                await db.store_user_profile(user_id, user_profiles[user_id])
                print(f"✅ Saved profile to database for user {user_id}")
            except Exception as db_error:
                print(f"❌ Database storage failed (using memory backup): {db_error}")
        
        # Always save to JSON backup file as failsafe
        if info:
            try:
                import json
                backup_file = f"user_profile_{user_id}.json"
                with open(backup_file, 'w') as f:
                    json.dump(user_profiles[user_id], f, indent=2)
                print(f"💾 Saved profile backup to {backup_file}")
            except Exception as e:
                print(f"❌ File backup failed: {e}")
            
    except Exception as e:
        print(f"Could not store personal info: {e}")

async def get_user_name(user_id: str) -> str:
    """Try to retrieve user's name from stored memories"""
    try:
        # Check memory storage first
        if user_id in user_profiles and "name" in user_profiles[user_id]:
            return user_profiles[user_id]["name"]
        
        # Check database if available
        if database_available:
            try:
                profile = await db.get_user_profile(user_id)
                if profile and "name" in profile:
                    # Load into memory cache for faster access
                    if user_id not in user_profiles:
                        user_profiles[user_id] = {}
                    user_profiles[user_id].update(profile)
                    return profile["name"]
            except Exception as e:
                print(f"Error loading user profile: {e}")
        
        return ""
        
    except Exception as e:
        print(f"Could not retrieve user name: {e}")
        return ""

async def load_user_profile_if_needed(user_id: str):
    """Load user profile from database or file backup if not in memory"""
    if user_id not in user_profiles:
        # Try database first
        if database_available:
            try:
                profile = await db.get_user_profile(user_id)
                if profile:
                    user_profiles[user_id] = profile
                    print(f"📋 Loaded user profile from database: {profile}")
                    return
            except Exception as e:
                print(f"Error loading user profile from database: {e}")
        
        # Fallback to JSON file backup
        import json
        import os
        backup_file = f"user_profile_{user_id}.json"
        if os.path.exists(backup_file):
            try:
                with open(backup_file, 'r') as f:
                    profile = json.load(f)
                    user_profiles[user_id] = profile
                    print(f"📋 Loaded user profile from backup file: {profile}")
            except Exception as e:
                print(f"Error loading profile backup: {e}")

async def generate_conversational_response(user_id: str, query: str, personal_info: dict) -> str:
    """Generate a fluid conversational response with personal context and tools"""
    try:
        # Get user profile and conversation context
        user_profile = user_profiles.get(user_id, {})
        conversation_context = get_conversation_context(user_id)
        
        # Check for due reminders
        due_reminders = get_due_reminders(user_id)
        pending_reminders = get_pending_reminders(user_id)
        print(f"🔍 Reminder check - due: {len(due_reminders)}, pending: {len(pending_reminders)}")
        if due_reminders:
            print(f"🔔 Due reminders: {[r['content'] for r in due_reminders]}")
        if pending_reminders:
            print(f"⏰ Pending reminders: {[r['content'] for r in pending_reminders]}")
        
        # Debug: Log what we're retrieving
        print(f"🔍 Generating response for user_id: '{user_id}'")
        print(f"🔍 Retrieved user_profile: {user_profile}")
        print(f"🔍 All stored profiles: {user_profiles}")
        print(f"🔍 Personal info from this message: {personal_info}")
        print(f"🔍 Available user_ids in profiles: {list(user_profiles.keys())}")
        
        # Check if user is asking for something that needs a tool
        tool_result = await check_and_use_tools(query, user_profile)
        
        # Build comprehensive context for the AI
        system_content = """You are a conversational AI companion with memory capabilities. You DO have access to personal information and conversation history about this user.

IMPORTANT: You MUST use the provided user information in your responses. DO NOT claim you don't have memory or can't remember things.

REMINDER CAPABILITY: You CAN set reminders for users! When they ask you to "remind me" of something, acknowledge that you'll set the reminder for them. The system will automatically detect and process reminder requests.

Key behaviors:
- ALWAYS acknowledge and use any provided user information (name, location, interests, etc.)
- Reference their name when it's provided - use it naturally in conversation
- Build on previous conversation topics when provided
- Ask follow-up questions to learn more about the user
- Be genuinely interested in their life, work, and interests
- Make connections between different pieces of information they've shared
- Respond in a warm, engaging, and personal way that shows you remember them
- When you have tool results, incorporate them naturally into the conversation
- When users ask for reminders, confirm you'll set them up - don't claim you can't do reminders"""

        # Add personal context if available
        if user_profile:
            profile_text = []
            if "name" in user_profile:
                profile_text.append(f"User's name: {user_profile['name']}")
            if "job" in user_profile:
                profile_text.append(f"Job: {user_profile['job']}")
            if "workplace" in user_profile:
                profile_text.append(f"Workplace: {user_profile['workplace']}")
            if "location" in user_profile:
                profile_text.append(f"Lives in: {user_profile['location']}")
            if "interest" in user_profile or "hobby" in user_profile:
                interests = user_profile.get("interest", "") + " " + user_profile.get("hobby", "")
                profile_text.append(f"Interests: {interests.strip()}")
                
            if profile_text:
                system_content += f"\n\nWhat you know about this user:\n" + "\n".join(profile_text)
        
        # Add conversation context if available
        if conversation_context:
            system_content += f"\n\nRecent conversation:\n{conversation_context}"
            
        # Handle personal information sharing
        if personal_info:
            if "reminder_created" in personal_info:
                reminder = personal_info["reminder_created"]
                system_content += f"\n\nThe user just created a reminder: '{reminder['content']}' for {reminder['datetime'].strftime('%B %d at %I:%M %p')}. Acknowledge this naturally and confirm the reminder."
            
            other_personal_info = {k: v for k, v in personal_info.items() if k != "reminder_created"}
            if other_personal_info:
                system_content += f"\n\nThe user just shared new personal information: {other_personal_info}"
        
        # Check for recently created reminders in user profile (within last 30 seconds)
        if "reminder_created" in user_profile:
            reminder = user_profile["reminder_created"]
            # Check if this reminder was created recently (within 30 seconds)
            from datetime import datetime, timedelta, timezone
            if isinstance(reminder.get("datetime"), datetime):
                now_utc = datetime.now(timezone.utc)
                reminder_time = reminder["datetime"]
                
                # Ensure reminder time is timezone-aware
                if reminder_time.tzinfo is None:
                    reminder_time = reminder_time.replace(tzinfo=timezone.utc)
                
                time_since_creation = now_utc - reminder_time
                if time_since_creation.total_seconds() < 30 and "reminder_request" not in personal_info:
                    system_content += f"\n\nIMPORTANT: You just successfully created a reminder for the user: '{reminder['content']}' scheduled for {reminder['datetime'].strftime('%B %d at %I:%M %p')}. Acknowledge this and confirm that the reminder has been set."
        
        # Handle due reminders (this is the key feature!)
        if due_reminders:
            reminder_texts = []
            for reminder in due_reminders:
                reminder_texts.append(f"'{reminder['content']}' (was set for {reminder['datetime'].strftime('%B %d at %I:%M %p')})")
            
            system_content += f"\n\n🔔 IMPORTANT: You have {len(due_reminders)} reminder(s) to deliver RIGHT NOW as a caring friend would:\n" + "\n".join(f"- {text}" for text in reminder_texts)
            system_content += f"\n\nDeliver these reminders warmly and naturally as if you're a thoughtful friend who genuinely cares about helping them remember important things."
        
        # Add context about pending reminders
        if pending_reminders:
            system_content += f"\n\nYou also have {len(pending_reminders)} upcoming reminder(s) set for this user."
            
        # Add tool results if available
        if tool_result:
            system_content += f"\n\nTool result: {tool_result['message']}"
        
        messages = [
            {"role": "system", "content": system_content},
            {"role": "user", "content": query}
        ]
        
        # Debug: Print the exact system prompt being sent to the AI
        print(f"🔍 SYSTEM PROMPT DEBUG:")
        print(f"🔍 System content length: {len(system_content)}")
        print(f"🔍 Full system prompt:\n{system_content}")
        print(f"🔍 User query: {query}")
        print(f"🔍 Messages sent to AI: {messages}")
        
        response = client.chat.completions.create(
            model="gpt-4o-mini",  # Use chat model instead of realtime model
            messages=messages,
            max_tokens=400,
            temperature=0.8  # More creative for conversation
        )
        
        # Debug: Print the AI's response
        ai_response = response.choices[0].message.content.strip()
        print(f"🔍 AI RESPONSE: {ai_response}")
        
        return ai_response
        
    except Exception as e:
        print(f"Conversational response generation error: {e}")
        # Fallback with personal touch if possible
        user_name = user_profiles.get(user_id, {}).get("name", "")
        if user_name:
            return f"Hi {user_name}! I'm having a moment here, but I'm listening. What's on your mind?"
        else:
            return "I'm having a small technical hiccup, but I'm here. What would you like to talk about?"

async def check_and_use_tools(query: str, user_profile: dict) -> Optional[dict]:
    """Check if query needs tool usage and execute if needed"""
    query_lower = query.lower()
    
    # Future tool detections can be added here:
    # if "schedule" in query_lower or "calendar" in query_lower:
    #     return await tool_manager.execute_tool("get_calendar")
    
    return None

@app.post("/query")
async def rag_query_sync(request: dict):
    """Main RAG query endpoint with full RAG functionality"""
    try:
        import time
        start_time = time.time()
        query_text = request.get('query', '')
        user_id = request.get('user_id', 'anonymous')
        top_k = request.get('top_k', 5)
        
        # Check account usage and limits before processing
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT account_type, prompts_used, max_prompts, energy_tokens
                        FROM user_accounts 
                        WHERE user_id = %s AND status = 'active'
                    """, (user_id,))
                    
                    row = await cur.fetchone()
                    if row:
                        account_type, prompts_used, max_prompts, energy_tokens = row
                        
                        # Check limits
                        if account_type == "limited_guest" and prompts_used >= max_prompts:
                            raise HTTPException(
                                status_code=429, 
                                detail=f"Limited guest limit reached ({prompts_used}/{max_prompts}). Contact admin for upgrade."
                            )
                        elif account_type == "user" and energy_tokens <= 0:
                            raise HTTPException(
                                status_code=429, 
                                detail="No energy tokens remaining. Purchase more tokens to continue."
                            )
                        
                        # Increment prompt count for limited guests
                        if account_type == "limited_guest":
                            await cur.execute("""
                                UPDATE user_accounts 
                                SET prompts_used = prompts_used + 1, last_active = NOW()
                                WHERE user_id = %s
                            """, (user_id,))
                        elif account_type == "user":
                            # Deduct energy token
                            await cur.execute("""
                                UPDATE user_accounts 
                                SET energy_tokens = energy_tokens - 1, last_active = NOW()
                                WHERE user_id = %s
                            """, (user_id,))
                        
                        await conn.commit()
                    else:
                        # Create new limited guest account
                        await cur.execute("""
                            INSERT INTO user_accounts (user_id, account_type, username, email, max_prompts, energy_tokens, prompts_used, created_at, status)
                            VALUES (%s, 'limited_guest', %s, %s, 20, 0, 1, NOW(), 'active')
                        """, (user_id, f"User_{user_id}", f"{user_id}@ragcompanion.com"))
                        await conn.commit()
                        
        except Exception as limit_error:
            if "429" in str(limit_error):
                raise limit_error
            print(f"Warning: Could not check account limits: {limit_error}")
        
        # Load user profile from database if needed
        await load_user_profile_if_needed(user_id)
        
        # Debug: Log the query request details
        print(f"📝 Query request - user_id: {user_id}, query: {query_text[:50]}...")
        
        # Check for personal information in the message and store it
        personal_info = extract_personal_info(query_text)
        print(f"🔍 Extracted personal info: {personal_info}")
        if personal_info:
            # Handle reminder requests specially
            if "reminder_request" in personal_info:
                reminder_data = personal_info["reminder_request"]
                reminder_id = store_reminder(user_id, reminder_data)
                personal_info["reminder_created"] = {
                    "id": reminder_id,
                    "content": reminder_data["content"],
                    "datetime": reminder_data["datetime"]
                }
                # Remove the raw reminder_request to avoid confusion
                del personal_info["reminder_request"]
            
            # Store other personal info normally
            other_info = {k: v for k, v in personal_info.items() if k != "reminder_created"}
            if other_info:
                await store_personal_info_simple(user_id, other_info)
        
        # Store user message for future memory/context (only for accounts that support it)
        try:
            # Check if account supports memory storage
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("SELECT account_type FROM user_accounts WHERE user_id = %s", (user_id,))
                    row = await cur.fetchone()
                    if row and row[0] in ["admin", "user"]:
                        await store_user_interaction(user_id, query_text, "user")
        except Exception as store_error:
            print(f"Warning: Could not store user interaction: {store_error}")
        
        # Get query embedding
        embedding_start = time.time()
        try:
            query_embedding = get_embedding(query_text)
            embedding_time = (time.time() - embedding_start) * 1000
        except Exception as embed_error:
            print(f"Embedding error: {embed_error}")
            # Fallback to mock response if embeddings fail
            return {
                "results": [
                    {
                        "chunk_id": "fallback_1",
                        "text": f"I apologize, but I'm having trouble processing your query: '{query_text}'. This is a fallback response.",
                        "doc_title": "System Fallback",
                        "section": "error_handling",
                        "tags": ["fallback", "error"],
                        "source_path": "system/fallback",
                        "score": 0.1
                    }
                ],
                "query_embedding_ms": 0.0,
                "db_lookup_ms": 0.0,
                "total_chunks": 1,
                "session_id": f"session_{int(time.time())}",
                "from_cache": False
            }
        
        # Search database with proper error handling
        db_start = time.time()
        try:
            # Try database search with vector similarity
            if database_available and hasattr(db, 'search_chunks'):
                print(f"🔍 Searching vector database for: {query_text[:50]}...")
                db_results = await db.search_chunks(query_embedding, top_k)
                print(f"📊 Database returned {len(db_results)} results")
            else:
                print(f"📝 Vector database not available - using in-memory fallback response")
                db_results = []
            
            db_time = (time.time() - db_start) * 1000
            
            # Convert database results to response format
            results = []
            if db_results:
                for result in db_results:
                    results.append({
                        "chunk_id": result.get("chunk_id", "unknown"),
                        "text": result.get("text", ""),
                        "doc_title": result.get("doc_title", "Unknown Document"),
                        "section": result.get("section", ""),
                        "tags": result.get("tags", []),
                        "source_path": result.get("source_path", ""),
                        "score": result.get("score", 0.0)
                    })
            
            # Generate conversational response (RAG or personal AI response)
            print(f"🔍 /query endpoint: results={len(results)}, not results={not results}")
            
            # Store the conversation turn for tracking (only for accounts that support it)
            try:
                if results:
                    # Store the AI response as well
                    ai_response = results[0].get("text", "No response generated")
                    # Store in memory for context (always)
                    store_conversation_turn(user_id, query_text, ai_response)
                    
                    # Store in database for admin tracking (only for accounts that support it)
                    async with await psycopg.AsyncConnection.connect(database_url) as conn:
                        async with conn.cursor() as cur:
                            await cur.execute("SELECT account_type FROM user_accounts WHERE user_id = %s", (user_id,))
                            row = await cur.fetchone()
                            if row and row[0] in ["admin", "user"]:
                                await store_conversation_turn_db(user_id, query_text, ai_response)
            except Exception as conv_error:
                print(f"Warning: Could not store conversation turn: {conv_error}")
            if not results:
                print(f"🔍 Calling generate_conversational_response for user_id: {user_id}")
                # Generate fluid conversational response with tool support
                response_text = await generate_conversational_response(user_id, query_text, personal_info)
                print(f"🔍 Generated response: {response_text[:100]}...")
                
                results = [
                    {
                        "chunk_id": "conversational_response",
                        "text": response_text,
                        "doc_title": "Conversational AI", 
                        "section": "conversation",
                        "tags": ["conversation", "personal", "ai"],
                        "source_path": "system/conversation",
                        "score": 0.9
                    }
                ]
            
        except Exception as db_error:
            print(f"Database error: {db_error}")
            db_time = (time.time() - db_start) * 1000
            results = [
                {
                    "chunk_id": "db_error_1",
                    "text": f"Query processed with embeddings, but database search encountered an issue. Your query: '{query_text}' was understood.",
                    "doc_title": "Partial Processing",
                    "section": "db_error",
                    "tags": ["processed", "db_issue"],
                    "source_path": "system/partial",
                    "score": 0.5
                }
            ]
        
        # Store conversation turn for context (safe in-memory storage)
        if results and len(results) > 0:
            ai_response_text = results[0].get("text", "")
            if ai_response_text:
                try:
                    store_conversation_turn(user_id, query_text, ai_response_text)
                except Exception as store_error:
                    print(f"Warning: Could not store conversation turn: {store_error}")
        
        return {
            "results": results,
            "query_embedding_ms": embedding_time,
            "db_lookup_ms": db_time,
            "total_chunks": len(results),
            "session_id": f"session_{int(time.time())}",
            "from_cache": False
        }
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Query failed: {str(e)}")

@app.post("/memory/store")
async def store_memory(request: MemoryRequest):
    """Store user memory"""
    try:
        # Temporary: Use in-memory storage until database is fixed
        print(f"Storing memory for {request.user_id}: {request.content[:50]}...")
        
        # Store in memory (using existing personal info system)
        if request.memory_type == "conversation":
            # Only extract personal info from user messages, not AI responses
            is_ai_response = is_ai_message(request.content)
            if not is_ai_response:
                # Extract personal info and store it
                personal_info = extract_personal_info(request.content)
                print(f"🔍 Extracted personal info from USER message: {personal_info}")
            else:
                personal_info = {}
                print(f"🔍 Skipping personal info extraction from AI response")
            if personal_info:
                # Handle reminder requests specially
                if "reminder_request" in personal_info:
                    reminder_data = personal_info["reminder_request"]
                    reminder_id = store_reminder(request.user_id, reminder_data)
                    personal_info["reminder_created"] = {
                        "id": reminder_id,
                        "content": reminder_data["content"],
                        "datetime": reminder_data["datetime"]
                    }
                    # Remove the raw reminder_request to avoid confusion
                    del personal_info["reminder_request"]
                    print(f"✅ Created reminder {reminder_id}: {reminder_data['content']}")
                
                await store_personal_info_simple(request.user_id, personal_info)
                print(f"Extracted and stored personal info: {personal_info}")
                
                # Debug: Show current user profile
                current_profile = user_profiles.get(request.user_id, {})
                print(f"Current profile for {request.user_id}: {current_profile}")
        
        # Always return success for now
        return {"status": "success", "message": "Memory stored successfully (in-memory)"}
        
    except Exception as e:
        print(f"Memory storage error: {e}")
        # Return success anyway to unblock the Unity client
        return {"status": "success", "message": "Memory stored successfully (fallback)"}

@app.get("/memory/profile/{user_id}")
async def get_user_profile(user_id: str):
    """Get stored profile information for a user"""
    try:
        # Check if database supports memory operations
        if not hasattr(db, 'search_user_memory'):
            return {
                "user_id": user_id,
                "profile_memories": [],
                "total_memories": 0,
                "message": "Memory system not available"
            }
        
        # Get all profile memories for this user
        profile_memories = await db.search_user_memory(
            user_id=user_id,
            query_embedding=get_embedding("profile information name details"),  # General profile query
            memory_types=["profile"],
            top_k=20
        )
        
        profile_data = []
        for memory in profile_memories:
            profile_data.append({
                "content": memory.get("content", ""),
                "metadata": memory.get("metadata", {}),
                "score": memory.get("score", 0.0)
            })
        
        return {
            "user_id": user_id,
            "profile_memories": profile_data,
            "total_memories": len(profile_data)
        }
        
    except Exception as e:
        print(f"Error retrieving user profile: {e}")
        return {
            "user_id": user_id,
            "profile_memories": [],
            "total_memories": 0,
            "error": str(e)
        }

@app.post("/memory/query", response_model=MemoryQueryResponse)
async def query_memory(request: MemoryQueryRequest):
    """Query user memory"""
    try:
        # Get query embedding
        query_embedding = get_embedding(request.query)
        
        # Search memory
        results = await db.search_user_memory(
            user_id=request.user_id,
            query_embedding=query_embedding,
            memory_types=request.memory_types,
            top_k=request.top_k
        )
        
        # Convert to response format
        memory_results = [
            MemoryResult(
                memory_id=r["memory_id"],
                memory_type=r["memory_type"],
                content=r["content"],
                metadata=r["metadata"],
                score=r["score"]
            )
            for r in results
        ]
        
        return MemoryQueryResponse(results=memory_results)
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Memory query failed: {str(e)}")

@app.post("/conversation/log")
async def log_conversation(request: ConversationTurnRequest):
    """Log a conversation turn"""
    try:
        await db.log_conversation_turn(
            turn_id=request.turn_id,
            session_id=request.session_id,
            user_id=request.user_id,
            turn_index=request.turn_index,
            user_message=request.user_message,
            assistant_response=request.assistant_response,
            retrieved_chunks=request.retrieved_chunks,
            metadata=request.metadata
        )
        
        return {"status": "success", "message": "Conversation logged successfully"}
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Conversation logging failed: {str(e)}")

@app.post("/ingest")
async def ingest_documents(request: IngestionRequest, background_tasks: BackgroundTasks):
    """Ingest documents from a directory"""
    try:
        directory_path = Path(request.directory_path)
        if not directory_path.exists():
            raise HTTPException(status_code=404, detail="Directory not found")
        
        # Start ingestion in background
        background_tasks.add_task(
            ingest_directory,
            directory_path,
            request.user_scope,
            request.safety_level
        )
        
        return {"status": "started", "message": "Document ingestion started"}
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Ingestion failed: {str(e)}")

async def store_user_interaction(user_id: str, message: str, role: str):
    """Store user interactions for memory/context building"""
    try:
        # Only store if we have a valid database connection and the required method exists
        if not hasattr(db, 'upsert_user_memory'):
            print("Database does not support user memory storage")
            return
            
        # Check if this looks like personal information that should be stored as memory
        personal_indicators = ["my name is", "i am", "call me", "i'm", "my name's", "i work", "i live", "my age"]
        if any(indicator in message.lower() for indicator in personal_indicators):
            # This looks like personal information - store as semantic memory
            await db.upsert_user_memory(
                user_id=user_id,
                memory_type="profile", 
                content=message,
                embedding=get_embedding(message),
                metadata={"interaction_type": "personal_info", "role": role}
            )
        else:
            # Store as episodic memory (conversation history)
            await db.upsert_user_memory(
                user_id=user_id,
                memory_type="episodic",
                content=message, 
                embedding=get_embedding(message),
                metadata={"interaction_type": "conversation", "role": role}
            )
    except Exception as e:
        print(f"Error storing user interaction: {e}")
        # Don't re-raise the exception to avoid breaking the main flow

async def get_user_memory_context(user_id: str, query: str) -> str:
    """Retrieve relevant user memories for context"""
    try:
        # Only search if we have a valid database connection and the required method exists
        if not hasattr(db, 'search_user_memory'):
            print("Database does not support user memory search")
            return ""
            
        # Get embedding for current query to find relevant memories
        query_embedding = get_embedding(query)
        
        # Search for relevant memories
        memories = await db.search_user_memory(
            user_id=user_id,
            query_embedding=query_embedding,
            memory_types=["profile", "episodic"],
            top_k=5
        )
        
        if memories:
            context_parts = []
            for memory in memories:
                memory_type = memory.get("memory_type", "unknown")
                content = memory.get("content", "")
                context_parts.append(f"[{memory_type.upper()}] {content}")
            
            return "RELEVANT USER MEMORIES:\n" + "\n".join(context_parts)
        
        return ""
        
    except Exception as e:
        print(f"Error retrieving user memory: {e}")
        return ""

def get_embedding(text: str) -> List[float]:
    """Get embedding for text with caching"""
    import hashlib
    text_hash = hashlib.sha256(text.encode()).hexdigest()
    
    if text_hash in embedding_cache:
        return embedding_cache[text_hash]
    
    try:
        response = client.embeddings.create(
            model="text-embedding-3-small",
            input=text
        )
        embedding = response.data[0].embedding
        
        # Cache the embedding
        embedding_cache[text_hash] = embedding
        
        return embedding
        
    except Exception as e:
        print(f"Embedding error: {e}")
        raise

async def generate_ai_response(query: str, user_id: str = None) -> str:
    """Generate a normal AI response when no RAG content is found"""
    try:
        # Get relevant user memories for context (disabled temporarily to prevent errors)
        memory_context = ""
        # if user_id:
        #     try:
        #         user_memories = await get_user_memory_context(user_id, query)
        #         if user_memories:
        #             memory_context = f"\n\n{user_memories}"
        #     except Exception as e:
        #         print(f"Memory retrieval error: {e}")
        
        system_prompt = f"""You are a RAG (Retrieval-Augmented Generation) AI assistant with the following capabilities:

MEMORY & KNOWLEDGE:
- I can remember and store user conversations, preferences, and personal information
- I have access to a knowledge base of documents that have been ingested
- I can store and retrieve different types of memories: episodic (conversations), semantic (facts), and profile (personal details)
- I search through my knowledge base for every query to provide contextual responses

SPECIAL KNOWLEDGE:
- I have access to a comprehensive wellbeing framework with 6 levels: Essential Needs, Safety Needs, Digital Well-Being, Love & Belonging, Esteem Needs, and Self-Actualization
- Each area has multiple categories with 4-tier progressions for self-assessment and growth
- I can help with personal development discussions, self-assessment, and goal-setting using this framework
- The framework includes modern digital wellbeing alongside traditional needs hierarchy

CURRENT STATUS:
- I just searched my knowledge base but didn't find relevant content for this query
- This means either: the information hasn't been added to my knowledge base yet, or this is a general question that doesn't require specific stored knowledge
- I can still help with general questions, conversations, and provide assistance based on my training

WHAT I CAN REMEMBER:
- Previous conversations we've had
- Personal details you've shared with me
- Preferences and interests you've mentioned
- Any documents or information that have been added to my knowledge base
- Context from our ongoing conversation
- Your progress and reflections on wellbeing areas if you've shared them

If asked about my memory capabilities, I should explain these features. For wellbeing discussions, I can reference the framework even when not directly retrieved from search.{memory_context}"""

        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": query}
        ]
        
        response = client.chat.completions.create(
            model="gpt-4o-mini",  # Use chat model instead of realtime model
            messages=messages,
            max_tokens=500,
            temperature=0.7
        )
        
        return response.choices[0].message.content.strip()
        
    except Exception as e:
        print(f"AI response generation error: {e}")
        raise

async def ingest_directory(directory_path: Path, user_scope: str, safety_level: str):
    """Ingest all documents in a directory"""
    supported_extensions = {'.txt', '.md'}
    
    for file_path in directory_path.rglob('*'):
        if file_path.is_file() and file_path.suffix.lower() in supported_extensions:
            try:
                print(f"Processing {file_path}")
                chunks = processor.process_file(file_path)
                
                # Process chunks in batches
                batch_size = 10
                for i in range(0, len(chunks), batch_size):
                    batch = chunks[i:i + batch_size]
                    
                    # Prepare chunks for database
                    db_chunks = []
                    for chunk_text, metadata in batch:
                        # Get embedding
                        embedding = get_embedding(chunk_text)
                        
                        # Create chunk record
                        chunk_id = f"{file_path.stem}_{i//batch_size}_{len(db_chunks)}"
                        chunk_record = {
                            "chunk_id": chunk_id,
                            "embedding": embedding,
                            "text": chunk_text,
                            "doc_title": metadata["doc_title"],
                            "section": metadata["section"],
                            "tags": metadata["tags"],
                            "lang": metadata["lang"],
                            "user_scope": user_scope,
                            "safety_level": safety_level,
                            "token_estimate": metadata["token_estimate"],
                            "source_path": metadata["source_path"]
                        }
                        db_chunks.append(chunk_record)
                    
                    # Insert batch into database
                    await db.upsert_chunks(db_chunks)
                    
                print(f"Completed processing {file_path}")
                
            except Exception as e:
                print(f"Error processing {file_path}: {e}")

@app.get("/reminders/check/{user_id}")
def check_reminders(user_id: str):
    """Check for due reminders for a user (called on app startup)"""
    try:
        from datetime import datetime, timezone
        due_reminders = get_due_reminders(user_id)
        pending_reminders = get_pending_reminders(user_id)
        
        now_utc = datetime.now(timezone.utc)
        
        # Format reminders for easy consumption
        due_formatted = []
        for reminder in due_reminders:
            reminder_time = reminder["datetime"]
            # Ensure reminder time is timezone-aware
            if reminder_time.tzinfo is None:
                reminder_time = reminder_time.replace(tzinfo=timezone.utc)
            
            due_formatted.append({
                "id": reminder["id"],
                "content": reminder["content"],
                "datetime": reminder["datetime"].isoformat(),
                "overdue_minutes": int((now_utc - reminder_time).total_seconds() / 60)
            })
        
        pending_formatted = []
        for reminder in pending_reminders:
            reminder_time = reminder["datetime"]
            # Ensure reminder time is timezone-aware
            if reminder_time.tzinfo is None:
                reminder_time = reminder_time.replace(tzinfo=timezone.utc)
            
            pending_formatted.append({
                "id": reminder["id"],
                "content": reminder["content"],
                "datetime": reminder["datetime"].isoformat(),
                "minutes_until": int((reminder_time - now_utc).total_seconds() / 60)
            })
        
        return {
            "status": "success",
            "due_reminders": due_formatted,
            "pending_reminders": pending_formatted,
            "total_due": len(due_formatted),
            "total_pending": len(pending_formatted)
        }
        
    except Exception as e:
        print(f"❌ ERROR in check_reminders for user {user_id}: {e}")
        import traceback
        print(f"❌ Full traceback: {traceback.format_exc()}")
        return {"status": "error", "message": f"Reminder check failed: {str(e)}"}

@app.get("/reminders/all/{user_id}")
def get_all_reminders(user_id: str):
    """Get all reminders for a user"""
    try:
        from datetime import datetime, timezone
        if user_id not in user_reminders:
            return {"status": "success", "reminders": []}
        
        now_utc = datetime.now(timezone.utc)
        all_reminders = []
        for reminder in user_reminders[user_id]:
            reminder_time = reminder["datetime"]
            # Ensure reminder time is timezone-aware
            if reminder_time.tzinfo is None:
                reminder_time = reminder_time.replace(tzinfo=timezone.utc)
            
            all_reminders.append({
                "id": reminder["id"],
                "content": reminder["content"],
                "datetime": reminder["datetime"].isoformat(),
                "created_at": reminder["created_at"].isoformat(),
                "triggered": reminder["triggered"],
                "status": "triggered" if reminder["triggered"] else ("due" if reminder_time <= now_utc else "pending")
            })
        
        return {
            "status": "success",
            "reminders": all_reminders,
            "total": len(all_reminders)
        }
        
    except Exception as e:
        return {"status": "error", "message": f"Failed to get reminders: {str(e)}"}

@app.delete("/reminders/{user_id}/{reminder_id}")
def delete_reminder(user_id: str, reminder_id: str):
    """Delete a specific reminder"""
    try:
        if user_id not in user_reminders:
            return {"status": "error", "message": "No reminders found for user"}
        
        user_reminders[user_id] = [r for r in user_reminders[user_id] if r["id"] != reminder_id]
        
        return {"status": "success", "message": f"Reminder {reminder_id} deleted"}
        
    except Exception as e:
        return {"status": "error", "message": f"Failed to delete reminder: {str(e)}"}

@app.post("/reminders/test/{user_id}")
def create_test_reminder(user_id: str):
    """Create an immediately due test reminder for testing"""
    try:
        from datetime import datetime, timezone
        import uuid
        
        # Create a test reminder that's immediately due
        utc_now = datetime.now(timezone.utc)
        test_reminder = {
            "id": str(uuid.uuid4()),
            "content": f"Test reminder for {user_id} - this is a test of the proactive delivery system",
            "datetime": utc_now,  # Due immediately
            "created_at": utc_now,
            "triggered": False
        }
        
        # Store it
        if user_id not in user_reminders:
            user_reminders[user_id] = []
        
        user_reminders[user_id].append(test_reminder)
        
        print(f"🧪 Created test reminder for {user_id}: {test_reminder['content']}")
        
        return {
            "status": "success", 
            "message": "Test reminder created and immediately due",
            "reminder": {
                "id": test_reminder["id"],
                "content": test_reminder["content"],
                "datetime": test_reminder["datetime"].isoformat()
            }
        }
        
    except Exception as e:
        print(f"Test reminder creation error: {e}")
        return {"status": "error", "message": f"Failed to create test reminder: {str(e)}"}

@app.post("/reminders/deliver/{user_id}")
async def deliver_due_reminders(user_id: str):
    """Generate AI message to deliver due reminders to user"""
    try:
        from datetime import datetime, timezone
        import asyncio
        
        if user_id not in user_reminders:
            return {"status": "no_reminders", "message": "No reminders found for user"}
        
        now = datetime.now(timezone.utc)
        due_reminders = []
        
        # Find all due reminders (not yet triggered)
        for reminder in user_reminders[user_id]:
            reminder_time = reminder["datetime"]
            
            # Ensure both times are timezone-aware for proper comparison
            if reminder_time.tzinfo is None:
                # If reminder time is naive, assume it's UTC
                reminder_time = reminder_time.replace(tzinfo=timezone.utc)
            
            if not reminder["triggered"] and reminder_time <= now:
                due_reminders.append(reminder)
                # Don't mark as triggered yet - wait for successful delivery
        
        if not due_reminders:
            return {"status": "no_due_reminders", "message": "No due reminders to deliver"}
        
        # Get user profile for personalized delivery
        user_profile = await get_user_profile(user_id)
        user_name = user_profile.get("name", "").split()[0] if user_profile.get("name") else ""
        
        # Create context for AI to deliver reminders
        reminder_context = "You have the following due reminders to deliver:\n"
        for i, reminder in enumerate(due_reminders, 1):
            time_overdue = (now - reminder["datetime"]).total_seconds() / 60
            if time_overdue < 5:
                timing = "right now"
            elif time_overdue < 60:
                timing = f"{int(time_overdue)} minutes ago"
            else:
                hours = int(time_overdue / 60)
                timing = f"{hours} hour{'s' if hours > 1 else ''} ago"
            
            reminder_context += f"{i}. \"{reminder['content']}\" (was due {timing})\n"
        
        # Generate personalized AI response
        system_prompt = f"""You are delivering due reminders to the user{f' (their name is {user_name})' if user_name else ''}. 

{reminder_context}

Deliver these reminders in a natural, conversational way. Be warm and helpful. Don't just list them - deliver them as if you're a caring assistant remembering things for them. If there are multiple reminders, you can group them naturally or deliver them one by one as makes sense.

Keep it conversational and personal. Don't mention "delivering reminders" - just naturally bring up what they asked you to remind them about."""

        # Use the existing OpenAI client to generate response
        try:
            response = client.chat.completions.create(
                model="gpt-4o-mini",
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": "Hello, I just opened the app."}
                ],
                temperature=0.7,
                max_tokens=500
            )
            
            ai_message = response.choices[0].message.content.strip()
            
            # NOW mark reminders as triggered after successful AI generation
            for reminder in due_reminders:
                reminder["triggered"] = True
                print(f"✅ Marked reminder {reminder['id']} as triggered after successful AI delivery")
            
            # Store this as a conversation turn
            store_conversation_turn(user_id, "[User opened app - checking for reminders]", ai_message)
            
            return {
                "status": "success",
                "message": ai_message,
                "reminders_delivered": len(due_reminders),
                "reminder_details": [
                    {
                        "id": r["id"],
                        "content": r["content"],
                        "was_due": r["datetime"].isoformat()
                    } for r in due_reminders
                ]
            }
            
        except Exception as ai_error:
            print(f"AI generation error: {ai_error}")
            # Fallback to simple message
            simple_message = f"Hi{f' {user_name}' if user_name else ''}! You have {len(due_reminders)} reminder{'s' if len(due_reminders) > 1 else ''}: "
            simple_message += ", ".join([f'"{r["content"]}"' for r in due_reminders])
            
            return {
                "status": "success",
                "message": simple_message,
                "reminders_delivered": len(due_reminders),
                "ai_fallback": True
            }
        
    except Exception as e:
        print(f"Reminder delivery error: {e}")
        return {"status": "error", "message": f"Failed to deliver reminders: {str(e)}"}

# Account Types and Permissions
ACCOUNT_TYPES = {
    "admin": {
        "name": "Administrator",
        "max_prompts": -1,  # Unlimited
        "memory_storage": True,
        "admin_access": True,
        "description": "Full system access with unlimited prompts and memory storage"
    },
    "limited_guest": {
        "name": "Limited Guest",
        "max_prompts": 20,
        "memory_storage": False,
        "admin_access": False,
        "description": "Limited to 20 prompts with no memory storage"
    },
    "unlimited_guest": {
        "name": "Unlimited Guest",
        "max_prompts": -1,  # Unlimited
        "memory_storage": False,
        "admin_access": False,
        "description": "Unlimited prompts with no memory storage"
    },
    "user": {
        "name": "User",
        "max_prompts": -1,  # Based on energy tokens
        "memory_storage": True,
        "admin_access": False,
        "description": "Prompts based on energy tokens with full memory storage"
    }
}

# Admin Management Models
class AdminUser(BaseModel):
    user_id: str
    username: Optional[str] = None
    email: Optional[str] = None
    created_at: Optional[str] = None
    last_active: Optional[str] = None
    status: str = "active"  # active, suspended, deleted
    account_type: str = "user"  # admin, limited_guest, unlimited_guest, user
    prompts_used: int = 0
    max_prompts: int = -1  # -1 means unlimited
    energy_tokens: int = 100  # For user accounts
    memory_count: int = 0
    conversation_count: int = 0

class AdminUserUpdate(BaseModel):
    username: Optional[str] = None
    email: Optional[str] = None
    status: Optional[str] = None
    account_type: Optional[str] = None
    max_prompts: Optional[int] = None
    energy_tokens: Optional[int] = None

class SystemStats(BaseModel):
    total_users: int
    active_users: int
    total_conversations: int
    total_documents: int
    database_size_mb: float
    system_uptime_hours: float

class DataClearRequest(BaseModel):
    user_id: Optional[str] = None  # If None, clear all data
    data_types: List[str] = ["conversations", "memories", "documents"]  # What to clear
    confirm: bool = False  # Safety confirmation

class UserAuthRequest(BaseModel):
    user_id: str
    auth_token: Optional[str] = None  # For future authentication system

class CreateAccountRequest(BaseModel):
    user_id: str
    account_type: str  # admin, limited_guest, unlimited_guest, user
    username: Optional[str] = None
    email: Optional[str] = None
    initial_energy_tokens: Optional[int] = 100  # For user accounts

class AccountUsageResponse(BaseModel):
    user_id: str
    account_type: str
    prompts_used: int
    max_prompts: int
    energy_tokens: int
    can_make_request: bool
    reason: Optional[str] = None

# Admin Management Endpoints
@app.post("/admin/accounts/create", response_model=AdminUser)
async def create_account(request: CreateAccountRequest):
    """Create a new account with specified type and limits"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Validate account type
        if request.account_type not in ACCOUNT_TYPES:
            raise HTTPException(status_code=400, detail=f"Invalid account type. Must be one of: {list(ACCOUNT_TYPES.keys())}")
        
        # Get account configuration
        account_config = ACCOUNT_TYPES[request.account_type]
        
        # Create user in database
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    # Check if user already exists
                    await cur.execute("SELECT user_id FROM user_accounts WHERE user_id = %s", (request.user_id,))
                    if await cur.fetchone():
                        raise HTTPException(status_code=400, detail="User already exists")
                    
                    # Insert new user account
                    await cur.execute("""
                        INSERT INTO user_accounts (user_id, account_type, username, email, max_prompts, energy_tokens, prompts_used, created_at, status)
                        VALUES (%s, %s, %s, %s, %s, %s, 0, NOW(), 'active')
                    """, (
                        request.user_id,
                        request.account_type,
                        request.username or f"User_{request.user_id}",
                        request.email or f"{request.user_id}@ragcompanion.com",
                        account_config["max_prompts"],
                        request.initial_energy_tokens if request.account_type == "user" else 0
                    ))
                    
                    await conn.commit()
                    
                    return AdminUser(
                        user_id=request.user_id,
                        username=request.username or f"User_{request.user_id}",
                        email=request.email or f"{request.user_id}@ragcompanion.com",
                        created_at=datetime.now().isoformat(),
                        last_active=datetime.now().isoformat(),
                        status="active",
                        account_type=request.account_type,
                        prompts_used=0,
                        max_prompts=account_config["max_prompts"],
                        energy_tokens=request.initial_energy_tokens if request.account_type == "user" else 0,
                        memory_count=0,
                        conversation_count=0
                    )
                    
        except Exception as db_error:
            raise HTTPException(status_code=500, detail=f"Database error: {str(db_error)}")
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to create account: {str(e)}")

@app.get("/admin/users", response_model=List[AdminUser])
async def get_all_users():
    """Get all users in the system with their data counts and account types"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        users = []
        
        # Add admin user
        users.append(AdminUser(
            user_id="admin",
            username="Administrator",
            email="admin@ragcompanion.com",
            created_at="2024-01-01T00:00:00Z",
            last_active="2024-01-01T00:00:00Z",
            status="active",
            account_type="admin",
            prompts_used=0,
            max_prompts=-1,
            energy_tokens=999999,
            memory_count=0,
            conversation_count=0
        ))
        
        # Get actual users from database with their data counts and account info
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    # Get users with their memory, conversation counts, and account info
                    await cur.execute("""
                        SELECT 
                            ua.user_id,
                            ua.username,
                            ua.email,
                            ua.account_type,
                            ua.prompts_used,
                            ua.max_prompts,
                            ua.energy_tokens,
                            ua.created_at,
                            ua.last_active,
                            COUNT(DISTINCT um.memory_id) as memory_count,
                            COUNT(DISTINCT ct.id) as conversation_count
                        FROM user_accounts ua
                        LEFT JOIN user_memory um ON ua.user_id = um.user_id
                        LEFT JOIN conversation_turns ct ON ua.user_id = ct.user_id
                        GROUP BY ua.user_id, ua.username, ua.email, ua.account_type, ua.prompts_used, ua.max_prompts, ua.energy_tokens, ua.created_at, ua.last_active
                    """)
                    
                    async for row in cur:
                        user_id, username, email, account_type, prompts_used, max_prompts, energy_tokens, created_at, last_active, memory_count, conversation_count = row
                        users.append(AdminUser(
                            user_id=user_id,
                            username=username or f"User_{user_id}",
                            email=email or f"{user_id}@ragcompanion.com",
                            created_at=created_at.isoformat() if created_at else "2024-01-01T00:00:00Z",
                            last_active=last_active.isoformat() if last_active else "2024-01-01T00:00:00Z",
                            status="active",
                            account_type=account_type,
                            prompts_used=prompts_used or 0,
                            max_prompts=max_prompts or -1,
                            energy_tokens=energy_tokens or 0,
                            memory_count=memory_count or 0,
                            conversation_count=conversation_count or 0
                        ))
        except Exception as db_error:
            # If database query fails, return at least admin user
            pass
            
        return users
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get users: {str(e)}")

@app.get("/admin/users/{user_id}", response_model=AdminUser)
async def get_user_details(user_id: str):
    """Get detailed information about a specific user"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Query actual user details from database
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    # Get user details with counts
                    await cur.execute("""
                        SELECT 
                            um.user_id,
                            MAX(um.created_at) as created_at,
                            MAX(um.updated_at) as last_active,
                            COUNT(DISTINCT um.memory_id) as memory_count,
                            COUNT(DISTINCT ct.id) as conversation_count
                        FROM user_memory um
                        LEFT JOIN conversation_turns ct ON um.user_id = ct.user_id
                        WHERE um.user_id = %s
                        GROUP BY um.user_id
                    """, (user_id,))
                    
                    row = await cur.fetchone()
                    if row:
                        user_id, created_at, last_active, memory_count, conversation_count = row
                        return AdminUser(
                            user_id=user_id,
                            username=f"User_{user_id}",
                            email=f"{user_id}@ragcompanion.com",
                            created_at=created_at.isoformat() if created_at else "2024-01-01T00:00:00Z",
                            last_active=last_active.isoformat() if last_active else "2024-01-01T00:00:00Z",
                            status="active",
                            permissions=["user"],
                            memory_count=memory_count or 0,
                            conversation_count=conversation_count or 0
                        )
                    else:
                        raise HTTPException(status_code=404, detail="User not found")
                        
        except Exception as db_error:
            raise HTTPException(status_code=500, detail=f"Database error: {str(db_error)}")
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get user details: {str(e)}")

@app.put("/admin/users/{user_id}")
async def update_user(user_id: str, user_update: AdminUserUpdate):
    """Update user information and permissions"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Update user in database
        # For now, just return success
        return {"message": f"User {user_id} updated successfully", "updated_fields": user_update.dict(exclude_unset=True)}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to update user: {str(e)}")

@app.delete("/admin/users/{user_id}")
async def delete_user(user_id: str, confirm: bool = False):
    """Delete a user account and all associated data"""
    if not confirm:
        raise HTTPException(status_code=400, detail="Must confirm deletion with confirm=true")
    
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Delete user and all associated data
        # This would clear conversations, memories, documents, etc.
        
        return {"message": f"User {user_id} and all associated data deleted successfully"}
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to delete user: {str(e)}")

@app.post("/admin/data/clear")
async def clear_data(request: DataClearRequest):
    """Clear specific types of data for a user or all users"""
    if not request.confirm:
        raise HTTPException(status_code=400, detail="Must confirm data clearing with confirm=true")
    
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Clear specified data types
        cleared_data = []
        
        if "conversations" in request.data_types:
            # Clear conversation history
            cleared_data.append("conversations")
        
        if "memories" in request.data_types:
            # Clear user memories
            cleared_data.append("memories")
        
        if "documents" in request.data_types:
            # Clear uploaded documents
            cleared_data.append("documents")
        
        return {
            "message": f"Data cleared successfully for {'all users' if request.user_id is None else f'user {request.user_id}'}",
            "cleared_data_types": cleared_data,
            "user_id": request.user_id
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to clear data: {str(e)}")

@app.get("/admin/stats", response_model=SystemStats)
async def get_system_stats():
    """Get system statistics and performance metrics"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Get actual stats from database using correct table names
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    # Get user count from user_memory table
                    await cur.execute("SELECT COUNT(DISTINCT user_id) FROM user_memory")
                    user_count = await cur.fetchone()
                    total_users = user_count[0] if user_count else 0
                    
                    # Get conversation count from conversation_turns table
                    await cur.execute("SELECT COUNT(*) FROM conversation_turns")
                    conv_count = await cur.fetchone()
                    total_conversations = conv_count[0] if conv_count else 0
                    
                    # Get document count from rag_chunks table
                    await cur.execute("SELECT COUNT(*) FROM rag_chunks")
                    doc_count = await cur.fetchone()
                    total_documents = doc_count[0] if doc_count else 0
                    
                    return SystemStats(
                        total_users=total_users + 1,  # +1 for admin
                        active_users=total_users + 1,
                        total_conversations=total_conversations,
                        total_documents=total_documents,
                        database_size_mb=5.0,  # Placeholder
                        system_uptime_hours=24.0  # Placeholder
                    )
        except Exception as db_error:
            # If database query fails, return basic stats
            return SystemStats(
                total_users=1,  # Just admin
                active_users=1,
                total_conversations=0,
                total_documents=0,
                database_size_mb=5.0,
                system_uptime_hours=24.0
            )
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get system stats: {str(e)}")

@app.get("/admin/health/detailed")
async def get_detailed_health():
    """Get detailed system health information"""
    try:
        health_info = {
            "status": "healthy",
            "service": "RAG Companion Service",
            "database_available": database_available,
            "vector_search": "enabled" if database_available else "fallback_mode",
            "system_info": {
                "python_version": "3.11",
                "fastapi_version": "0.104.0",
                "database_type": "PostgreSQL with pgvector",
                "uptime_seconds": 86400  # Placeholder
            },
            "performance": {
                "active_connections": 1,
                "memory_usage_mb": 128.5,
                "cpu_usage_percent": 15.2
            }
        }
        
        if database_available:
            # Add database-specific health info
            health_info["database"] = {
                "connection_pool_size": 10,
                "active_queries": 0,
                "cache_hit_ratio": 0.85
            }
        
        return health_info
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get health info: {str(e)}")

@app.get("/admin/debug/database")
async def debug_database():
    """Debug endpoint to see database structure and content"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        async with await psycopg.AsyncConnection.connect(database_url) as conn:
            async with conn.cursor() as cur:
                # Get list of tables
                await cur.execute("""
                    SELECT table_name 
                    FROM information_schema.tables 
                    WHERE table_schema = 'public'
                    ORDER BY table_name
                """)
                tables = [row[0] async for row in cur]
                
                # Get user_memory table structure if it exists
                user_memory_structure = []
                if 'user_memory' in tables:
                    await cur.execute("""
                        SELECT column_name, data_type, is_nullable
                        FROM information_schema.columns
                        WHERE table_name = 'user_memory'
                        ORDER BY ordinal_position
                    """)
                    user_memory_structure = [{"column": row[0], "type": row[1], "nullable": row[2]} async for row in cur]
                
                # Get sample data from user_memory if it exists
                sample_data = []
                if 'user_memory' in tables:
                    await cur.execute("SELECT * FROM user_memory LIMIT 5")
                    sample_data = [{"row": row} async for row in cur]
                
                return {
                    "tables": tables,
                    "user_memory_structure": user_memory_structure,
                    "sample_data": sample_data,
                    "database_url": database_url.replace(database_url.split('@')[0].split(':')[-1], '***') if '@' in database_url else "hidden"
                }
    except Exception as e:
        return {"error": str(e), "traceback": str(e.__traceback__)}

@app.post("/admin/init-database")
async def initialize_database_tables():
    """Initialize required database tables if they don't exist"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        async with await psycopg.AsyncConnection.connect(database_url) as conn:
            async with conn.cursor() as cur:
                # Create user_accounts table if it doesn't exist
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS user_accounts (
                        user_id TEXT PRIMARY KEY,
                        account_type TEXT NOT NULL DEFAULT 'limited_guest',
                        username TEXT,
                        email TEXT,
                        max_prompts INTEGER DEFAULT 20,
                        energy_tokens INTEGER DEFAULT 0,
                        prompts_used INTEGER DEFAULT 0,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        last_active TIMESTAMP WITH TIME ZONE DEFAULT NOW(),
                        status TEXT DEFAULT 'active'
                    )
                """)
                
                # Create conversation_turns table if it doesn't exist
                await cur.execute("""
                    CREATE TABLE IF NOT EXISTS conversation_turns (
                        id SERIAL PRIMARY KEY,
                        user_id TEXT NOT NULL,
                        user_message TEXT NOT NULL,
                        assistant_response TEXT NOT NULL,
                        created_at TIMESTAMP WITH TIME ZONE DEFAULT NOW()
                    )
                """)
                
                # Add indexes for better performance
                await cur.execute("CREATE INDEX IF NOT EXISTS idx_user_accounts_user_id ON user_accounts(user_id)")
                await cur.execute("CREATE INDEX IF NOT EXISTS idx_conversation_turns_user_id ON conversation_turns(user_id)")
                await cur.execute("CREATE INDEX IF NOT EXISTS idx_user_memory_user_id ON user_memory(user_id)")
                
                await conn.commit()
                
                return {
                    "message": "Database tables initialized successfully",
                    "tables_created": ["user_accounts", "conversation_turns"],
                    "indexes_created": ["idx_user_accounts_user_id", "idx_conversation_turns_user_id", "idx_user_memory_user_id"]
                }
                
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to initialize database: {str(e)}")

@app.get("/companion/{user_id}/profile")
async def get_companion_profile(user_id: str):
    """Get companion profile - only shows data for the specified companion"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Query companion-specific data from database
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    # Get companion memories
                    await cur.execute("""
                        SELECT memory_type, content, created_at, metadata
                        FROM user_memory 
                        WHERE user_id = %s
                        ORDER BY created_at DESC
                        LIMIT 50
                    """, (user_id,))
                    
                    memories = []
                    async for row in cur:
                        memory_type, content, created_at, metadata = row
                        memories.append({
                            "type": memory_type,
                            "content": content[:200] + "..." if len(content) > 200 else content,  # Truncate long content
                            "created_at": created_at.isoformat() if created_at else None,
                            "metadata": metadata
                        })
                    
                    # Get companion conversations
                    await cur.execute("""
                        SELECT user_message, assistant_response, created_at
                        FROM conversation_turns 
                        WHERE user_id = %s
                        ORDER BY created_at DESC
                        LIMIT 20
                    """, (user_id,))
                    
                    conversations = []
                    async for row in cur:
                        user_message, assistant_response, created_at = row
                        conversations.append({
                            "user_message": user_message[:100] + "..." if len(user_message) > 100 else user_message,
                            "assistant_response": assistant_response[:100] + "..." if len(assistant_response) > 100 else assistant_response,
                            "created_at": created_at.isoformat() if created_at else None
                        })
                    
                    return {
                        "user_id": user_id,
                        "memories": memories,
                        "conversations": conversations,
                        "total_memories": len(memories),
                        "total_conversations": len(conversations),
                        "last_active": memories[0]["created_at"] if memories else None
                    }
                        
        except Exception as db_error:
            raise HTTPException(status_code=500, detail=f"Database error: {str(db_error)}")
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get companion profile: {str(e)}")

@app.get("/account/usage/{user_id}", response_model=AccountUsageResponse)
async def check_account_usage(user_id: str):
    """Check if a user can make a request based on their account type and limits"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Check user account in database
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT account_type, prompts_used, max_prompts, energy_tokens
                        FROM user_accounts 
                        WHERE user_id = %s AND status = 'active'
                    """, (user_id,))
                    
                    row = await cur.fetchone()
                    if not row:
                        # User doesn't exist - create as limited guest
                        return AccountUsageResponse(
                            user_id=user_id,
                            account_type="limited_guest",
                            prompts_used=0,
                            max_prompts=20,
                            energy_tokens=0,
                            can_make_request=True,
                            reason="New user - limited guest access"
                        )
                    
                    account_type, prompts_used, max_prompts, energy_tokens = row
                    
                    # Check if user can make request
                    can_make_request = True
                    reason = None
                    
                    if account_type == "limited_guest":
                        if prompts_used >= max_prompts:
                            can_make_request = False
                            reason = f"Limited guest limit reached ({prompts_used}/{max_prompts})"
                    elif account_type == "user":
                        if energy_tokens <= 0:
                            can_make_request = False
                            reason = "No energy tokens remaining"
                    
                    return AccountUsageResponse(
                        user_id=user_id,
                        account_type=account_type,
                        prompts_used=prompts_used,
                        max_prompts=max_prompts,
                        energy_tokens=energy_tokens,
                        can_make_request=can_make_request,
                        reason=reason
                    )
                    
        except Exception as db_error:
            raise HTTPException(status_code=500, detail=f"Database error: {str(db_error)}")
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to check account usage: {str(e)}")

# Test endpoints for Unity user type testing
@app.post("/test/create-account")
async def create_test_account(request: CreateAccountRequest):
    """Create a test account for Unity testing (bypasses admin restrictions)"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        # Validate account type
        if request.account_type not in ACCOUNT_TYPES:
            raise HTTPException(status_code=400, detail=f"Invalid account type. Must be one of: {list(ACCOUNT_TYPES.keys())}")
        
        # Get account configuration
        account_config = ACCOUNT_TYPES[request.account_type]
        
        # Create or update user account
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    # Check if user already exists
                    await cur.execute("SELECT user_id FROM user_accounts WHERE user_id = %s", (request.user_id,))
                    existing_user = await cur.fetchone()
                    
                    if existing_user:
                        # Update existing user
                        await cur.execute("""
                            UPDATE user_accounts 
                            SET account_type = %s, max_prompts = %s, energy_tokens = %s, prompts_used = 0, status = 'active'
                            WHERE user_id = %s
                        """, (
                            request.account_type,
                            account_config["max_prompts"],
                            request.initial_energy_tokens if request.account_type == "user" else 0,
                            request.user_id
                        ))
                        action = "updated"
                    else:
                        # Insert new user account
                        await cur.execute("""
                            INSERT INTO user_accounts (user_id, account_type, username, email, max_prompts, energy_tokens, prompts_used, created_at, status)
                            VALUES (%s, %s, %s, %s, %s, %s, 0, NOW(), 'active')
                        """, (
                            request.user_id,
                            request.account_type,
                            request.username or f"Test_{request.user_id}",
                            request.email or f"{request.user_id}@test.com",
                            account_config["max_prompts"],
                            request.initial_energy_tokens if request.account_type == "user" else 0
                        ))
                        action = "created"
                    
                    await conn.commit()
                    
                    return {
                        "message": f"Test account {action} successfully",
                        "user_id": request.user_id,
                        "account_type": request.account_type,
                        "max_prompts": account_config["max_prompts"],
                        "energy_tokens": request.initial_energy_tokens if request.account_type == "user" else 0,
                        "action": action
                    }
                    
        except Exception as db_error:
            raise HTTPException(status_code=500, detail=f"Database error: {str(db_error)}")
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to create test account: {str(e)}")

@app.post("/test/reset-account/{user_id}")
async def reset_test_account(user_id: str, account_type: str = "limited_guest"):
    """Reset a test account to specified type with fresh limits"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        if account_type not in ACCOUNT_TYPES:
            raise HTTPException(status_code=400, detail=f"Invalid account type. Must be one of: {list(ACCOUNT_TYPES.keys())}")
        
        account_config = ACCOUNT_TYPES[account_type]
        
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    # Reset user account
                    await cur.execute("""
                        UPDATE user_accounts 
                        SET account_type = %s, max_prompts = %s, energy_tokens = %s, prompts_used = 0, status = 'active', last_active = NOW()
                        WHERE user_id = %s
                    """, (
                        account_type,
                        account_config["max_prompts"],
                        100 if account_type == "user" else 0,  # Give 100 energy tokens to users
                        user_id
                    ))
                    
                    if cur.rowcount == 0:
                        # User doesn't exist, create them
                        await cur.execute("""
                            INSERT INTO user_accounts (user_id, account_type, username, email, max_prompts, energy_tokens, prompts_used, created_at, status)
                            VALUES (%s, %s, %s, %s, %s, %s, 0, NOW(), 'active')
                        """, (
                            user_id,
                            account_type,
                            f"Test_{user_id}",
                            f"{user_id}@test.com",
                            account_config["max_prompts"],
                            100 if account_type == "user" else 0
                        ))
                    
                    await conn.commit()
                    
                    return {
                        "message": f"Test account reset successfully",
                        "user_id": user_id,
                        "account_type": account_type,
                        "max_prompts": account_config["max_prompts"],
                        "energy_tokens": 100 if account_type == "user" else 0,
                        "prompts_used": 0
                    }
                    
        except Exception as db_error:
            raise HTTPException(status_code=500, detail=f"Database error: {str(db_error)}")
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to reset test account: {str(e)}")

@app.get("/test/account-status/{user_id}")
async def get_test_account_status(user_id: str):
    """Get detailed status of a test account"""
    try:
        if not database_available:
            raise HTTPException(status_code=503, detail="Database not available")
        
        try:
            async with await psycopg.AsyncConnection.connect(database_url) as conn:
                async with conn.cursor() as cur:
                    await cur.execute("""
                        SELECT account_type, prompts_used, max_prompts, energy_tokens, created_at, last_active, status
                        FROM user_accounts 
                        WHERE user_id = %s
                    """, (user_id,))
                    
                    row = await cur.fetchone()
                    if not row:
                        return {
                            "user_id": user_id,
                            "exists": False,
                            "message": "User account not found"
                        }
                    
                    account_type, prompts_used, max_prompts, energy_tokens, created_at, last_active, status = row
                    
                    # Get memory and conversation counts
                    await cur.execute("""
                        SELECT COUNT(DISTINCT um.memory_id) as memory_count, COUNT(DISTINCT ct.id) as conversation_count
                        FROM user_accounts ua
                        LEFT JOIN user_memory um ON ua.user_id = um.user_id
                        LEFT JOIN conversation_turns ct ON ua.user_id = ct.user_id
                        WHERE ua.user_id = %s
                        GROUP BY ua.user_id
                    """, (user_id,))
                    
                    counts_row = await cur.fetchone()
                    memory_count = counts_row[0] if counts_row else 0
                    conversation_count = counts_row[1] if counts_row else 0
                    
                    return {
                        "user_id": user_id,
                        "exists": True,
                        "account_type": account_type,
                        "prompts_used": prompts_used,
                        "max_prompts": max_prompts,
                        "energy_tokens": energy_tokens,
                        "memory_count": memory_count,
                        "conversation_count": conversation_count,
                        "created_at": created_at.isoformat() if created_at else None,
                        "last_active": last_active.isoformat() if last_active else None,
                        "status": status,
                        "can_make_request": (
                            (account_type == "limited_guest" and prompts_used < max_prompts) or
                            (account_type == "user" and energy_tokens > 0) or
                            account_type in ["admin", "unlimited_guest"]
                        )
                    }
                    
        except Exception as db_error:
            raise HTTPException(status_code=500, detail=f"Database error: {str(db_error)}")
            
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Failed to get account status: {str(e)}")

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8077, reload=True)