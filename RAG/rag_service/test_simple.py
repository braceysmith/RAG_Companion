#!/usr/bin/env python3
"""
Simple test script to verify basic RAG service functionality
"""

import requests
import json

def test_basic_endpoints():
    """Test basic endpoints to verify service is working"""
    base_url = "http://localhost:8077"
    
    print("🧪 Testing basic RAG service functionality...")
    
    # Test 1: Health endpoint
    try:
        response = requests.get(f"{base_url}/health")
        print(f"✅ Health endpoint: {response.status_code}")
        if response.status_code == 200:
            health_data = response.json()
            print(f"   Database: {health_data.get('database_available')}")
            print(f"   Conversation Manager: {health_data.get('conversation_manager')}")
    except Exception as e:
        print(f"❌ Health endpoint failed: {e}")
    
    # Test 2: Simple test endpoint
    try:
        response = requests.get(f"{base_url}/test-simple")
        print(f"✅ Simple test endpoint: {response.status_code}")
        if response.status_code == 200:
            test_data = response.json()
            print(f"   Status: {test_data.get('status')}")
    except Exception as e:
        print(f"❌ Simple test endpoint failed: {e}")
    
    # Test 3: Test query endpoint
    try:
        test_data = {
            "query": "Hello, this is a test",
            "user_id": "test_user_001"
        }
        response = requests.post(f"{base_url}/test-query", json=test_data)
        print(f"✅ Test query endpoint: {response.status_code}")
        if response.status_code == 200:
            query_data = response.json()
            print(f"   Response: {query_data.get('text')}")
            print(f"   Conversation stored: {query_data.get('conversation_stored')}")
    except Exception as e:
        print(f"❌ Test query endpoint failed: {e}")
    
    # Test 4: Main query endpoint (simplified)
    try:
        test_data = {
            "query": "Hello, can you help me?",
            "user_id": "test_user_001"
        }
        response = requests.post(f"{base_url}/query", json=test_data)
        print(f"✅ Main query endpoint: {response.status_code}")
        if response.status_code == 200:
            query_data = response.json()
            print(f"   Response received successfully")
        else:
            print(f"   Error: {response.text[:200]}")
    except Exception as e:
        print(f"❌ Main query endpoint failed: {e}")

if __name__ == "__main__":
    test_basic_endpoints()
    print("\n�� Test completed!")
