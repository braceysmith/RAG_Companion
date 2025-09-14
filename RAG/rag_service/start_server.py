#!/usr/bin/env python3
"""
Server startup script for RAG Companion System
Handles API key configuration and server startup
"""

import os
import sys
import asyncio
import uvicorn
from dotenv import load_dotenv

def check_api_key():
    """Check if OpenAI API key is properly configured"""
    load_dotenv()
    api_key = os.getenv("OPENAI_API_KEY")
    
    if not api_key or api_key.startswith("your_"):
        print("❌ OpenAI API key not configured!")
        print("\n🔧 To fix this:")
        print("1. Get your OpenAI API key from: https://platform.openai.com/api-keys")
        print("2. Update the .env file:")
        print("   OPENAI_API_KEY=sk-your-actual-api-key-here")
        print("3. Restart this server")
        print("\n⚠️  Server will run with limited functionality until API key is configured.")
        return False
    
    print("✅ OpenAI API key configured")
    return True

def check_dependencies():
    """Check if all required dependencies are installed"""
    required_packages = [
        'fastapi', 'uvicorn', 'openai', 'websockets', 
        'python-multipart', 'pydub', 'requests'
    ]
    
    missing_packages = []
    for package in required_packages:
        try:
            __import__(package.replace('-', '_'))
        except ImportError:
            missing_packages.append(package)
    
    if missing_packages:
        print(f"❌ Missing packages: {', '.join(missing_packages)}")
        print(f"Install with: pip3 install {' '.join(missing_packages)}")
        return False
    
    print("✅ All dependencies installed")
    return True

def main():
    """Main startup function"""
    print("🚀 Starting RAG Companion System...")
    print("=" * 50)
    
    # Check dependencies
    if not check_dependencies():
        sys.exit(1)
    
    # Check API key
    api_key_ok = check_api_key()
    
    # Import and test core modules
    try:
        print("📦 Loading core modules...")
        from rag_api import app
        from mcp_tools import tool_manager
        from audio_handler import audio_handler
        from hybrid_rag_system import hybrid_rag
        
        print("✅ Core modules loaded successfully")
        
        # Show available tools
        tools = tool_manager.get_available_tools()
        print(f"🔧 Available tools: {[tool['name'] for tool in tools]}")
        
    except Exception as e:
        print(f"❌ Error loading modules: {e}")
        sys.exit(1)
    
    print("✅ Server ready!")
    print("\n📱 Available endpoints:")
    print("- Health check: http://localhost:8077/health")
    print("- Text query: http://localhost:8077/query")
    print("- Audio processing: http://localhost:8077/audio/process")
    print("- Mobile voice: http://localhost:8077/mobile/voice")
    print("- WebSocket realtime: ws://localhost:8077/ws/realtime/{user_id}")
    print("- Tools info: http://localhost:8077/tools")
    print("- Sprig generation: http://localhost:8077/sprig/generate")
    
    if not api_key_ok:
        print("\n⚠️  Limited functionality - configure API key for full features")
    
    print("\n🎵 Hybrid RAG Architecture:")
    print("- Local RAG: Personal/private data (stored on device)")
    print("- Cloud RAG: General/public data (stored on server)")
    print("- Smart routing based on content sensitivity")
    
    print("\\n" + "=" * 50)
    print("🌟 Server starting on http://localhost:8077")
    
    # Start the server
    uvicorn.run(
        "rag_api:app",
        host="0.0.0.0",
        port=8077,
        reload=True,
        log_level="info"
    )

if __name__ == "__main__":
    main()