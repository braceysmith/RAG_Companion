#!/bin/bash

# RAG Companion Communication System - Dependency Installer
# This script handles dependency installation with fallbacks for problematic packages

set -e  # Exit on any error

echo "🚀 RAG Companion Communication System - Dependency Installer"
echo "============================================================"

# Detect operating system
if [[ "$OSTYPE" == "linux-gnu"* ]]; then
    echo "🐧 Linux detected"
    
    # Update package list
    echo "🔧 Updating package list..."
    if command -v apt-get &> /dev/null; then
        sudo apt-get update
        echo "✅ Package list updated"
        
        # Install system dependencies
        echo "🔧 Installing system dependencies..."
        sudo apt-get install -y portaudio19-dev python3-pyaudio
        echo "✅ System dependencies installed"
    elif command -v yum &> /dev/null; then
        sudo yum install -y portaudio-devel
        echo "✅ System dependencies installed"
    elif command -v dnf &> /dev/null; then
        sudo dnf install -y portaudio-devel
        echo "✅ System dependencies installed"
    else
        echo "⚠️  Unsupported package manager. Please install PortAudio manually."
    fi
    
elif [[ "$OSTYPE" == "darwin"* ]]; then
    echo "🍎 macOS detected"
    
    # Check if Homebrew is installed
    if command -v brew &> /dev/null; then
        echo "🔧 Installing PortAudio via Homebrew..."
        brew install portaudio
        echo "✅ PortAudio installed"
    else
        echo "⚠️  Homebrew not found. Please install it first:"
        echo "   /bin/bash -c \"\$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)\""
        exit 1
    fi
    
else
    echo "⚠️  Unsupported operating system. Please install dependencies manually."
    exit 1
fi

# Install Python dependencies
echo ""
echo "🐍 Installing Python dependencies..."

# Core dependencies (essential)
echo "📦 Installing core dependencies..."
pip install websockets>=11.0.3 Pillow>=10.0.0 requests>=2.31.0 asyncio-mqtt>=0.16.1

# Optional dependencies with fallbacks
echo "🔧 Installing optional dependencies..."
pip install openai>=1.0.0 pyttsx3>=2.90 gtts>=2.3.2 SpeechRecognition>=3.10.0 sounddevice>=0.4.6

# Image processing
echo "🖼️  Installing image processing libraries..."
pip install opencv-python>=4.8.0 numpy>=1.24.0

# Additional utilities
echo "🔧 Installing additional utilities..."
pip install websocket-client>=1.6.0 structlog>=23.1.0 python-dotenv>=1.0.0 colorama>=0.4.6

# Development tools
echo "🧪 Installing development tools..."
pip install pytest>=7.4.0 pytest-asyncio>=0.21.0 black>=23.0.0 flake8>=6.0.0 mypy>=1.5.0

# Try PyAudio last (most problematic)
echo "🎤 Attempting to install PyAudio..."
if pip install pyaudio>=0.2.11; then
    echo "✅ PyAudio installed successfully"
else
    echo "⚠️  PyAudio installation failed. This is common and not critical."
    echo "   The system will use alternative audio libraries (sounddevice, SpeechRecognition)"
fi

# Verify installation
echo ""
echo "🔍 Verifying installation..."
python3 -c "
import websockets
import PIL
import requests
import asyncio
print('✅ All core dependencies imported successfully')
"

echo ""
echo "🎉 Installation completed successfully!"
echo ""
echo "📚 Next steps:"
echo "   1. Run: python3 test_communication_system.py"
echo "   2. Run: python3 example_usage.py"
echo "   3. Check the README.md for usage instructions"
echo ""
echo "🚀 Ready to use the RAG Companion Communication System!"
