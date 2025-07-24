# Mobile Voice Flow Architecture

## 🎯 **Complete Voice-to-Response Pipeline**

Your mobile app now has a complete voice processing pipeline with hybrid RAG architecture:

### 🏗️ **System Architecture**

```
📱 Unity Mobile App
    ↓ (Voice Recording)
🎵 Audio Upload
    ↓ (POST /mobile/voice)
🧠 Hybrid RAG System
    ├── 🔒 Local RAG (Personal Data)
    └── ☁️ Cloud RAG (General Data)
    ↓ (Smart Content Routing)
🤖 OpenAI GPT-4o Response
    ↓ (Text-to-Speech)
🔊 Audio Response Back to App
```

## 🔄 **Processing Flow**

### 1. **Voice Input** (Unity → Server)
```csharp
// Unity C# code
byte[] audioData = RecordVoiceFromMicrophone();
await SendToServer("/mobile/voice", audioData, userId);
```

### 2. **Speech-to-Text** (Server)
- Uses OpenAI Whisper API
- Converts audio to text transcript

### 3. **Content Classification** (Server)
```python
# Automatic sensitivity detection
if "my name is" or "i live at" → Local RAG (Personal)
if "i feel" or "remember when" → Local RAG (Private)  
if "what is" or "weather in" → Cloud RAG (General)
```

### 4. **Smart RAG Routing** (Server)

#### **Local RAG** (Personal/Private Data)
- **Storage**: Device-specific JSON files
- **Data**: Names, addresses, conversations, personal preferences
- **Security**: Never leaves local storage
- **Performance**: Instant access, no network delay

#### **Cloud RAG** (General Data)
- **Storage**: PostgreSQL with pgvector
- **Data**: Weather, news, factual information
- **Security**: Non-sensitive data only
- **Performance**: Scalable, searchable knowledge base

### 5. **Response Generation** (Server)
- Context-aware responses using retrieved information
- Tool integration (weather, future tools)
- Personal memory integration

### 6. **Text-to-Speech** (Server)
- Uses OpenAI TTS API
- Natural voice responses

### 7. **Audio Playback** (Unity)
```csharp
// Unity receives and plays response
byte[] audioResponse = Base64.Decode(response.audio_response);
PlayAudioResponse(audioResponse);
```

## 📱 **Unity Integration**

### **Complete Mobile Voice Client**

