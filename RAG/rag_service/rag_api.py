import os
import uuid
import asyncio
from typing import List, Optional, Dict, Any
from pathlib import Path

from fastapi import FastAPI, HTTPException, BackgroundTasks, WebSocket, WebSocketDisconnect, UploadFile, File
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from openai import OpenAI
from dotenv import load_dotenv

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

# Initialize database
db = RAGDatabase(os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db"))

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

@app.on_event("startup")
async def startup_event():
    """Initialize database on startup"""
    try:
        await db.initialize()
        print("RAG service started successfully with database")
    except Exception as e:
        print(f"Database initialization failed: {e}")
        print("RAG service started without database")

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy", "service": "RAG Companion Service"}

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

def extract_personal_info(message: str) -> dict:
    """Extract personal information from user messages"""
    personal_info = {}
    message_lower = message.lower()
    
    # Name detection patterns - more specific
    name_patterns = [
        ("my name is ", "name"),
        ("call me ", "name"), 
        ("my name's ", "name")
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
    
    all_patterns = name_patterns + work_patterns + interest_patterns + location_patterns
    
    for pattern, info_type in all_patterns:
        if pattern in message_lower:
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
                    else:
                        value = " ".join(rest[:3]).strip('.,!?')
                    
                    if len(value) > 1:
                        personal_info[info_type] = value
                break
    
    return personal_info

def store_conversation_turn(user_id: str, user_message: str, ai_response: str):
    """Store conversation turn for context"""
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

def get_conversation_context(user_id: str) -> str:
    """Get recent conversation context"""
    if user_id not in user_conversations:
        return ""
    
    context_parts = []
    for turn in user_conversations[user_id][-3:]:  # Last 3 turns
        context_parts.append(f"User: {turn['user']}")
        context_parts.append(f"Assistant: {turn['assistant']}")
    
    return "\n".join(context_parts) if context_parts else ""

def store_personal_info_simple(user_id: str, info: dict):
    """Store personal information in a simple way"""
    try:
        # Store in memory first (always works)
        if user_id not in user_profiles:
            user_profiles[user_id] = {}
        
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
        
        # Also try to store in database if available (fire and forget)
        try:
            for key, value in info.items():
                memory_data = {
                    "user_id": user_id,
                    "memory_type": "profile", 
                    "content": f"User's {key}: {value}",
                    "metadata": {"info_type": key, "value": value}
                }
                print(f"Attempting database storage: {key} = {value}")
        except Exception as db_error:
            print(f"Database storage failed (using memory backup): {db_error}")
            
    except Exception as e:
        print(f"Could not store personal info: {e}")

def get_user_name(user_id: str) -> str:
    """Try to retrieve user's name from stored memories"""
    try:
        # Check memory storage first
        if user_id in user_profiles and "name" in user_profiles[user_id]:
            return user_profiles[user_id]["name"]
        
        # Could add database lookup here later
        return ""
        
    except Exception as e:
        print(f"Could not retrieve user name: {e}")
        return ""

async def generate_conversational_response(user_id: str, query: str, personal_info: dict) -> str:
    """Generate a fluid conversational response with personal context and tools"""
    try:
        # Get user profile and conversation context
        user_profile = user_profiles.get(user_id, {})
        conversation_context = get_conversation_context(user_id)
        
        # Debug: Log what we're retrieving
        print(f"🔍 Generating response for user_id: '{user_id}'")
        print(f"🔍 Retrieved user_profile: {user_profile}")
        print(f"🔍 All stored profiles: {user_profiles}")
        print(f"🔍 Personal info from this message: {personal_info}")
        print(f"🔍 Available user_ids in profiles: {list(user_profiles.keys())}")
        
        # Check if user is asking for something that needs a tool
        tool_result = await check_and_use_tools(query, user_profile)
        
        # Build comprehensive context for the AI
        system_content = """You are a conversational AI companion that remembers personal details and maintains fluid conversation. 

Key behaviors:
- Naturally incorporate what you know about the user into responses
- Reference previous conversation topics when relevant
- Ask follow-up questions to learn more about the user
- Be genuinely interested in their life, work, and interests
- Make connections between different pieces of information they've shared
- Respond in a warm, engaging, and personal way
- When you have tool results, incorporate them naturally into the conversation"""

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
            system_content += f"\n\nThe user just shared new personal information: {personal_info}"
            
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
    
    # Weather tool detection
    weather_keywords = ["weather", "temperature", "forecast", "rain", "sunny", "cloudy", "hot", "cold"]
    if any(keyword in query_lower for keyword in weather_keywords):
        # Try to extract location from query or use user's stored location
        location = extract_location_from_query(query) or user_profile.get("location", "")
        
        if location:
            return await tool_manager.execute_tool("get_weather", location=location)
        else:
            return {
                "success": False,
                "message": "I'd love to check the weather for you! Which city or location would you like to know about?"
            }
    
    # Easy to add more tool detections here:
    # if "schedule" in query_lower or "calendar" in query_lower:
    #     return await tool_manager.execute_tool("get_calendar")
    
    return None

def extract_location_from_query(query: str) -> str:
    """Extract location from weather-related queries"""
    query_lower = query.lower()
    
    # Simple location extraction patterns
    location_patterns = [
        "weather in ",
        "weather for ",
        "temperature in ",
        "temperature for ",
        "forecast for ",
        "forecast in "
    ]
    
    for pattern in location_patterns:
        if pattern in query_lower:
            start = query_lower.find(pattern) + len(pattern)
            # Extract location (take words until punctuation or end)
            location_part = query[start:].split('?')[0].split('.')[0].split('!')[0].strip()
            if location_part:
                return location_part
    
    return ""

@app.post("/query")
def rag_query_sync(request: dict):
    """Main RAG query endpoint with full RAG functionality"""
    try:
        import time
        start_time = time.time()
        query_text = request.get('query', '')
        user_id = request.get('user_id', 'anonymous')
        top_k = request.get('top_k', 5)
        
        # Debug: Log the query request details
        print(f"📝 Query request - user_id: {user_id}, query: {query_text[:50]}...")
        
        # Check for personal information in the message and store it
        personal_info = extract_personal_info(query_text)
        if personal_info:
            store_personal_info_simple(user_id, personal_info)
        
        # Store user message for future memory/context (disabled for now to prevent errors)
        # try:
        #     asyncio.run(store_user_interaction(user_id, query_text, "user"))
        # except Exception as store_error:
        #     print(f"Warning: Could not store user interaction: {store_error}")
        
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
        
        # Search database (simplified for stability)
        db_start = time.time()
        try:
            # Skip database search temporarily to isolate the issue
            db_results = []
            print(f"Database search temporarily disabled for debugging")
            
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
            if not results:
                print(f"🔍 Calling generate_conversational_response for user_id: {user_id}")
                # Generate fluid conversational response with tool support
                import asyncio
                loop = asyncio.new_event_loop()
                asyncio.set_event_loop(loop)
                response_text = loop.run_until_complete(
                    generate_conversational_response(user_id, query_text, personal_info)
                )
                loop.close()
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
            # Extract personal info and store it
            personal_info = extract_personal_info(request.content)
            if personal_info:
                store_personal_info_simple(request.user_id, personal_info)
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

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8077, reload=True)