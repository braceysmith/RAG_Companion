# 🚀 Quick Start Guide

## Installation Options

### Option 1: Automated Installation (Recommended)
```bash
# Make script executable and run
chmod +x install.sh
./install.sh
```

### Option 2: Python Script Installation
```bash
python install_dependencies.py
```

### Option 3: Manual Installation
```bash
# Install system dependencies first
sudo apt-get install portaudio19-dev python3-pyaudio  # Linux
brew install portaudio  # macOS

# Then install Python packages
pip install -r requirements.txt
```

## Quick Test

```bash
# Test basic functionality
python test_communication_system.py

# Run examples
python example_usage.py
```

## Basic Usage

```python
from companion_system.communication_system import CommunicationSystem, MessageType

# Initialize system
cs = CommunicationSystem(openai_api_key="your-key-here")

# Create chat session
session_id = cs.create_chat_session("user123", "companion456")

# Send message
message_id = cs.send_message(session_id, "user123", "Hello!", MessageType.TEXT)

# Process message
response = cs.process_message(session_id, message_id)
print(response)
```

## What's Included

✅ **Communication System** - Real-time WebSocket messaging  
✅ **Voice Support** - TTS/STT with OpenAI and fallbacks  
✅ **Image Processing** - Vision analysis with GPT-4  
✅ **Session Management** - Full lifecycle management  
✅ **Testing Suite** - Comprehensive test coverage  
✅ **Examples** - Ready-to-run usage examples  
✅ **Documentation** - Complete API reference  

## Next Steps

1. **Read the full README** for detailed documentation
2. **Check TROUBLESHOOTING.md** if you encounter issues
3. **Explore the examples** to understand usage patterns
4. **Integrate with Unity** using the WebSocket client

## Need Help?

- 📖 **README.md** - Complete documentation
- 🔧 **TROUBLESHOOTING.md** - Common issues and solutions
- 🧪 **test_communication_system.py** - Test the system
- 💡 **example_usage.py** - Usage examples
- 📦 **requirements.txt** - Dependencies list

---

**Ready to build amazing AI companions! 🎉**
