"""
Hybrid RAG System for Mobile Voice Companion
- Local RAG: Personal/encrypted data stored on device
- Cloud RAG: General non-encrypted data stored on server
- Smart routing based on content sensitivity
"""

import os
import json
import hashlib
import asyncio
import logging
from typing import Dict, Any, List, Optional, Tuple
from enum import Enum
from dataclasses import dataclass
from openai import OpenAI
from dotenv import load_dotenv

# Load environment variables
load_dotenv()
client = OpenAI(api_key=os.getenv("OPENAI_API_KEY"))

logger = logging.getLogger(__name__)

class DataSensitivity(Enum):
    """Classification for data sensitivity levels"""
    PERSONAL = "personal"      # Names, addresses, phone numbers, personal preferences
    PRIVATE = "private"        # Conversations, diary entries, personal thoughts
    GENERAL = "general"        # Weather, news, public information
    PUBLIC = "public"          # Wikipedia-style factual information

@dataclass
class RAGQuery:
    """Structured query for the hybrid RAG system"""
    text: str
    user_id: str
    sensitivity: Optional[DataSensitivity] = None
    context_window: int = 5
    include_tools: bool = True

@dataclass
class RAGResponse:
    """Structured response from the hybrid RAG system"""
    response_text: str
    source: str  # "local", "cloud", or "hybrid"
    sensitivity: DataSensitivity
    retrieved_chunks: List[Dict[str, Any]]
    tool_results: Optional[Dict[str, Any]] = None
    personal_info_extracted: Optional[Dict[str, Any]] = None

class ContentClassifier:
    """Classifies content sensitivity for routing decisions"""
    
    def __init__(self):
        # Personal information patterns
        self.personal_patterns = [
            # Identity
            "my name is", "call me", "i'm called", "i am",
            # Contact info  
            "my phone", "my email", "my address", "i live at",
            # Personal details
            "my birthday", "my age", "born in", "i work at", "my job",
            # Relationships
            "my wife", "my husband", "my partner", "my kids", "my family",
            # Health
            "my doctor", "my medication", "i have", "diagnosed with",
            # Financial
            "my bank", "my credit card", "my salary", "i earn"
        ]
        
        # Private conversation patterns
        self.private_patterns = [
            "i feel", "i think", "i believe", "my opinion", "personally",
            "i'm worried", "i'm excited", "i hate", "i love",
            "remember when", "yesterday i", "last week", "my plan is"
        ]
        
        # General/public query patterns
        self.general_patterns = [
            "what is", "who is", "when did", "where is", "how does",
            "tell me about", "explain", "weather in", "news about",
            "definition of", "history of"
        ]
    
    def classify_sensitivity(self, text: str) -> DataSensitivity:
        """Classify the sensitivity level of text content"""
        text_lower = text.lower()
        
        # Check for personal information
        if any(pattern in text_lower for pattern in self.personal_patterns):
            return DataSensitivity.PERSONAL
            
        # Check for private thoughts/conversations
        if any(pattern in text_lower for pattern in self.private_patterns):
            return DataSensitivity.PRIVATE
            
        # Check for general queries
        if any(pattern in text_lower for pattern in self.general_patterns):
            return DataSensitivity.GENERAL
        
        # Default to private for safety
        return DataSensitivity.PRIVATE

