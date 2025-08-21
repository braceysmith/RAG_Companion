#!/usr/bin/env python3
"""
Test script for conversation database functionality

This script tests:
1. Database connection
2. Table creation
3. Conversation session management
4. Conversation turn storage
5. Content storage
6. Retrieval and search
"""

import asyncio
import os
import sys
from datetime import datetime
from dotenv import load_dotenv

# Add the current directory to Python path
sys.path.append(os.path.dirname(os.path.abspath(__file__)))

from database import RAGDatabase
from conversation_manager import ConversationManager

# Load environment variables
load_dotenv()

async def test_database_connection():
    """Test basic database connection"""
    print("🔍 Testing database connection...")
    
    database_url = os.getenv("DATABASE_URL")
    if not database_url:
        print("❌ DATABASE_URL not set in environment")
        return False
    
    try:
        db = RAGDatabase(database_url)
        await db.initialize()
        print("✅ Database connection successful")
        return db
    except Exception as e:
        print(f"❌ Database connection failed: {e}")
        return False

async def test_conversation_manager(db):
    """Test conversation manager functionality"""
    print("\n🔍 Testing conversation manager...")
    
    try:
        cm = ConversationManager(db)
        print("✅ Conversation manager created")
        return cm
    except Exception as e:
        print(f"❌ Conversation manager creation failed: {e}")
        return False

async def test_conversation_session(cm):
    """Test conversation session creation and management"""
    print("\n🔍 Testing conversation session management...")
    
    try:
        # Test user ID
        test_user_id = "test_user_001"
        
        # Start a new session
        session_id = await cm.start_conversation_session(
            user_id=test_user_id,
            metadata={"test": True, "source": "test_script"}
        )
        print(f"✅ Created session: {session_id}")
        
        # Verify session is active
        active_session = await cm.get_active_session(test_user_id)
        if active_session == session_id:
            print("✅ Session is active")
        else:
            print(f"❌ Session not active: expected {session_id}, got {active_session}")
            return False
        
        return session_id, test_user_id
        
    except Exception as e:
        print(f"❌ Session management failed: {e}")
        return False

async def test_conversation_turn(cm, session_id, user_id):
    """Test conversation turn storage"""
    print("\n🔍 Testing conversation turn storage...")
    
    try:
        # Test messages
        user_message = "Hello! Can you help me with a question about machine learning?"
        ai_response = "Of course! I'd be happy to help you with machine learning questions. What would you like to know?"
        
        # Add conversation turn
        turn_id = await cm.add_conversation_turn(
            session_id=session_id,
            user_id=user_id,
            user_message=user_message,
            assistant_response=ai_response,
            retrieved_chunks=["chunk_001", "chunk_002"],
            metadata={"test": True, "turn_type": "initial_greeting"}
        )
        print(f"✅ Added conversation turn: {turn_id}")
        
        # Add content for user message
        content_id_1 = await cm.add_conversation_content(
            turn_id=turn_id,
            user_id=user_id,
            content_type="text",
            content_data=user_message,
            content_order=0,
            is_user_content=True
        )
        print(f"✅ Added user content: {content_id_1}")
        
        # Add content for AI response
        content_id_2 = await cm.add_conversation_content(
            turn_id=turn_id,
            user_id=user_id,
            content_type="text",
            content_data=ai_response,
            content_order=1,
            is_user_content=False
        )
        print(f"✅ Added AI content: {content_id_2}")
        
        return turn_id
        
    except Exception as e:
        print(f"❌ Conversation turn storage failed: {e}")
        return False

async def test_conversation_retrieval(cm, user_id):
    """Test conversation retrieval and search"""
    print("\n🔍 Testing conversation retrieval...")
    
    try:
        # Get conversation history
        history = await cm.get_conversation_history(user_id, limit=5)
        print(f"✅ Retrieved {len(history)} conversation sessions")
        
        if history:
            session = history[0]
            print(f"   Session: {session['session']['session_id']}")
            print(f"   Turns: {len(session['turns'])}")
            
            for i, turn_data in enumerate(session['turns']):
                turn = turn_data['turn']
                content = turn_data['content']
                print(f"   Turn {i+1}: {len(content)} content items")
        
        # Test conversation context
        context = await cm.get_conversation_context(user_id, context_length=3)
        print(f"✅ Retrieved conversation context ({len(context)} characters)")
        
        # Test search
        search_results = await cm.search_conversation_history(user_id, "machine learning", limit=3)
        print(f"✅ Search returned {len(search_results)} results")
        
        return True
        
    except Exception as e:
        print(f"❌ Conversation retrieval failed: {e}")
        return False

async def test_session_summary(cm, session_id):
    """Test session summary functionality"""
    print("\n🔍 Testing session summary...")
    
    try:
        summary = await cm.get_session_summary(session_id)
        if summary:
            print("✅ Session summary retrieved:")
            print(f"   User: {summary['user_id']}")
            print(f"   Started: {summary['started_at']}")
            print(f"   Total turns: {summary['total_turns']}")
            print(f"   Content stats: {summary['content_stats']}")
            print(f"   MCP tools: {summary['mcp_tools_used']}")
        else:
            print("❌ No session summary found")
            return False
        
        return True
        
    except Exception as e:
        print(f"❌ Session summary failed: {e}")
        return False

async def test_cleanup(cm, session_id):
    """Test session cleanup"""
    print("\n🔍 Testing session cleanup...")
    
    try:
        # End the session
        success = await cm.end_conversation_session(session_id)
        if success:
            print("✅ Session ended successfully")
        else:
            print("❌ Failed to end session")
            return False
        
        # Verify session is no longer active
        active_session = await cm.get_active_session("test_user_001")
        if active_session is None:
            print("✅ Session is no longer active")
        else:
            print(f"❌ Session still active: {active_session}")
            return False
        
        return True
        
    except Exception as e:
        print(f"❌ Session cleanup failed: {e}")
        return False

async def main():
    """Main test function"""
    print("🚀 Starting conversation database tests...")
    print("=" * 50)
    
    # Test database connection
    db = await test_database_connection()
    if not db:
        print("\n❌ Database tests failed - cannot continue")
        return
    
    # Test conversation manager
    cm = await test_conversation_manager(db)
    if not cm:
        print("\n❌ Conversation manager tests failed - cannot continue")
        return
    
    # Test session management
    session_result = await test_conversation_session(cm)
    if not session_result:
        print("\n❌ Session management tests failed - cannot continue")
        return
    
    session_id, user_id = session_result
    
    # Test conversation turn storage
    turn_id = await test_conversation_turn(cm, session_id, user_id)
    if not turn_id:
        print("\n❌ Conversation turn tests failed - cannot continue")
        return
    
    # Test retrieval and search
    retrieval_success = await test_conversation_retrieval(cm, user_id)
    if not retrieval_success:
        print("\n❌ Conversation retrieval tests failed")
    
    # Test session summary
    summary_success = await test_session_summary(cm, session_id)
    if not summary_success:
        print("\n❌ Session summary tests failed")
    
    # Test cleanup
    cleanup_success = await test_cleanup(cm, session_id)
    if not cleanup_success:
        print("\n❌ Session cleanup tests failed")
    
    print("\n" + "=" * 50)
    if all([retrieval_success, summary_success, cleanup_success]):
        print("🎉 All conversation database tests passed!")
    else:
        print("⚠️  Some tests failed - check the output above")
    
    print("\n✅ Test completed!")

if __name__ == "__main__":
    asyncio.run(main())
