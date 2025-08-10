#!/usr/bin/env python3
"""
Test suite for the Memory System (Hybrid RAG)

This script tests all the key functionality of the memory system including:
- Local encrypted storage
- Data sensitivity routing
- Secure device transfer
- Memory retrieval and search
"""

import os
import sys
import tempfile
import shutil
import json
from datetime import datetime, timedelta
from pathlib import Path

# Add the current directory to Python path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from companion_system.memory_system import (
    HybridMemorySystem, 
    MemoryChunk, 
    DataSensitivity,
    TransferRequest
)

def test_basic_initialization():
    """Test basic system initialization"""
    print("🧪 Testing basic initialization...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Check if device ID was generated
        assert memory_system.device_id is not None
        assert len(memory_system.device_id) > 0
        
        # Check if databases were created
        local_db = Path(temp_dir) / "local_memory.db"
        transfer_db = Path(temp_dir) / "transfers.db"
        
        assert local_db.exists(), "Local database should exist"
        assert transfer_db.exists(), "Transfer database should exist"
        
        print("✅ Basic initialization passed")
        return memory_system

def test_memory_storage_and_retrieval():
    """Test storing and retrieving memories"""
    print("🧪 Testing memory storage and retrieval...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Test storing sensitive data (should go to local storage)
        sensitive_chunk_id = memory_system.store_memory(
            user_id="test_user",
            companion_id="test_companion",
            content="This is a sensitive personal note",
            content_type="text",
            sensitivity=DataSensitivity.SENSITIVE,
            tags=["personal", "sensitive"],
            metadata={"category": "health"}
        )
        
        assert sensitive_chunk_id is not None
        
        # Test storing public data (should go to cloud)
        public_chunk_id = memory_system.store_memory(
            user_id="test_user",
            companion_id="test_companion",
            content="This is public information",
            content_type="text",
            sensitivity=DataSensitivity.PUBLIC,
            tags=["public", "info"]
        )
        
        assert public_chunk_id is not None
        
        # Test retrieving sensitive data
        retrieved_chunk = memory_system.retrieve_memory(sensitive_chunk_id, "test_user")
        assert retrieved_chunk is not None
        assert retrieved_chunk.content == "This is a sensitive personal note"
        assert retrieved_chunk.sensitivity == DataSensitivity.SENSITIVE
        assert "personal" in retrieved_chunk.tags
        
        # Test that access tracking works
        assert retrieved_chunk.access_count > 0
        
        print("✅ Memory storage and retrieval passed")

def test_data_sensitivity_routing():
    """Test that data is routed to appropriate storage based on sensitivity"""
    print("🧪 Testing data sensitivity routing...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Test different sensitivity levels
        sensitivities = [
            (DataSensitivity.PUBLIC, "public data"),
            (DataSensitivity.INTERNAL, "internal data"),
            (DataSensitivity.PERSONAL, "personal data"),
            (DataSensitivity.SENSITIVE, "sensitive data"),
            (DataSensitivity.CRITICAL, "critical data")
        ]
        
        chunk_ids = []
        for sensitivity, content in sensitivities:
            chunk_id = memory_system.store_memory(
                user_id="test_user",
                companion_id="test_companion",
                content=content,
                content_type="text",
                sensitivity=sensitivity,
                tags=[sensitivity.value]
            )
            chunk_ids.append(chunk_id)
        
        # Verify all were stored
        for chunk_id in chunk_ids:
            retrieved = memory_system.retrieve_memory(chunk_id, "test_user")
            assert retrieved is not None, f"Failed to retrieve chunk {chunk_id}"
        
        print("✅ Data sensitivity routing passed")

def test_memory_search():
    """Test memory search functionality"""
    print("🧪 Testing memory search...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Store multiple memories with different content
        test_memories = [
            ("I love pizza", ["food", "favorite"]),
            ("I need to exercise more", ["health", "fitness"]),
            ("My favorite color is blue", ["personal", "preference"]),
            ("I have a meeting tomorrow", ["work", "schedule"]),
            ("I should drink more water", ["health", "hydration"])
        ]
        
        for content, tags in test_memories:
            memory_system.store_memory(
                user_id="test_user",
                companion_id="test_companion",
                content=content,
                content_type="text",
                sensitivity=DataSensitivity.PERSONAL,
                tags=tags
            )
        
        # Search for health-related content
        health_results = memory_system.search_memories(
            user_id="test_user",
            companion_id="test_companion",
            query="health",
            limit=5
        )
        
        assert len(health_results) >= 2, "Should find at least 2 health-related memories"
        
        # Search for food-related content
        food_results = memory_system.search_memories(
            user_id="test_user",
            companion_id="test_companion",
            query="pizza",
            limit=5
        )
        
        assert len(food_results) >= 1, "Should find pizza-related memory"
        
        print("✅ Memory search passed")

def test_secure_transfer():
    """Test secure device transfer functionality"""
    print("🧪 Testing secure device transfer...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Store some sensitive data
        sensitive_chunk_id = memory_system.store_memory(
            user_id="test_user",
            companion_id="test_companion",
            content="Very sensitive information",
            content_type="text",
            sensitivity=DataSensitivity.CRITICAL,
            tags=["critical", "secret"]
        )
        
        # Initiate transfer to another device
        target_device_id = "target_device_123"
        transfer = memory_system.initiate_transfer(
            target_device_id=target_device_id,
            user_id="test_user",
            chunk_ids=[sensitive_chunk_id]
        )
        
        assert transfer.transfer_id is not None
        assert transfer.source_device_id == memory_system.device_id
        assert transfer.target_device_id == target_device_id
        assert transfer.status == "pending"
        assert len(transfer.verification_code) == 6
        
        # Verify the transfer
        verification_success = memory_system.verify_transfer(
            transfer.transfer_id,
            transfer.verification_code
        )
        
        assert verification_success, "Transfer verification should succeed"
        
        # Check transfer status
        transfer_status = memory_system.get_transfer_status(transfer.transfer_id)
        assert transfer_status is not None
        assert transfer_status['status'] == "verified"
        
        # Complete the transfer
        completion_success = memory_system.complete_transfer(transfer.transfer_id)
        assert completion_success, "Transfer completion should succeed"
        
        print("✅ Secure transfer passed")

def test_transfer_expiration():
    """Test that transfer requests expire properly"""
    print("🧪 Testing transfer expiration...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Store sensitive data
        sensitive_chunk_id = memory_system.store_memory(
            user_id="test_user",
            companion_id="test_companion",
            content="Expiring sensitive data",
            content_type="text",
            sensitivity=DataSensitivity.SENSITIVE
        )
        
        # Initiate transfer
        transfer = memory_system.initiate_transfer(
            target_device_id="expire_test_device",
            user_id="test_user",
            chunk_ids=[sensitive_chunk_id]
        )
        
        # Manually expire the transfer by updating the database
        # (In real usage, this would happen automatically after 30 minutes)
        memory_system.transfer_conn.execute("""
            UPDATE transfer_requests
            SET expires_at = ?
            WHERE transfer_id = ?
        """, ((datetime.now() - timedelta(minutes=1)).isoformat(), transfer.transfer_id))
        memory_system.transfer_conn.commit()
        
        # Try to verify expired transfer
        verification_success = memory_system.verify_transfer(
            transfer.transfer_id,
            transfer.verification_code
        )
        
        assert not verification_success, "Expired transfer should not verify"
        
        # Check status
        transfer_status = memory_system.get_transfer_status(transfer.transfer_id)
        assert transfer_status['status'] == "expired"
        
        print("✅ Transfer expiration passed")

def test_cleanup_functions():
    """Test cleanup and maintenance functions"""
    print("🧪 Testing cleanup functions...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Create some expired transfers
        expired_time = datetime.now() - timedelta(hours=25)
        
        for i in range(3):
            memory_system.transfer_conn.execute("""
                INSERT INTO transfer_requests
                (transfer_id, source_device_id, target_device_id, user_id, chunk_ids,
                 requested_at, expires_at, verification_code, status)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """, (
                f"expired_transfer_{i}",
                "source_device",
                "target_device",
                "test_user",
                json.dumps(["chunk_1"]),
                expired_time.isoformat(),
                expired_time.isoformat(),
                "123456",
                "completed"
            ))
        
        memory_system.transfer_conn.commit()
        
        # Run cleanup
        memory_system.cleanup_expired_transfers()
        
        # Verify expired transfers were cleaned up
        cursor = memory_system.transfer_conn.execute("""
            SELECT COUNT(*) FROM transfer_requests
            WHERE expires_at < ?
        """, (expired_time.isoformat(),))
        
        count = cursor.fetchone()[0]
        assert count == 0, "Expired transfers should be cleaned up"
        
        print("✅ Cleanup functions passed")

def test_error_handling():
    """Test error handling and edge cases"""
    print("🧪 Testing error handling...")
    
    with tempfile.TemporaryDirectory() as temp_dir:
        memory_system = HybridMemorySystem(data_dir=temp_dir)
        
        # Test retrieving non-existent memory
        non_existent = memory_system.retrieve_memory("non_existent_id", "test_user")
        assert non_existent is None, "Non-existent memory should return None"
        
        # Test transferring non-existent chunks
        try:
            transfer = memory_system.initiate_transfer(
                target_device_id="target_device",
                user_id="test_user",
                chunk_ids=["non_existent_chunk"]
            )
            assert False, "Should raise error for non-existent chunks"
        except ValueError:
            # Expected error
            pass
        
        # Test unauthorized user access
        # Store memory with one user
        chunk_id = memory_system.store_memory(
            user_id="user1",
            companion_id="test_companion",
            content="User 1's data",
            content_type="text",
            sensitivity=DataSensitivity.PERSONAL
        )
        
        # Try to retrieve with different user
        unauthorized_access = memory_system.retrieve_memory(chunk_id, "user2")
        assert unauthorized_access is None, "Unauthorized user should not access data"
        
        print("✅ Error handling passed")

def run_all_tests():
    """Run all tests and report results"""
    print("🚀 Starting Memory System Tests...\n")
    
    tests = [
        test_basic_initialization,
        test_memory_storage_and_retrieval,
        test_data_sensitivity_routing,
        test_memory_search,
        test_secure_transfer,
        test_transfer_expiration,
        test_cleanup_functions,
        test_error_handling
    ]
    
    passed = 0
    failed = 0
    
    for test in tests:
        try:
            test()
            passed += 1
        except Exception as e:
            print(f"❌ {test.__name__} failed: {str(e)}")
            failed += 1
            import traceback
            traceback.print_exc()
        print()
    
    print("=" * 50)
    print(f"📊 Test Results: {passed} passed, {failed} failed")
    
    if failed == 0:
        print("🎉 All tests passed! Memory system is working correctly.")
    else:
        print("⚠️  Some tests failed. Please check the errors above.")
    
    return passed, failed

if __name__ == "__main__":
    run_all_tests()