class LocalRAGStore:
    """Local storage for personal/private data - stays on device"""
    
    def __init__(self, user_id: str):
        self.user_id = user_id
        self.storage_path = f"local_rag_{user_id}.json"
        self.memory_store = self._load_local_data()
        
    def _load_local_data(self) -> Dict[str, Any]:
        """Load local data from device storage"""
        try:
            if os.path.exists(self.storage_path):
                with open(self.storage_path, 'r') as f:
                    return json.load(f)
            return {"personal_info": {}, "conversations": [], "memories": []}
        except Exception as e:
            logger.error(f"Error loading local data: {e}")
            return {"personal_info": {}, "conversations": [], "memories": []}
    
    def _save_local_data(self):
        """Save data to local device storage"""
        try:
            with open(self.storage_path, 'w') as f:
                json.dump(self.memory_store, f, indent=2)
        except Exception as e:
            logger.error(f"Error saving local data: {e}")
    
    def store_memory(self, content: str, memory_type: str, metadata: Dict[str, Any] = None):
        """Store a memory locally"""
        memory = {
            "content": content,
            "type": memory_type,
            "timestamp": metadata.get("timestamp") if metadata else None,
            "metadata": metadata or {}
        }
        
        if memory_type == "personal_info":
            # Extract and store personal information
            from rag_api import extract_personal_info
            personal_data = extract_personal_info(content)
            self.memory_store["personal_info"].update(personal_data)
        else:
            self.memory_store["memories"].append(memory)
            
        # Keep only last 100 memories for performance
        if len(self.memory_store["memories"]) > 100:
            self.memory_store["memories"] = self.memory_store["memories"][-100:]
            
        self._save_local_data()
    
    def search_memories(self, query: str, top_k: int = 5) -> List[Dict[str, Any]]:
        """Search local memories"""
        query_lower = query.lower()
        relevant_memories = []
        
        # Search personal info
        for key, value in self.memory_store["personal_info"].items():
            if query_lower in str(value).lower():
                relevant_memories.append({
                    "content": f"User's {key}: {value}",
                    "type": "personal_info",
                    "score": 0.9
                })
        
        # Search memories
        for memory in self.memory_store["memories"]:
            content_lower = memory["content"].lower()
            if any(word in content_lower for word in query_lower.split()):
                # Simple relevance scoring
                score = sum(1 for word in query_lower.split() if word in content_lower) / len(query_lower.split())
                memory["score"] = score
                relevant_memories.append(memory)
        
        # Sort by relevance and return top_k
        relevant_memories.sort(key=lambda x: x.get("score", 0), reverse=True)
        return relevant_memories[:top_k]

class CloudRAGStore:
    """Cloud storage for general/public data - server-based"""
    
    def __init__(self):
        # Import existing database connection
        from database import RAGDatabase
        
        self.db = RAGDatabase(os.getenv("DATABASE_URL"))
        self.get_embedding = None  # Will be set when needed
    
    async def search_general_knowledge(self, query: str, top_k: int = 5) -> List[Dict[str, Any]]:
        """Search cloud-based general knowledge"""
        try:
            # Initialize database if needed
            if not hasattr(self.db, '_initialized'):
                await self.db.initialize()
                self.db._initialized = True
            
            # Get query embedding (lazy import to avoid circular dependency)
            if self.get_embedding is None:
                # Import at runtime to avoid circular imports
                import importlib
                rag_api = importlib.import_module('rag_api')
                self.get_embedding = rag_api.get_embedding
            
            query_embedding = self.get_embedding(query)
            
            # Search cloud database
            # Note: This would use the existing RAG database search
            # For now, return empty as we focus on the architecture
            return []
            
        except Exception as e:
            logger.error(f"Cloud RAG search error: {e}")
            return []

