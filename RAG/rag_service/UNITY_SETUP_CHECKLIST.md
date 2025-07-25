# Unity Setup Checklist for Hybrid Voice RAG

## 🔧 **Required Changes to Existing Unity Scripts:**

### 1. **Update MobileRAGClient.cs**
- ✅ **Add the `MobileVoiceResponse` class** (from MobileRAGClient_Update.cs)
- ✅ **Add the `ProcessVoiceQuery` method** (from MobileRAGClient_Update.cs)
- ✅ **Update `cloudApiUrl`** to point to your server:
  ```csharp
  [SerializeField] private string cloudApiUrl = "http://localhost:8077";  // Or your deployed URL
  ```

### 2. **Update MobileAudioManager.cs**
- ✅ **Add `ConvertAudioClipToWAV` method** (from MobileAudioManager_Update.cs)
- ✅ **Add `PlayAudioFromBytes` method** (from MobileAudioManager_Update.cs)
- ✅ **Make `recordedClip` public** or add a getter:
  ```csharp
  public AudioClip recordedClip { get; private set; }
  ```

### 3. **Update MobileRAGCompanionSystem.cs**
- ✅ **Add voice processing fields** (from MobileRAGCompanionSystem_Update.cs)
- ✅ **Add `SendVoiceMessageAsync` method** (from MobileRAGCompanionSystem_Update.cs)
- ✅ **Add voice recording control methods** (from MobileRAGCompanionSystem_Update.cs)
- ✅ **Add audio event subscriptions** to `Start()` method
- ✅ **Update `userId`** field:
  ```csharp
  [SerializeField] private string userId = "mobile-user";  // This will be used for local RAG
  ```

### 4. **Update MobileCompanionUI.cs** (If exists)
Add these UI methods if they don't exist:
```csharp
public void ShowProcessingIndicator(string message) { /* Show loading spinner */ }
public void HideProcessingIndicator() { /* Hide loading spinner */ }
public void ShowRecordingIndicator() { /* Show microphone icon */ }
public void HideRecordingIndicator() { /* Hide microphone icon */ }
public void ShowTranscript(string transcript) { /* Display what user said */ }
public void ShowResponse(string response) { /* Display AI response */ }
public void ShowError(string error) { /* Display error message */ }
```

## 🎯 **Scene Setup in RAG_Test03:**

### **Required Components on GameObject:**
1. **MobileRAGCompanionSystem** (your main script)
   - Assign `MobileCompanionUI` reference
   - Assign `MobileAudioManager` reference  
   - Set `cloudRAGUrl` to `http://localhost:8077` (or your server URL)
   - Set `userId` to unique identifier for this device/user

2. **MobileAudioManager** 
   - Assign `AudioSource` component
   - Configure `sampleRate = 16000` (optimal for speech)
   - Set `maxRecordingLength = 30` seconds

3. **MobileRAGClient**
   - Set `cloudApiUrl` to `http://localhost:8077`
   - Configure timeout and retry settings

### **UI Button Setup:**
Add a voice button that calls:
```csharp
// For push-to-talk
public void OnVoiceButtonPressed()
{
    companionSystem.StartVoiceRecording();
}

public void OnVoiceButtonReleased()  
{
    companionSystem.StopVoiceRecording();
}

// Or for toggle recording
public void OnVoiceToggle()
{
    if (companionSystem.IsRecording)
        companionSystem.StopVoiceRecording();
    else
        companionSystem.StartVoiceRecording();
}
```

## 📱 **Required Unity Packages:**

Make sure these are installed via Package Manager:
- **Newtonsoft Json** (for JSON serialization)
- **Unity Web Request** (should be built-in)
- **Audio** (should be built-in)

Add to `manifest.json` if needed:
```json
{
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

## 🔄 **Testing the Integration:**

### **1. Start the Server:**
```bash
cd /path/to/rag_service
python3 start_server.py
```

### **2. Configure Unity:**
- Update server URLs in Unity scripts
- Build and run on device
- Test voice recording button

### **3. Test Voice Flow:**
1. **Tap voice button** → Should start recording
2. **Say something** → "My name is Alex" or "What's the weather?"
3. **Release button** → Should process and respond
4. **Check logs** → Should see transcript and response source (local/cloud)

## 🎵 **Expected Behavior:**

### **Personal Queries** (Local RAG):
- **Input**: "My name is Sarah"
- **Expected**: Stores locally, responds personally
- **Log**: `Response source: local (personal)`

### **General Queries** (Cloud RAG + Tools):
- **Input**: "What's the weather in Tokyo?"
- **Expected**: Uses weather tool, cloud processing
- **Log**: `Response source: cloud (general)`, `Tool used: get_weather`

### **Voice Response**:
- Should play audio response after text processing
- Audio should be natural speech from OpenAI TTS

## ⚠️ **Common Issues:**

1. **"Connection refused"** → Server not running on port 8077
2. **"API key not configured"** → Update `.env` file on server
3. **No audio playback** → Check AudioSource component assignment
4. **Microphone not working** → Check platform permissions

Your Unity setup should work with minimal changes since you already have a solid mobile RAG foundation!