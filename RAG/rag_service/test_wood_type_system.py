#!/usr/bin/env python3
"""
Test script for the Enhanced Wood Type System
"""

import requests
import json
import time

# Configuration
BASE_URL = "http://localhost:8077"

def test_wood_type_endpoints():
    """Test all wood type system endpoints"""
    print("🌳 Testing Enhanced Wood Type System")
    print("=" * 50)
    
    # Test 1: Get all wood types
    print("\n🧪 Test 1: Get all wood types")
    print("-" * 30)
    try:
        response = requests.get(f"{BASE_URL}/wood/types", timeout=30)
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! Found {data['total_count']} wood types")
            print(f"First wood type: {data['wood_types'][0]['name']}")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 2: Get specific wood type
    print("\n🧪 Test 2: Get specific wood type (Walnut)")
    print("-" * 30)
    try:
        response = requests.get(f"{BASE_URL}/wood/types/1", timeout=30)
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! Wood type: {data['name']}")
            print(f"Description: {data['description']}")
            print(f"Grain Pattern: {data['grainPattern']}")
            print(f"Color Palette: {data['colorPalette']}")
            print(f"Personality Traits: {', '.join(data['personalityTraits'])}")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 3: Generate DALL-E prompt
    print("\n🧪 Test 3: Generate DALL-E prompt for Oak")
    print("-" * 30)
    try:
        request_data = {
            "wood_type_id": 0,
            "personality_traits": ["reliable", "traditional"],
            "style": "natural"
        }
        
        response = requests.post(
            f"{BASE_URL}/wood/dalle-prompt",
            json=request_data,
            headers={"Content-Type": "application/json"},
            timeout=30
        )
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! Wood type: {data['wood_type']}")
            print(f"DALL-E Prompt: {data['prompt']}")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 4: Get wood types by personality trait
    print("\n🧪 Test 4: Get wood types by personality trait (elegant)")
    print("-" * 30)
    try:
        response = requests.get(f"{BASE_URL}/wood/types/by-trait/elegant", timeout=30)
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 200:
            data = response.json()
            print(f"✅ Success! Found {data['total_count']} wood types with 'elegant' trait")
            for wood in data['wood_types']:
                print(f"  - {wood['name']}: {wood['description']}")
        else:
            print(f"❌ Failed: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    # Test 5: Test error handling
    print("\n🧪 Test 5: Test error handling (invalid wood type)")
    print("-" * 30)
    try:
        response = requests.get(f"{BASE_URL}/wood/types/999", timeout=30)
        print(f"Status Code: {response.status_code}")
        
        if response.status_code == 404:
            print("✅ Success! Correctly returned 404 for invalid wood type")
        else:
            print(f"❌ Unexpected response: {response.text}")
    except Exception as e:
        print(f"❌ Error: {e}")
    
    print("\n" + "=" * 50)
    print("🌳 Wood type system testing completed!")

def test_dalle_prompt_variations():
    """Test different DALL-E prompt variations"""
    print("\n🎨 Testing DALL-E Prompt Variations")
    print("-" * 30)
    
    test_cases = [
        {
            "name": "Natural Oak",
            "data": {"wood_type_id": 0, "style": "natural"}
        },
        {
            "name": "Weathered Walnut",
            "data": {"wood_type_id": 1, "style": "weathered", "personality_traits": ["rustic"]}
        },
        {
            "name": "Polished Cherry",
            "data": {"wood_type_id": 2, "style": "polished", "personality_traits": ["elegant", "luxurious"]}
        },
        {
            "name": "Modern Maple",
            "data": {"wood_type_id": 3, "style": "modern", "personality_traits": ["clean", "modern"]}
        }
    ]
    
    for i, test_case in enumerate(test_cases, 1):
        print(f"\n🧪 Test {i}: {test_case['name']}")
        try:
            response = requests.post(
                f"{BASE_URL}/wood/dalle-prompt",
                json=test_case["data"],
                headers={"Content-Type": "application/json"},
                timeout=30
            )
            
            if response.status_code == 200:
                data = response.json()
                print(f"✅ Success!")
                print(f"Wood: {data['wood_type']}")
                print(f"Prompt: {data['prompt'][:100]}...")
            else:
                print(f"❌ Failed: {response.text}")
        except Exception as e:
            print(f"❌ Error: {e}")

if __name__ == "__main__":
    test_wood_type_endpoints()
    test_dalle_prompt_variations()
