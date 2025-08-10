#!/usr/bin/env python3
"""
Dependency Installation Script for RAG Companion Communication System

This script handles the installation of dependencies with fallbacks for
problematic packages like PyAudio.
"""

import subprocess
import sys
import os
from typing import List, Tuple

def run_command(command: List[str], description: str) -> bool:
    """Run a command and return success status"""
    print(f"🔧 {description}...")
    try:
        result = subprocess.run(command, capture_output=True, text=True, check=True)
        print(f"✅ {description} completed successfully")
        return True
    except subprocess.CalledProcessError as e:
        print(f"❌ {description} failed:")
        print(f"   Error: {e.stderr}")
        return False

def install_package(package: str, description: str = None) -> bool:
    """Install a single package"""
    if description is None:
        description = f"Installing {package}"
    return run_command([sys.executable, "-m", "pip", "install", package], description)

def install_system_dependencies():
    """Install system-level dependencies"""
    print("🚀 Installing system dependencies...")
    
    # Detect operating system
    if sys.platform.startswith('linux'):
        print("🐧 Linux detected - attempting to install PortAudio...")
        
        # Try different package managers
        package_managers = [
            (["sudo", "apt-get", "update"], "Updating apt package list"),
            (["sudo", "apt-get", "install", "-y", "portaudio19-dev", "python3-pyaudio"], "Installing PortAudio (apt)"),
        ]
        
        for command, description in package_managers:
            if not run_command(command, description):
                print(f"⚠️  {description} failed, trying alternative...")
                continue
    elif sys.platform.startswith('darwin'):
        print("🍎 macOS detected - attempting to install PortAudio...")
        if not run_command(["brew", "install", "portaudio"], "Installing PortAudio (Homebrew)"):
            print("⚠️  Homebrew installation failed. Please install manually: brew install portaudio")
    else:
        print("⚠️  Unsupported platform. Please install PortAudio manually.")

def install_python_dependencies():
    """Install Python dependencies with fallbacks"""
    print("\n🐍 Installing Python dependencies...")
    
    # Core dependencies (essential)
    core_packages = [
        "websockets>=11.0.3",
        "Pillow>=10.0.0", 
        "requests>=2.31.0",
        "asyncio-mqtt>=0.16.1"
    ]
    
    # Optional dependencies (with fallbacks)
    optional_packages = [
        ("openai>=1.0.0", "OpenAI API integration"),
        ("pyttsx3>=2.90", "Text-to-speech (offline)"),
        ("gtts>=2.3.2", "Text-to-speech (Google)"),
        ("SpeechRecognition>=3.10.0", "Speech recognition"),
        ("sounddevice>=0.4.6", "Audio input/output"),
        ("opencv-python>=4.8.0", "Image processing"),
        ("numpy>=1.24.0", "Numerical computing"),
        ("websocket-client>=1.6.0", "WebSocket client"),
        ("structlog>=23.1.0", "Structured logging"),
        ("python-dotenv>=1.0.0", "Environment variables"),
        ("colorama>=0.4.6", "Colored terminal output")
    ]
    
    # Install core packages first
    print("📦 Installing core dependencies...")
    for package in core_packages:
        if not install_package(package):
            print(f"❌ Critical: Failed to install {package}")
            return False
    
    # Install optional packages with fallbacks
    print("\n🔧 Installing optional dependencies...")
    for package, description in optional_packages:
        if not install_package(package, description):
            print(f"⚠️  Warning: Failed to install {package} - {description}")
            continue
    
    # Try PyAudio last (most problematic)
    print("\n🎤 Attempting to install PyAudio...")
    if not install_package("pyaudio>=0.2.11", "PyAudio (audio input/output)"):
        print("⚠️  PyAudio installation failed. This is common and not critical.")
        print("   The system will use alternative audio libraries (sounddevice, SpeechRecognition)")
    
    return True

def install_development_dependencies():
    """Install development and testing dependencies"""
    print("\n🧪 Installing development dependencies...")
    
    dev_packages = [
        "pytest>=7.4.0",
        "pytest-asyncio>=0.21.0",
        "black>=23.0.0",
        "flake8>=6.0.0",
        "mypy>=1.5.0"
    ]
    
    for package in dev_packages:
        install_package(package, f"Development tool: {package}")

def verify_installation():
    """Verify that key dependencies are working"""
    print("\n🔍 Verifying installation...")
    
    # Test imports
    test_imports = [
        ("websockets", "WebSocket support"),
        ("PIL", "Image processing"),
        ("requests", "HTTP requests"),
        ("asyncio", "Async programming")
    ]
    
    all_good = True
    for module, description in test_imports:
        try:
            __import__(module)
            print(f"✅ {description}: {module}")
        except ImportError:
            print(f"❌ {description}: {module} - FAILED")
            all_good = False
    
    return all_good

def main():
    """Main installation function"""
    print("🚀 RAG Companion Communication System - Dependency Installer")
    print("=" * 60)
    
    try:
        # Install system dependencies
        install_system_dependencies()
        
        # Install Python dependencies
        if not install_python_dependencies():
            print("\n❌ Critical dependencies failed to install. Please check the errors above.")
            return False
        
        # Install development dependencies
        install_development_dependencies()
        
        # Verify installation
        if verify_installation():
            print("\n🎉 Installation completed successfully!")
            print("\n📚 Next steps:")
            print("   1. Run: python test_communication_system.py")
            print("   2. Run: python example_usage.py")
            print("   3. Check the README.md for usage instructions")
            return True
        else:
            print("\n⚠️  Installation completed with warnings. Some features may not work.")
            return False
            
    except KeyboardInterrupt:
        print("\n⏹️  Installation interrupted by user")
        return False
    except Exception as e:
        print(f"\n❌ Installation failed with error: {e}")
        return False

if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)
