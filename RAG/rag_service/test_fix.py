#!/usr/bin/env python3
"""
Quick test script to verify the database fix works
"""

import requests
import json

# Configuration
RAG_SERVER_URL = "http://localhost:8000"  # Update with your server URL
TEST_USER_ID = "mobile-bracey02"  # Use the user ID from your logs

def test_health_endpoint():
    """Test the health endpoint"""
    print("🏥 Testing health endpoint...")
    
    try:
        response = requests.get(f"{RAG_SERVER_URL}/health", timeout=5)
        print(f"📡 Response status: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Health check response: {json.dumps(data, indent=2)}")
            return data.get("database") == "connected"
        else:
            print(f"❌ Health check failed: {response.status_code}")
            return False
            
    except Exception as e:
        print(f"❌ Health check error: {e}")
        return False

def test_user_images_endpoint():
    """Test the user images endpoint"""
    print(f"\n📸 Testing user images endpoint for user: {TEST_USER_ID}")
    
    try:
        response = requests.get(f"{RAG_SERVER_URL}/user_images/{TEST_USER_ID}?limit=10&content_type=image", timeout=10)
        print(f"📡 Response status: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ User images response: {json.dumps(data, indent=2)}")
            
            if data.get("success"):
                image_count = len(data.get("images", []))
                print(f"🎯 Found {image_count} images for user {TEST_USER_ID}")
                return True
            else:
                print(f"⚠️ Server returned success=false: {data.get('error', 'Unknown error')}")
                return False
        else:
            print(f"❌ HTTP error {response.status_code}")
            print(f"📄 Error response: {response.text}")
            return False
            
    except Exception as e:
        print(f"❌ Request error: {e}")
        return False

def main():
    """Run the tests"""
    print("🚀 Testing RAG Server Database Fix")
    print("=" * 50)
    
    # Test health endpoint
    health_ok = test_health_endpoint()
    
    # Test user images endpoint
    images_ok = test_user_images_endpoint()
    
    # Summary
    print("\n" + "=" * 50)
    print("📊 Test Results:")
    print(f"   Health Endpoint: {'✅' if health_ok else '❌'}")
    print(f"   User Images:     {'✅' if images_ok else '❌'}")
    
    if health_ok and images_ok:
        print("\n🎉 All tests passed! The database fix is working.")
    elif health_ok and not images_ok:
        print("\n⚠️ Database is connected but user images endpoint has issues.")
        print("   Check if the user has any images in the database.")
    else:
        print("\n❌ Database connection issues detected.")
        print("   Check your DATABASE_URL and ensure PostgreSQL is running.")
    
    print("\n💡 Next steps:")
    if health_ok:
        print("   1. Try loading images in Unity again")
        print("   2. Check Unity console for new debug messages")
    else:
        print("   1. Verify your DATABASE_URL environment variable")
        print("   2. Ensure PostgreSQL is running and accessible")
        print("   3. Check RAG server logs for database errors")

if __name__ == "__main__":
    main()
