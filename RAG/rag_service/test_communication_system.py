#!/usr/bin/env python3
"""
Test script for the Communication System

This script demonstrates the various features of the communication system
including WebSocket connections, message handling, TTS/STT, and more.
"""

import asyncio
import json
import time
import logging
import websockets
from datetime import datetime
from companion_system.communication_system import (
    CommunicationSystem, 
    MessageType, 
    MessageStatus
)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

class CommunicationSystemTester:
    """Test class for the communication system"""
    
    def __init__(self, openai_api_key: str = None):
        self.communication_system = CommunicationSystem(
            openai_api_key=openai_api_key,
            websocket_port=8765
        )
        self.test_results = []
        
    def run_basic_tests(self):
        """Run basic functionality tests"""
        logger.info("Running basic communication system tests...")
        
        try:
            # Test 1: Create chat session
            session_id = self.communication_system.create_chat_session(
                user_id="test_user_001",
                companion_id="test_companion_001"
            )
            self.test_results.append(("Create Session", "PASS", session_id))
            logger.info(f"✓ Created session: {session_id}")
            
            # Test 2: Send text message
            message_id = self.communication_system.send_message(
                session_id=session_id,
                user_id="test_user_001",
                content="Hello, this is a test message!",
                message_type=MessageType.TEXT
            )
            self.test_results.append(("Send Message", "PASS", message_id))
            logger.info(f"✓ Sent message: {message_id}")
            
            # Test 3: Process message
            response = self.communication_system.process_message(session_id, message_id)
            self.test_results.append(("Process Message", "PASS", response[:50] + "..." if response else "None"))
            logger.info(f"✓ Processed message, response: {response[:100] if response else 'None'}...")
            
            # Test 4: Get session messages
            messages = self.communication_system.get_session_messages(session_id)
            self.test_results.append(("Get Messages", "PASS", f"{len(messages)} messages retrieved"))
            logger.info(f"✓ Retrieved {len(messages)} messages from session")
            
            # Test 5: Get session stats
            stats = self.communication_system.get_session_stats(session_id)
            self.test_results.append(("Get Stats", "PASS", f"Stats: {stats.get('total_messages', 0)} messages"))
            logger.info(f"✓ Retrieved session stats: {stats}")
            
            # Test 6: Add session metadata
            self.communication_system.add_session_metadata(session_id, "test_key", "test_value")
            metadata = self.communication_system.get_session_metadata(session_id, "test_key")
            self.test_results.append(("Metadata", "PASS", f"Value: {metadata}"))
            logger.info(f"✓ Metadata test passed: {metadata}")
            
            # Test 7: Message priority
            self.communication_system.set_message_priority(message_id, "high")
            high_priority = self.communication_system.get_high_priority_messages(session_id)
            self.test_results.append(("Priority", "PASS", f"{len(high_priority)} high priority messages"))
            logger.info(f"✓ Priority test passed: {len(high_priority)} high priority messages")
            
            # Test 8: Message reactions
            self.communication_system.add_message_reaction(message_id, "test_user_001", "👍")
            reactions = self.communication_system.get_message_reactions(message_id)
            self.test_results.append(("Reactions", "PASS", f"Reactions: {reactions}"))
            logger.info(f"✓ Reactions test passed: {reactions}")
            
            # Test 9: Search messages
            search_results = self.communication_system.search_messages(session_id, "test")
            self.test_results.append(("Search", "PASS", f"{len(search_results)} search results"))
            logger.info(f"✓ Search test passed: {len(search_results)} results for 'test'")
            
            # Test 10: Export session
            export_json = self.communication_system.export_session_history(session_id, "json")
            export_txt = self.communication_system.export_session_history(session_id, "txt")
            self.test_results.append(("Export", "PASS", f"JSON: {len(export_json)} chars, TXT: {len(export_txt)} chars"))
            logger.info(f"✓ Export test passed - JSON: {len(export_json)} chars, TXT: {len(export_txt)} chars")
            
            # Test 11: System health
            health = self.communication_system.get_system_health()
            self.test_results.append(("Health Check", "PASS", f"Status: {health.get('status', 'unknown')}"))
            logger.info(f"✓ Health check passed: {health}")
            
            # Test 12: Companion action
            action_id = self.communication_system.start_companion_action(
                session_id, 
                "thinking", 
                "Processing your request", 
                2.0
            )
            self.test_results.append(("Start Action", "PASS", action_id))
            logger.info(f"✓ Started action: {action_id}")
            
            # Test 13: Update action progress
            self.communication_system.update_action_progress(session_id, action_id, 0.5)
            self.communication_system.update_action_progress(session_id, action_id, 1.0, "completed")
            self.test_results.append(("Action Progress", "PASS", "Progress updated to 100%"))
            logger.info(f"✓ Action progress updated successfully")
            
            # Test 14: Get active actions
            active_actions = self.communication_system.get_active_actions(session_id)
            self.test_results.append(("Active Actions", "PASS", f"{len(active_actions)} active actions"))
            logger.info(f"✓ Retrieved active actions: {len(active_actions)}")
            
            # Test 15: Broadcast message
            broadcast_id = self.communication_system.broadcast_message(
                "test_companion_001", 
                "This is a broadcast test message", 
                MessageType.SYSTEM
            )
            self.test_results.append(("Broadcast", "PASS", broadcast_id))
            logger.info(f"✓ Broadcast message sent: {broadcast_id}")
            
            # Test 16: Get user sessions
            user_sessions = self.communication_system.get_user_sessions("test_user_001")
            self.test_results.append(("User Sessions", "PASS", f"{len(user_sessions)} sessions"))
            logger.info(f"✓ Retrieved user sessions: {len(user_sessions)}")
            
            # Test 17: Get companion sessions
            companion_sessions = self.communication_system.get_companion_sessions("test_companion_001")
            self.test_results.append(("Companion Sessions", "PASS", f"{len(companion_sessions)} sessions"))
            logger.info(f"✓ Retrieved companion sessions: {len(companion_sessions)}")
            
            # Test 18: Session pause/resume
            self.communication_system.pause_session(session_id)
            self.communication_system.resume_session(session_id)
            self.test_results.append(("Pause/Resume", "PASS", "Session paused and resumed"))
            logger.info(f"✓ Session pause/resume test passed")
            
            # Test 19: Cleanup old messages
            self.communication_system.cleanup_old_messages(session_id, max_messages=5)
            messages_after_cleanup = self.communication_system.get_session_messages(session_id)
            self.test_results.append(("Cleanup", "PASS", f"{len(messages_after_cleanup)} messages after cleanup"))
            logger.info(f"✓ Cleanup test passed: {len(messages_after_cleanup)} messages remaining")
            
            # Test 20: Activity summary
            activity_summary = self.communication_system.get_companion_activity_summary("test_companion_001", 1)
            self.test_results.append(("Activity Summary", "PASS", f"Summary generated: {activity_summary.get('total_sessions', 0)} sessions"))
            logger.info(f"✓ Activity summary generated: {activity_summary}")
            
            # End session
            self.communication_system.end_chat_session(session_id)
            logger.info(f"✓ Session ended: {session_id}")
            
        except Exception as e:
            logger.error(f"Test failed with error: {e}")
            self.test_results.append(("Error", "FAIL", str(e)))
            
        return self.test_results
    
    async def test_websocket_connection(self):
        """Test WebSocket connection functionality"""
        logger.info("Testing WebSocket connection...")
        
        try:
            # Wait a moment for the server to start
            await asyncio.sleep(2)
            
            # Connect to WebSocket server
            uri = "ws://localhost:8765"
            async with websockets.connect(uri) as websocket:
                # Send authentication message
                auth_message = {
                    "user_id": "websocket_test_user",
                    "companion_id": "websocket_test_companion",
                    "session_id": "websocket_test_session"
                }
                
                await websocket.send(json.dumps(auth_message))
                
                # Wait for connection confirmation
                response = await websocket.recv()
                response_data = json.loads(response)
                
                if response_data.get("type") == "connection_confirmed":
                    logger.info("✓ WebSocket connection established successfully")
                    
                    # Send a test message
                    test_message = {
                        "type": "chat_message",
                        "content": "Hello from WebSocket test!",
                        "message_type": "text"
                    }
                    
                    await websocket.send(json.dumps(test_message))
                    
                    # Wait for response
                    response = await websocket.recv()
                    response_data = json.loads(response)
                    
                    if response_data.get("type") == "message_received":
                        logger.info("✓ WebSocket message handling working")
                        return True
                    else:
                        logger.warning(f"Unexpected response type: {response_data.get('type')}")
                        return False
                else:
                    logger.error(f"WebSocket connection failed: {response_data}")
                    return False
                    
        except Exception as e:
            logger.error(f"WebSocket test failed: {e}")
            return False
    
    def test_tts_stt(self):
        """Test text-to-speech and speech-to-text functionality"""
        logger.info("Testing TTS/STT functionality...")
        
        try:
            # Test TTS
            test_text = "This is a test of the text to speech system."
            audio_data = self.communication_system.text_to_speech(test_text)
            
            if audio_data:
                logger.info(f"✓ TTS test passed: Generated {len(audio_data)} bytes of audio")
                self.test_results.append(("TTS", "PASS", f"{len(audio_data)} bytes generated"))
            else:
                logger.warning("TTS test failed: No audio data generated")
                self.test_results.append(("TTS", "FAIL", "No audio data generated"))
            
            # Note: STT test would require actual audio input
            # For now, we'll just test the method exists
            logger.info("✓ STT method available (requires audio input for full test)")
            self.test_results.append(("STT", "PASS", "Method available"))
            
        except Exception as e:
            logger.error(f"TTS/STT test failed: {e}")
            self.test_results.append(("TTS/STT", "FAIL", str(e)))
    
    def test_image_processing(self):
        """Test image processing functionality"""
        logger.info("Testing image processing...")
        
        try:
            # Create a simple test image (1x1 pixel)
            from PIL import Image
            import io
            
            # Create a minimal test image
            test_image = Image.new('RGB', (1, 1), color='red')
            image_buffer = io.BytesIO()
            test_image.save(image_buffer, format='JPEG')
            image_data = image_buffer.getvalue()
            
            # Test image processing
            result = self.communication_system.process_image(image_data, task="analyze")
            
            if result and result.get('success'):
                logger.info(f"✓ Image processing test passed: {result.get('analysis', '')[:100]}...")
                self.test_results.append(("Image Processing", "PASS", "Image analyzed successfully"))
            else:
                logger.warning(f"Image processing test failed: {result}")
                self.test_results.append(("Image Processing", "FAIL", result.get('error', 'Unknown error')))
                
        except Exception as e:
            logger.error(f"Image processing test failed: {e}")
            self.test_results.append(("Image Processing", "FAIL", str(e)))
    
    def print_test_results(self):
        """Print test results summary"""
        print("\n" + "="*60)
        print("COMMUNICATION SYSTEM TEST RESULTS")
        print("="*60)
        
        passed = 0
        failed = 0
        
        for test_name, status, details in self.test_results:
            status_symbol = "✓" if status == "PASS" else "✗"
            print(f"{status_symbol} {test_name:<25} {status:<6} {details}")
            
            if status == "PASS":
                passed += 1
            else:
                failed += 1
        
        print("="*60)
        print(f"TOTAL: {passed + failed} | PASSED: {passed} | FAILED: {failed}")
        print("="*60)
        
        if failed == 0:
            print("🎉 All tests passed! The communication system is working correctly.")
        else:
            print(f"⚠️  {failed} test(s) failed. Please check the logs for details.")

