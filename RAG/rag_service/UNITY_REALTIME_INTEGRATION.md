# Unity Realtime API Integration Guide

## 🎯 Overview

This guide shows how to integrate Unity with the updated OpenAI Realtime API implementation following official OpenAI documentation patterns.

## 🔧 Updated Server Implementation

The server now follows official OpenAI Realtime API patterns:
- ✅ **WebSocket URL**: `wss://api.openai.com/v1/realtime?model=gpt-5-realtime-preview`
- ✅ **Authentication**: Official headers with `OpenAI-Beta: realtime=v1`
- ✅ **Event Handling**: Official event types and formats
- ✅ **Tool Integration**: Proper function calling with weather tool
- ✅ **Session Management**: Official session configuration

## 📱 Unity C# Integration

### Option 1: WebSocket Real-time Audio (Recommended)

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;
using Newtonsoft.Json;
using System.Text;

public class RealtimeAudioClient : MonoBehaviour
{
    [Header("Configuration")]
    public string serverUrl = "ws://localhost:8077";  // Your server URL
    public string userId = "unity_user";
    
    [Header("Audio Settings")]
    public AudioSource audioSource;
    public int sampleRate = 16000;
    public int recordingLength = 10;
    
    private WebSocket webSocket;
    private AudioClip microphoneClip;
    private bool isRecording = false;
    private bool isConnected = false;
    
    void Start()
    {
        ConnectToRealtimeAPI();
    }
    
    void ConnectToRealtimeAPI()
    {
        string wsUrl = $"{serverUrl}/ws/realtime/{userId}";
        webSocket = new WebSocket(wsUrl);
        
        webSocket.OnOpen += (sender, e) =>
        {
            Debug.Log("✅ Connected to Realtime API");
            isConnected = true;
        };
        
        webSocket.OnMessage += (sender, e) =>
        {
            if (e.IsBinary)
            {
                // Received audio response - play it
                PlayReceivedAudio(e.RawData);
            }
            else
            {
                // Handle JSON events
                HandleRealtimeEvent(e.Data);
            }
        };
        
        webSocket.OnError += (sender, e) =>
        {
            Debug.LogError($"❌ WebSocket error: {e.Message}");
        };
        
        webSocket.OnClose += (sender, e) =>
        {
            Debug.Log("🔌 WebSocket connection closed");
            isConnected = false;
        };
        
        webSocket.Connect();
    }
    
    void HandleRealtimeEvent(string jsonData)
    {
        try
        {
            var eventData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);
            string eventType = eventData.GetValueOrDefault("type", "unknown").ToString();
            
            Debug.Log($"📨 Received event: {eventType}");
            
            switch (eventType)
            {
                case "session.created":
                    Debug.Log("🎯 Session created successfully");
                    break;
                case "conversation.created":
                    Debug.Log("💬 Conversation started");
                    break;
                case "response.audio.transcript.delta":
                    // Real-time transcript of AI response
                    string transcript = eventData.GetValueOrDefault("delta", "").ToString();
                    Debug.Log($"🗣️ AI saying: {transcript}");
                    break;
                case "input_audio_buffer.speech_started":
                    Debug.Log("🎤 User started speaking");
                    break;
                case "input_audio_buffer.speech_stopped":
                    Debug.Log("🔇 User stopped speaking");
                    break;
                case "error":
                    var error = eventData.GetValueOrDefault("error", new Dictionary<string, object>());
                    Debug.LogError($"❌ API Error: {error}");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error handling realtime event: {ex.Message}");
        }
    }
    
    void PlayReceivedAudio(byte[] audioData)
    {
        // Convert PCM16 bytes to AudioClip and play
        StartCoroutine(PlayAudioFromBytes(audioData));
    }
    
