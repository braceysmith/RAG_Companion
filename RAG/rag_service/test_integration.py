#!/usr/bin/env python3
"""
Integration test for the companion system with memory
"""

import os
import sys
import tempfile
from pathlib import Path

# Add the current directory to Python path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from companion_system.memory_system import HybridMemorySystem, DataSensitivity
from companion_system.companion_manager import CompanionManager
from companion_system.companion_core import CompanionCore

def test_companion_with_memory():
    """Test that companions can use the memory system"""
    print("🧪 Testing companion integration with memory...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        # Initialize memory system
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Initialize companion manager
        companion_manager = CompanionManager(storage_path=temp_dir)
        
        # Create a user
        user_id = companion_manager.create_user("test@example.com", "password123")
        assert user_id is not None
        
        # Create a companion
        companion_id = companion_manager.create_companion(
            user_id=user_id,
            name="Test Companion",
            personality_type="openness",
            description="A test companion"
        )
        assert companion_id is not None
        
        # Get the companion
        companion = companion_manager.get_companion(companion_id, user_id)
        assert companion is not None
        
        # Test that companion can store memories
        memory_id = companion.store_memory(
            content="I had a great conversation with the user today",
            content_type="text",
            sensitivity=DataSensitivity.PERSONAL,
            tags=["conversation", "positive"]
        )
        assert memory_id is not None
        
        # Test that companion can retrieve memories
        memory = companion.retrieve_memory(memory_id)
        assert memory is not None
        assert "great conversation" in memory.content
        
        # Test that companion can search memories
        search_results = companion.search_memories("conversation", limit=5)
        assert len(search_results) >= 1
        assert any("great conversation" in result.content for result in search_results)
        
        print("✅ Companion integration with memory passed")

def test_memory_persistence():
    """Test that memories persist between sessions"""
    print("🧪 Testing memory persistence...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        # First session - store memory
        memory_system1 = HybridMemorySystem(data_dir=temp_dir)
        memory_id = memory_system1.store_memory(
            user_id="persist_user",
            companion_id="persist_companion",
            content="This memory should persist",
            content_type="text",
            sensitivity=DataSensitivity.PERSONAL
        )
        memory_system1.close()
        
        # Second session - retrieve memory
        memory_system2 = HybridMemorySystem(data_dir=temp_dir)
        retrieved_memory = memory_system2.retrieve_memory(memory_id, "persist_user")
        
        assert retrieved_memory is not None
        assert retrieved_memory.content == "This memory should persist"
        
        memory_system2.close()
        print("✅ Memory persistence passed")

if __name__ == "__main__":
    print("🚀 Starting Integration Tests...\n")
    
    try:
        test_companion_with_memory()
        test_memory_persistence()
        print("\n🎉 All integration tests passed!")
    except Exception as e:
        print(f"\n❌ Integration test failed: {str(e)}")
        import traceback
        traceback.print_exc()
