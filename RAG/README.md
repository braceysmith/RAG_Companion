# Unity RAG Companion System

A complete implementation of the Unity-4o-mini-realtime-companion-rag-build-guide featuring real-time AI conversation with Retrieval-Augmented Generation (RAG) capabilities.

## Architecture Overview

This system implements the 3-tier architecture specified in the guide:

```
[Unity Audio/Avatar] → [Realtime Session Manager] → [OpenAI 4o-mini Realtime]
                    ↓                                      ↑
              [Turn Coordinator] → [RAG Microservice] → [pgvector DB]
                    ↓                                      
              [Context Assembler] ← [4-Layer Memory System]
```

## Core Components

### 🔧 **Unity Components**

#### **RAGCompanionSystem.cs**
- Main orchestrator that coordinates all components
- Handles system initialization and lifecycle management
- Provides unified API for external interactions

#### **RealtimeSessionManager.cs**
- WebSocket connection to OpenAI 4o-mini Realtime API
- Real-time audio streaming and processing
- Voice Activity Detection (VAD) integration
- Audio frame buffering and transmission

#### **TurnCoordinator.cs**
- Orchestrates conversation turns end-to-end
- Query classification for RAG/memory retrieval
- Parallel processing of knowledge and memory queries
- Latency management and timeout handling

#### **ContextAssembler.cs**
- Sophisticated context assembly with token budgeting
- 4-layer memory integration (working, episodic, semantic, profile)
- Knowledge block formatting and deduplication
- Priority-based context selection

#### **ConversationState.cs**
- Manages conversation history and user facts
- Automatic fact extraction from conversations
- Token counting and memory pruning
- Session persistence and archival

#### **RAGClient.cs**
- HTTP client for RAG microservice communication
- Query caching and performance optimization
- Memory management (store/retrieve operations)
- Conversation logging and analytics

#### **AvatarSpeechController.cs**
- Text-to-speech audio playback
- Lip-sync animation from audio streams
- Facial expression control
- Viseme mapping and blend shape control

### 🐍 **Python RAG Microservice**

#### **rag_api.py**
- FastAPI service with full RAG capabilities
- OpenAI text-embedding-3-small integration
- pgvector similarity search
- 4-layer memory system backend

#### **database.py**
- PostgreSQL + pgvector database management
- Vectorized document storage and retrieval
- User memory management
- Conversation turn logging

#### **chunker.py**
- Intelligent document chunking with overlap
- Metadata extraction and enrichment
- Multi-format support (text, markdown)
- Token estimation and optimization

## Features Implemented

### ✅ **Core RAG Features**
- **Document Ingestion**: Automated processing of text/markdown files
- **Vector Search**: Semantic similarity using OpenAI embeddings
- **Context Assembly**: Token-budgeted context with priority ranking
- **Memory Layers**: Working, episodic, semantic, and profile memory
- **Query Classification**: Automatic determination of retrieval needs

### ✅ **Real-time Features**
- **Audio Streaming**: WebSocket-based audio transmission
- **Voice Activity Detection**: Server-side VAD integration
- **Low Latency**: Optimized for sub-400ms response times
- **Conversation Flow**: Orchestrated turn-taking with interruption handling

### ✅ **Avatar Integration**
- **Lip Sync**: Real-time mouth animation from audio
- **Facial Expressions**: Emotion-based expression control
- **Viseme Mapping**: Phoneme-to-mouth-shape conversion
- **Blend Shape Control**: Direct facial animation control

### ✅ **Advanced Features**
- **Memory Management**: Automatic fact extraction and storage
- **User Profiles**: Persistent user preferences and characteristics
- **Conversation Logging**: Complete interaction history
- **Performance Monitoring**: Latency tracking and optimization
- **Error Handling**: Comprehensive error recovery and logging

## Quick Start

### 1. **Setup RAG Microservice**

```bash
cd rag_service
cp .env.example .env
# Add your OpenAI API key to .env
docker-compose up -d
```

### 2. **Unity Setup**