    IEnumerator PlayAudioFromBytes(byte[] audioData)
    {
        try
        {
            // Convert PCM16 data to float array
            float[] samples = new float[audioData.Length / 2];
            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = (short)((audioData[i * 2 + 1] << 8) | audioData[i * 2]) / 32768.0f;
            }
            
            // Create AudioClip from samples
            AudioClip responseClip = AudioClip.Create("Response", samples.Length, 1, sampleRate, false);
            responseClip.SetData(samples, 0);
            
            // Play the audio
            audioSource.clip = responseClip;
            audioSource.Play();
            
            // Wait for playback to finish
            yield return new WaitForSeconds(responseClip.length);
            
            Debug.Log("🔊 Audio response played");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error playing audio: {ex.Message}");
        }
    }
    
    public void StartRecording()
    {
        if (!isConnected)
        {
            Debug.LogWarning("Not connected to Realtime API");
            return;
        }
        
        if (isRecording) return;
        
        isRecording = true;
        microphoneClip = Microphone.Start(null, true, recordingLength, sampleRate);
        
        Debug.Log("🎤 Started recording...");
        StartCoroutine(StreamMicrophoneData());
    }
    
    public void StopRecording()
    {
        if (!isRecording) return;
        
        isRecording = false;
        Microphone.End(null);
        
        Debug.Log("🔇 Stopped recording");
    }
    
    IEnumerator StreamMicrophoneData()
    {
        int lastSample = 0;
        
        while (isRecording && isConnected)
        {
            int currentSample = Microphone.GetPosition(null);
            
            if (currentSample > lastSample)
            {
                // Get new audio data
                float[] samples = new float[currentSample - lastSample];
                microphoneClip.GetData(samples, lastSample);
                
                // Convert to PCM16 bytes
                byte[] audioBytes = ConvertToPCM16(samples);
                
                // Send to Realtime API
                if (webSocket.IsAlive)
                {
                    webSocket.Send(audioBytes);
                }
                
                lastSample = currentSample;
            }
            
            yield return new WaitForSeconds(0.1f); // Stream at 10fps
        }
    }
    
    byte[] ConvertToPCM16(float[] samples)
    {
        byte[] pcmData = new byte[samples.Length * 2];
        
        for (int i = 0; i < samples.Length; i++)
        {
            short sample = (short)(samples[i] * 32767f);
            pcmData[i * 2] = (byte)(sample & 0xFF);
            pcmData[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        
        return pcmData;
    }
    
    // UI Button handlers
    public void OnStartTalking()
    {
        StartRecording();
    }
    
    public void OnStopTalking()
    {
        StopRecording();
    }
    
    void OnDestroy()
    {
        if (webSocket != null && webSocket.IsAlive)
        {
            webSocket.Close();
        }
    }
}
```

### Option 2: Simple Audio Processing (Alternative)

```csharp
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class SimpleAudioClient : MonoBehaviour
{
    [Header("Configuration")]
    public string serverUrl = "http://localhost:8077";
    public string userId = "unity_user";
    
    [Header("Audio")]
    public AudioSource audioSource;
    
    public void RecordAndSendAudio()
    {
        StartCoroutine(RecordAudioCoroutine());
    }
    
    IEnumerator RecordAudioCoroutine()
    {
        // Record 5 seconds of audio
        AudioClip recordedClip = Microphone.Start(null, false, 5, 44100);
        
        Debug.Log("🎤 Recording for 5 seconds...");
        yield return new WaitForSeconds(5f);
        
        Microphone.End(null);
        Debug.Log("🔇 Recording finished");
        
        // Convert to WAV bytes
        byte[] audioData = ConvertAudioClipToWAV(recordedClip);
        
        // Send to server
        yield return StartCoroutine(SendAudioToServer(audioData));
    }
    
    IEnumerator SendAudioToServer(byte[] audioData)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("audio", audioData, "recording.wav", "audio/wav");
        form.AddField("user_id", userId);
        form.AddField("audio_format", "wav");
        
        using (UnityWebRequest request = UnityWebRequest.Post($"{serverUrl}/audio/process", form))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = request.downloadHandler.text;
                HandleAudioResponse(response);
            }
            else
            {
                Debug.LogError($"❌ Audio request failed: {request.error}");
            }
        }
    }
    
    void HandleAudioResponse(string jsonResponse)
    {
        try
        {
            var response = JsonConvert.DeserializeObject<AudioProcessResponse>(jsonResponse);
            
            Debug.Log($"📝 Transcript: {response.transcript}");
            Debug.Log($"💬 Response: {response.response_text}");
            
            if (response.tool_result != null)
            {
                Debug.Log($"🔧 Tool used: {response.tool_result}");
            }
            
            // Play audio response
            if (!string.IsNullOrEmpty(response.audio_response))
            {
                byte[] audioBytes = System.Convert.FromBase64String(response.audio_response);
                StartCoroutine(PlayMP3Audio(audioBytes));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error parsing audio response: {ex.Message}");
        }
    }
    
    IEnumerator PlayMP3Audio(byte[] mp3Data)
    {
        // Save MP3 to temporary file and load as AudioClip
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "response.mp3");
        System.IO.File.WriteAllBytes(tempPath, mp3Data);
        
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip($"file://{tempPath}", AudioType.MPEG))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
                
                Debug.Log("🔊 Playing audio response");
            }
        }
    }
    
    byte[] ConvertAudioClipToWAV(AudioClip clip)
    {
        // WAV conversion implementation (simplified)
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);
        
        // Convert to 16-bit PCM WAV format
        // (Implementation details omitted for brevity)
        return new byte[0]; // Placeholder
    }
}

[System.Serializable]
public class AudioProcessResponse
{
    public string transcript;
    public string response_text;
    public string audio_response;
    public object tool_result;
    public object personal_info;
}
```

## 🎯 Usage Examples

### Voice Weather Query
1. **User speaks**: "What's the weather in Tokyo?"
2. **Unity sends**: Audio stream to WebSocket
3. **Server processes**: Speech → Weather tool → Response
4. **Unity receives**: Audio response saying current weather
5. **Result**: Natural voice conversation with weather data

### Personal Memory
1. **User speaks**: "My name is Sarah"
2. **Server stores**: Personal information in user profile
3. **Future conversations**: AI remembers and uses "Sarah"
4. **Result**: Personalized voice interactions

## ⚙️ Required Dependencies

### Unity Packages
```json
{
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1"
  }
}
```

### WebSocket Library
- **WebSocket-Sharp**: For real-time WebSocket communication
- **Alternative**: Use Unity's built-in WebSocket when available

## 🎵 Audio Format Requirements

- **Input**: PCM16, 16kHz, mono for real-time streaming
- **Output**: PCM16 audio from server (real-time)
- **Alternative**: WAV/MP3 for simple processing mode

## 🚨 Setup Checklist

1. ✅ **Server API Key**: Configure OpenAI API key in server `.env`
2. ✅ **WebSocket URL**: Point Unity to your server WebSocket endpoint
3. ✅ **Microphone Permissions**: Enable microphone access in Unity
4. ✅ **Audio Source**: Attach AudioSource component for playback
5. ✅ **Error Handling**: Implement connection error recovery

The integration now follows official OpenAI Realtime API patterns with proper event handling, tool integration, and audio streaming capabilities!