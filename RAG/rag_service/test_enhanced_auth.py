#!/usr/bin/env python3
"""
Test script for Enhanced Authentication System

This script tests the enhanced authentication system to ensure it properly
maps Unity user IDs to consistent user identifiers based on email addresses.
"""

import asyncio
import aiohttp
import json
import os
from typing import Dict, Any

# Test configuration
SERVER_URL = "http://localhost:8077"
TEST_EMAIL = "test@example.com"

async def test_enhanced_authentication():
    """Test the enhanced authentication system"""
    
    async with aiohttp.ClientSession() as session:
        print("🧪 Testing Enhanced Authentication System")
        print("=" * 50)
        
        # Test 1: First device authentication
        print("\n📱 Test 1: First device authentication")
        device1_auth = {
            "unity_user_id": "unity_user_123_device1",
            "email": TEST_EMAIL,
            "access_token": "fake_token_1",
            "device_id": "device_001",
            "platform": "iOS",
            "player_name": "Test User"
        }
        
        async with session.post(f"{SERVER_URL}/auth/unity/authenticate", json=device1_auth) as resp:
            if resp.status == 200:
                data = await resp.json()
                print(f"✅ First device auth successful")
                print(f"   Primary User ID: {data['primary_user_id']}")
                print(f"   Email: {data['email']}")
                print(f"   Is New User: {data['is_new_user']}")
                print(f"   Message: {data['message']}")
                primary_user_id = data['primary_user_id']
            else:
                print(f"❌ First device auth failed: {resp.status}")
                return
        
        # Test 2: Second device authentication (same email)
        print("\n📱 Test 2: Second device authentication (same email)")
        device2_auth = {
            "unity_user_id": "unity_user_456_device2",
            "email": TEST_EMAIL,
            "access_token": "fake_token_2",
            "device_id": "device_002",
            "platform": "Android",
            "player_name": "Test User"
        }
        
        async with session.post(f"{SERVER_URL}/auth/unity/authenticate", json=device2_auth) as resp:
            if resp.status == 200:
                data = await resp.json()
                print(f"✅ Second device auth successful")
                print(f"   Primary User ID: {data['primary_user_id']}")
                print(f"   Email: {data['email']}")
                print(f"   Is New User: {data['is_new_user']}")
                print(f"   Message: {data['message']}")
                
                # Verify same primary user ID
                if data['primary_user_id'] == primary_user_id:
                    print(f"✅ Same primary user ID - mapping working correctly!")
                else:
                    print(f"❌ Different primary user ID - mapping failed!")
                    return
            else:
                print(f"❌ Second device auth failed: {resp.status}")
                return
        
        # Test 3: Third device authentication (same email)
        print("\n📱 Test 3: Third device authentication (same email)")
        device3_auth = {
            "unity_user_id": "unity_user_789_device3",
            "email": TEST_EMAIL,
            "access_token": "fake_token_3",
            "device_id": "device_003",
            "platform": "Windows",
            "player_name": "Test User"
        }
        
        async with session.post(f"{SERVER_URL}/auth/unity/authenticate", json=device3_auth) as resp:
            if resp.status == 200:
                data = await resp.json()
                print(f"✅ Third device auth successful")
                print(f"   Primary User ID: {data['primary_user_id']}")
                print(f"   Email: {data['email']}")
                print(f"   Is New User: {data['is_new_user']}")
                print(f"   Message: {data['message']}")
                
                # Verify same primary user ID
                if data['primary_user_id'] == primary_user_id:
                    print(f"✅ Same primary user ID - mapping working correctly!")
                else:
                    print(f"❌ Different primary user ID - mapping failed!")
                    return
            else:
                print(f"❌ Third device auth failed: {resp.status}")
                return
        
        # Test 4: Get user mappings
        print(f"\n📊 Test 4: Get user mappings for {primary_user_id}")
        async with session.get(f"{SERVER_URL}/auth/user/{primary_user_id}/mappings") as resp:
            if resp.status == 200:
                mappings = await resp.json()
                print(f"✅ Retrieved {len(mappings)} device mappings:")
                for i, mapping in enumerate(mappings, 1):
                    print(f"   Device {i}: {mapping['unity_user_id']} ({mapping['platform']}) - {mapping['last_seen']}")
            else:
                print(f"❌ Failed to get user mappings: {resp.status}")
        
        # Test 5: Get user info
        print(f"\n👤 Test 5: Get user info for {primary_user_id}")
        async with session.get(f"{SERVER_URL}/auth/user/{primary_user_id}/info") as resp:
            if resp.status == 200:
                info = await resp.json()
                print(f"✅ User info retrieved:")
                print(f"   Primary User ID: {info['primary_user_id']}")
                print(f"   Email: {info['email']}")
                print(f"   Total Devices: {info['total_devices']}")
                print(f"   Platforms: {', '.join(info['platforms'])}")
            else:
                print(f"❌ Failed to get user info: {resp.status}")
        
        # Test 6: Different email (should create new user)
        print(f"\n📧 Test 6: Different email (should create new user)")
        different_email_auth = {
            "unity_user_id": "unity_user_999_device4",
            "email": "different@example.com",
            "access_token": "fake_token_4",
            "device_id": "device_004",
            "platform": "iOS",
            "player_name": "Different User"
        }
        
        async with session.post(f"{SERVER_URL}/auth/unity/authenticate", json=different_email_auth) as resp:
            if resp.status == 200:
                data = await resp.json()
                print(f"✅ Different email auth successful")
                print(f"   Primary User ID: {data['primary_user_id']}")
                print(f"   Email: {data['email']}")
                print(f"   Is New User: {data['is_new_user']}")
                
                # Verify different primary user ID
                if data['primary_user_id'] != primary_user_id:
                    print(f"✅ Different primary user ID - separate user created correctly!")
                else:
                    print(f"❌ Same primary user ID - user separation failed!")
            else:
                print(f"❌ Different email auth failed: {resp.status}")
        
        print("\n🎉 All tests completed!")

async def test_server_availability():
    """Test if the server is available"""
    try:
        async with aiohttp.ClientSession() as session:
            async with session.get(f"{SERVER_URL}/health") as resp:
                if resp.status == 200:
                    print("✅ Server is available")
                    return True
                else:
                    print(f"❌ Server health check failed: {resp.status}")
                    return False
    except Exception as e:
        print(f"❌ Server not available: {e}")
        return False

async def main():
    """Main test function"""
    print("Enhanced Authentication System Test")
    print("=" * 50)
    
    # Check server availability
    if not await test_server_availability():
        print("\n❌ Server is not available. Please start the server first:")
        print("   uvicorn rag_api:app --host 0.0.0.0 --port 8077")
        return
    
    # Run authentication tests
    await test_enhanced_authentication()

if __name__ == "__main__":
    asyncio.run(main())
