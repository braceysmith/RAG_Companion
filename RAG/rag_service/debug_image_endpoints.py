#!/usr/bin/env python3
"""
Debug script for RAG Companion Image Endpoints

This script tests the image storage endpoints to help identify server-side issues.
Run this to verify your RAG server is working correctly.
"""

import requests
import json
import time
from pathlib import Path

# Configuration - UPDATE THESE VALUES
RAG_SERVER_URL = "http://localhost:8000"  # Your RAG server URL
TEST_USER_ID = "mobile-user"  # Should match Unity client user ID

def test_server_connection():
    """Test basic server connectivity"""
    print("🔌 Testing server connection...")
    
    try:
        response = requests.get(f"{RAG_SERVER_URL}/", timeout=5)
        print(f"✅ Server is reachable (HTTP {response.status_code})")
        return True
    except requests.exceptions.ConnectionError:
        print("❌ Cannot connect to server - check if it's running")
        return False
    except requests.exceptions.Timeout:
        print("❌ Server connection timeout")
        return False
    except Exception as e:
        print(f"❌ Connection error: {e}")
        return False

def test_user_images_endpoint():
    """Test the user images endpoint"""
    print(f"\n📸 Testing user images endpoint for user: {TEST_USER_ID}")
    
    url = f"{RAG_SERVER_URL}/user_images/{TEST_USER_ID}?limit=10&content_type=image"
    
    try:
        response = requests.get(url, timeout=10)
        print(f"📡 Response status: {response.status_code}")
        print(f"📡 Response headers: {dict(response.headers)}")
        
        if response.status_code == 200:
            try:
                data = response.json()
                print(f"✅ JSON response: {json.dumps(data, indent=2)}")
                
                if data.get("success"):
                    image_count = len(data.get("images", []))
                    print(f"🎯 Found {image_count} images for user {TEST_USER_ID}")
                    return True
                else:
                    print("⚠️ Server returned success=false")
                    return False
                    
            except json.JSONDecodeError as e:
                print(f"❌ Invalid JSON response: {e}")
                print(f"📄 Raw response: {response.text[:500]}...")
                return False
        else:
            print(f"❌ HTTP error {response.status_code}")
            print(f"📄 Error response: {response.text}")
            return False
            
    except Exception as e:
        print(f"❌ Request error: {e}")
        return False

def test_image_generation():
    """Test image generation endpoint"""
    print(f"\n🎨 Testing image generation for user: {TEST_USER_ID}")
    
    url = f"{RAG_SERVER_URL}/generate_image"
    payload = {
        "prompt": "A simple test image - a blue circle on white background",
        "size": "512x512",  # Smaller size for faster testing
        "user_id": TEST_USER_ID,
        "session_id": f"debug-session-{int(time.time())}",
        "turn_id": f"debug-turn-{int(time.time())}"
    }
    
    try:
        print(f"📤 Sending request to: {url}")
        print(f"📤 Payload: {json.dumps(payload, indent=2)}")
        
        response = requests.post(url, json=payload, timeout=30)
        print(f"📡 Response status: {response.status_code}")
        print(f"📡 Response headers: {dict(response.headers)}")
        
        if response.status_code == 200:
            try:
                data = response.json()
                print(f"✅ JSON response: {json.dumps(data, indent=2)}")
                
                if data.get("success") and data.get("content_id"):
                    print(f"🎉 Image generated successfully!")
                    print(f"   Content ID: {data['content_id']}")
                    print(f"   File path: {data.get('file_path', 'N/A')}")
                    print(f"   File size: {data.get('file_size', 'N/A')} bytes")
                    return data["content_id"]
                else:
                    print("⚠️ Image generation failed or no content ID")
                    if "warning" in data:
                        print(f"   Warning: {data['warning']}")
                    return None
                    
            except json.JSONDecodeError as e:
                print(f"❌ Invalid JSON response: {e}")
                print(f"📄 Raw response: {response.text[:500]}...")
                return None
        else:
            print(f"❌ HTTP error {response.status_code}")
            print(f"📄 Error response: {response.text}")
            return None
            
    except Exception as e:
        print(f"❌ Request error: {e}")
        return None