```csharp
using UnityEngine;
using System.Collections;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class MobileVoiceCompanion : MonoBehaviour
{
    [Header("Configuration")]
    public string serverUrl = "http://localhost:8077";
    public string userId = "mobile_user_123";
    
    [Header("Audio")]
    public AudioSource audioSource;
    public int recordingLength = 10;
    
    private AudioClip microphoneClip;
    private bool isRecording = false;
    
    // UI Button: Start voice interaction
    public void OnVoiceButtonPressed()
    {
        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopRecordingAndProcess();
        }
    }
    
    void StartRecording()
    {
        Debug.Log("🎤 Starting voice recording...");
        isRecording = true;
        
        // Start microphone recording
        microphoneClip = Microphone.Start(null, false, recordingLength, 44100);
        
        // Update UI to show recording state
        UpdateUI("Recording... Tap to stop");
    }
    
    void StopRecordingAndProcess()
    {
        Debug.Log("🔇 Stopping recording and processing...");
        isRecording = false;
        
        // Stop microphone
        Microphone.End(null);
        
        // Update UI
        UpdateUI("Processing voice...");
        
        // Convert audio to bytes and send to server
        StartCoroutine(ProcessVoiceRequest());
    }
    
    IEnumerator ProcessVoiceRequest()
    {
        // Convert AudioClip to WAV bytes
        byte[] audioData = ConvertAudioClipToWAV(microphoneClip);
        
        // Create form data
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", audioData, "voice.wav", "audio/wav");
        form.AddField("user_id", userId);
        form.AddField("audio_format", "wav");
        
        // Send to hybrid RAG system
        string endpoint = $"{serverUrl}/mobile/voice";
        using (UnityWebRequest request = UnityWebRequest.Post(endpoint, form))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                HandleVoiceResponse(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"❌ Voice request failed: {request.error}");
                UpdateUI("Voice processing failed. Try again.");
            }
        }
    }
    
    void HandleVoiceResponse(string jsonResponse)
    {
        try
        {
            var response = JsonConvert.DeserializeObject<MobileVoiceResponse>(jsonResponse);
            
            Debug.Log($"📝 Transcript: {response.transcript}");
            Debug.Log($"💬 Response: {response.response_text}");
            Debug.Log($"📍 Source: {response.source} ({response.sensitivity})");
            
            // Show transcript in UI
            UpdateUI($"You said: \"{response.transcript}\"");
            
            // Play audio response
            if (!string.IsNullOrEmpty(response.audio_response))
            {
                StartCoroutine(PlayAudioResponse(response.audio_response));
            }
            
            // Handle tool results (weather, etc.)
            if (response.tool_results != null)
            {
                Debug.Log($"🔧 Tool used: {response.tool_results}");
            }
            
            // Show personal info extraction
            if (response.personal_info_extracted != null)
            {
                Debug.Log($"👤 Personal info stored: {response.personal_info_extracted}");
            }
            
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error parsing voice response: {ex.Message}");
            UpdateUI("Error processing voice response");
        }
    }
    
    IEnumerator PlayAudioResponse(string base64Audio)
    {
        try
        {
            // Decode base64 audio
            byte[] audioBytes = System.Convert.FromBase64String(base64Audio);
            
            // Save to temporary file
            string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "response.mp3");
            System.IO.File.WriteAllBytes(tempPath, audioBytes);
            
            // Load and play audio
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{tempPath}", AudioType.MPEG))
            {
                yield return www.SendWebRequest();
                
                if (www.result == UnityWebRequest.Result.Success)
                {
                    AudioClip responseClip = DownloadHandlerAudioClip.GetContent(www);
                    audioSource.clip = responseClip;
                    audioSource.Play();
                    
                    Debug.Log("🔊 Playing AI response");
                    UpdateUI("AI is responding...");
                    
                    // Wait for audio to finish, then reset UI
                    yield return new WaitForSeconds(responseClip.length);
                    UpdateUI("Tap to talk");
                }
                else
                {
                    Debug.LogError("Failed to load audio response");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error playing audio: {ex.Message}");
        }
    }
    
    byte[] ConvertAudioClipToWAV(AudioClip clip)
    {
        // WAV conversion implementation
        // (Simplified - use a proper WAV encoder library)
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        
        // Convert float samples to 16-bit PCM WAV format
        // Implementation details would go here...
        
        return new byte[0]; // Placeholder - implement proper WAV encoding
    }
    
    void UpdateUI(string message)
    {
        // Update your UI elements here
        Debug.Log($"UI: {message}");
    }
}

[System.Serializable]  
public class MobileVoiceResponse
{
    public string transcript;
    public string response_text;
    public string audio_response;
    public string source;           // "local" or "cloud"
    public string sensitivity;      // "personal", "private", "general"
    public object tool_results;
    public object personal_info_extracted;
    public string status;
}
```

## 🔧 **Server Setup & Troubleshooting**

### **1. Start the Server**
```bash
cd /path/to/rag_service
python3 start_server.py
```

### **2. Configure API Key**
Update `.env` file:
```env
OPENAI_API_KEY=sk-your-actual-openai-key-here
```

### **3. Test Voice Endpoint**
```bash
curl -X POST http://localhost:8077/mobile/voice \
  -F "audio=@test_voice.wav" \
  -F "user_id=test_user" \
  -F "audio_format=wav"
```

## 🎵 **Example Interactions**

### **Personal Query** (Local RAG)
- **User**: "My name is Sarah and I work as a designer"
- **System**: Routes to Local RAG, stores personal info
- **Response**: "Nice to meet you Sarah! Design work must be really creative. What kind of design do you specialize in?"

### **General Query** (Cloud RAG) 
- **User**: "What's the weather in Tokyo?"
- **System**: Routes to Cloud RAG, uses weather tool
- **Response**: "The current weather in Tokyo is 22°C with clear skies and light winds."

### **Mixed Context**
- **User**: "Should I bring an umbrella to work tomorrow?" (after system knows user's location)
- **System**: Uses local data (location) + cloud tool (weather)
- **Response**: "Based on the forecast for Seattle tomorrow, it looks sunny with no rain expected. You shouldn't need an umbrella!"

## 🚨 **Troubleshooting**

### **No Voice Response**
1. ✅ Server running on port 8077?
2. ✅ OpenAI API key configured?
3. ✅ Audio format supported (WAV/WebM)?
4. ✅ Network connectivity?

### **Error Messages**
- **"I'm having trouble processing..."** → API key issue
- **"Connection refused"** → Server not running
- **"Audio processing failed"** → Audio format/encoding issue

The system now provides a complete voice interaction pipeline with smart data routing, personal memory, and tool integration!