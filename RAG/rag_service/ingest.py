#!/usr/bin/env python3
"""
Standalone ingestion script for RAG documents
Usage: python ingest.py [data_directory]
"""

import os
import sys
import asyncio
from pathlib import Path
from dotenv import load_dotenv
from openai import OpenAI

from database import RAGDatabase
from chunker import DocumentChunker, DocumentProcessor

# Load environment variables
load_dotenv()

# Configuration
DATA_DIR = Path("data/raw")
DB_URL = os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db")
OPENAI_API_KEY = os.getenv("OPENAI_API_KEY")

async def main():
    if len(sys.argv) > 1:
        data_dir = Path(sys.argv[1])
    else:
        data_dir = DATA_DIR
    
    if not data_dir.exists():
        print(f"Data directory {data_dir} does not exist")
        sys.exit(1)
    
    print(f"Starting ingestion from {data_dir}")
    
    # Initialize components
    client = OpenAI(api_key=OPENAI_API_KEY)
    db = RAGDatabase(DB_URL)
    chunker = DocumentChunker()
    processor = DocumentProcessor(chunker)
    
    # Initialize database
    await db.initialize()
    
    # Embedding cache
    embedding_cache = {}
    
    async def get_embedding(text: str) -> list:
        """Get embedding with caching"""
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
            embedding_cache[text_hash] = embedding
            return embedding
        except Exception as e:
            print(f"Embedding error: {e}")
            raise
    
    # Process files
    supported_extensions = {'.txt', '.md'}
    processed_files = 0
    total_chunks = 0
    
    for file_path in data_dir.rglob('*'):
        if file_path.is_file() and file_path.suffix.lower() in supported_extensions:
            try:
                print(f"Processing {file_path}")
                chunks = processor.process_file(file_path)
                
                if not chunks:
                    print(f"No chunks generated for {file_path}")
                    continue
                
                # Process chunks in batches
                batch_size = 10
                for i in range(0, len(chunks), batch_size):
                    batch = chunks[i:i + batch_size]
                    
                    # Prepare chunks for database
                    db_chunks = []
                    for j, (chunk_text, metadata) in enumerate(batch):
                        # Get embedding
                        embedding = await get_embedding(chunk_text)
                        
                        # Create chunk record
                        chunk_id = f"{file_path.stem}_{i//batch_size}_{j}"
                        chunk_record = {
                            "chunk_id": chunk_id,
                            "embedding": embedding,
                            "text": chunk_text,
                            "doc_title": metadata["doc_title"],
                            "section": metadata["section"],
                            "tags": metadata["tags"],
                            "lang": metadata["lang"],
                            "user_scope": metadata["user_scope"],
                            "safety_level": metadata["safety_level"],
                            "token_estimate": metadata["token_estimate"],
                            "source_path": metadata["source_path"]
                        }
                        db_chunks.append(chunk_record)
                    
                    # Insert batch into database
                    await db.upsert_chunks(db_chunks)
                    print(f"  Inserted batch {i//batch_size + 1} ({len(db_chunks)} chunks)")
                
                processed_files += 1
                total_chunks += len(chunks)
                print(f"Completed {file_path} - {len(chunks)} chunks")
                
            except Exception as e:
                print(f"Error processing {file_path}: {e}")
    
    print(f"\nIngestion complete:")
    print(f"  Files processed: {processed_files}")
    print(f"  Total chunks: {total_chunks}")
    print(f"  Cached embeddings: {len(embedding_cache)}")

if __name__ == "__main__":
    asyncio.run(main())