class HybridRAGSystem:
    """Main hybrid RAG system orchestrator"""
    
    def __init__(self):
        self.classifier = ContentClassifier()
        self.local_stores = {}  # user_id -> LocalRAGStore
        self.cloud_store = CloudRAGStore()
        
        # Import tool manager for external capabilities
        from mcp_tools import tool_manager
        self.tool_manager = tool_manager
    
    def get_local_store(self, user_id: str) -> LocalRAGStore:
        """Get or create local store for user"""
        if user_id not in self.local_stores:
            self.local_stores[user_id] = LocalRAGStore(user_id)
        return self.local_stores[user_id]
    
    async def process_query(self, query: RAGQuery) -> RAGResponse:
        """Process a query using the hybrid RAG system"""
        
        # Classify sensitivity if not provided
        if query.sensitivity is None:
            query.sensitivity = self.classifier.classify_sensitivity(query.text)
        
        logger.info(f"Processing query with sensitivity: {query.sensitivity.value}")
        
        # Route based on sensitivity
        if query.sensitivity in [DataSensitivity.PERSONAL, DataSensitivity.PRIVATE]:
            return await self._process_local_query(query)
        else:
            return await self._process_cloud_query(query)
    
    async def _process_local_query(self, query: RAGQuery) -> RAGResponse:
        """Process query using local RAG"""
        local_store = self.get_local_store(query.user_id)
        
        # Search local memories
        relevant_memories = local_store.search_memories(query.text, top_k=query.context_window)
        
        # Check for tool usage (weather, etc.)
        tool_result = None
        if query.include_tools:
            from rag_api import check_and_use_tools
            user_profile = local_store.memory_store["personal_info"]
            tool_result = await check_and_use_tools(query.text, user_profile)
        
        # Generate response with local context
        response_text = await self._generate_response_with_context(
            query.text, 
            relevant_memories,
            query.user_id,
            "local",
            tool_result
        )
        
        # Extract and store any new personal info
        from rag_api import extract_personal_info
        personal_info = extract_personal_info(query.text)
        if personal_info:
            local_store.store_memory(query.text, "personal_info", {"extracted": personal_info})
        
        # Store conversation
        local_store.store_memory(query.text, "conversation", {
            "response": response_text,
            "timestamp": "now"  # Would use actual timestamp
        })
        
        return RAGResponse(
            response_text=response_text,
            source="local",
            sensitivity=query.sensitivity,
            retrieved_chunks=[{"content": mem["content"], "score": mem.get("score", 0)} for mem in relevant_memories],
            tool_results=tool_result,
            personal_info_extracted=personal_info
        )
    
    async def _process_cloud_query(self, query: RAGQuery) -> RAGResponse:
        """Process query using cloud RAG"""
        
        # Search cloud knowledge base
        cloud_results = await self.cloud_store.search_general_knowledge(query.text, query.context_window)
        
        # Check for tool usage
        tool_result = None
        if query.include_tools:
            # For general queries, we can still use tools but without personal context
            from rag_api import check_and_use_tools
            tool_result = await check_and_use_tools(query.text, {})
        
        # Generate response with cloud context
        response_text = await self._generate_response_with_context(
            query.text,
            cloud_results,
            query.user_id,
            "cloud", 
            tool_result
        )
        
        return RAGResponse(
            response_text=response_text,
            source="cloud",
            sensitivity=query.sensitivity,
            retrieved_chunks=cloud_results,
            tool_results=tool_result,
            personal_info_extracted=None
        )
    
    async def _generate_response_with_context(
        self, 
        query: str, 
        context: List[Dict[str, Any]], 
        user_id: str,
        source: str,
        tool_result: Optional[Dict[str, Any]] = None
    ) -> str:
        """Generate response using OpenAI with context"""
        
        try:
            # Build context string
            context_text = ""
            if context:
                context_text = "Relevant information:\n"
                for item in context:
                    context_text += f"- {item.get('content', '')}\n"
            
            # Build system prompt based on source
            if source == "local":
                system_prompt = f"""You are a personal AI companion with access to the user's personal information and conversation history. 
                
Be warm, personal, and naturally incorporate what you know about them. Reference their personal details when relevant.

{context_text}

{"Tool result: " + str(tool_result) if tool_result else ""}"""
            else:
                system_prompt = f"""You are a helpful AI assistant providing general information and assistance.

{context_text}

{"Tool result: " + str(tool_result) if tool_result else ""}"""
            
            # Generate response
            response = client.chat.completions.create(
                model="gpt-4o-mini-realtime-preview",
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": query}
                ],
                max_tokens=400,
                temperature=0.8
            )
            
            return response.choices[0].message.content.strip()
            
        except Exception as e:
            logger.error(f"Response generation error: {e}")
            return "I'm having trouble processing your request right now. Could you try again?"

# Global hybrid RAG system instance (lazy initialization)
hybrid_rag = None

def get_hybrid_rag():
    """Get hybrid RAG system instance (lazy initialization)"""
    global hybrid_rag
    if hybrid_rag is None:
        hybrid_rag = HybridRAGSystem()
    return hybrid_rag

# Mobile voice flow functions
async def process_voice_query(audio_data: bytes, user_id: str, audio_format: str = "webm") -> Dict[str, Any]:
    """Complete mobile voice processing pipeline"""
    try:
        # Import audio handler
        from audio_handler import audio_handler
        
        # Step 1: Speech to text
        transcript = await audio_handler.speech_to_text(audio_data, audio_format)
        logger.info(f"Voice transcript: {transcript}")
        
        # Step 2: Process with hybrid RAG
        query = RAGQuery(text=transcript, user_id=user_id)
        rag_system = get_hybrid_rag()
        rag_response = await rag_system.process_query(query)
        
        # Step 3: Text to speech
        audio_response = await audio_handler.text_to_speech(rag_response.response_text)
        
        # Step 4: Return complete response
        import base64
        return {
            "transcript": transcript,
            "response_text": rag_response.response_text,
            "audio_response": base64.b64encode(audio_response).decode(),
            "source": rag_response.source,
            "sensitivity": rag_response.sensitivity.value,
            "tool_results": rag_response.tool_results,
            "personal_info_extracted": rag_response.personal_info_extracted,
            "status": "success"
        }
        
    except Exception as e:
        logger.error(f"Voice query processing error: {e}")
        return {
            "transcript": "",
            "response_text": "I'm having trouble processing your voice request.",
            "audio_response": "",
            "source": "error",
            "sensitivity": "general",
            "tool_results": None,
            "personal_info_extracted": None,
            "status": "error",
            "error": str(e)
        }