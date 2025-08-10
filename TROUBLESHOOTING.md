# 🔧 Troubleshooting Guide

## Common Installation Issues

### 1. PyAudio Installation Failures

**Problem**: `fatal error: portaudio.h: No such file or directory`

**Solutions**:

#### Linux (Ubuntu/Debian):
```bash
sudo apt-get update
sudo apt-get install portaudio19-dev python3-pyaudio
```

#### Linux (CentOS/RHEL/Fedora):
```bash
# CentOS/RHEL
sudo yum install portaudio-devel

# Fedora
sudo dnf install portaudio-devel
```

#### macOS:
```bash
brew install portaudio
```

#### Alternative (Skip PyAudio):
If PyAudio continues to fail, the system will use alternative audio libraries:
```bash
pip install sounddevice SpeechRecognition
```

### 2. OpenCV Installation Issues

**Problem**: OpenCV fails to install or import

**Solutions**:
```bash
# Try the headless version (no GUI dependencies)
pip install opencv-python-headless

# Or install system dependencies first
sudo apt-get install libgl1-mesa-glx libglib2.0-0  # Ubuntu/Debian
```

### 3. WebSocket Connection Issues

**Problem**: WebSocket server won't start or connections fail

**Solutions**:
```bash
# Check if port is already in use
netstat -tulpn | grep :8765

# Kill process using the port
sudo kill -9 <PID>

# Or change the port in your code
communication_system = CommunicationSystem(websocket_port=8766)
```

### 4. OpenAI API Issues

**Problem**: OpenAI API calls fail

**Solutions**:
```bash
# Set your API key
export OPENAI_API_KEY="your-api-key-here"

# Or create a .env file
echo "OPENAI_API_KEY=your-api-key-here" > .env

# Verify API key is working
python -c "import openai; openai.api_key='your-key'; print('API key valid')"
```

## Runtime Issues

### 1. Audio Input/Output Problems

**Problem**: No audio input or output

**Solutions**:
```bash
# Check audio devices
python -c "import sounddevice as sd; print(sd.query_devices())"

# Test microphone
python -c "import sounddevice as sd; print('Recording 3 seconds...'); sd.rec(3*44100, samplerate=44100, channels=1); sd.wait(); print('Done')"
```

### 2. Image Processing Failures

**Problem**: Image analysis fails

**Solutions**:
```bash
# Check PIL installation
python -c "from PIL import Image; print('PIL working')"

# Check OpenCV
python -c "import cv2; print('OpenCV working')"

# Verify image file format
file your_image.jpg
```

### 3. Memory Issues

**Problem**: System runs out of memory

**Solutions**:
```bash
# Clean up old sessions
communication_system.cleanup_inactive_sessions(0)

# Limit message history
communication_system.cleanup_old_messages(session_id, max_messages=100)

# Monitor memory usage
python -c "import psutil; print(f'Memory: {psutil.virtual_memory().percent}%')"
```

## Performance Issues

### 1. Slow Response Times

**Problem**: System responds slowly

**Solutions**:
```bash
# Check system resources
htop
iostat 1

# Optimize WebSocket handling
# Reduce message processing complexity
# Use async processing for heavy operations
```

### 2. High CPU Usage

**Problem**: System uses too much CPU

**Solutions**:
```bash
# Profile Python code
python -m cProfile -o profile.stats your_script.py

# Analyze profile
python -c "import pstats; p = pstats.Stats('profile.stats'); p.sort_stats('cumulative').print_stats(10)"
```

## Network Issues

### 1. WebSocket Connection Drops

**Problem**: Connections keep dropping

**Solutions**:
```bash
# Check network stability
ping google.com

# Increase WebSocket timeout
# Add heartbeat mechanism
# Implement reconnection logic
```

### 2. Firewall Issues

**Problem**: Can't connect to WebSocket server

**Solutions**:
```bash
# Check firewall rules
sudo ufw status  # Ubuntu
sudo firewall-cmd --list-all  # CentOS

# Allow port through firewall
sudo ufw allow 8765
```

## Testing Issues

### 1. Test Failures

**Problem**: Tests fail unexpectedly

**Solutions**:
```bash
# Run tests with verbose output
pytest -v test_communication_system.py

# Run specific test
pytest test_communication_system.py::test_function_name

# Check test environment
python -c "import sys; print(sys.path)"
```

### 2. Import Errors

**Problem**: Module import failures

**Solutions**:
```bash
# Check Python path
python -c "import sys; print('\n'.join(sys.path))"

# Install in development mode
pip install -e .

# Check virtual environment
which python
pip list
```

## System-Specific Issues

### Linux Issues

**Problem**: Permission denied errors

**Solutions**:
```bash
# Fix permissions
sudo chown -R $USER:$USER /path/to/project

# Check SELinux
getenforce
sudo setenforce 0  # Temporarily disable
```

### macOS Issues

**Problem**: Homebrew package conflicts

**Solutions**:
```bash
# Update Homebrew
brew update && brew upgrade

# Clean up old packages
brew cleanup

# Reinstall problematic packages
brew uninstall --ignore-dependencies package_name
brew install package_name
```

### Windows Issues

**Problem**: Path issues or missing DLLs

**Solutions**:
```bash
# Use Windows Subsystem for Linux (WSL)
wsl --install

# Or use conda environment
conda create -n rag_companion python=3.11
conda activate rag_companion
```

## Getting Help

### 1. Check Logs
```bash
# Enable debug logging
export LOG_LEVEL=DEBUG

# Check system logs
journalctl -u your-service -f  # Linux
log show --predicate 'process == "Python"' --last 1h  # macOS
```

### 2. Common Debug Commands
```bash
# Check Python version
python --version

# Check pip version
pip --version

# List installed packages
pip list

# Check system info
python -c "import platform; print(platform.platform())"
python -c "import sys; print(sys.version)"
```

### 3. Environment Verification
```bash
# Create minimal test
python -c "
try:
    import websockets
    import PIL
    import requests
    print('✅ Core dependencies working')
except ImportError as e:
    print(f'❌ Import error: {e}')
"
```

## Still Having Issues?

1. **Check the GitHub Issues** for known problems
2. **Create a minimal reproduction** of your issue
3. **Include system information** (OS, Python version, error messages)
4. **Share relevant logs** and error output

## Quick Fix Commands

```bash
# Complete reset and reinstall
pip uninstall -r requirements.txt -y
pip cache purge
pip install -r requirements.txt

# Alternative installation method
./install.sh  # or
python install_dependencies.py

# Check system health
python -c "from companion_system.communication_system import CommunicationSystem; cs = CommunicationSystem(); print(cs.get_system_health())"
```

---

**Remember**: Most issues are related to system dependencies or Python environment conflicts. The installation scripts handle these automatically, but manual intervention may be needed on some systems.
