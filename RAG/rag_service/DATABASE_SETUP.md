# Database Setup for RAG Companion

The RAG system uses PostgreSQL with the pgvector extension for vector similarity search.

## Current Status
- ❌ Database not configured (using in-memory fallback)
- ✅ Reminder system working with in-memory storage
- ✅ User profiles working with in-memory storage

## Railway Setup (Recommended)

1. **Add PostgreSQL Service to Railway:**
   ```bash
   # In your Railway project dashboard
   # Click "New" -> "Database" -> "PostgreSQL"
   ```

2. **Enable pgvector extension:**
   ```sql
   CREATE EXTENSION IF NOT EXISTS vector;
   ```

3. **Set environment variable in Railway:**
   ```
   DATABASE_URL=postgresql://user:password@host:port/database
   ```
   (Railway will provide this automatically)

## Local Development Setup

1. **Install PostgreSQL and pgvector:**
   ```bash
   # macOS
   brew install postgresql pgvector
   
   # Start PostgreSQL
   brew services start postgresql
   ```

2. **Create database:**
   ```bash
   createdb rag_db
   psql rag_db -c "CREATE EXTENSION IF NOT EXISTS vector;"
   ```

3. **Set environment variable:**
   ```bash
   export DATABASE_URL="postgresql://localhost:5432/rag_db"
   ```

## Benefits of Vector Database

When enabled, you get:
- ✅ **Semantic search** - Find relevant information by meaning, not just keywords
- ✅ **Long-term memory** - Persistent storage of conversations and knowledge
- ✅ **Smart retrieval** - AI can find related information from past conversations
- ✅ **Scalability** - Handle large amounts of documents and conversations

## Current Fallback Mode

Without the database, the system uses:
- ❌ Simple keyword matching instead of semantic search
- ❌ In-memory storage (data lost on restart)
- ✅ All core features still work (reminders, conversations, memory)

## Check Status

Visit `/health` endpoint to see current database status:
```json
{
  "database_available": false,
  "vector_search": "fallback_mode"
}
```