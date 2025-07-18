# RAG Companion Service

A FastAPI microservice for Retrieval-Augmented Generation (RAG) with OpenAI embeddings and pgvector storage.

## Features

- **Document Ingestion**: Process text and markdown files with intelligent chunking
- **Vector Search**: Semantic search using OpenAI text-embedding-3-small
- **Memory Management**: 4-layer memory system (working, episodic, semantic, profile)
- **Conversation Logging**: Track conversation turns and retrieved contexts
- **User Scoping**: Multi-user support with data isolation
- **Safety Filtering**: Content safety levels and filtering

## Quick Start

### Using Docker (Recommended)

1. Copy environment file:
```bash
cp .env.example .env
```

2. Add your OpenAI API key to `.env`:
```
OPENAI_API_KEY=your_api_key_here
```

3. Start services:
```bash
docker-compose up -d
```

4. The API will be available at `http://localhost:8077`

### Manual Setup

1. Install PostgreSQL with pgvector extension
2. Create database:
```sql
CREATE DATABASE rag_db;
```

3. Install dependencies:
```bash
pip install -r requirements.txt
```

4. Set environment variables:
```bash
export DATABASE_URL="postgresql://username:password@localhost:5432/rag_db"
export OPENAI_API_KEY="your_api_key_here"
```

5. Start the service:
```bash
python rag_api.py
```

## API Endpoints

### Document Retrieval

**POST /query**
```json
{
  "query": "How does the energy system work?",
  "user_id": "u-12345",
  "top_k": 5,
  "filters": {
    "user_scope": ["global", "u-12345"],
    "safety_level": ["public"]
  }
}
```

### Memory Management

**POST /memory/store**
```json
{
  "user_id": "u-12345",
  "memory_type": "semantic",
  "content": "User prefers morning workouts",
  "metadata": {"category": "fitness"}
}
```

**POST /memory/query**
```json
{
  "user_id": "u-12345",
  "query": "workout preferences",
  "memory_types": ["semantic", "episodic"],
  "top_k": 3
}
```

### Document Ingestion

**POST /ingest**
```json
{
  "directory_path": "/path/to/documents",
  "user_scope": "global",
  "safety_level": "public"
}
```

### Conversation Logging

**POST /conversation/log**
```json
{
  "turn_id": "turn_123",
  "session_id": "session_456",
  "user_id": "u-12345",
  "turn_index": 1,
  "user_message": "Hello",
  "assistant_response": "Hi there!",
  "retrieved_chunks": ["chunk_1", "chunk_2"]
}
```

## Document Ingestion

### Using the API
Send a POST request to `/ingest` with the directory path containing your documents.

### Using the CLI script
```bash
python ingest.py /path/to/documents
```

### Supported Formats
- `.txt` - Plain text files
- `.md` - Markdown files with section awareness

## Memory System

The service implements a 4-layer memory architecture:

1. **Working Context**: Active session conversation history
2. **Episodic Memory**: Specific events and interactions
3. **Semantic Memory**: General knowledge and preferences
4. **Declarative Profile**: Structured user facts

## Development

### Running Tests
```bash
pytest tests/
```

### API Documentation
Visit `http://localhost:8077/docs` for interactive API documentation.

### Database Schema
The service creates the following tables:
- `rag_chunks`: Document chunks with embeddings
- `user_memory`: User-specific memory with embeddings
- `conversation_sessions`: Conversation session metadata
- `conversation_turns`: Individual conversation turns

## Configuration

### Environment Variables
- `OPENAI_API_KEY`: OpenAI API key for embeddings
- `DATABASE_URL`: PostgreSQL connection string
- `SERVICE_HOST`: Service host (default: 0.0.0.0)
- `SERVICE_PORT`: Service port (default: 8077)

### Chunking Parameters
- `tokens_per_chunk`: 600 (adjustable in chunker.py)
- `overlap`: 80 tokens (adjustable in chunker.py)
- `embedding_model`: text-embedding-3-small

## Production Considerations

1. **Security**: Implement authentication and authorization
2. **Rate Limiting**: Add rate limiting for API endpoints
3. **Monitoring**: Add structured logging and metrics
4. **Scaling**: Consider read replicas for database
5. **Caching**: Implement Redis for embedding cache
6. **Backup**: Regular database backups

## Troubleshooting

### Common Issues

1. **Database Connection**: Check DATABASE_URL and PostgreSQL service
2. **Missing pgvector**: Ensure pgvector extension is installed
3. **OpenAI API**: Verify API key and quota limits
4. **Memory Issues**: Monitor embedding cache size

### Logs
Check application logs for detailed error information:
```bash
docker-compose logs rag_service
```