#!/usr/bin/env python3
"""
Test script for RAG Companion Image Storage System

This script tests the image generation, storage, and retrieval functionality.
Run this after starting your RAG server to verify everything works.
"""

import requests
import json
import time
from pathlib import Path

# Configuration
RAG_SERVER_URL = "http://localhost:8000"  # Update with your server URL
TEST_USER_ID = "test-user-123"
TEST_SESSION_ID = "test-session-456"

def test_image_generation():
    """Test image generation and storage"""
    print("🧪 Testing image generation and storage...")
    
    url = f"{RAG_SERVER_URL}/generate_image"
    payload = {
        "prompt": "A cute robot playing with a cat in a garden",
        "size": "1024x1024",
        "user_id": TEST_USER_ID,
        "session_id": TEST_SESSION_ID,
        "turn_id": f"test-turn-{int(time.time())}"
    }
    
    try:
        response = requests.post(url, json=payload)
        response.raise_for_status()
        
        result = response.json()
        print(f"✅ Image generation response: {json.dumps(result, indent=2)}")
        
        if result.get("success") and result.get("content_id"):
            print(f"🎨 Image generated successfully with content ID: {result['content_id']}")
            return result["content_id"]
        else:
            print("❌ Image generation failed or no content ID returned")
            return None
            
    except Exception as e:
        print(f"❌ Error testing image generation: {e}")
        return None

def test_image_retrieval(content_id):
    """Test retrieving a specific image"""
    if not content_id:
        print("⚠️ Skipping image retrieval test - no content ID")
        return False
    
    print(f"🧪 Testing image retrieval for content ID: {content_id}")
    
    url = f"{RAG_SERVER_URL}/image/{content_id}"
    
    try:
        response = requests.get(url)
        response.raise_for_status()
        
        # Check if we got an image
        content_type = response.headers.get('content-type', '')
        if content_type.startswith('image/'):
            print(f"✅ Image retrieved successfully: {content_type}")
            print(f"📏 Image size: {len(response.content)} bytes")
            return True
        else:
            print(f"❌ Unexpected content type: {content_type}")
            return False
            
    except Exception as e:
        print(f"❌ Error testing image retrieval: {e}")
        return False

def test_user_images():
    """Test retrieving user's image list"""
    print("🧪 Testing user images retrieval...")
    
    url = f"{RAG_SERVER_URL}/user_images/{TEST_USER_ID}?limit=10&content_type=image"
    
    try:
        response = requests.get(url)
        response.raise_for_status()
        
        result = response.json()
        print(f"✅ User images response: {json.dumps(result, indent=2)}")
        
        if result.get("success"):
            image_count = len(result.get("images", []))
            print(f"📸 Found {image_count} images for user {TEST_USER_ID}")
            return True
        else:
            print("❌ Failed to retrieve user images")
            return False
            
    except Exception as e:
        print(f"❌ Error testing user images retrieval: {e}")
        return False

def test_image_deletion(content_id):
    """Test deleting an image"""
    if not content_id:
        print("⚠️ Skipping image deletion test - no content ID")
        return False
    
    print(f"🧪 Testing image deletion for content ID: {content_id}")
    
    url = f"{RAG_SERVER_URL}/image/{content_id}?user_id={TEST_USER_ID}"
    
    try:
        response = requests.delete(url)
        response.raise_for_status()
        
        result = response.json()
        print(f"✅ Image deletion response: {json.dumps(result, indent=2)}")
        
        if result.get("success"):
            print(f"🗑️ Image {content_id} deleted successfully")
            return True
        else:
            print("❌ Failed to delete image")
            return False
            
    except Exception as e:
        print(f"❌ Error testing image deletion: {e}")
        return False

def check_storage_directory():
    """Check if the multimedia storage directory exists"""
    print("🧪 Checking storage directory...")
    
    storage_dir = Path("multimedia/images")
    if storage_dir.exists():
        image_files = list(storage_dir.glob("*.png"))
        print(f"✅ Storage directory exists: {storage_dir}")
        print(f"📁 Found {len(image_files)} image files")
        return True
    else:
        print(f"❌ Storage directory not found: {storage_dir}")
        return False

def main():
    """Run all tests"""
    print("🚀 Starting RAG Companion Image Storage System Tests")
    print("=" * 60)
    
    # Check storage directory
    storage_ok = check_storage_directory()
    
    # Test image generation
    content_id = test_image_generation()
    
    # Wait a moment for processing
    if content_id:
        print("⏳ Waiting 3 seconds for image processing...")
        time.sleep(3)
    
    # Test image retrieval
    retrieval_ok = test_image_retrieval(content_id)
    
    # Test user images list
    list_ok = test_user_images()
    
    # Test image deletion (only if we have a content ID)
    deletion_ok = test_image_deletion(content_id) if content_id else False
    
    # Summary
    print("\n" + "=" * 60)
    print("📊 Test Results Summary:")
    print(f"   Storage Directory: {'✅' if storage_ok else '❌'}")
    print(f"   Image Generation:  {'✅' if content_id else '❌'}")
    print(f"   Image Retrieval:   {'✅' if retrieval_ok else '❌'}")
    print(f"   User Images List:  {'✅' if list_ok else '❌'}")
    print(f"   Image Deletion:    {'✅' if deletion_ok else '❌'}")
    
    if all([storage_ok, content_id, retrieval_ok, list_ok]):
        print("\n🎉 All tests passed! Image storage system is working correctly.")
    else:
        print("\n⚠️ Some tests failed. Check the logs above for details.")
    
    print("\n💡 Tips:")
    print("   - Ensure your RAG server is running")
    print("   - Check server logs for any errors")
    print("   - Verify database connection and permissions")
    print("   - Check storage directory write permissions")

if __name__ == "__main__":
    main()
