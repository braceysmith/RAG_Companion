"""
Example: Conversation Reconstruction with Media Content

This example demonstrates how to fully reconstruct previous conversations
including all generated multimedia content when a user logs in.
"""

import asyncio
import uuid
from pathlib import Path
from typing import Dict, Any

from database import RAGDatabase
from multimedia_storage import MultimediaStorageService
from enhanced_mcp_tools import EnhancedMCPToolManager
from conversation_reconstructor import ConversationReconstructor

async def main():
    """Main example showing conversation reconstruction"""
    print("🚀 Starting Conversation Reconstruction Example")
    
    # Initialize database
    db_url = "postgresql://username:password@localhost:5432/rag_db"
    database = RAGDatabase(db_url)
    
    try:
        await database.initialize()
        print("✅ Database initialized successfully")
    except Exception as e:
        print(f"⚠️ Database initialization failed: {e}")
        print("Continuing with file-only storage...")
        database = None
    
    # Initialize services
    multimedia_storage = MultimediaStorageService(
        storage_dir="conversation_reconstruction_example",
        database=database
    )
    
    mcp_manager = EnhancedMCPToolManager(multimedia_storage)
    
    reconstructor = ConversationReconstructor(database, multimedia_storage)
    
    print("✅ All services initialized")
    
    # Example user
    user_id = "user_reconstruction_123"
    
    # Simulate a previous conversation session
    print("\n💬 Simulating a previous conversation session...")
    
    session_id = str(uuid.uuid4())
    turn_id = str(uuid.uuid4())
    
    # Simulate user asking for content generation
    if database:
        # Store user message
        await database.store_conversation_content(
            content_id=str(uuid.uuid4()),
            turn_id=turn_id,
            user_id=user_id,
            content_type="text",
            content_data="Can you generate an image of a mountain landscape and some peaceful audio?",
            content_order=0,
            is_user_content=True
        )
        
        # Generate image
        image_result = await mcp_manager.execute_tool_with_storage(
            tool_name="image_generator",
            user_id=user_id,
            session_id=session_id,
            turn_id=turn_id,
            prompt="A serene mountain landscape with snow-capped peaks and alpine meadows",
            style="realistic",
            size="1024x1024"
        )
        
        if image_result["success"]:
            print(f"✅ Generated image: {image_result['message']}")
            
            # Store assistant response
            await database.store_conversation_content(
                content_id=str(uuid.uuid4()),
                turn_id=turn_id,
                user_id=user_id,
                content_type="text",
                content_data="I've generated a beautiful mountain landscape image for you!",
                content_order=1,
                is_user_content=False
            )
            
            # Store image reference
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
        
        # Generate audio
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
            print(f"✅ Generated audio: {audio_result['message']}")
            
            # Store audio reference
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
        
        # Store session
        await database.log_conversation_turn(
            turn_id=turn_id,
            session_id=session_id,
            user_id=user_id,
            turn_index=0,
            user_message="Can you generate an image of a mountain landscape and some peaceful audio?",
            assistant_response="I've generated a beautiful mountain landscape image and peaceful audio for you!",
            retrieved_chunks=[],
            metadata={"content_generated": True}
        )
        
        print("✅ Previous conversation session simulated")
    
    # Now simulate user logging in and reconstructing conversation
    print("\n🔐 Simulating user login and conversation reconstruction...")
    
    # Get user's conversation history
    user_conversations = await reconstructor.reconstruct_user_conversations(user_id, limit=5)
    
    print(f"📚 Found {len(user_conversations)} previous conversations")
    
    if user_conversations:
        latest_conversation = user_conversations[0]
        print(f"\n📖 Latest conversation details:")
        print(f"   Session ID: {latest_conversation.session_id}")
        print(f"   Started: {latest_conversation.started_at}")
        print(f"   Total Messages: {latest_conversation.total_messages}")
        print(f"   Total Media: {latest_conversation.total_multimedia}")
        print(f"   MCP Tools Used: {', '.join(latest_conversation.mcp_tools_used)}")
        
        # Show conversation content
        print(f"\n💬 Conversation content:")
        for i, message in enumerate(latest_conversation.messages, 1):
            timestamp = message.timestamp.strftime('%H:%M:%S')
            sender = "👤 User" if message.is_user_message else "🤖 Assistant"
            
            print(f"\n   Message {i} - {timestamp}")
            print(f"   {sender}:")
            
            if message.text_content:
                print(f"     Text: {message.text_content}")
            
            if message.mcp_tool_used:
                print(f"     Tool: {message.mcp_tool_used}")
                if message.tool_parameters:
                    print(f"     Parameters: {message.tool_parameters}")
            
            if message.multimedia_content:
                print(f"     Media ({len(message.multimedia_content)} items):")
                for media in message.multimedia_content:
                    print(f"       • {media['content_type']}: {media['file_name']}")
                    print(f"         Size: {media['file_size']} bytes")
                    print(f"         Generated: {media['is_generated']}")
                    if media['generation_prompt']:
                        print(f"         Prompt: {media['generation_prompt'][:60]}...")
                    print(f"         File: {media['file_path']}")
    
    # Example: Reconstruct conversation with accessible media files
    print("\n📁 Reconstructing conversation with accessible media files...")
    
    if user_conversations:
        session_id = user_conversations[0].session_id
        
        # Create output directory for media files
        output_dir = f"reconstructed_media_{session_id}"
        
        reconstruction_result = await reconstructor.reconstruct_conversation_with_media_files(
            session_id=session_id,
            output_dir=output_dir
        )
        
        if reconstruction_result:
            print(f"✅ Conversation reconstructed successfully!")
            print(f"   Media count: {reconstruction_result['media_count']}")
            print(f"   Output directory: {output_dir}")
            
            # Show accessible media
            if reconstruction_result['accessible_media']:
                print(f"\n📱 Accessible media files:")
                for media in reconstruction_result['accessible_media']:
                    print(f"   • {media['content_type']}: {media['file_name']}")
                    print(f"     Original: {media['original_path']}")
                    print(f"     Accessible: {media['accessible_path']}")
                    print(f"     Generated by: {media['generation_tool']}")
                    if media['generation_prompt']:
                        print(f"     Prompt: {media['generation_prompt'][:50]}...")
    
    # Example: Export conversation to different formats
    print("\n📤 Exporting conversation to different formats...")
    
    if user_conversations:
        session_id = user_conversations[0].session_id
        
        # Export to JSON
        json_export = await reconstructor.export_conversation(
            session_id=session_id,
            export_format="json",
            include_media=True
        )
        
        if json_export:
            json_file = f"conversation_export_{session_id}.json"
            with open(json_file, 'w') as f:
                f.write(json_export)
            print(f"✅ Exported to JSON: {json_file}")
        
        # Export to Markdown
        md_export = await reconstructor.export_conversation(
            session_id=session_id,
            export_format="markdown",
            include_media=True
        )
        
        if md_export:
            md_file = f"conversation_export_{session_id}.md"
            with open(md_file, 'w') as f:
                f.write(md_export)
            print(f"✅ Exported to Markdown: {md_file}")
        
        # Export to HTML
        html_export = await reconstructor.export_conversation(
            session_id=session_id,
            export_format="html",
            include_media=True
        )
        
        if html_export:
            html_file = f"conversation_export_{session_id}.html"
            with open(html_file, 'w') as f:
                f.write(html_export)
            print(f"✅ Exported to HTML: {html_file}")
    
    # Example: Search and filter conversations
    print("\n🔍 Example: Searching and filtering conversations...")
    
    if database:
        # Get all multimedia content for the user
        user_content = multimedia_storage.get_user_content(user_id)
        print(f"   User has {len(user_content)} multimedia files:")
        
        for content in user_content:
            print(f"     • {content.content_type}: {content.file_name}")
            print(f"       Generated: {content.is_generated}")
            if content.generation_tool:
                print(f"       Tool: {content.generation_tool}")
            if content.generation_prompt:
                print(f"       Prompt: {content.generation_prompt[:40]}...")
            print(f"       File: {content.file_path}")
            print()
    
    print("\n🎉 Conversation Reconstruction Example completed successfully!")
    
    # Show final statistics
    if database:
        analytics = await database.get_multimedia_content_analytics(user_id=user_id)
        print(f"\n📊 Final User Analytics:")
        print(f"   Total content: {analytics.get('total_content', 0)}")
        print(f"   Content with embeddings: {analytics.get('with_embeddings', 0)}")
        print(f"   Generated content: {analytics.get('generated_content', 0)}")
        
        if analytics.get('by_type'):
            print(f"   Content by type:")
            for content_type, count in analytics['by_type'].items():
                print(f"     {content_type}: {count}")

if __name__ == "__main__":
    # Run the example
    asyncio.run(main())
