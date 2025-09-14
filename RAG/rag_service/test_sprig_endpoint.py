#!/usr/bin/env python3
"""
Test script for the Sprig character generation endpoint
"""

import requests
import json
import time

# Configuration
BASE_URL = "http://localhost:8077"
ENDPOINT = "/sprig/generate"

def test_sprig_generation():
    """Test the Sprig generation endpoint"""
    print("🎭 Testing Sprig Character Generation Endpoint")
    print("=" * 50)
    
    # Test data
    test_cases = [
        {
            "name": "Basic character generation",
            "data": {
                "prompt": "Create a brave warrior character with magical abilities",
                "user_id": "test_user_001",
                "model": "gpt-4o-mini"
            }
        },
        {
            "name": "Detailed character with backstory",
            "data": {
                "prompt": "Generate a mysterious mage character who was once a noble but now lives as a hermit. Include personality traits, magical abilities, and a detailed backstory.",
                "user_id": "test_user_002",
                "model": "gpt-4o"
            }
        },
        {
            "name": "Invalid model test",
            "data": {
                "prompt": "Create a character",
                "user_id": "test_user_003",
                "model": "invalid-model"
            }
        },
        {
            "name": "Empty prompt test",
            "data": {
                "prompt": "",
                "user_id": "test_user_004",
                "model": "gpt-4o-mini"
            }
        },
        {
            "name": "Missing user_id test",
            "data": {
                "prompt": "Create a character",
                "user_id": "",
                "model": "gpt-4o-mini"
            }
        }
    ]
    
    for i, test_case in enumerate(test_cases, 1):
        print(f"\n🧪 Test {i}: {test_case['name']}")
        print("-" * 30)
        
        try:
            response = requests.post(
                f"{BASE_URL}{ENDPOINT}",
                json=test_case["data"],
                headers={"Content-Type": "application/json"},
                timeout=60
            )
            
            print(f"Status Code: {response.status_code}")
            
            if response.status_code == 200:
                result = response.json()
                if result.get("success"):
                    print("✅ Success!")
                    print(f"Response: {result['response'][:200]}...")
                else:
                    print("❌ Failed!")
                    print(f"Error: {result.get('error', 'Unknown error')}")
            else:
                print(f"❌ HTTP Error: {response.status_code}")
                print(f"Response: {response.text}")
                
        except requests.exceptions.RequestException as e:
            print(f"❌ Request failed: {e}")
        except Exception as e:
            print(f"❌ Unexpected error: {e}")
        
        # Small delay between tests
        time.sleep(1)
    
    print("\n" + "=" * 50)
    print("🎭 Sprig endpoint testing completed!")

def test_rate_limiting():
    """Test rate limiting functionality"""
    print("\n🚦 Testing Rate Limiting")
    print("-" * 30)
    
    user_id = "rate_limit_test_user"
    prompt = "Create a test character"
    
    print(f"Making {SPRIG_RATE_LIMIT + 2} requests to test rate limiting...")
    
    for i in range(SPRIG_RATE_LIMIT + 2):
        try:
            response = requests.post(
                f"{BASE_URL}{ENDPOINT}",
                json={
                    "prompt": f"{prompt} #{i+1}",
                    "user_id": user_id,
                    "model": "gpt-4o-mini"
                },
                headers={"Content-Type": "application/json"},
                timeout=30
            )
            
            result = response.json()
            if result.get("success"):
                print(f"Request {i+1}: ✅ Success")
            else:
                print(f"Request {i+1}: ❌ {result.get('error', 'Unknown error')}")
                
        except Exception as e:
            print(f"Request {i+1}: ❌ Error - {e}")
        
        time.sleep(0.1)  # Small delay between requests

if __name__ == "__main__":
    # Import rate limit constant from the main file
    try:
        from rag_api import SPRIG_RATE_LIMIT
    except ImportError:
        SPRIG_RATE_LIMIT = 10  # Default fallback
    
    test_sprig_generation()
    test_rate_limiting()
