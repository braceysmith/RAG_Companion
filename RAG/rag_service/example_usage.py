#!/usr/bin/env python3
"""
Example usage of the Communication System

This script demonstrates how to use the communication system
for basic chat functionality with a companion.
"""

import asyncio
import time
from companion_system.communication_system import CommunicationSystem, MessageType

def basic_chat_example():
    """Basic chat example without WebSocket"""
    print("🤖 Basic Chat Example")
    print("=" * 40)
    
    # Initialize communication system
    comm_system = CommunicationSystem(websocket_port=8766)  # Different port to avoid conflicts
    
    try:
        # Create a chat session
        session_id = comm_system.create_chat_session(
            user_id="alice",
            companion_id="ai_companion"
        )
        print(f"✅ Created chat session: {session_id}")
        
        # Send some messages
        messages = [
            "Hello! How are you today?",
            "Can you tell me about the weather?",
            "What's your favorite color?",
            "Tell me a joke!"
        ]
        
        for i, message in enumerate(messages, 1):
            print(f"\n👤 User: {message}")
            
            # Send message
            message_id = comm_system.send_message(
                session_id=session_id,
                user_id="alice",
                content=message,
                message_type=MessageType.TEXT
            )
            
            # Process message and get response
            response = comm_system.process_message(session_id, message_id)
            print(f"🤖 Companion: {response}")
            
            # Small delay between messages
            time.sleep(1)
        
        # Get session statistics
        stats = comm_system.get_session_stats(session_id)
        print(f"\n📊 Session Stats:")
        print(f"   Total messages: {stats.get('total_messages', 0)}")
        print(f"   Average response time: {stats.get('average_response_time', 0):.2f}s")
        print(f"   Session duration: {stats.get('session_duration', 0):.2f}s")
        
        # Export chat history
        history = comm_system.export_session_history(session_id, "txt")
        print(f"\n📝 Chat History (first 200 chars):")
        print(history[:200] + "..." if len(history) > 200 else history)
        
        # End session
        comm_system.end_chat_session(session_id)
        print(f"\n✅ Session ended successfully")
        
    except Exception as e:
        print(f"❌ Error: {e}")
    finally:
        # Cleanup
        comm_system.cleanup_inactive_sessions(0)

def companion_action_example():
    """Example of companion actions and progress updates"""
    print("\n🎭 Companion Actions Example")
    print("=" * 40)
    
    comm_system = CommunicationSystem(websocket_port=8767)
    
    try:
        session_id = comm_system.create_chat_session(
            user_id="bob",
            companion_id="task_companion"
        )
        
        # Start a companion action
        action_id = comm_system.start_companion_action(
            session_id=session_id,
            action_type="research",
            description="Researching the latest AI developments",
            estimated_duration=5.0
        )
        print(f"✅ Started action: {action_id}")
        
        # Simulate progress updates
        for progress in [0.2, 0.4, 0.6, 0.8, 1.0]:
            time.sleep(0.5)  # Simulate work time
            comm_system.update_action_progress(session_id, action_id, progress)
            print(f"📈 Progress: {progress * 100:.0f}%")
        
        # Mark as completed
        comm_system.update_action_progress(session_id, action_id, 1.0, "completed")
        print("✅ Action completed!")
        
        # Get active actions
        active_actions = comm_system.get_active_actions(session_id)
        print(f"📋 Active actions: {len(active_actions)}")
        
        comm_system.end_chat_session(session_id)
        
    except Exception as e:
        print(f"❌ Error: {e}")
    finally:
        comm_system.cleanup_inactive_sessions(0)

def session_management_example():
    """Example of session management features"""
    print("\n🔧 Session Management Example")
    print("=" * 40)
    
    comm_system = CommunicationSystem(websocket_port=8768)
    
    try:
        # Create multiple sessions
        sessions = []
        for i in range(3):
            session_id = comm_system.create_chat_session(
                user_id=f"user_{i}",
                companion_id=f"companion_{i}"
            )
            sessions.append(session_id)
            
            # Send a message to each session
            comm_system.send_message(
                session_id=session_id,
                user_id=f"user_{i}",
                content=f"Hello from user {i}!",
                message_type=MessageType.TEXT
            )
        
        print(f"✅ Created {len(sessions)} sessions")
        
        # Get user sessions
        user_sessions = comm_system.get_user_sessions("user_0")
        print(f"📱 User 0 has {len(user_sessions)} active sessions")
        
        # Get companion sessions
        companion_sessions = comm_system.get_companion_sessions("companion_0")
        print(f"🤖 Companion 0 has {len(companion_sessions)} active sessions")
        
        # Add metadata to a session
        comm_system.add_session_metadata(sessions[0], "topic", "AI discussion")
        comm_system.add_session_metadata(sessions[0], "priority", "high")
        
        # Get metadata
        topic = comm_system.get_session_metadata(sessions[0], "topic")
        priority = comm_system.get_session_metadata(sessions[0], "priority")
        print(f"📝 Session metadata - Topic: {topic}, Priority: {priority}")
        
        # Pause and resume a session
        comm_system.pause_session(sessions[0])
        print("⏸️  Session paused")
        
        comm_system.resume_session(sessions[0])
        print("▶️  Session resumed")
        
        # Get system health
        health = comm_system.get_system_health()
        print(f"💚 System health: {health.get('status', 'unknown')}")
        
        # Clean up all sessions
        for session_id in sessions:
            comm_system.end_chat_session(session_id)
        print("✅ All sessions ended")
        
    except Exception as e:
        print(f"❌ Error: {e}")
    finally:
        comm_system.cleanup_inactive_sessions(0)

def main():
    """Run all examples"""
    print("🚀 Communication System Examples")
    print("=" * 50)
    
    try:
        # Run examples
        basic_chat_example()
        companion_action_example()
        session_management_example()
        
        print("\n🎉 All examples completed successfully!")
        
    except KeyboardInterrupt:
        print("\n⏹️  Examples interrupted by user")
    except Exception as e:
        print(f"\n❌ Examples failed: {e}")

if __name__ == "__main__":
    main()
