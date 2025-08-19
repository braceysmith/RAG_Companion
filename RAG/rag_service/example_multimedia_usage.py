"""
Example Usage of Multimedia Storage System with MCP Tools

This example demonstrates how to:
1. Initialize the multimedia storage system
2. Use MCP tools to generate content
3. Store and retrieve multimedia content
4. Track conversation content with multimedia references
5. Query and analyze stored content
"""

import asyncio
import uuid
from pathlib import Path
from typing import Dict, Any

from database import RAGDatabase
from multimedia_storage import MultimediaStorageService
from enhanced_mcp_tools import EnhancedMCPToolManager

async def main():
    """Main example function"""
    print("🚀 Starting Multimedia Storage System Example")
    
    # Initialize database (you'll need to set up your database connection)
    db_url = "postgresql://username:password@localhost:5432/rag_db"
    database = RAGDatabase(db_url)
    
    try:
        # Initialize database tables
        print("📊 Initializing database...")
        await database.initialize()
        print("✅ Database initialized successfully")
    except Exception as e:
        print(f"⚠️ Database initialization failed: {e}")
        print("Continuing with file-only storage...")
        database = None
    
    # Initialize multimedia storage service
    print("📁 Initializing multimedia storage...")
    multimedia_storage = MultimediaStorageService(
        storage_dir="multimedia_example",
        database=database
    )
    print("✅ Multimedia storage initialized")
    
    # Initialize enhanced MCP tool manager
    print("🔧 Initializing MCP tools...")
    mcp_manager = EnhancedMCPToolManager(multimedia_storage)
    print("✅ MCP tools initialized")
    
    # Example user and session
    user_id = "user_123"
    session_id = str(uuid.uuid4())
    turn_id = str(uuid.uuid4())
    
    print(f"\n👤 User ID: {user_id}")
    print(f"💬 Session ID: {session_id}")
    print(f"🔄 Turn ID: {turn_id}")
    
    # Example 1: Generate an image using MCP tool
    print("\n🎨 Example 1: Generating an image...")
    image_result = await mcp_manager.execute_tool_with_storage(
        tool_name="image_generator",
        user_id=user_id,
        session_id=session_id,
        turn_id=turn_id,
        prompt="A beautiful sunset over mountains with a lake in the foreground",
        style="realistic",
        size="1024x1024",
        quality="hd"
    )
    
    if image_result["success"]:
        print(f"✅ Image generated: {image_result['message']}")
        print(f"   Content ID: {image_result.get('content_id', 'N/A')}")
        print(f"   Metadata: {image_result['metadata']}")
    else:
        print(f"❌ Image generation failed: {image_result['error']}")
    
    # Example 2: Generate audio using MCP tool
    print("\n🎵 Example 2: Generating audio...")
    audio_result = await mcp_manager.execute_tool_with_storage(
        tool_name="audio_generator",
        user_id=user_id,
        session_id=session_id,
        turn_id=turn_id,
        text="Welcome to our conversation! I'm excited to help you today.",
        audio_type="speech",
        voice="alloy",
        style="upbeat"
    )
    
    if audio_result["success"]:
        print(f"✅ Audio generated: {audio_result['message']}")
        print(f"   Content ID: {audio_result.get('content_id', 'N/A')}")
        print(f"   Metadata: {audio_result['metadata']}")
    else:
        print(f"❌ Audio generation failed: {audio_result['error']}")
    
    # Example 3: Process a document using MCP tool
    print("\n📄 Example 3: Processing a document...")
    
    # Create a sample document for processing
    sample_doc_path = "sample_document.txt"
    with open(sample_doc_path, 'w') as f:
        f.write("This is a sample document for testing the document processing tool.\n")
        f.write("It contains multiple lines of text that can be processed.\n")
        f.write("The tool should be able to extract text, summarize, and analyze this content.\n")
    
    doc_result = await mcp_manager.execute_tool_with_storage(
        tool_name="document_processor",
        user_id=user_id,
        session_id=session_id,
        turn_id=turn_id,
        file_path=sample_doc_path,
        operation="summarize",
        output_format="txt"
    )
    
    if doc_result["success"]:
        print(f"✅ Document processed: {doc_result['message']}")
        print(f"   Content ID: {doc_result.get('content_id', 'N/A')}")
        print(f"   Metadata: {doc_result['metadata']}")
    else:
        print(f"❌ Document processing failed: {doc_result['error']}")
    
    # Clean up sample document
    if Path(sample_doc_path).exists():
        Path(sample_doc_path).unlink()
    
    # Example 4: Store conversation content with multimedia references
    print("\n💬 Example 4: Storing conversation content...")
    
    if database:
        # Store user message
        await database.store_conversation_content(
            content_id=str(uuid.uuid4()),
            turn_id=turn_id,
            user_id=user_id,
            content_type="text",
            content_data="Can you generate an image of a sunset and some audio for me?",
            content_order=0,
            is_user_content=True
        )
        
        # Store assistant response with multimedia references
        await database.store_conversation_content(
            content_id=str(uuid.uuid4()),
            turn_id=turn_id,
            user_id=user_id,
            content_type="text",
            content_data="I've generated a beautiful sunset image and some audio for you!",
            content_order=1,
            is_user_content=False
        )
        
        # Store multimedia content references
        if image_result.get('content_id'):
            await database.store_conversation_content(
                content_id=str(uuid.uuid4()),
                turn_id=turn_id,
                user_id=user_id,
                content_type="image",
                multimedia_id=image_result['content_id'],
                content_order=2,
                is_user_content=False,
                mcp_tool_used="image_generator"
            )
        
        if audio_result.get('content_id'):
            await database.store_conversation_content(
                content_id=str(uuid.uuid4()),
                turn_id=turn_id,
                user_id=user_id,
                content_type="audio",
                multimedia_id=audio_result['content_id'],
                content_order=3,
                is_user_content=False,
                mcp_tool_used="audio_generator"
            )
        
        print("✅ Conversation content stored successfully")
    
    # Example 5: Query and analyze stored content
    print("\n🔍 Example 5: Querying stored content...")
    
    # Get all multimedia content for the user
    user_content = multimedia_storage.get_user_content(user_id)
    print(f"📊 User has {len(user_content)} multimedia files:")
    
    for content in user_content:
        print(f"   • {content.content_type}: {content.file_name}")
        print(f"     Size: {content.file_size} bytes")
        print(f"     Generated: {content.is_generated}")
        if content.generation_tool:
            print(f"     Tool: {content.generation_tool}")
        print(f"     Tags: {', '.join(content.tags)}")
        print()
    
    # Get storage statistics
    storage_stats = multimedia_storage.get_storage_stats()
    print(f"📈 Storage Statistics:")
    print(f"   Total files: {storage_stats['total_files']}")
    print(f"   Total size: {storage_stats['total_size']} bytes")
    
    for content_type, stats in storage_stats['by_type'].items():
        print(f"   {content_type.capitalize()}: {stats['count']} files, {stats['size']} bytes")
    
    # Example 6: Get MCP tool execution history
    print("\n📋 Example 6: MCP Tool Execution History...")
    
    tool_history = mcp_manager.get_tool_execution_history()
    print(f"📊 Total tool executions: {len(tool_history)}")
    
    for execution in tool_history[:3]:  # Show last 3 executions
        print(f"   • {execution['tool_name']}: {execution['success']}")
        print(f"     Time: {execution['execution_time_ms']}ms")
        print(f"     User: {execution['user_id']}")
        if execution.get('error_message'):
            print(f"     Error: {execution['error_message']}")
        print()
    
    # Example 7: Retrieve specific content
    print("\n📥 Example 7: Retrieving specific content...")
    
    if user_content:
        first_content = user_content[0]
        retrieved_content = multimedia_storage.get_content(first_content.content_id)
        
        if retrieved_content:
            print(f"✅ Retrieved content: {retrieved_content.file_name}")
            print(f"   Type: {retrieved_content.content_type}")
            print(f"   Path: {retrieved_content.file_path}")
            print(f"   MIME: {retrieved_content.mime_type}")
            print(f"   Hash: {retrieved_content.content_hash[:16]}...")
        else:
            print("❌ Failed to retrieve content")
    
    print("\n🎉 Example completed successfully!")
    
    # Optional: Clean up orphaned files
    print("\n🧹 Cleaning up orphaned files...")
    cleaned_count = multimedia_storage.cleanup_orphaned_files()
    print(f"✅ Cleaned up {cleaned_count} orphaned files")

if __name__ == "__main__":
    # Run the example
    asyncio.run(main())