1. Open the Unity project in Unity 6000.0.32f1
2. Add the `RAGCompanionSystem` prefab to your scene
3. Configure the system settings:
   - Set your OpenAI API key
   - Configure RAG service URL (default: http://localhost:8077)
   - Set user ID for the session

### 3. **Document Ingestion**

```bash
# Add documents to the RAG service
python rag_service/ingest.py path/to/your/documents
```

### 4. **Test the System**

- Press Play in Unity
- The system will automatically initialize
- Use the F1-F3 keys for quick test queries
- Monitor the console for system status

## Configuration

### **Unity Configuration**

Edit the `RAGCompanionSystem` component settings:

```csharp
[Header("System Configuration")]
public string userId = "your-user-id";
public string ragServiceUrl = "http://localhost:8077";
public string openAIApiKey = "your-openai-key";
public bool enableAudioCapture = true;
public bool enableTTSPlayback = true;
```

### **RAG Service Configuration**

Edit `rag_service/.env`:

```env
OPENAI_API_KEY=your_openai_api_key_here
DATABASE_URL=postgresql://rag_user:rag_password@localhost:5432/rag_db
```

### **Token Budgeting**

The system implements sophisticated token budgeting:

- **System Instructions**: 10% of total tokens
- **Conversation History**: 30% of total tokens
- **Retrieved Knowledge**: 40% of total tokens
- **User Memory**: 10% of total tokens
- **Buffer**: 10% for model output

## Memory System

### **4-Layer Architecture**

1. **Working Context**: Active conversation turns (in-memory)
2. **Episodic Memory**: Specific events and interactions (database)
3. **Semantic Memory**: General knowledge and patterns (vector search)
4. **Declarative Profile**: Structured user facts (key-value store)

### **Automatic Fact Extraction**

The system automatically extracts facts from conversations:

```csharp
// Example extracted facts
"preference.food" → "likes almond flour pancakes"
"personal.location" → "lives in Connecticut"
"experience.work" → "worked on Unity projects"
```

## Performance Optimizations

### **Latency Controls**
- **Two-stage retrieval**: Keyword pre-filter → vector re-rank
- **Adaptive top-K**: Dynamic result count based on query complexity
- **Async prefetch**: Start retrieval during long user speech
- **Embedding cache**: SHA256-based caching to avoid duplicate embeddings
- **Delta contexting**: Only update changed knowledge blocks

### **Memory Management**
- **Conversation pruning**: Remove old turns beyond token/count limits
- **Query caching**: Cache recent RAG queries for faster responses
- **Batch processing**: Process multiple documents in batches
- **Background ingestion**: Non-blocking document processing

## Testing

### **Built-in Testing**

The system includes comprehensive testing:

```csharp
// Use the RAGSystemTester component
- F10: Run all system tests
- F11: Send test message
- F12: Test document retrieval
```

### **API Testing**

Test the RAG microservice directly:

```bash
curl -X POST http://localhost:8077/query \
  -H "Content-Type: application/json" \
  -d '{
    "query": "How does the energy system work?",
    "user_id": "test-user",
    "top_k": 5
  }'
```

## Production Considerations

### **Security**
- ✅ API key management (server-side rotation)
- ✅ User data isolation and scoping
- ✅ Request rate limiting
- ✅ Input validation and sanitization

### **Scalability**
- ✅ Stateless RAG service design
- ✅ Database connection pooling
- ✅ Embedding cache optimization
- ✅ Horizontal scaling support

### **Monitoring**
- ✅ Structured logging with turn IDs
- ✅ Performance metrics collection
- ✅ Error tracking and alerting
- ✅ Health check endpoints

## Troubleshooting

### **Common Issues**

1. **RAG Service Not Responding**
   - Check Docker containers: `docker-compose ps`
   - Verify database connection: `docker-compose logs postgres`
   - Check API key configuration

2. **Realtime Session Fails**
   - Verify OpenAI API key is valid
   - Check network connectivity
   - Monitor WebSocket connection logs

3. **No Audio Playback**
   - Verify microphone permissions
   - Check audio device configuration
   - Enable audio components in Unity

4. **Poor Retrieval Quality**
   - Verify documents are properly ingested
   - Check embedding generation logs
   - Adjust relevance threshold settings

### **Debug Commands**

```bash
# Check RAG service health
curl http://localhost:8077/health

# View database tables
docker-compose exec postgres psql -U rag_user -d rag_db -c "\dt"

# Check ingestion logs
docker-compose logs rag_service
```

## Extending the System

### **Adding New Memory Types**

```csharp
// Add to ConversationState.cs
public void AddCustomMemory(string type, string content) {
    var fact = new UserFact(type, "custom", content);
    // Process and store
}
```

### **Custom Query Classification**

```csharp
// Extend TurnCoordinator.cs
private bool NeedsCustomRetrieval(string transcript) {
    // Add custom classification logic
    return transcript.Contains("custom-keyword");
}
```

### **Additional Document Types**

```python
# Extend chunker.py
def process_pdf_file(self, file_path):
    # Add PDF processing logic
    pass
```

## API Reference

### **Unity API**

```csharp
// Main system control
RAGCompanionSystem.StartNewSession()
RAGCompanionSystem.SendMessage(string message)
RAGCompanionSystem.StopCurrentSession()

// Component access
RAGCompanionSystem.RagClient.QueryAsync(query, userId)
RAGCompanionSystem.TurnCoordinator.SetUserId(userId)
RAGCompanionSystem.ConversationState.GetUserFacts()
```

### **REST API**

```http
POST /query              # Query knowledge base
POST /memory/store       # Store user memory
POST /memory/query       # Query user memory
POST /conversation/log   # Log conversation turn
POST /ingest            # Ingest documents
GET  /health            # Service health check
```

## Requirements

### **Unity Requirements**
- Unity 6000.0.32f1 or later
- Newtonsoft.Json package
- Unity Input System package
- TextMeshPro package

### **System Requirements**
- Python 3.11+
- PostgreSQL 15+ with pgvector
- Docker and Docker Compose
- OpenAI API key

## License

This implementation follows the Unity-4o-mini-realtime-companion-rag-build-guide specifications for educational and development purposes.

---

## Build Status

- ✅ **Python RAG Microservice**: Complete with pgvector integration
- ✅ **Unity Realtime Session**: WebSocket implementation with audio streaming
- ✅ **Context Assembly**: Token budgeting and 4-layer memory system
- ✅ **Turn Coordination**: End-to-end conversation orchestration
- ✅ **Avatar Integration**: TTS playback with lip-sync animation
- ✅ **Memory Management**: Automatic fact extraction and storage
- ✅ **Performance Optimization**: Caching, batching, and latency controls
- ✅ **Testing Framework**: Comprehensive system validation

**System Status**: ✅ **Production Ready** - Full guide compliance achieved