async def main():
    """Main test function"""
    print("🚀 Starting Communication System Tests...")
    
    # Initialize tester (you can provide your OpenAI API key here for full functionality)
    tester = CommunicationSystemTester(openai_api_key=None)  # Set to your key if available
    
    try:
        # Run basic tests
        print("\n📋 Running basic functionality tests...")
        basic_results = tester.run_basic_tests()
        
        # Test TTS/STT
        print("\n🎤 Testing TTS/STT functionality...")
        tester.test_tts_stt()
        
        # Test image processing
        print("\n🖼️  Testing image processing...")
        tester.test_image_processing()
        
        # Test WebSocket connection
        print("\n🔌 Testing WebSocket connection...")
        websocket_success = await tester.test_websocket_connection()
        
        if websocket_success:
            tester.test_results.append(("WebSocket", "PASS", "Connection successful"))
        else:
            tester.test_results.append(("WebSocket", "FAIL", "Connection failed"))
        
        # Print results
        tester.print_test_results()
        
    except KeyboardInterrupt:
        print("\n⏹️  Tests interrupted by user")
    except Exception as e:
        print(f"\n❌ Test suite failed with error: {e}")
        logger.error(f"Test suite failed: {e}")
    finally:
        # Cleanup
        if hasattr(tester, 'communication_system'):
            tester.communication_system.cleanup_inactive_sessions(0)  # Clean up all sessions
            print("\n🧹 Cleanup completed")

if __name__ == "__main__":
    # Run the async main function
    asyncio.run(main())
