import os
import uuid
import asyncio
from typing import List, Optional, Dict, Any
from pathlib import Path

from fastapi import FastAPI, HTTPException, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from openai import OpenAI
from dotenv import load_dotenv

from database import RAGDatabase
from chunker import DocumentChunker, DocumentProcessor

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

# Global state
embedding_cache = {}

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

@app.post("/query")
def rag_query_sync(request: dict):
    """Main RAG query endpoint with full RAG functionality"""
    try:
        import time
        start_time = time.time()
        query_text = request.get('query', '')
        user_id = request.get('user_id', 'anonymous')
        top_k = request.get('top_k', 5)
        
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
        
        # Search database
        db_start = time.time()
        try:
            # Search database for similar chunks
            db_results = []
            if hasattr(db, 'search_chunks') and callable(getattr(db, 'search_chunks')):
                try:
                    import asyncio
                    loop = asyncio.new_event_loop()
                    asyncio.set_event_loop(loop)
                    db_results = loop.run_until_complete(
                        db.search_chunks(query_embedding, top_k, request.get('filters', {}))
                    )
                    loop.close()
                except Exception as search_error:
                    print(f"Database search error: {search_error}")
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
            
            # If no database results, generate normal AI response
            if not results:
                try:
                    import asyncio
                    loop = asyncio.new_event_loop()
                    asyncio.set_event_loop(loop)
                    ai_response = loop.run_until_complete(
                        generate_ai_response(query_text, user_id)
                    )
                    loop.close()
                    
                    results = [
                        {
                            "chunk_id": "ai_generated",
                            "text": ai_response,
                            "doc_title": "AI Response",
                            "section": "generated",
                            "tags": ["ai_generated", "no_rag"],
                            "source_path": "system/ai",
                            "score": 0.7
                        }
                    ]
                except Exception as ai_error:
                    print(f"AI response generation error: {ai_error}")
                    results = [
                        {
                            "chunk_id": "fallback_search",
                            "text": f"I understand your question about '{query_text}', but I'm currently unable to provide a detailed response.",
                            "doc_title": "System Response",
                            "section": "fallback",
                            "tags": ["fallback", "error"],
                            "source_path": "system/fallback",
                            "score": 0.3
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
        # Get embedding for the content
        embedding = get_embedding(request.content)
        
        # Store in database
        await db.upsert_user_memory(
            user_id=request.user_id,
            memory_type=request.memory_type,
            content=request.content,
            embedding=embedding,
            metadata=request.metadata
        )
        
        return {"status": "success", "message": "Memory stored successfully"}
        
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Memory storage failed: {str(e)}")

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
        # Check if we have any stored memories for this user
        memory_context = ""
        if user_id and hasattr(db, 'get_user_memory_summary'):
            try:
                memory_summary = await db.get_user_memory_summary(user_id)
                if memory_summary:
                    memory_context = f"\n\nRELEVANT USER CONTEXT:\n{memory_summary}"
            except Exception as e:
                print(f"Memory retrieval error: {e}")
        
        system_prompt = f"""You are a RAG (Retrieval-Augmented Generation) AI assistant with the following capabilities:

MEMORY & KNOWLEDGE:
- I can remember and store user conversations, preferences, and personal information
- I have access to a knowledge base of documents that have been ingested
- I can store and retrieve different types of memories: episodic (conversations), semantic (facts), and profile (personal details)
- I search through my knowledge base for every query to provide contextual responses

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

If asked about my memory capabilities, I should explain these features. Otherwise, I'll provide helpful responses to general questions.{memory_context}"""

        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": query}
        ]
        
        response = client.chat.completions.create(
            model="gpt-3.5-turbo",
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