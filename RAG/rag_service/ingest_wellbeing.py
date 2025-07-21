#!/usr/bin/env python3
"""
Script to ingest the wellbeing framework into the RAG database.
This will make the framework searchable and accessible during conversations.
"""

import asyncio
import os
from pathlib import Path
from dotenv import load_dotenv

from database import RAGDatabase
from chunker import DocumentChunker, DocumentProcessor

# Load environment variables
load_dotenv()

async def ingest_wellbeing_framework():
    """Ingest the wellbeing framework into the RAG database"""
    
    # Initialize database
    db = RAGDatabase(os.getenv("DATABASE_URL", "postgresql://user:password@localhost/rag_db"))
    await db.initialize()
    
    # Initialize chunker and processor
    chunker = DocumentChunker()
    processor = DocumentProcessor(chunker)
    
    # Process the wellbeing framework document
    framework_path = Path("wellbeing_framework.md")
    
    if not framework_path.exists():
        print(f"Error: {framework_path} not found")
        return
    
    print(f"Processing {framework_path}...")
    
    try:
        # Process the document into chunks
        chunks = processor.process_file(framework_path)
        print(f"Generated {len(chunks)} chunks from the wellbeing framework")
        
        # Get embeddings and store in database
        from rag_api import get_embedding
        
        db_chunks = []
        for i, (chunk_text, metadata) in enumerate(chunks):
            print(f"Processing chunk {i+1}/{len(chunks)}...")
            
            # Get embedding for this chunk
            embedding = get_embedding(chunk_text)
            
            # Create chunk record
            chunk_record = {
                "chunk_id": f"wellbeing_framework_{i}",
                "embedding": embedding,
                "text": chunk_text,
                "doc_title": "Comprehensive Wellbeing Framework",
                "section": metadata.get("section", "General"),
                "tags": ["wellbeing", "maslow", "personal_development", "self_assessment", "framework"],
                "lang": "en",
                "user_scope": "global",
                "safety_level": "public",
                "token_estimate": metadata["token_estimate"],
                "source_path": str(framework_path)
            }
            db_chunks.append(chunk_record)
        
        # Insert chunks into database
        print("Storing chunks in database...")
        await db.upsert_chunks(db_chunks)
        
        print(f"✅ Successfully ingested {len(db_chunks)} chunks from the wellbeing framework")
        print("The framework is now searchable in your RAG system!")
        
    except Exception as e:
        print(f"Error processing wellbeing framework: {e}")
        raise
    
    finally:
        await db.close()

if __name__ == "__main__":
    asyncio.run(ingest_wellbeing_framework())