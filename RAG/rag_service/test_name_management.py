#!/usr/bin/env python3
"""
Test script for the Client-Side Name Management System
"""

import requests
import json
import time

# Configuration
BASE_URL = "http://localhost:8077"

def test_name_management():
    """Test the name management endpoints"""
    print("👤 Testing Client-Side Name Management System")
    print("=" * 50)
    
    test_user_id = "test_user_name_001"
    
    # Test 1: Set user name
    print("\n🧪 Test 1: Set user name")
    print("-" * 30)
    try:
        request_data = {
            "user_id": test_user_id,
            "name": "Alice Johnson"
        }
        
        response = requests.post(
            f"{BASE_URL}/user/set-name",
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=30
        )
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! {data['message']}")
            print(f"Name set: {data['name']}")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 2: Get user name
    print("\n🧪 Test 2: Get user name")
    print("-" * 30)
    try:
        response = requests.get(f"{BASE_URL}/user/name/{test_user_id}", timeout=30)
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! {data['message']}")
            print(f"Retrieved name: {data['name']}")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 3: Update user name
    print("\n🧪 Test 3: Update user name")
    print("-" * 30)
    try:
        request_data = {
            "user_id": test_user_id,
            "name": "Alice Smith"
        }
        
        response = requests.post(
            f"{BASE_URL}/user/set-name",
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=30
        )
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! {data['message']}")
            print(f"Updated name: {data['name']}")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 4: Verify name update
    print("\n🧪 Test 4: Verify name update")
    print("-" * 30)
    try:
        response = requests.get(f"{BASE_URL}/user/name/{test_user_id}", timeout=30)
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! {data['message']}")
            print(f"Current name: {data['name']}")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 5: Test error handling (empty name)
    print("\n🧪 Test 5: Test error handling (empty name)")
    print("-" * 30)
    try:
        request_data = {
            "user_id": test_user_id,
            "name": ""
        }
        
        response = requests.post(
            f"{BASE_URL}/user/set-name",
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=30
        )
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            if not data['success']:
                print(f"✅ Success! Correctly rejected empty name: {data['message']}")
            else:
                print(f"❌ Failed: Should have rejected empty name")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 6: Test error handling (missing user_id)
    print("\n🧪 Test 6: Test error handling (missing user_id)")
    print("-" * 30)
    try:
        request_data = {
            "user_id": "",
            "name": "Test Name"
        }
        
        response = requests.post(
            f"{BASE_URL}/user/set-name",
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=30
        )
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            if not data['success']:
                print(f"✅ Success! Correctly rejected missing user_id: {data['message']}")
            else:
                print(f"❌ Failed: Should have rejected missing user_id")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 7: Test name with special characters
    print("\n🧪 Test 7: Test name with special characters")
    print("-" * 30)
    try:
        request_data = {
            "user_id": test_user_id,
            "name": "  Jean-Pierre O'Connor  "  # Test trimming and title case
        }
        
        response = requests.post(
            f"{BASE_URL}/user/set-name",
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=30
        )
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! {data['message']}")
            print(f"Cleaned name: '{data['name']}'")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 8: Get name for non-existent user
    print("\n🧪 Test 8: Get name for non-existent user")
    print("-" * 30)
    try:
        response = requests.get(f"{BASE_URL}/user/name/non_existent_user", timeout=30)
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            if not data['success']:
                print(f"✅ Success! Correctly handled non-existent user: {data['message']}")
            else:
                print(f"❌ Failed: Should have indicated no name set")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    print("\n" + "=" * 50)
    print("👤 Name management testing completed!")

def test_name_integration_with_rag():
    """Test that names work with RAG queries"""
    print("\n🧠 Testing Name Integration with RAG Queries")
    print("-" * 30)
    
    test_user_id = "test_rag_name_001"
    
    # Set a name first
    print("Setting name for RAG test...")
    try:
        request_data = {
            "user_id": test_user_id,
            "name": "Bob Wilson"
        }
        
        response = requests.post(
            f"{BASE_URL}/user/set-name",
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=30
        )
        
        if response.status_code == 200:
            print("✅ Name set successfully")
        else:
            print(f"❌ Failed to set name: {response.text}")
            return
    except Exception as e:
        print(f"❌ Error setting name: {e}")
        return
    
    # Test RAG query that should use the name
    print("\nTesting RAG query with name...")
    try:
        request_data = {
            "query": "Hello, how are you today?",
            "user_id": test_user_id
        }
        
        response = requests.post(
            f"{BASE_URL}/query",
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=30
        )
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ RAG query successful")
            print(f"Response: {data.get('response', 'No response')[:100]}...")
            
            # Check if the response contains the user's name
            if "Bob" in data.get('response', ''):
                print("✅ Name integration working - response contains user name")
            else:
                print("⚠️  Name integration may not be working - response doesn't contain name")
        else:
            print(f"❌ RAG query failed: {response.text}")
    except Exception as e:
        print(f"❌ Error with RAG query: {e}")

if __name__ == "__main__":
    test_name_management()
    test_name_integration_with_rag()
