"""
Example: pgvector Integration with Multimedia Storage System

This example demonstrates how to use your existing pgvector setup with the
multimedia storage system for semantic search and similarity matching.
"""

import asyncio
import uuid
from pathlib import Path
from typing import Dict, Any

from database import RAGDatabase
from multimedia_storage import MultimediaStorageService
from enhanced_mcp_tools import EnhancedMCPToolManager

# Mock embedding function - replace with your actual OpenAI embedding function
async def get_embedding(text: str) -> list:
    """Mock embedding function - replace with your actual OpenAI embedding"""
    # This is a placeholder - you should use your existing embedding function
    # from your RAG system
    import random
    return [random.uniform(-1, 1) for _ in range(1536)]

async def main():
    """Main example showing pgvector integration"""
    print("🚀 Starting pgvector Integration Example")
    
    # Initialize database with your existing pgvector setup
    db_url = "postgresql://username:password@localhost:5432/rag_db"
    database = RAGDatabase(db_url)
    
    try:
        # Initialize database tables (including multimedia embeddings)
        print("📊 Initializing database with pgvector support...")
        await database.initialize()
        print("✅ Database initialized successfully with pgvector")
    except Exception as e:
        print(f"⚠️ Database initialization failed: {e}")
        print("Continuing with file-only storage...")
        database = None
    
    # Initialize multimedia storage service
    print("📁 Initializing multimedia storage...")
    multimedia_storage = MultimediaStorageService(
        storage_dir="multimedia_pgvector_example",
        database=database
    )
    print("✅ Multimedia storage initialized")
    
    # Initialize enhanced MCP tool manager
    print("🔧 Initializing MCP tools...")
    mcp_manager = EnhancedMCPToolManager(multimedia_storage)
    print("✅ MCP tools initialized")
    
    # Example user and session
    user_id = "user_pgvector_123"
    session_id = str(uuid.uuid4())
    turn_id = str(uuid.uuid4())
    
    print(f"\n👤 User ID: {user_id}")
    print(f"💬 Session ID: {session_id}")
    print(f"🔄 Turn ID: {turn_id}")
    
    # Example 1: Generate content with embeddings
    print("\n🎨 Example 1: Generating content with vector embeddings...")
    
    # Generate an image
    image_result = await mcp_manager.execute_tool_with_storage(
        tool_name="image_generator",
        user_id=user_id,
        session_id=session_id,
        turn_id=turn_id,
        prompt="A serene mountain landscape with snow-capped peaks",
        style="realistic",
        size="1024x1024"
    )
    
    if image_result["success"]:
        content_id = image_result["content_id"]
        print(f"✅ Image generated: {image_result['message']}")
        print(f"   Content ID: {content_id}")
        
        # Store with embedding for semantic search
        if database:
            description = f"A serene mountain landscape with snow-capped peaks in realistic style"
            embedding = await get_embedding(description)
            
            await multimedia_storage.store_multimedia_embedding(
                content_id=content_id,
                user_id=user_id,
                description=description,
                embedding=embedding,
                content_type="image",
                metadata={
                    "style": "realistic",
                    "size": "1024x1024",
                    "prompt": "A serene mountain landscape with snow-capped peaks"
                }
            )
            print(f"   ✅ Vector embedding stored for semantic search")
    
    # Example 2: Generate more content for similarity testing
    print("\n🏔️ Example 2: Generating more content for similarity testing...")
    
    # Generate another mountain-related image
    image2_result = await mcp_manager.execute_tool_with_storage(
        tool_name="image_generator",
        user_id=user_id,
        session_id=session_id,
        turn_id=turn_id,
        prompt="Rocky mountain peaks with alpine meadows",
        style="realistic",
        size="1024x1024"
    )
    
    if image2_result["success"]:
        content_id2 = image2_result["content_id"]
        print(f"✅ Second image generated: {image2_result['message']}")
        
        # Store with embedding
        if database:
            description2 = f"Rocky mountain peaks with alpine meadows in realistic style"
            embedding2 = await get_embedding(description2)
            
            await multimedia_storage.store_multimedia_embedding(
                content_id=content_id2,
                user_id=user_id,
                description=description2,
                embedding=embedding2,
                content_type="image",
                metadata={
                    "style": "realistic",
                    "size": "1024x1024",
                    "prompt": "Rocky mountain peaks with alpine meadows"
                }
            )
    
    # Example 3: Generate audio content
    print("\n🎵 Example 3: Generating audio content with embeddings...")
    
    audio_result = await mcp_manager.execute_tool_with_storage(
        tool_name="audio_generator",
        user_id=user_id,
        session_id=session_id,
        turn_id=turn_id,
        text="Peaceful mountain meditation music with nature sounds",
        audio_type="ambient",
        style="calm"
    )
    
    if audio_result["success"]:
        audio_content_id = audio_result["content_id"]
        print(f"✅ Audio generated: {audio_result['message']}")
        
        # Store with embedding
        if database:
            audio_description = "Peaceful mountain meditation music with nature sounds in calm ambient style"
            audio_embedding = await get_embedding(audio_description)
            
            await multimedia_storage.store_multimedia_embedding(
                content_id=audio_content_id,
                user_id=user_id,
                description=audio_description,
                embedding=audio_embedding,
                content_type="audio",
                metadata={
                    "audio_type": "ambient",
                    "style": "calm",
                    "text": "Peaceful mountain meditation music with nature sounds"
                }
            )
    
    # Example 4: Semantic search using pgvector
    print("\n🔍 Example 4: Semantic search using pgvector...")
    
    if database:
        # Search for mountain-related content
        mountain_query = "mountain landscape nature"
        mountain_embedding = await get_embedding(mountain_query)
        
        print(f"   Searching for: '{mountain_query}'")
        
        mountain_results = await multimedia_storage.search_multimedia_by_similarity(
            query_embedding=mountain_embedding,
            user_id=user_id,
            top_k=5
        )
        
        print(f"   Found {len(mountain_results)} similar results:")
        for i, result in enumerate(mountain_results, 1):
            print(f"     {i}. {result['description'][:60]}...")
            print(f"        Type: {result['content_type']}, Score: {result['similarity_score']:.3f}")
        
        # Search for peaceful/calm content
        peaceful_query = "peaceful calm serene"
        peaceful_embedding = await get_embedding(peaceful_query)
        
        print(f"\n   Searching for: '{peaceful_query}'")
        
        peaceful_results = await multimedia_storage.search_multimedia_by_similarity(
            query_embedding=peaceful_embedding,
            user_id=user_id,
            top_k=3
        )
        
        print(f"   Found {len(peaceful_results)} peaceful results:")
        for i, result in enumerate(peaceful_results, 1):
            print(f"     {i}. {result['description'][:60]}...")
            print(f"        Type: {result['content_type']}, Score: {result['similarity_score']:.3f}")
    
    # Example 5: Content recommendations
    print("\n💡 Example 5: Content recommendations using similarity...")
    
    if database and image_result.get('content_id'):
        recommendations = await multimedia_storage.get_content_recommendations(
            user_id=user_id,
            content_id=image_result['content_id'],
            top_k=3
        )
        
        print(f"   Recommendations based on first image:")
        for i, rec in enumerate(recommendations, 1):
            print(f"     {i}. {rec['description'][:60]}...")
            print(f"        Type: {rec['content_type']}, Score: {rec['similarity_score']:.3f}")
    
    # Example 6: Find similar content
    print("\n🔗 Example 6: Finding similar content...")
    
    if database and image_result.get('content_id'):
        similar_content = await database.get_similar_multimedia_content(
            content_id=image_result['content_id'],
            top_k=3,
            user_id=user_id
        )
        
        print(f"   Content similar to first image:")
        for i, similar in enumerate(similar_content, 1):
            print(f"     {i}. {similar['description'][:60]}...")
            print(f"        Type: {similar['content_type']}, Score: {similar['similarity_score']:.3f}")
    
    # Example 7: Analytics with pgvector
    print("\n📊 Example 7: Analytics with pgvector integration...")
    
    if database:
        analytics = await database.get_multimedia_content_analytics(user_id=user_id)
        
        print(f"   Multimedia Content Analytics:")
        print(f"     Total content: {analytics.get('total_content', 0)}")
        print(f"     Content with embeddings: {analytics.get('with_embeddings', 0)}")
        print(f"     Generated content: {analytics.get('generated_content', 0)}")
        
        if analytics.get('by_type'):
            print(f"     Content by type:")
            for content_type, count in analytics['by_type'].items():
                print(f"       {content_type}: {count}")
    
    # Example 8: Cross-modal search
    print("\n🔄 Example 8: Cross-modal search (text to multimedia)...")
    
    if database:
        # Search for content that matches a text description
        search_text = "mountain nature peaceful"
        search_embedding = await get_embedding(search_text)
        
        print(f"   Searching for: '{search_text}'")
        
        cross_modal_results = await multimedia_storage.search_multimedia_by_similarity(
            query_embedding=search_embedding,
            user_id=user_id,
            top_k=5
        )
        
        print(f"   Cross-modal results:")
        for i, result in enumerate(cross_modal_results, 1):
            print(f"     {i}. [{result['content_type'].upper()}] {result['description'][:50]}...")
            print(f"        Similarity: {result['similarity_score']:.3f}")
    
    print("\n🎉 pgvector Integration Example completed successfully!")
    
    # Show storage statistics
    storage_stats = multimedia_storage.get_storage_stats()
    print(f"\n📈 Final Storage Statistics:")
    print(f"   Total files: {storage_stats['total_files']}")
    print(f"   Total size: {storage_stats['total_size']} bytes")
    
    for content_type, stats in storage_stats['by_type'].items():
        if stats['count'] > 0:
            print(f"   {content_type.capitalize()}: {stats['count']} files, {stats['size']} bytes")

if __name__ == "__main__":
    # Run the example
    asyncio.run(main())