def test_image_retrieval(content_id):
    """Test retrieving a specific image"""
    if not content_id:
        print("⚠️ Skipping image retrieval test - no content ID")
        return False
    
    print(f"\n🖼️ Testing image retrieval for content ID: {content_id}")
    
    url = f"{RAG_SERVER_URL}/image/{content_id}"
    
    try:
        response = requests.get(url, timeout=10)
        print(f"📡 Response status: {response.status_code}")
        print(f"📡 Response headers: {dict(response.headers)}")
        
        if response.status_code == 200:
            content_type = response.headers.get('content-type', '')
            content_length = response.headers.get('content-length', 'unknown')
            
            print(f"✅ Image retrieved successfully!")
            print(f"   Content-Type: {content_type}")
            print(f"   Content-Length: {content_length}")
            print(f"   Response size: {len(response.content)} bytes")
            
            if content_type.startswith('image/'):
                print(f"   ✅ Valid image content type: {content_type}")
                return True
            else:
                print(f"   ⚠️ Unexpected content type: {content_type}")
                return False
        else:
            print(f"❌ HTTP error {response.status_code}")
            print(f"📄 Error response: {response.text}")
            return False
            
    except Exception as e:
        print(f"❌ Request error: {e}")
        return False

def check_storage_directory():
    """Check if the multimedia storage directory exists and has images"""
    print(f"\n📁 Checking storage directory...")
    
    storage_dir = Path("multimedia/images")
    if storage_dir.exists():
        image_files = list(storage_dir.glob("*.png"))
        print(f"✅ Storage directory exists: {storage_dir}")
        print(f"📁 Found {len(image_files)} image files")
        
        if image_files:
            print("📋 Image files:")
            for i, img_file in enumerate(image_files[:5]):  # Show first 5
                size = img_file.stat().st_size
                print(f"   {i+1}. {img_file.name} ({size} bytes)")
            if len(image_files) > 5:
                print(f"   ... and {len(image_files) - 5} more")
        return True
    else:
        print(f"❌ Storage directory not found: {storage_dir}")
        return False

def main():
    """Run all debug tests"""
    print("🚀 RAG Companion Image Endpoints Debug")
    print("=" * 60)
    
    # Test basic connectivity
    if not test_server_connection():
        print("\n❌ Cannot proceed - server is not reachable")
        print("💡 Make sure your RAG server is running and accessible")
        return
    
    # Check storage directory
    check_storage_directory()
    
    # Test user images endpoint
    user_images_ok = test_user_images_endpoint()
    
    # Test image generation
    content_id = test_image_generation()
    
    # Test image retrieval if generation was successful
    retrieval_ok = False
    if content_id:
        print(f"\n⏳ Waiting 5 seconds for image processing...")
        time.sleep(5)
        retrieval_ok = test_image_retrieval(content_id)
    
    # Summary
    print("\n" + "=" * 60)
    print("📊 Debug Results Summary:")
    print(f"   Server Connection:    ✅")
    print(f"   User Images Endpoint: {'✅' if user_images_ok else '❌'}")
    print(f"   Image Generation:     {'✅' if content_id else '❌'}")
    print(f"   Image Retrieval:      {'✅' if retrieval_ok else '❌'}")
    
    print("\n💡 Troubleshooting Tips:")
    
    if not user_images_ok:
        print("   - Check if user has any images in the database")
        print("   - Verify the user ID matches what's in your database")
        print("   - Check server logs for database connection issues")
    
    if not content_id:
        print("   - Verify OpenAI API key is set in your RAG server")
        print("   - Check if DALL-E 3 API is accessible")
        print("   - Verify server has write permissions to multimedia/ directory")
    
    if not retrieval_ok and content_id:
        print("   - Check if image file was actually saved to disk")
        print("   - Verify file permissions in multimedia/images/ directory")
        print("   - Check server logs for file access errors")
    
    print("\n🔍 Next Steps:")
    print("   1. Check your RAG server console/logs for errors")
    print("   2. Verify the database tables exist and are accessible")
    print("   3. Check file system permissions for the multimedia/ directory")
    print("   4. Ensure your Unity client is using the correct user ID")

if __name__ == "__main__":
    main()
